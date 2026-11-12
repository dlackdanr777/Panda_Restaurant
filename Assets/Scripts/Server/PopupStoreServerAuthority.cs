using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Deploy this domain source with a trusted Function host. It contains no Backend SDK, Unity,
// client account lookup, concrete database, local clock or automatically registered game hook.
namespace PandaRestaurant.PopupStoreServer
{
    public enum Operation { Access, Start, Query, IssueOrder, Pack, Settle, Claim, Abandon }
    public enum Status { Ok, Invalid, Disabled, Unauthorized, Conflict, NotFound, ClockUnavailable, Rejected }
    public enum BusinessEndReason { None, Natural, Abandoned }

    [Serializable]
    public sealed class Command
    {
        public string Protocol = PopupStoreServerAuthority.Protocol;
        public Operation Operation;
        public string RequestId;
        public string LookupRequestId;
        public string BusinessSessionId;
        public string ExpectedPolicyVersion;
        public string OrderId;
        public string ReceiptId;
        public string[] Materials;
    }

    [Serializable]
    public sealed class Response
    {
        public string Protocol = PopupStoreServerAuthority.Protocol;
        public string RequestId;
        public Status Status;
        public string Error;
        public string AppliedRequestId, AppliedCommandHash;
        public Operation AppliedOperation;
        public AccessSnapshot Access;
        public BusinessSnapshot Business;
        public OrderSnapshot Order;
        public DecisionSnapshot Decision;
        public GrantSnapshot Grant;
        // Latest only on a fresh read, never a historical claim/replay balance.
        public WalletSnapshot Wallet;
    }

    [Serializable]
    public sealed class WalletSnapshot
    {
        public string AccountId, WalletId;
        public int Balance;
        public long Version;
    }

    [Serializable]
    public sealed class AccessSnapshot
    {
        public string AccountId, GameDay, PolicyVersion;
        public int DailyLimit, Used, Remaining;
        public long ServerUtcTicks, NextResetUtcTicks;
        public bool WritesEnabled;
    }

    [Serializable]
    public sealed class OrderSnapshot
    {
        public string OrderId, BusinessSessionId;
        public int Phase, Slot;
        public string[] Materials;
        public double IssuedAtGameplaySeconds, ArrivedAtGameplaySeconds = -1, ExpiresAtGameplaySeconds;
        public bool Packed, Expired;
    }

    [Serializable]
    public sealed class DecisionSnapshot
    {
        public string DecisionId, PolicyVersion;
        public int Grade, Kind, Diamonds;
    }

    [Serializable]
    public sealed class GrantSnapshot
    {
        public string GrantId, GrantKey, DecisionId;
        public int Diamonds, BalanceAfter;
        public long WalletVersion;
    }

    [Serializable]
    public sealed class BusinessSnapshot
    {
        public string AccountId, BusinessSessionId, StartRequestId, PolicyVersion, ClockProtocolVersion, StartGameDay;
        public long StartedAtUtcTicks;
        public int Phase, HighestClearedGrade, TotalScore, SuccessfulOrders;
        public BusinessEndReason EndReason;
        public bool IsSettled, AwaitingPhaseCleanup;
        public double ElapsedGameplaySeconds, PhaseDeadlineSeconds;
        public OrderSnapshot[] Orders;
        public DecisionSnapshot Decision;
        public GrantSnapshot Grant;
    }

    /// <summary>Created ONLY from the Function runtime's verified authentication context. Never
    /// deserialize a principal/account ID from a command, client cookie, custom header or journal.</summary>
    public sealed class AuthenticatedPrincipal
    {
        public string AccountId { get; private set; }
        public AuthenticatedPrincipal(string trustedAccountId) { AccountId = trustedAccountId; }
    }

    /// <summary>Read returns a detached authoritative snapshot. Transact must serialize or CAS
    /// the ENTIRE account aggregate, including request journal, business, grant, wallet and version.
    /// Its callback may retry after CAS contention; Commit=false must not write. A successful return
    /// means durable commit; network uncertainty must be surfaced by the host as indeterminate.
    /// No client GameData/table read-modify-write implementation conforms. Migration must cover ALL
    /// wallet writers, not only popup claims. Production implementations require deployment proof.</summary>
    public interface IAtomicAccountStore
    {
        bool AtomicAccountTransactionsVerified { get; }
        bool FullWalletMigrationVerified { get; }
        AccountState Read(string accountId);
        Response Transact(string accountId, Func<AccountState, AtomicMutation> operation);
    }

    public sealed class AtomicMutation
    {
        public AccountState State;
        public Response Response;
        public bool Commit;
        public AtomicMutation(AccountState state, Response response, bool commit)
        { State = state; Response = response; Commit = commit; }
    }

    /// <summary>UTC is server-owned and used for quota dates only. Gameplay elapsed is a separate
    /// monotonic, server-authenticated coordinate excluding approved transition/pause time. The
    /// production host MUST implement and verify pause, background, app quit, resume, reconnect and
    /// deadline rules before enabling this interface. Samples must cap at the persisted current
    /// phase deadline until cleanup/transition completes; they cannot carry into an unstarted phase.
    /// An abandon-cleanup hold freezes at the persisted elapsed coordinate. There is no DateTime.Now,
    /// Stopwatch, client timestamp or implicit continuous-wallclock/paused-on-quit fallback here.
    /// This service must be read-only within a transaction; its origin is the persisted business.</summary>
    public interface IServerClock
    {
        bool GameplayProtocolVerified { get; }
        string ProtocolVersion { get; }
        DateTimeOffset UtcNow { get; }
        bool TryGetElapsedGameplaySeconds(string accountId, string businessId, long startedAtUtcTicks, out double elapsed);
    }

