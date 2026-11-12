using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PandaRestaurant.PopupStoreServer;

namespace Muks.BackEnd
{
    public enum PopupStoreServerClientState { RecoverFirst, Ready, Sending, ReconciliationRequired, Invalidated }
    public enum PopupStoreServerClientOutcome { Confirmed, Unavailable, Indeterminate, Invalidated }

    public sealed class PopupStoreServerClientResult
    {
        public PopupStoreServerClientOutcome Outcome { get; }
        public Response Response { get; }
        public string Error { get; }
        internal PopupStoreServerClientResult(PopupStoreServerClientOutcome outcome, Response response = null, string error = null)
        { Outcome = outcome; Response = response; Error = error; }
    }

    /// <summary>One authenticated canonical gamerId + restoration epoch. An explicitly injected
    /// host owns this client; it is never attached automatically to a popup or legacy 90s game.
    /// Completion conveys a verified server record, NOT permission to AddDia or animate a paid reward.
    /// Use a migrated wallet reconciler and query its latest balance before paid UI completion.</summary>
    public sealed class PopupStoreServerClient
    {
        private readonly IPopupStoreFunctionTransport _transport;
        private readonly IPopupStoreServerRequestJournal _journal;
        private readonly Func<PopupStoreRewardAccountSession> _readSession;
        private readonly PopupStoreRewardAccountSession _session;
        private bool _busy, _recoveryChecked;
        private long _operation;
        public PopupStoreServerClientState State { get; private set; } = PopupStoreServerClientState.RecoverFirst;
        public string LastError { get; private set; }
        public PopupStoreRewardAccountSession Session => _session;
        public bool IsCurrentSession => Current();
        public int SubmitCount { get; private set; }
        public int QueryCount { get; private set; }

        public PopupStoreServerClient(IPopupStoreFunctionTransport transport, IPopupStoreServerRequestJournal journal,
            Func<PopupStoreRewardAccountSession> readSession)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _readSession = readSession ?? throw new ArgumentNullException(nameof(readSession));
            _session = ReadSession();
        }

        public bool QueryAccess(Action<PopupStoreServerClientResult> completed)
            => Read(new Command { Operation = Operation.Access, RequestId = NewQueryId() }, completed, false);

        public bool QueryBusiness(string businessId, Action<PopupStoreServerClientResult> completed)
            => Read(new Command { Operation = Operation.Query, RequestId = NewQueryId(), BusinessSessionId = businessId }, completed, false);

        public bool QueryRecordedRequest(string requestId, Action<PopupStoreServerClientResult> completed)
            => Read(new Command { Operation = Operation.Query, RequestId = NewQueryId(), LookupRequestId = requestId }, completed, false);

        public bool HasPendingRequest
        {
            get
            {
                if (!Current()) return true;
                try { return _journal.Read(_session.AccountId) != null; }
                catch { State = PopupStoreServerClientState.ReconciliationRequired; return true; }
            }
        }

        /// <summary>Required after construction even when the local journal is absent/deleted.
        /// A server-held active/settled business is recovered; a local reset never creates authority.</summary>
        public bool Recover(Action<PopupStoreServerClientResult> completed)
        {
            if (!Current() || _busy) return false;
            try
            {
                var pending = _journal.Read(_session.AccountId);
                if (pending != null)
                {
                    var original = Decode(pending.CommandJson);
                    if (original.RequestId != pending.RequestId || !IsWrite(original.Operation)) throw new InvalidOperationException();
                    return Dispatch(new Command { Operation = Operation.Query, RequestId = NewQueryId(),
                        LookupRequestId = original.RequestId, BusinessSessionId = original.BusinessSessionId }, original, pending, completed, true);
                }
                return Read(new Command { Operation = Operation.Query, RequestId = NewQueryId() }, completed, true);
            }
            catch { return LocalFailure("저장된 서버 요청을 확인할 수 없습니다. 서버 복구가 필요합니다.", completed); }
        }

