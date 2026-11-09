using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public enum PopupStoreGameDataState { Empty, Started, Finalized, Claimed, NoReward, Abandoned }

    /// <summary>One account's latest business in its existing GameData row. This is client-authored
    /// persistence, not server-authoritative gameplay or a cross-device compare-and-swap.</summary>
    public sealed class PopupStoreGameDataRecord
    {
        public const string FieldName = "PopupStoreAccountV1";
        public const int SchemaVersion = 1;
        private const int MaximumJsonLength = 12000;
        private readonly JObject _data;
        private readonly string _json;
        private PopupStoreGameDataRecord(JObject data) { _data = data; _json = data.ToString(Formatting.None); }
        public long Revision => (long)_data["Revision"];
        public string DayKey => (string)_data["DayKey"];
        public DateTime StartedAtUtc => State == PopupStoreGameDataState.Empty ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
            : DateTime.ParseExact((string)_data["StartedAtUtc"], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        public int UsedCount => (int)_data["UsedCount"];
        public string StartRequestId => (string)_data["StartRequestId"];
        public string SessionId => (string)_data["SessionId"];
        public string BusinessSessionId => SessionId;
        public string ProfileId => (string)_data["ProfileId"];
        public int ProfileVersion => (int)_data["ProfileVersion"];
        public PopupStoreGameDataState State => (PopupStoreGameDataState)(int)_data["State"];
        public int FinalScore => (int)_data["FinalScore"];
        public long SuccessfulOrders => (long)_data["SuccessfulOrders"];
        public PopupStoreGradeTier HighestGrade => (PopupStoreGradeTier)(int)_data["HighestGrade"];
        public PopupStorePhaseEndReason EndReason => (PopupStorePhaseEndReason)(int)_data["EndReason"];
        public string RewardPolicyId => (string)_data["RewardPolicyId"];
        public string DecisionId => (string)_data["DecisionId"];
        public int RewardRoll => (int)_data["RewardRoll"];
        public PopupStoreRewardKind RewardKind => (PopupStoreRewardKind)(int)_data["RewardKind"];
        public int Diamonds => (int)_data["Diamonds"];
        public string LastRequestId => (string)_data["LastRequestId"];
        public bool IsRewardPending => State == PopupStoreGameDataState.Finalized;
        public bool IsTerminal => State != PopupStoreGameDataState.Empty && State != PopupStoreGameDataState.Started;
        public string ToJson() => _json;
        public bool Matches(PopupStoreGameDataRecord other) => other != null && JToken.DeepEquals(_data, other._data);

        public static PopupStoreGameDataRecord Empty => new PopupStoreGameDataRecord(new JObject
        {
            ["Version"] = SchemaVersion, ["Revision"] = 0L, ["DayKey"] = "", ["StartedAtUtc"] = "", ["UsedCount"] = 0,
            ["StartRequestId"] = "", ["SessionId"] = "", ["ProfileId"] = "", ["ProfileVersion"] = 0,
            ["State"] = 0, ["FinalScore"] = 0, ["SuccessfulOrders"] = 0L, ["HighestGrade"] = 0,
            ["EndReason"] = 0, ["RewardPolicyId"] = "", ["DecisionId"] = "", ["RewardRoll"] = -1,
            ["RewardKind"] = 0, ["Diamonds"] = 0, ["LastRequestId"] = ""
        });

        /// <summary>Only genuine field absence means an old account. A present corrupt field does
        /// not reset usage or reward state; callers can keep unrelated account restoration available.</summary>
        public static bool TryRead(string json, bool fieldPresent, out PopupStoreGameDataRecord record, out string error)
        {
            record = null; error = null;
            if (!fieldPresent) { record = Empty; return true; }
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength) throw new FormatException();
                JObject data;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 })
                {
                    data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException();
                }
                var empty = Empty;
                if (data.Count != empty._data.Count) throw new FormatException();
                foreach (var field in empty._data.Properties())
                    if (data[field.Name] == null || data[field.Name].Type != field.Value.Type) throw new FormatException();
                if ((long)data["Version"] != SchemaVersion) throw new FormatException();
                var candidate = new PopupStoreGameDataRecord(data);
                if (!candidate.Valid()) throw new FormatException();
                record = candidate; return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is OverflowException || ex is ArgumentException)
            { error = "팝업스토어 저장 기록을 확인할 수 없습니다. 사용 횟수나 보상을 초기화하지 않습니다."; return false; }
        }

        public bool TryStart(DateTime serverUtc, string requestId, string sessionId, string profileId, int profileVersion,
            out PopupStoreGameDataRecord next, out string error)
        {
            next = null; error = null;
            if (serverUtc.Kind != DateTimeKind.Utc || !Id(requestId) || !Id(sessionId) || !Id(profileId) || profileVersion < 1)
                return Fail("확인된 서버 UTC 시각과 새 영업 식별자가 필요합니다.", out error);
            string day;
            try { day = AttendanceProgress.GameDay(serverUtc.AddHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
            catch (ArgumentOutOfRangeException) { return Fail("서버 시각 범위가 올바르지 않습니다.", out error); }
            if (State == PopupStoreGameDataState.Started || IsRewardPending || (UsedCount != 0 && string.CompareOrdinal(day, DayKey) <= 0)
                || requestId == LastRequestId || requestId == StartRequestId || sessionId == SessionId)
                return Fail("진행 중 영업·미수령 보상 또는 오늘 사용한 영업권을 먼저 확인해 주세요.", out error);
            var values = Empty._data;
            values["DayKey"] = day; values["UsedCount"] = 1; values["StartRequestId"] = requestId;
            values["StartedAtUtc"] = serverUtc.ToString("O", CultureInfo.InvariantCulture);
            values["SessionId"] = sessionId; values["ProfileId"] = profileId; values["ProfileVersion"] = profileVersion;
            values["State"] = (int)PopupStoreGameDataState.Started;
            return Next(values, requestId, out next, out error);
        }

        public bool TryFinalize(PopupStorePhaseSession phase, PopupStoreRewardPolicySnapshot policy, int roll,
            string requestId, out PopupStoreGameDataRecord next, out string error)
        {
            next = null; error = null;
            var result = phase?.FinalResult;
            if (State != PopupStoreGameDataState.Started || result == null || !phase.IsTerminal || phase.FinalizationCount != 1
                || result.BusinessSessionId != SessionId || phase.BusinessSessionId != SessionId
                || result.ProfileId != ProfileId || result.ProfileVersion != ProfileVersion
                || phase.Profile.ProfileId != ProfileId || phase.Profile.Version != ProfileVersion
                || result.TotalScore != phase.TotalScore || result.HighestClearedGrade != phase.HighestClearedGrade
                || result.SuccessfulOrders != phase.TotalSuccessfulOrders || result.SuccessfulOrders < 0 || result.TotalScore < 0
                || !Id(requestId) || requestId == StartRequestId || requestId == LastRequestId)
                return Fail("현재 영업의 원래 확정 결과가 필요합니다.", out error);
            var values = (JObject)_data.DeepClone();
            values["FinalScore"] = result.TotalScore; values["SuccessfulOrders"] = result.SuccessfulOrders;
            values["HighestGrade"] = (int)result.HighestClearedGrade; values["EndReason"] = (int)result.EndReason;
            bool eligible = (result.EndReason == PopupStorePhaseEndReason.Completed || result.EndReason == PopupStorePhaseEndReason.TimedOut)
                && result.HighestClearedGrade >= PopupStoreGradeTier.C && result.HighestClearedGrade <= PopupStoreGradeTier.SSS;
            values["State"] = (int)(eligible ? PopupStoreGameDataState.Finalized
                : result.EndReason == PopupStorePhaseEndReason.Abandoned ? PopupStoreGameDataState.Abandoned : PopupStoreGameDataState.NoReward);
            if (eligible)
            {
                if (policy == null || policy.Schema != PopupStoreRewardSchema.R1Permille || policy.Denominator != 1000
                    || policy.PolicyId != PopupStoreRewardTable.R1PolicyId
                    || !policy.TryGetReward(result.HighestClearedGrade, out var entry, out error)
                    || !PopupStoreRewardSelector.TrySelect(entry, roll, policy.Denominator, out var kind, out int diamonds, out error))
                    return Fail(error ?? "승인된 R1 보상 정책을 확인할 수 없습니다.", out error);
                values["RewardPolicyId"] = policy.PolicyId; values["DecisionId"] = requestId;
                values["RewardRoll"] = roll; values["RewardKind"] = (int)kind; values["Diamonds"] = diamonds;
            }
            return Next(values, requestId, out next, out error);
        }

        public bool TryAbandon(string sessionId, string requestId, out PopupStoreGameDataRecord next, out string error)
        {
            next = null; error = null;
            if (State != PopupStoreGameDataState.Started || sessionId != SessionId || requestId == StartRequestId || requestId == LastRequestId)
                return Fail("현재 진행 중 영업만 포기할 수 있습니다.", out error);
            var values = (JObject)_data.DeepClone(); values["State"] = (int)PopupStoreGameDataState.Abandoned;
            values["EndReason"] = (int)PopupStorePhaseEndReason.Abandoned;
            return Next(values, requestId, out next, out error);
        }

        public bool TryClaim(string requestId, out PopupStoreGameDataRecord next, out string error)
        {
            next = null; error = null;
            if (!IsRewardPending || requestId == StartRequestId || requestId == LastRequestId || requestId == DecisionId)
                return Fail("수령할 확정 보상이 없거나 이미 사용한 요청입니다.", out error);
            var values = (JObject)_data.DeepClone(); values["State"] = (int)PopupStoreGameDataState.Claimed;
            return Next(values, requestId, out next, out error);
        }

        public bool IsValidSuccessor(PopupStoreGameDataRecord next)
        {
            if (next == null || Revision == long.MaxValue || next.Revision != Revision + 1 || !next.Valid()
                || next.LastRequestId == LastRequestId || next.LastRequestId == StartRequestId) return false;
            if (next.State == PopupStoreGameDataState.Started)
                return !IsRewardPending && State != PopupStoreGameDataState.Started && next.StartRequestId == next.LastRequestId
                    && next.SessionId != SessionId && (UsedCount == 0 || string.CompareOrdinal(next.DayKey, DayKey) > 0);
            if (SessionId != next.SessionId || StartRequestId != next.StartRequestId || DayKey != next.DayKey || StartedAtUtc != next.StartedAtUtc
                || UsedCount != next.UsedCount || ProfileId != next.ProfileId || ProfileVersion != next.ProfileVersion) return false;
            if (State == PopupStoreGameDataState.Started)
                return next.State == PopupStoreGameDataState.Finalized || next.State == PopupStoreGameDataState.NoReward || next.State == PopupStoreGameDataState.Abandoned;
            if (State != PopupStoreGameDataState.Finalized || next.State != PopupStoreGameDataState.Claimed) return false;
            var old = (JObject)_data.DeepClone(); var current = (JObject)next._data.DeepClone();
            foreach (var field in new[] { "State", "Revision", "LastRequestId" }) { old.Remove(field); current.Remove(field); }
            return JToken.DeepEquals(old, current);
        }

        private bool Next(JObject values, string request, out PopupStoreGameDataRecord next, out string error)
        {
            next = null; error = null;
            if (!Id(request) || Revision == long.MaxValue) return Fail("저장 요청 또는 기록 버전이 올바르지 않습니다.", out error);
            values["Revision"] = Revision + 1; values["LastRequestId"] = request;
            var candidate = new PopupStoreGameDataRecord(values);
            if (!IsValidSuccessor(candidate)) return Fail("허용되지 않은 영업 기록 전환입니다.", out error);
            next = candidate; return true;
        }

        private bool Valid()
        {
            if (Revision < 0 || !Enum.IsDefined(typeof(PopupStoreGameDataState), State) || FinalScore < 0 || SuccessfulOrders < 0
                || HighestGrade < PopupStoreGradeTier.None || HighestGrade > PopupStoreGradeTier.SSS
                || !Enum.IsDefined(typeof(PopupStorePhaseEndReason), EndReason)
                || !Enum.IsDefined(typeof(PopupStoreRewardKind), RewardKind)) return false;
            if (State == PopupStoreGameDataState.Empty) return Matches(Empty);
            if (Revision < 1 || UsedCount != 1 || !Id(StartRequestId) || !Id(SessionId) || !Id(ProfileId)
                || !Id(LastRequestId) || ProfileVersion < 1
                || !DateTime.TryParseExact(DayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !DateTime.TryParseExact((string)_data["StartedAtUtc"], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var started)
                || started.Kind != DateTimeKind.Utc || started > DateTime.MaxValue.AddHours(-9)
                || AttendanceProgress.GameDay(started.AddHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != DayKey) return false;
            if (State == PopupStoreGameDataState.Started)
                return LastRequestId == StartRequestId && FinalScore == 0 && SuccessfulOrders == 0 && HighestGrade == PopupStoreGradeTier.None
                    && EndReason == PopupStorePhaseEndReason.Completed && NoDecision();
            if (LastRequestId == StartRequestId) return false;
            if (State == PopupStoreGameDataState.Abandoned) return EndReason == PopupStorePhaseEndReason.Abandoned && NoDecision();
            if (State == PopupStoreGameDataState.NoReward)
                return NoDecision() && (HighestGrade == PopupStoreGradeTier.None || EndReason == PopupStorePhaseEndReason.CleanupFailed
                    || EndReason == PopupStorePhaseEndReason.CleanupCancelled);
            return HighestGrade >= PopupStoreGradeTier.C && FinalScore > 0 && SuccessfulOrders > 0
                && (EndReason == PopupStorePhaseEndReason.Completed || EndReason == PopupStorePhaseEndReason.TimedOut)
                && RewardPolicyId == PopupStoreRewardTable.R1PolicyId && Id(DecisionId) && DecisionId != StartRequestId
                && RewardRoll >= 0 && RewardRoll < 1000 && RewardMatchesApprovedPolicy()
                && (State == PopupStoreGameDataState.Finalized ? LastRequestId == DecisionId : LastRequestId != DecisionId);
        }
        private bool RewardMatchesApprovedPolicy()
        {
            // Reuse the existing approved immutable policy definition; this does not instantiate
            // its Function/Database authority or adopt its separate wallet infrastructure.
            var policy = PandaRestaurant.PopupStoreServer.PopupStoreServerAuthority.ApprovedPolicy();
            int kind = RewardRoll < policy.RewardWeights[0] ? 0
                : RewardRoll < policy.RewardWeights[0] + policy.RewardWeights[1] ? 1 : 2;
            int index = ((int)HighestGrade - 1) * 3 + kind;
            return index >= 0 && index < policy.RewardAmounts.Length && (int)RewardKind == kind
                && Diamonds == policy.RewardAmounts[index];
        }
        private bool NoDecision() => RewardPolicyId == "" && DecisionId == "" && RewardRoll == -1 && Diamonds == 0 && RewardKind == PopupStoreRewardKind.Basic;
        private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