    public interface ISecureRandom
    {
        bool IsCryptographicallySecure { get; }
        void Fill(byte[] bytes);
    }

    /// <summary>Trusted, read-only lifecycle evidence from the verified server gameplay protocol.
    /// A client arrival/delivery/leaving callback alone is NOT proof. Until this protocol is
    /// implemented, production admission is disabled. Unarrived orders cannot be packed; slots
    /// remain occupied through delivery/expiration and leaving, including a phase transition.</summary>
    public interface IServerOrderLifecycle
    {
        bool ProtocolVerified { get; }
        bool TryGetArrivalGameplaySeconds(string accountId, string businessId, string orderId, out double arrival);
        bool HasReleasedSlot(string accountId, string businessId, string orderId);
    }

    public sealed class SystemSecureRandom : ISecureRandom, IDisposable
    {
        private readonly RandomNumberGenerator _source = RandomNumberGenerator.Create();
        private readonly object _gate = new object();
        public bool IsCryptographicallySecure { get { return true; } }
        public void Fill(byte[] bytes) { lock (_gate) _source.GetBytes(bytes); }
        public void Dispose() { lock (_gate) _source.Dispose(); }
    }

    [Serializable]
    public sealed class DailyUsageState { public string GameDay; public int Used; }
    [Serializable]
    public sealed class RequestState { public string RequestId, Fingerprint; public Response Response; }
    [Serializable]
    public sealed class ReceiptState { public string ReceiptId, OrderId, Fingerprint; public Response Response; }

    /// <summary>Trusted store schema, never accepted from a request. Provision only through a
    /// verified migration that locks out legacy writers. Preserve history for supported recovery;
    /// retention/pruning is a server operational decision, never a client reset.</summary>
    [Serializable]
    public sealed class AccountState
    {
        public string AccountId, WalletId, WalletMigrationId;
        public bool WalletMigrationComplete;
        public int Diamonds;
        public long WalletVersion;
        public List<DailyUsageState> Usage = new List<DailyUsageState>();
        public List<BusinessState> Businesses = new List<BusinessState>();
        public List<RequestState> Requests = new List<RequestState>();
        public AccountState Copy() { return Copies.Account(this); }
    }

    [Serializable]
    public sealed class BusinessState
    {
        public BusinessSnapshot View;
        public PolicyState Policy;
        public double NextOrderAtGameplaySeconds;
        public bool AbandonPending;
        public List<ReceiptState> Receipts = new List<ReceiptState>();
    }

    /// <summary>Frozen at START. It is copied, persisted and compared against the recognized
    /// policy before execution, so mutable wire DTOs cannot silently alter in-flight balance.</summary>
    [Serializable]
    public sealed class PolicyState
    {
        public string Version, RewardVersion;
        public int DailyLimit;
        public int[] Seconds, Targets, PlushCounts, Patience, Points, RewardAmounts, RewardWeights;
        public double[] EntryIntervals;
    }

    public sealed class PopupStoreServerAuthority
    {
        public const string Protocol = "popup-store.dev-server-01.v1";
        public const string PolicyVersion = "PopupStore-DEV-SERVER-01-approved-v1";
        public const string RewardPolicyVersion = "PopupStoreDiamond-R1-20261103";

        /// <summary>The Function host uses this same typed envelope for pre-authority failures.
        /// It carries no applied-request evidence and cannot acknowledge a pending write.</summary>
        public static Response CreateHostFailureResponse(string requestId, Status status, string error)
        { return new Response { RequestId = requestId, Status = status, Error = error }; }

        private readonly IAtomicAccountStore _store;
        private readonly IServerClock _clock;
        private readonly ISecureRandom _random;
        private readonly IServerOrderLifecycle _lifecycle;
        private static readonly string[] Boxes = { "BOX_PINK", "BOX_BLUE", "BOX_MINT" };
        private static readonly string[] Ribbons = { "RIBBON_PINK", "RIBBON_YELLOW", "RIBBON_MINT" };

        public PopupStoreServerAuthority(IAtomicAccountStore store, IServerClock clock, ISecureRandom random, IServerOrderLifecycle lifecycle = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _lifecycle = lifecycle;
        }

        public bool WritesEnabled { get { return _store.AtomicAccountTransactionsVerified
            && _store.FullWalletMigrationVerified && _clock.GameplayProtocolVerified
            && !string.IsNullOrWhiteSpace(_clock.ProtocolVersion) && _random.IsCryptographicallySecure
            && _lifecycle != null && _lifecycle.ProtocolVerified; } }

        public static PolicyState ApprovedPolicy()
        {
            return new PolicyState { Version = PolicyVersion, RewardVersion = RewardPolicyVersion, DailyLimit = 1,
                Seconds = new[] { 22, 27, 30, 34, 38, 42 }, Targets = new[] { 200, 650, 1200, 1900, 2800, 3900 },
                PlushCounts = new[] { 5, 8, 12, 16, 24, 32 }, Patience = new[] { 22, 20, 18, 16, 14, 12 },
                Points = new[] { 100, 150, 220 }, EntryIntervals = new[] { 3.2, 3.0, 2.8, 2.6, 2.4, 2.2 },
                RewardWeights = new[] { 900, 97, 3 },
                RewardAmounts = new[] { 1, 3, 5, 3, 8, 15, 5, 15, 30, 10, 20, 100, 20, 30, 200, 30, 50, 300 } };
        }