        public bool Submit(Command command, Action<PopupStoreServerClientResult> completed)
        {
            if (!Current() || _busy) return false;
            if (!_recoveryChecked) return LocalFailure("기존 영업과 미확정 요청을 먼저 조회해 주세요.", completed);
            Command frozen;
            PopupStorePendingServerRequest pending;
            try
            {
                frozen = Decode(JsonConvert.SerializeObject(command));
                if (!IsWrite(frozen.Operation)) throw new ArgumentException();
                if (_journal.Read(_session.AccountId) != null)
                    return LocalFailure("이전 요청의 결과를 먼저 복구해 주세요.", completed);
                pending = new PopupStorePendingServerRequest(frozen.RequestId, JsonConvert.SerializeObject(frozen));
                // Only a proven new atomic create belongs to this first submission. Another
                // client may have prepared/sent the same identity between Read and Prepare.
                if (!_journal.Prepare(_session.AccountId, pending))
                    return LocalFailure("이미 기록된 동일 요청을 먼저 복구해 주세요.", completed);
            }
            catch { return LocalFailure("요청을 안전하게 기록하지 못해 전송하지 않았습니다.", completed); }
            return Dispatch(frozen, frozen, pending, completed, false);
        }

        /// <summary>After a read returned NotFound, an explicit retry may reuse ONLY the exact
        /// persisted command. Durable server request-ID uniqueness makes an in-flight retry safe;
        /// this is not a new claim or an assumption that the first request did nothing.</summary>
        public bool RetrySamePending(Action<PopupStoreServerClientResult> completed)
        {
            if (!Current() || _busy || State != PopupStoreServerClientState.ReconciliationRequired) return false;
            try
            {
                var pending = _journal.Read(_session.AccountId);
                if (pending == null) return LocalFailure("미확정 요청을 조회해 주세요.", completed);
                var original = Decode(pending.CommandJson);
                if (original.RequestId != pending.RequestId || !IsWrite(original.Operation)) throw new InvalidOperationException();
                return Dispatch(original, original, pending, completed, false);
            }
            catch { return LocalFailure("기존 요청 기록이 유효하지 않습니다.", completed); }
        }

        public void MarkResponseMissing()
        {
            if (!Current() || !_busy) return;
            _busy = false; State = PopupStoreServerClientState.ReconciliationRequired;
            LastError = "서버 결과를 확인 중입니다. 기존 요청을 조회해 주세요.";
        }

        private bool Read(Command command, Action<PopupStoreServerClientResult> completed, bool recovery)
        {
            if (!Current() || _busy) return false;
            return Dispatch(command, null, null, completed, recovery);
        }