        public Response Execute(AuthenticatedPrincipal principal, Command request)
        {
            if (principal == null || string.IsNullOrWhiteSpace(principal.AccountId) || principal.AccountId.Length > 256)
                return Reply(request, Status.Unauthorized, "A trusted authenticated principal is required.");
            // Copy before external store/clock callbacks. The caller cannot mutate a queued command.
            var command = CopyCommand(request);
            if (!ValidCommand(command)) return Reply(command, Status.Invalid, "Invalid protocol or command identity/payload.");
            if (command.Operation == Operation.Access || command.Operation == Operation.Query)
                return Read(principal.AccountId, command);
            if (!WritesEnabled) return Reply(command, Status.Disabled, "Verified atomic store, full wallet migration, secure RNG, gameplay clock and order lifecycle protocols are required.");
            // Store/transport exceptions intentionally propagate: a host must classify them as
            // indeterminate, never pretend that an admitted write was rejected before send.
            return _store.Transact(principal.AccountId, current => Mutate(principal.AccountId, command, current));
        }

        private Response Read(string accountId, Command command)
        {
            var state = _store.Read(accountId);
            if (!ValidAccount(state, accountId)) return Reply(command, Status.NotFound, "Authoritative account is not provisioned.");
            DateTimeOffset now;
            if (!TryUtc(out now)) return Reply(command, Status.ClockUnavailable, "Trusted UTC is unavailable.");
            var response = Reply(command, Status.Ok);
            response.Access = Access(state, now);
            response.Wallet = LatestWallet(state);
            if (command.Operation == Operation.Access) return response;
            if (command.LookupRequestId != null)
            {
                var previous = state.Requests.Find(x => x.RequestId == command.LookupRequestId);
                if (previous == null) { response.Status = Status.NotFound; response.Error = "No committed request found; this read is not a fence against an in-flight commit."; return response; }
                response = Copies.Response(previous.Response); response.RequestId = command.RequestId; response.Access = Access(state, now);
                response.Wallet = LatestWallet(state);
                return response;
            }
            BusinessState business = command.BusinessSessionId != null
                ? state.Businesses.Find(x => x.View.BusinessSessionId == command.BusinessSessionId) : Recoverable(state);
            if (business == null) { response.Status = Status.NotFound; response.Error = "No matching recoverable business."; return response; }
            return WithBusiness(response, business);
        }

        private AtomicMutation Mutate(string accountId, Command command, AccountState current)
        {
            if (!ValidAccount(current, accountId)) return NoWrite(current, command, Status.NotFound, "Authoritative account is not provisioned.");
            var state = current.Copy();
            string fingerprint = Fingerprint(command);
            var replay = state.Requests.Find(x => x.RequestId == command.RequestId);
            if (replay != null)
                return new AtomicMutation(current, replay.Fingerprint == fingerprint ? Copies.Response(replay.Response)
                    : Reply(command, Status.Conflict, "Request ID was already bound to another payload."), false);
            if (!WritesEnabled || !state.WalletMigrationComplete || string.IsNullOrWhiteSpace(state.WalletMigrationId) || string.IsNullOrWhiteSpace(state.WalletId))
                return NoWrite(current, command, Status.Disabled, "This account has no verified complete wallet migration.");
            DateTimeOffset now;
            if (!TryUtc(out now)) return NoWrite(current, command, Status.ClockUnavailable, "Trusted UTC is unavailable.");
            Response response;
            if (command.Operation == Operation.Start) response = Start(state, command, now);
            else
            {
                var business = state.Businesses.Find(x => x.View.BusinessSessionId == command.BusinessSessionId);
                if (business == null) response = Reply(command, Status.NotFound, "Business does not belong to the authenticated account.");
                else if (!RecognizedPolicy(business.Policy) || business.View.PolicyVersion != business.Policy.Version)
                    return NoWrite(current, command, Status.Disabled, "Frozen business policy is not recognized.");
                else response = BusinessCommand(state, business, command);
            }
            response.Access = Access(state, now);
            // Every acknowledged mutation attempt is journaled with its exact response. An SDK
            // timeout/retry cannot create a new order, reroll a reward or reapply a receipt.
            response.AppliedRequestId = command.RequestId;
            response.AppliedOperation = command.Operation;
            response.AppliedCommandHash = fingerprint;
            state.Requests.Add(new RequestState { RequestId = command.RequestId, Fingerprint = fingerprint, Response = Copies.Response(response) });
            return new AtomicMutation(state, response, true);
        }

        private Response Start(AccountState state, Command command, DateTimeOffset now)
        {
            var unresolved = Recoverable(state);
            if (unresolved != null) return WithBusiness(Reply(command, Status.Conflict, "Recover the existing business before starting another."), unresolved);
            if (command.ExpectedPolicyVersion != PolicyVersion) return Reply(command, Status.Conflict, "Refresh the current server policy before starting.");
            string day = GameDay(now);
            var usage = state.Usage.Find(x => x.GameDay == day);
            if (usage != null && usage.Used >= 1) return Reply(command, Status.Rejected, "The daily business allowance has been used.");
            var policy = ApprovedPolicy();
            var business = new BusinessState { Policy = policy, View = new BusinessSnapshot {
                AccountId = state.AccountId, BusinessSessionId = NewId("business"), StartRequestId = command.RequestId,
                PolicyVersion = policy.Version, ClockProtocolVersion = _clock.ProtocolVersion, StartGameDay = day,
                StartedAtUtcTicks = now.UtcDateTime.Ticks, Phase = 1, PhaseDeadlineSeconds = policy.Seconds[0], Orders = new OrderSnapshot[0] } };
            if (usage == null) { usage = new DailyUsageState { GameDay = day }; state.Usage.Add(usage); }
            usage.Used++; state.Businesses.Add(business);
            return WithBusiness(Reply(command, Status.Ok), business);
        }