        private bool Dispatch(Command sent, Command original, PopupStorePendingServerRequest pending,
            Action<PopupStoreServerClientResult> completed, bool recovery)
        {
            if (!Current()) return false;
            _busy = true; State = PopupStoreServerClientState.Sending;
            long serial = ++_operation;
            bool replied = false;
            if (IsWrite(sent.Operation)) SubmitCount++; else QueryCount++;
            try
            {
                _transport.Send(JsonConvert.SerializeObject(sent), received =>
                {
                    if (replied || serial != _operation || !Current()) return;
                    replied = true; _busy = false;
                    if (received == null || received.Outcome != PopupStoreFunctionTransportOutcome.ResponseReceived)
                    {
                        // Even a local pre-send rejection proves nothing about another client
                        // that may have adopted this durable identity and sent it concurrently.
                        // Retain it until correlated server evidence; recover/retry the SAME ID.
                        Unknown(completed, received?.ErrorCode ?? "response_missing");
                        return;
                    }
                    Response response;
                    try
                    {
                        var envelope = JObject.Parse(received.ResponseJson,
                            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        if (envelope["Protocol"]?.Type != JTokenType.String || envelope["RequestId"]?.Type != JTokenType.String
                            || envelope["Status"]?.Type != JTokenType.Integer) throw new InvalidOperationException("Explicit response envelope fields are required.");
                        if (!ValidWireShape(envelope, typeof(Response))) throw new InvalidOperationException("Response field types must be explicit.");
                        response = envelope.ToObject<Response>(JsonSerializer.Create(new JsonSerializerSettings {
                            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error }));
                        if (!ValidResponse(response, sent)) throw new InvalidOperationException();
                        if (pending != null)
                        {
                            if (response.AppliedRequestId != original.RequestId || response.AppliedOperation != original.Operation
                                || response.AppliedCommandHash != PopupStoreServerAuthority.CommandHash(original))
                            { Unknown(completed, "기존 요청의 확정 기록을 아직 찾지 못했습니다."); return; }
                            if (response.Status == Status.Ok && !ValidAppliedPayload(response, original)) throw new InvalidOperationException();
                            _journal.Complete(_session.AccountId, pending);
                            _recoveryChecked = true;
                        }
                        else if (recovery && response.Access != null
                            && (response.Status == Status.Ok || response.Status == Status.NotFound)) _recoveryChecked = true;
                    }
                    catch { Unknown(completed, "서버 응답의 계정·요청·데이터를 확인할 수 없습니다."); return; }
                    State = _recoveryChecked ? PopupStoreServerClientState.Ready : PopupStoreServerClientState.RecoverFirst;
                    LastError = response.Status == Status.Ok ? null : "서버 상태 확인이 필요합니다: " + response.Status;
                    Notify(completed, new PopupStoreServerClientResult(PopupStoreServerClientOutcome.Confirmed, response, LastError));
                });
                return true;
            }
            catch
            {
                if (!replied && serial == _operation && Current()) { replied = true; _busy = false; Unknown(completed, "전송 결과가 불명확합니다. 기존 요청을 조회해 주세요."); }
                return false;
            }
        }

        // Reject coercion and absent value fields before DTO defaults can erase malformed wire
        // evidence. In particular a missing balance must never become authoritative zero.
        private static bool ValidWireShape(JToken token, Type type)
        {
            if (token == null || token.Type == JTokenType.Null) return !type.IsValueType;
            if (type == typeof(string)) return token.Type == JTokenType.String;
            if (type == typeof(bool)) return token.Type == JTokenType.Boolean;
            if (type.IsEnum || type == typeof(int) || type == typeof(long)) return token.Type == JTokenType.Integer;
            if (type == typeof(double)) return token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
            if (type.IsArray) return token.Type == JTokenType.Array && token.Children().All(value => ValidWireShape(value, type.GetElementType()));
            if (token.Type != JTokenType.Object) return false;
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return fields.All(field => ValidWireShape(token[field.Name], field.FieldType));
        }

        private bool ValidResponse(Response response, Command sent)
        {
            if (response == null || response.Protocol != PopupStoreServerAuthority.Protocol || response.RequestId != sent.RequestId
                || !Enum.IsDefined(typeof(Status), response.Status)) return false;
            if (sent.LookupRequestId != null && ((response.AppliedRequestId != null && response.AppliedRequestId != sent.LookupRequestId)
                || (response.Status == Status.Ok && response.AppliedRequestId != sent.LookupRequestId))) return false;
            bool Id(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128
                && value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == ':');
            bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
            bool UtcTicks(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks;
            var access = response.Access;
            var wallet = response.Wallet;
            if (wallet != null && (sent.Operation != Operation.Access && sent.Operation != Operation.Query
                || wallet.AccountId != _session.AccountId || !Id(wallet.WalletId) || wallet.Balance < 0 || wallet.Version < 0)) return false;
            if (response.Status == Status.Ok && access == null) return false;
            if (access != null && (access.AccountId != _session.AccountId || access.DailyLimit != 1 || access.Used < 0 || access.Used > 1
                || access.Remaining != Math.Max(0, 1 - access.Used) || !UtcTicks(access.ServerUtcTicks)
                || access.PolicyVersion != PopupStoreServerAuthority.PolicyVersion
                || PopupStoreServerAuthority.GameDay(new DateTimeOffset(access.ServerUtcTicks, TimeSpan.Zero)) != access.GameDay
                || access.NextResetUtcTicks != PopupStoreServerAuthority.NextResetUtcTicks(new DateTimeOffset(access.ServerUtcTicks, TimeSpan.Zero)))) return false;
            var business = response.Business;
            if (business == null)
                return response.Order == null && response.Decision == null && response.Grant == null
                    && (sent.Operation != Operation.Query || response.Status != Status.Ok);
            var policy = PopupStoreServerAuthority.ApprovedPolicy();
            if (business.AccountId != _session.AccountId || !Id(business.BusinessSessionId) || !Id(business.StartRequestId)
                || (!string.IsNullOrEmpty(sent.BusinessSessionId) && sent.BusinessSessionId != business.BusinessSessionId)
                || business.Phase < 1 || business.Phase > 6 || business.HighestClearedGrade < 0 || business.HighestClearedGrade > 6
                || business.HighestClearedGrade > business.Phase || business.TotalScore < 0 || business.SuccessfulOrders < 0
                || (long)business.SuccessfulOrders * 100 > business.TotalScore || (long)business.SuccessfulOrders * 220 < business.TotalScore
                || business.PolicyVersion != PopupStoreServerAuthority.PolicyVersion || !Id(business.ClockProtocolVersion)
                || !UtcTicks(business.StartedAtUtcTicks)
                || PopupStoreServerAuthority.GameDay(new DateTimeOffset(business.StartedAtUtcTicks, TimeSpan.Zero)) != business.StartGameDay
                || !Finite(business.ElapsedGameplaySeconds) || !Finite(business.PhaseDeadlineSeconds) || business.PhaseDeadlineSeconds <= 0
                || business.ElapsedGameplaySeconds > business.PhaseDeadlineSeconds || business.Orders == null || business.Orders.Length > 256
                || (business.IsSettled && business.AwaitingPhaseCleanup)
                || !Enum.IsDefined(typeof(BusinessEndReason), business.EndReason)) return false;
            if (business.HighestClearedGrade > 0 && business.TotalScore < policy.Targets[business.HighestClearedGrade - 1]) return false;
            if ((!business.IsSettled && (business.EndReason != BusinessEndReason.None || business.HighestClearedGrade != business.Phase - 1))
                || (business.IsSettled && business.EndReason == BusinessEndReason.None)) return false;
            bool ValidOrder(OrderSnapshot order)
            {
                if (order == null || !Id(order.OrderId) || order.BusinessSessionId != business.BusinessSessionId
                    || order.Phase < 1 || order.Phase > business.Phase || order.Slot < 0 || order.Slot > 2
                    || order.Materials == null || order.Materials.Length < 3 || order.Materials.Length > 5
                    || !Finite(order.IssuedAtGameplaySeconds) || order.IssuedAtGameplaySeconds > business.ElapsedGameplaySeconds
                    || (order.Packed && order.Expired)) return false;
                int plushes = order.Materials.Length - 2;
                if ((order.Phase == 1 && plushes != 1) || ((order.Phase == 2 || order.Phase == 3) && plushes > 2)
                    || (order.Phase >= 4 && plushes < 2)) return false;
                for (int i = 0; i < plushes; i++)
                {
                    string material = order.Materials[i];
                    if (material == null || material.Length != 7 || !material.StartsWith("STAFF", StringComparison.Ordinal)
                        || material[5] < '0' || material[5] > '9' || material[6] < '0' || material[6] > '9') return false;
                    int number = (material[5] - '0') * 10 + material[6] - '0';
                    if (number < 1 || number > policy.PlushCounts[order.Phase - 1]) return false;
                }
                if (!new[] { "BOX_PINK", "BOX_BLUE", "BOX_MINT" }.Contains(order.Materials[plushes])
                    || !new[] { "RIBBON_PINK", "RIBBON_YELLOW", "RIBBON_MINT" }.Contains(order.Materials[plushes + 1])) return false;
                if (order.ArrivedAtGameplaySeconds == -1)
                    return !order.Packed && order.ExpiresAtGameplaySeconds == double.MaxValue;
                return Finite(order.ArrivedAtGameplaySeconds) && order.ArrivedAtGameplaySeconds >= order.IssuedAtGameplaySeconds
                    && order.ArrivedAtGameplaySeconds <= business.ElapsedGameplaySeconds
                    && order.ExpiresAtGameplaySeconds == order.ArrivedAtGameplaySeconds + policy.Patience[order.Phase - 1] + 4d * (plushes - 1);
            }
            if (business.Orders.Any(order => !ValidOrder(order))
                || business.Orders.Select(order => order.OrderId).Distinct(StringComparer.Ordinal).Count() != business.Orders.Length
                || business.Orders.Where(order => !order.Packed && !order.Expired).GroupBy(order => order.Slot).Any(group => group.Count() > 1)) return false;
            if (response.Order != null)
            {
                if (!ValidOrder(response.Order)) return false;
                var owned = business.Orders.SingleOrDefault(order => order.OrderId == response.Order.OrderId);
                if (owned == null || owned.Phase != response.Order.Phase || owned.Slot != response.Order.Slot
                    || !owned.Materials.SequenceEqual(response.Order.Materials, StringComparer.Ordinal)
                    || owned.IssuedAtGameplaySeconds != response.Order.IssuedAtGameplaySeconds
                    || owned.ArrivedAtGameplaySeconds != response.Order.ArrivedAtGameplaySeconds
                    || owned.ExpiresAtGameplaySeconds != response.Order.ExpiresAtGameplaySeconds
                    || owned.Packed != response.Order.Packed || owned.Expired != response.Order.Expired) return false;
            }
            var decision = response.Decision;
            if (!business.IsSettled)
                return decision == null && business.Decision == null && response.Grant == null && business.Grant == null;
            if (decision == null || business.Decision == null || !Id(decision.DecisionId)
                || decision.PolicyVersion != PopupStoreServerAuthority.RewardPolicyVersion || decision.Grade < 0 || decision.Grade > 6
                || decision.Kind < 0 || decision.Kind > 2
                || decision.Grade != (business.EndReason == BusinessEndReason.Abandoned ? 0 : business.HighestClearedGrade)
                || (decision.Grade == 0 ? decision.Kind != 0 || decision.Diamonds != 0
                    : decision.Diamonds != policy.RewardAmounts[(decision.Grade - 1) * 3 + decision.Kind])
                || decision.DecisionId != business.Decision.DecisionId || decision.PolicyVersion != business.Decision.PolicyVersion
                || decision.Grade != business.Decision.Grade || decision.Kind != business.Decision.Kind || decision.Diamonds != business.Decision.Diamonds) return false;
            var grant = response.Grant;
            if (grant == null) return business.Grant == null;
            return business.Grant != null && decision.Grade > 0 && Id(grant.GrantId)
                && grant.GrantKey == PopupStoreServerAuthority.GrantKey(business.AccountId, business.BusinessSessionId)
                && grant.DecisionId == decision.DecisionId && grant.Diamonds == decision.Diamonds
                && grant.BalanceAfter >= grant.Diamonds && grant.WalletVersion > 0
                && grant.GrantId == business.Grant.GrantId && grant.GrantKey == business.Grant.GrantKey
                && grant.DecisionId == business.Grant.DecisionId && grant.Diamonds == business.Grant.Diamonds
                && grant.BalanceAfter == business.Grant.BalanceAfter && grant.WalletVersion == business.Grant.WalletVersion;
        }

        private static bool ValidAppliedPayload(Response response, Command original)
        {
            var business = response.Business;
            if (business == null) return false;
            if (original.Operation == Operation.Start)
                return business.StartRequestId == original.RequestId && business.PolicyVersion == original.ExpectedPolicyVersion
                    && !business.IsSettled && business.Phase == 1 && business.TotalScore == 0 && business.SuccessfulOrders == 0;
            if (business.BusinessSessionId != original.BusinessSessionId) return false;
            if (original.Operation == Operation.IssueOrder)
                return response.Order != null && response.Order.BusinessSessionId == business.BusinessSessionId
                    && !string.IsNullOrWhiteSpace(response.Order.OrderId) && !response.Order.Packed && !response.Order.Expired;
            if (original.Operation == Operation.Pack)
            {
                var packed = business.Orders.SingleOrDefault(order => order.OrderId == original.OrderId);
                return packed != null && packed.Packed && original.Materials != null
                    && packed.Materials.OrderBy(value => value, StringComparer.Ordinal)
                        .SequenceEqual(original.Materials.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
            }
            if (original.Operation == Operation.Claim)
                return business.IsSettled && response.Decision != null
                    && (response.Decision.Grade == 0 ? response.Grant == null : response.Grant != null);
            if (original.Operation == Operation.Abandon) return business.IsSettled || business.AwaitingPhaseCleanup;
            return original.Operation == Operation.Settle;
        }

        internal static Command Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 32768) throw new ArgumentException();
            var obj = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            string[] allowed = { "Protocol", "Operation", "RequestId", "LookupRequestId", "BusinessSessionId", "ExpectedPolicyVersion", "OrderId", "ReceiptId", "Materials" };
            if (obj.Properties().Any(p => !allowed.Contains(p.Name))) throw new ArgumentException("Unsupported command fields.");
            if (obj["Protocol"]?.Type != JTokenType.String || obj["RequestId"]?.Type != JTokenType.String
                || obj["Operation"]?.Type != JTokenType.Integer) throw new ArgumentException("Explicit protocol and request fields are required.");
            foreach (string name in new[] { "LookupRequestId", "BusinessSessionId", "ExpectedPolicyVersion", "OrderId", "ReceiptId" })
                if (obj[name] != null && obj[name].Type != JTokenType.Null && obj[name].Type != JTokenType.String)
                    throw new ArgumentException("Optional command identities must be strings.");
            if (obj["Materials"] != null && obj["Materials"].Type != JTokenType.Null
                && (obj["Materials"].Type != JTokenType.Array || obj["Materials"].Children().Any(item => item.Type != JTokenType.String)))
                throw new ArgumentException("Materials must be an explicit string array.");
            var value = obj.ToObject<Command>();
            if (value == null || value.Protocol != PopupStoreServerAuthority.Protocol || !Enum.IsDefined(typeof(Operation), value.Operation)
                || string.IsNullOrWhiteSpace(value.RequestId) || value.RequestId.Length > 128) throw new ArgumentException();
            PopupStoreServerAuthority.CommandHash(value); // Full server schema BEFORE durable journal preparation.
            return value;
        }
        private bool Current()
        {
            if (State == PopupStoreServerClientState.Invalidated) return false;
            if (_session != null && ReferenceEquals(_session, ReadSession())) return true;
            State = PopupStoreServerClientState.Invalidated; _busy = false; ++_operation;
            LastError = "로그인 세션이 변경되었습니다. 다시 로그인한 계정으로 복구해 주세요."; return false;
        }
        private PopupStoreRewardAccountSession ReadSession() { try { return _readSession(); } catch { return null; } }
        private static bool IsWrite(Operation op) => op != Operation.Access && op != Operation.Query;
        private static string NewQueryId() => "query-" + Guid.NewGuid().ToString("N");
        private bool LocalFailure(string error, Action<PopupStoreServerClientResult> completed)
        { LastError = error; State = PopupStoreServerClientState.ReconciliationRequired; Notify(completed, new PopupStoreServerClientResult(PopupStoreServerClientOutcome.Indeterminate, error: error)); return false; }
        private void Unknown(Action<PopupStoreServerClientResult> completed, string error)
        { LastError = error; State = PopupStoreServerClientState.ReconciliationRequired; Notify(completed, new PopupStoreServerClientResult(PopupStoreServerClientOutcome.Indeterminate, error: error)); }
        private void Notify(Action<PopupStoreServerClientResult> callback, PopupStoreServerClientResult result)
        { if (!Current()) return; try { callback?.Invoke(result); } catch { LastError = "서버 결과는 보존됐습니다. 화면 갱신을 다시 확인해 주세요."; } }
    }
}