        private Response BusinessCommand(AccountState account, BusinessState business, Command command)
        {
            var view = business.View;
            // Receipt replay is valid after expiry, phase changes, settlement and claim.
            if (command.Operation == Operation.Pack)
            {
                var receipt = business.Receipts.Find(x => x.ReceiptId == command.ReceiptId);
                if (receipt != null)
                {
                    if (receipt.OrderId != command.OrderId || receipt.Fingerprint != MaterialsKey(command.Materials))
                        return WithBusiness(Reply(command, Status.Conflict, "Receipt identity was reused with different packing."), business);
                    var original = Copies.Response(receipt.Response); original.RequestId = command.RequestId; return original;
                }
            }
            if (!view.IsSettled)
            {
                double elapsed;
                if (view.ClockProtocolVersion != _clock.ProtocolVersion || !_clock.TryGetElapsedGameplaySeconds(account.AccountId,
                    view.BusinessSessionId, view.StartedAtUtcTicks, out elapsed) || !Finite(elapsed)
                    || elapsed < view.ElapsedGameplaySeconds || elapsed > view.PhaseDeadlineSeconds
                    || (business.AbandonPending && elapsed != view.ElapsedGameplaySeconds))
                    return WithBusiness(Reply(command, Status.ClockUnavailable, "Trusted gameplay time is unavailable or moved backwards."), business);
                if (!ObserveArrivals(business, elapsed))
                    return WithBusiness(Reply(command, Status.ClockUnavailable, "Trusted order arrival evidence is inconsistent."), business);
                Advance(business, elapsed);
            }
            switch (command.Operation)
            {
                case Operation.IssueOrder: return Issue(business, command);
                case Operation.Pack: return Pack(business, command);
                case Operation.Settle: return WithBusiness(Reply(command, Status.Ok), business);
                case Operation.Abandon:
                    // Successful-phase cleanup remains abandonable, as the approved phase
                    // session permits leaving before the next phase. A failed deadline has
                    // already selected natural settlement and cannot be relabelled.
                    if (!view.IsSettled && view.AwaitingPhaseCleanup && !business.AbandonPending
                        && view.TotalScore < business.Policy.Targets[view.Phase - 1])
                        return WithBusiness(Reply(command, Status.Rejected, "Natural settlement is already pending cleanup."), business);
                    if (!view.IsSettled)
                    {
                        business.AbandonPending = true; view.AwaitingPhaseCleanup = true;
                        foreach (var order in view.Orders) if (!order.Packed) order.Expired = true;
                        if (ReleasedPhase(business)) Finish(business, BusinessEndReason.Abandoned);
                    }
                    return WithBusiness(Reply(command, Status.Ok), business);
                case Operation.Claim: return Claim(account, business, command);
                default: return Reply(command, Status.Invalid, "Unsupported business operation.");
            }
        }

        private void Advance(BusinessState business, double target)
        {
            var view = business.View;
            if (business.AbandonPending)
            { if (ReleasedPhase(business)) Finish(business, BusinessEndReason.Abandoned); return; }
            while (!view.IsSettled)
            {
                double next = view.PhaseDeadlineSeconds;
                foreach (var order in view.Orders)
                    if (!order.Packed && !order.Expired && order.Phase == view.Phase) next = Math.Min(next, order.ExpiresAtGameplaySeconds);
                if (next > target) { view.ElapsedGameplaySeconds = target; return; }
                view.ElapsedGameplaySeconds = Math.Max(view.ElapsedGameplaySeconds, next);
                // A deadline wins a tie with packing/expiry. No sale can arrive at remaining=0.
                if (view.PhaseDeadlineSeconds > view.ElapsedGameplaySeconds)
                {
                    int expired = 0;
                    foreach (var order in view.Orders)
                        if (!order.Packed && !order.Expired && order.Phase == view.Phase && order.ExpiresAtGameplaySeconds <= view.ElapsedGameplaySeconds)
                        { order.Expired = true; expired++; }
                    view.PhaseDeadlineSeconds = Math.Max(view.ElapsedGameplaySeconds, view.PhaseDeadlineSeconds - expired * 3d);
                    if (view.PhaseDeadlineSeconds > view.ElapsedGameplaySeconds) continue;
                }
                view.AwaitingPhaseCleanup = true;
                foreach (var order in view.Orders) if (!order.Packed) order.Expired = true;
                if (!ReleasedPhase(business)) return;
                view.AwaitingPhaseCleanup = false;
                if (view.TotalScore < business.Policy.Targets[view.Phase - 1]) { Finish(business, BusinessEndReason.Natural); return; }
                view.HighestClearedGrade = view.Phase;
                if (view.Phase == 6) { Finish(business, BusinessEndReason.Natural); return; }
                foreach (var order in view.Orders) if (!order.Packed) order.Expired = true;
                view.Phase++; view.PhaseDeadlineSeconds = view.ElapsedGameplaySeconds + business.Policy.Seconds[view.Phase - 1];
                business.NextOrderAtGameplaySeconds = view.ElapsedGameplaySeconds;
                return; // One boundary per command; no unverified transition-time carry.
            }
        }

        private bool ReleasedPhase(BusinessState business)
        {
            var view = business.View;
            foreach (var order in view.Orders)
                if (order.Phase == view.Phase && !_lifecycle.HasReleasedSlot(view.AccountId, view.BusinessSessionId, order.OrderId)) return false;
            return true;
        }

        private bool ObserveArrivals(BusinessState business, double elapsed)
        {
            var view = business.View;
            var pending = new List<KeyValuePair<OrderSnapshot, double>>();
            foreach (var order in view.Orders)
            {
                if (order.Packed || order.Expired || order.Phase != view.Phase) continue;
                double arrival;
                if (!_lifecycle.TryGetArrivalGameplaySeconds(view.AccountId, view.BusinessSessionId, order.OrderId, out arrival)) continue;
                if (!Finite(arrival) || arrival < order.IssuedAtGameplaySeconds || arrival > elapsed
                    || (order.ArrivedAtGameplaySeconds >= 0 && order.ArrivedAtGameplaySeconds != arrival)) return false;
                if (order.ArrivedAtGameplaySeconds < 0)
                    pending.Add(new KeyValuePair<OrderSnapshot, double>(order, arrival));
            }
            foreach (var entry in pending)
            {
                var order = entry.Key; order.ArrivedAtGameplaySeconds = entry.Value;
                order.ExpiresAtGameplaySeconds = entry.Value + business.Policy.Patience[order.Phase - 1] + 4d * (order.Materials.Length - 3);
            }
            return true;
        }

        private Response Issue(BusinessState business, Command command)
        {
            var view = business.View;
            if (view.IsSettled || view.AwaitingPhaseCleanup) return WithBusiness(Reply(command, Status.Rejected, "Business is settled or waiting for trusted phase cleanup."), business);
            if (view.PhaseDeadlineSeconds - view.ElapsedGameplaySeconds <= 10d)
                return WithBusiness(Reply(command, Status.Rejected, "Customer entry is closed for this phase."), business);
            if (view.ElapsedGameplaySeconds < business.NextOrderAtGameplaySeconds)
                return WithBusiness(Reply(command, Status.Rejected, "Customer entry interval has not elapsed."), business);
            int slot = -1;
            for (int i = 0; i < 3; i++)
            {
                bool occupied = Array.Exists(view.Orders, x => x.Slot == i && ((!x.Packed && !x.Expired)
                    || !_lifecycle.HasReleasedSlot(view.AccountId, view.BusinessSessionId, x.OrderId)));
                if (!occupied) { slot = i; break; }
            }
            if (slot < 0) return WithBusiness(Reply(command, Status.Rejected, "All three server order slots are occupied."), business);
            int roll = Next(100), phase = view.Phase;
            int plushes = phase == 1 ? 1 : phase == 2 ? (roll < 70 ? 1 : 2) : phase == 3 ? (roll < 40 ? 1 : 2)
                : phase == 4 ? (roll < 70 ? 2 : 3) : phase == 5 ? (roll < 50 ? 2 : 3) : (roll < 20 ? 2 : 3);
            var materials = new string[plushes + 2];
            for (int i = 0; i < plushes; i++) materials[i] = "STAFF" + (Next(business.Policy.PlushCounts[phase - 1]) + 1).ToString("00", CultureInfo.InvariantCulture);
            materials[plushes] = Boxes[Next(3)]; materials[plushes + 1] = Ribbons[Next(3)];
            var order = new OrderSnapshot { OrderId = NewId("order"), BusinessSessionId = view.BusinessSessionId, Phase = phase, Slot = slot,
                Materials = materials, IssuedAtGameplaySeconds = view.ElapsedGameplaySeconds,
                ExpiresAtGameplaySeconds = double.MaxValue };
            var orders = new List<OrderSnapshot>(view.Orders); orders.Add(order); view.Orders = orders.ToArray();
            business.NextOrderAtGameplaySeconds = view.ElapsedGameplaySeconds + business.Policy.EntryIntervals[phase - 1];
            var response = WithBusiness(Reply(command, Status.Ok), business); response.Order = Copies.Order(order); return response;
        }

        private Response Pack(BusinessState business, Command command)
        {
            var view = business.View;
            var order = Array.Find(view.Orders, x => x.OrderId == command.OrderId);
            if (view.IsSettled || view.AwaitingPhaseCleanup || order == null || order.BusinessSessionId != view.BusinessSessionId || order.Phase != view.Phase
                || order.Packed || order.Expired || order.ArrivedAtGameplaySeconds < 0 || view.ElapsedGameplaySeconds >= order.ExpiresAtGameplaySeconds
                || view.ElapsedGameplaySeconds >= view.PhaseDeadlineSeconds)
                return WithBusiness(Reply(command, Status.Rejected, "Order is not active in this business and phase."), business);
            if (MaterialsKey(order.Materials) != MaterialsKey(command.Materials))
                return WithBusiness(Reply(command, Status.Conflict, "Packing must exactly match all material identities and quantities."), business);
            foreach (var candidate in view.Orders)
            {
                if (candidate.Phase != view.Phase || candidate.Packed || candidate.Expired || candidate.ArrivedAtGameplaySeconds < 0
                    || view.ElapsedGameplaySeconds >= candidate.ExpiresAtGameplaySeconds || MaterialsKey(candidate.Materials) != MaterialsKey(command.Materials)) continue;
                if (candidate.ExpiresAtGameplaySeconds < order.ExpiresAtGameplaySeconds
                    || (candidate.ExpiresAtGameplaySeconds == order.ExpiresAtGameplaySeconds && candidate.Slot < order.Slot))
                    return WithBusiness(Reply(command, Status.Conflict, "Exact packing must select shortest remaining patience, then the leftmost slot."), business);
            }
            int points = business.Policy.Points[order.Materials.Length - 3];
            if ((long)view.TotalScore + points > int.MaxValue || view.SuccessfulOrders == int.MaxValue)
                return WithBusiness(Reply(command, Status.Rejected, "Score/count arithmetic limit."), business);
            order.Packed = true; view.TotalScore += points; view.SuccessfulOrders++;
            var response = WithBusiness(Reply(command, Status.Ok), business);
            business.Receipts.Add(new ReceiptState { ReceiptId = command.ReceiptId, OrderId = order.OrderId,
                Fingerprint = MaterialsKey(command.Materials), Response = Copies.Response(response) });
            return response;
        }

        private void Finish(BusinessState business, BusinessEndReason reason)
        {
            var view = business.View;
            if (view.IsSettled) return;
            view.IsSettled = true; view.EndReason = reason; view.AwaitingPhaseCleanup = false;
            foreach (var order in view.Orders) if (!order.Packed) order.Expired = true;
            int grade = reason == BusinessEndReason.Abandoned ? 0 : view.HighestClearedGrade;
            int kind = 0, amount = 0;
            if (grade > 0)
            {
                int roll = Next(1000); kind = roll < 900 ? 0 : roll < 997 ? 1 : 2;
                amount = business.Policy.RewardAmounts[(grade - 1) * 3 + kind];
            }
            view.Decision = new DecisionSnapshot { DecisionId = NewId("decision"), PolicyVersion = business.Policy.RewardVersion,
                Grade = grade, Kind = kind, Diamonds = amount };
        }

        private Response Claim(AccountState account, BusinessState business, Command command)
        {
            var view = business.View;
            if (!view.IsSettled || view.Decision == null) return WithBusiness(Reply(command, Status.Rejected, "Settle the server business before claiming."), business);
            if (view.Grant != null || view.Decision.Grade == 0) return WithBusiness(Reply(command, Status.Ok), business);
            int amount = view.Decision.Diamonds;
            if ((long)account.Diamonds + amount > int.MaxValue || account.WalletVersion == long.MaxValue)
                return WithBusiness(Reply(command, Status.Rejected, "Wallet arithmetic limit."), business);
            account.Diamonds += amount; account.WalletVersion++;
            view.Grant = new GrantSnapshot { GrantId = NewId("grant"), GrantKey = GrantKey(account.AccountId, view.BusinessSessionId),
                DecisionId = view.Decision.DecisionId, Diamonds = amount, BalanceAfter = account.Diamonds, WalletVersion = account.WalletVersion };
            return WithBusiness(Reply(command, Status.Ok), business);
        }

        public static string GrantKey(string accountId, string businessId)
        { return "PopupStoreReward:" + accountId.Length.ToString(CultureInfo.InvariantCulture) + ":" + accountId + ":"
            + businessId.Length.ToString(CultureInfo.InvariantCulture) + ":" + businessId; }
        public static string GameDay(DateTimeOffset utc)
        { return utc.ToUniversalTime().AddHours(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static long NextResetUtcTicks(DateTimeOffset utc)
        { var date = utc.ToUniversalTime().UtcDateTime.Date.AddHours(3); if (utc.UtcDateTime >= date) date = date.AddDays(1); return date.Ticks; }

        private bool TryUtc(out DateTimeOffset now)
        { try { now = _clock.UtcNow; return now.Offset == TimeSpan.Zero && now.Year >= 2020 && now.Year < 9999; } catch { now = default(DateTimeOffset); return false; } }
        private AccessSnapshot Access(AccountState state, DateTimeOffset now)
        {
            string day = GameDay(now); var row = state.Usage.Find(x => x.GameDay == day); int used = row == null ? 0 : row.Used;
            return new AccessSnapshot { AccountId = state.AccountId, GameDay = day, PolicyVersion = PolicyVersion, DailyLimit = 1,
                Used = used, Remaining = Math.Max(0, 1 - used), ServerUtcTicks = now.UtcDateTime.Ticks, NextResetUtcTicks = NextResetUtcTicks(now),
                WritesEnabled = WritesEnabled && state.WalletMigrationComplete && !string.IsNullOrWhiteSpace(state.WalletMigrationId) && !string.IsNullOrWhiteSpace(state.WalletId) };
        }
        private static WalletSnapshot LatestWallet(AccountState state)
        { return !state.WalletMigrationComplete || string.IsNullOrWhiteSpace(state.WalletId) ? null
            : new WalletSnapshot { AccountId = state.AccountId, WalletId = state.WalletId, Balance = state.Diamonds, Version = state.WalletVersion }; }
        private static BusinessState Recoverable(AccountState state)
        { return state.Businesses.Find(x => !x.View.IsSettled || (x.View.Decision != null && x.View.Decision.Grade > 0 && x.View.Grant == null)); }
        private static bool ValidAccount(AccountState state, string accountId)
        { return state != null && state.AccountId == accountId && state.Diamonds >= 0 && state.WalletVersion >= 0
            && state.Usage != null && state.Businesses != null && state.Requests != null; }
        private static bool RecognizedPolicy(PolicyState value)
        {
            var approved = ApprovedPolicy();
            return value != null && value.Version == approved.Version && value.RewardVersion == approved.RewardVersion && value.DailyLimit == 1
                && Equal(value.Seconds, approved.Seconds) && Equal(value.Targets, approved.Targets) && Equal(value.PlushCounts, approved.PlushCounts)
                && Equal(value.Patience, approved.Patience) && Equal(value.Points, approved.Points) && Equal(value.RewardAmounts, approved.RewardAmounts)
                && Equal(value.RewardWeights, approved.RewardWeights) && Equal(value.EntryIntervals, approved.EntryIntervals);
        }
        private static bool Equal<T>(T[] left, T[] right)
        { if (left == null || right == null || left.Length != right.Length) return false; for (int i = 0; i < left.Length; i++) if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false; return true; }
        private int Next(int bound)
        {
            var bytes = new byte[4]; uint limit = uint.MaxValue - (uint.MaxValue % (uint)bound);
            uint value; do { _random.Fill(bytes); value = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24); } while (value >= limit);
            return (int)(value % bound);
        }
        private string NewId(string prefix)
        { var bytes = new byte[16]; _random.Fill(bytes); var result = new StringBuilder(prefix + "-"); foreach (byte value in bytes) result.Append(value.ToString("x2", CultureInfo.InvariantCulture)); return result.ToString(); }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0; }
        private static bool Identifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_' && c != '.' && c != ':') return false;
            return true;
        }
        private static bool ValidCommand(Command command)
        {
            if (command == null || command.Protocol != Protocol || !Identifier(command.RequestId) || !Enum.IsDefined(typeof(Operation), command.Operation)) return false;
            if (command.LookupRequestId != null && !Identifier(command.LookupRequestId)) return false;
            if (command.BusinessSessionId != null && !Identifier(command.BusinessSessionId)) return false;
            if (command.ExpectedPolicyVersion != null && !Identifier(command.ExpectedPolicyVersion)) return false;
            if (command.Operation == Operation.Access && (command.BusinessSessionId != null || command.LookupRequestId != null || command.ExpectedPolicyVersion != null)) return false;
            if (command.Operation == Operation.Start && (command.ExpectedPolicyVersion == null || command.BusinessSessionId != null || command.LookupRequestId != null)) return false;
            if (command.Operation != Operation.Start && command.ExpectedPolicyVersion != null) return false;
            if (command.Operation != Operation.Query && command.LookupRequestId != null) return false;
            if (command.Operation != Operation.Access && command.Operation != Operation.Start && command.Operation != Operation.Query && command.BusinessSessionId == null) return false;
            if (command.Operation == Operation.Pack)
            {
                if (!Identifier(command.OrderId) || !Identifier(command.ReceiptId) || command.Materials == null || command.Materials.Length < 3 || command.Materials.Length > 5) return false;
                foreach (string material in command.Materials) if (!Identifier(material)) return false;
            }
            else if (command.Materials != null || command.OrderId != null || command.ReceiptId != null) return false;
            return true;
        }
        private static Command CopyCommand(Command c)
        { return c == null ? null : new Command { Protocol = c.Protocol, Operation = c.Operation, RequestId = c.RequestId, LookupRequestId = c.LookupRequestId,
            BusinessSessionId = c.BusinessSessionId, ExpectedPolicyVersion = c.ExpectedPolicyVersion, OrderId = c.OrderId, ReceiptId = c.ReceiptId,
            Materials = c.Materials == null ? null : (string[])c.Materials.Clone() }; }
        private static string MaterialsKey(string[] materials)
        { if (materials == null) return "null"; var sorted = (string[])materials.Clone(); Array.Sort(sorted, StringComparer.Ordinal); var result = new StringBuilder(); foreach (var item in sorted) Append(result, item); return result.ToString(); }
        private static void Append(StringBuilder text, string value)
        { if (value == null) text.Append("-1:"); else text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value); }
        public static string CommandHash(Command command)
        {
            if (!ValidCommand(command)) throw new ArgumentException("A valid immutable command is required.", nameof(command));
            return Fingerprint(CopyCommand(command));
        }
        private static string Fingerprint(Command command)
        {
            var text = new StringBuilder(); Append(text, command.Protocol); Append(text, ((int)command.Operation).ToString(CultureInfo.InvariantCulture));
            Append(text, command.RequestId); Append(text, command.LookupRequestId); Append(text, command.BusinessSessionId); Append(text, command.ExpectedPolicyVersion);
            Append(text, command.OrderId); Append(text, command.ReceiptId); Append(text, MaterialsKey(command.Materials));
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static Response Reply(Command command, Status status, string error = null)
        { return new Response { RequestId = command == null ? null : command.RequestId, Status = status, Error = error }; }
        private static Response WithBusiness(Response response, BusinessState business)
        { response.Business = Copies.BusinessView(business.View); response.Decision = Copies.Decision(business.View.Decision); response.Grant = Copies.Grant(business.View.Grant); return response; }
        private static Response WithAccess(Response response, AccessSnapshot access) { response.Access = access; return response; }
        private static AtomicMutation NoWrite(AccountState state, Command command, Status status, string error)
        { return new AtomicMutation(state, Reply(command, status, error), false); }
    }

    // Explicit deep copies keep a returned mutable DTO from mutating authority state or replay
    // history. No serializer, filesystem or engine dependency is needed by the deployed core.
    internal static class Copies
    {
        internal static AccountState Account(AccountState x)
        {
            var y = new AccountState { AccountId = x.AccountId, WalletId = x.WalletId, WalletMigrationId = x.WalletMigrationId, WalletMigrationComplete = x.WalletMigrationComplete,
                Diamonds = x.Diamonds, WalletVersion = x.WalletVersion };
            foreach (var d in x.Usage) y.Usage.Add(new DailyUsageState { GameDay = d.GameDay, Used = d.Used });
            foreach (var r in x.Requests) y.Requests.Add(new RequestState { RequestId = r.RequestId, Fingerprint = r.Fingerprint, Response = Response(r.Response) });
            foreach (var b in x.Businesses)
            {
                var copy = new BusinessState { View = BusinessView(b.View), Policy = Policy(b.Policy), NextOrderAtGameplaySeconds = b.NextOrderAtGameplaySeconds, AbandonPending = b.AbandonPending };
                foreach (var r in b.Receipts) copy.Receipts.Add(new ReceiptState { ReceiptId = r.ReceiptId, OrderId = r.OrderId, Fingerprint = r.Fingerprint, Response = Response(r.Response) });
                y.Businesses.Add(copy);
            }
            return y;
        }
        internal static PolicyState Policy(PolicyState p)
        { return p == null ? null : new PolicyState { Version = p.Version, RewardVersion = p.RewardVersion, DailyLimit = p.DailyLimit,
            Seconds = Clone(p.Seconds), Targets = Clone(p.Targets), PlushCounts = Clone(p.PlushCounts), Patience = Clone(p.Patience), Points = Clone(p.Points),
            RewardAmounts = Clone(p.RewardAmounts), RewardWeights = Clone(p.RewardWeights), EntryIntervals = p.EntryIntervals == null ? null : (double[])p.EntryIntervals.Clone() }; }
        private static int[] Clone(int[] source) { return source == null ? null : (int[])source.Clone(); }
        internal static Response Response(Response x)
        { return x == null ? null : new Response { Protocol = x.Protocol, RequestId = x.RequestId, Status = x.Status, Error = x.Error,
            AppliedRequestId = x.AppliedRequestId, AppliedOperation = x.AppliedOperation, AppliedCommandHash = x.AppliedCommandHash,
            Access = Access(x.Access), Business = BusinessView(x.Business), Order = Order(x.Order), Decision = Decision(x.Decision), Grant = Grant(x.Grant),
            Wallet = x.Wallet == null ? null : new WalletSnapshot { AccountId = x.Wallet.AccountId, WalletId = x.Wallet.WalletId, Balance = x.Wallet.Balance, Version = x.Wallet.Version } }; }
        private static AccessSnapshot Access(AccessSnapshot x)
        { return x == null ? null : new AccessSnapshot { AccountId = x.AccountId, GameDay = x.GameDay, PolicyVersion = x.PolicyVersion, DailyLimit = x.DailyLimit,
            Used = x.Used, Remaining = x.Remaining, ServerUtcTicks = x.ServerUtcTicks, NextResetUtcTicks = x.NextResetUtcTicks, WritesEnabled = x.WritesEnabled }; }
        internal static BusinessSnapshot BusinessView(BusinessSnapshot x)
        {
            if (x == null) return null;
            var y = new BusinessSnapshot { AccountId = x.AccountId, BusinessSessionId = x.BusinessSessionId, StartRequestId = x.StartRequestId,
                PolicyVersion = x.PolicyVersion, ClockProtocolVersion = x.ClockProtocolVersion, StartGameDay = x.StartGameDay, StartedAtUtcTicks = x.StartedAtUtcTicks,
                Phase = x.Phase, HighestClearedGrade = x.HighestClearedGrade, TotalScore = x.TotalScore, SuccessfulOrders = x.SuccessfulOrders, EndReason = x.EndReason,
                IsSettled = x.IsSettled, AwaitingPhaseCleanup = x.AwaitingPhaseCleanup, ElapsedGameplaySeconds = x.ElapsedGameplaySeconds, PhaseDeadlineSeconds = x.PhaseDeadlineSeconds,
                Decision = Decision(x.Decision), Grant = Grant(x.Grant), Orders = new OrderSnapshot[x.Orders.Length] };
            for (int i = 0; i < x.Orders.Length; i++) y.Orders[i] = Order(x.Orders[i]); return y;
        }
        internal static OrderSnapshot Order(OrderSnapshot x)
        { return x == null ? null : new OrderSnapshot { OrderId = x.OrderId, BusinessSessionId = x.BusinessSessionId, Phase = x.Phase, Slot = x.Slot,
            Materials = x.Materials == null ? null : (string[])x.Materials.Clone(), IssuedAtGameplaySeconds = x.IssuedAtGameplaySeconds,
            ArrivedAtGameplaySeconds = x.ArrivedAtGameplaySeconds,
            ExpiresAtGameplaySeconds = x.ExpiresAtGameplaySeconds, Packed = x.Packed, Expired = x.Expired }; }
        internal static DecisionSnapshot Decision(DecisionSnapshot x)
        { return x == null ? null : new DecisionSnapshot { DecisionId = x.DecisionId, PolicyVersion = x.PolicyVersion, Grade = x.Grade, Kind = x.Kind, Diamonds = x.Diamonds }; }
        internal static GrantSnapshot Grant(GrantSnapshot x)
        { return x == null ? null : new GrantSnapshot { GrantId = x.GrantId, GrantKey = x.GrantKey, DecisionId = x.DecisionId, Diamonds = x.Diamonds, BalanceAfter = x.BalanceAfter, WalletVersion = x.WalletVersion }; }
    }
}
