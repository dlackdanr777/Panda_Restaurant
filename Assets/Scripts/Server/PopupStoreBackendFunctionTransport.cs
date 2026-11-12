using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public enum PopupStoreFunctionTransportOutcome
    { Unavailable, RejectedBeforeSend, ResponseReceived, Indeterminate }

    public sealed class PopupStoreFunctionTransportResult
    {
        public PopupStoreFunctionTransportOutcome Outcome { get; }
        public string ResponseJson { get; }
        public string ErrorCode { get; }
        internal PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome outcome,
            string responseJson = null, string errorCode = null)
        { Outcome = outcome; ResponseJson = responseJson; ErrorCode = errorCode; }
    }

    public interface IPopupStoreFunctionTransport
    {
        void Send(string commandJson, Action<PopupStoreFunctionTransportResult> completed);
    }

    /// <summary>Deployment evidence supplied by a future development host, never authentication
    /// or server authority. No configuration asset is created and all gates default to false.</summary>
    public sealed class PopupStoreFunctionDevelopmentConfiguration
    {
        public const string SupportedProtocol = "popup-store.dev-server-01.v1";
        public bool Enabled { get; }
        public string Environment { get; }
        public string FunctionName { get; }
        public string Protocol { get; }
        public bool DevelopmentProjectVerified { get; }
        public bool FunctionProtocolVerified { get; }
        public bool AtomicWalletMigrationVerified { get; }
        public TimeSpan Timeout { get; }

        public PopupStoreFunctionDevelopmentConfiguration(bool enabled = false, string environment = null,
            string functionName = null, string protocol = SupportedProtocol,
            bool developmentProjectVerified = false, bool functionProtocolVerified = false,
            bool atomicWalletMigrationVerified = false, int timeoutSeconds = 30)
        {
            Enabled = enabled; Environment = environment; FunctionName = functionName; Protocol = protocol;
            DevelopmentProjectVerified = developmentProjectVerified;
            FunctionProtocolVerified = functionProtocolVerified;
            AtomicWalletMigrationVerified = atomicWalletMigrationVerified;
            Timeout = TimeSpan.FromSeconds(Math.Max(1, Math.Min(120, timeoutSeconds)));
        }

        internal bool IsValid
        {
            get
            {
                if (!Enabled || Environment != "development" || !DevelopmentProjectVerified
                    || !FunctionProtocolVerified || !AtomicWalletMigrationVerified
                    || Protocol != SupportedProtocol || string.IsNullOrEmpty(FunctionName)
                    || !FunctionName.StartsWith("dev_", StringComparison.Ordinal) || FunctionName.Length > 64)
                    return false;
                foreach (char c in FunctionName)
                    if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z')
                        && !(c >= '0' && c <= '9') && c != '_') return false;
                return FunctionName.Length > 4;
            }
        }
    }

    /// <summary>SDK 5.15 BFunc transport only. It never initializes, logs in, retries, grants,
    /// saves, or activates UI. Create a new instance for each authenticated restoration epoch.
    /// Post-send uncertainty is never evidence that the authority did nothing.</summary>
    public sealed class PopupStoreBackendFunctionTransport : IPopupStoreFunctionTransport
    {
        internal sealed class NativeResponse
        {
            internal readonly bool Success;
            internal readonly string Json;
            internal NativeResponse(bool success, string json) { Success = success; Json = json; }
        }

        private readonly PopupStoreFunctionDevelopmentConfiguration _configuration;
        private readonly Func<bool> _sessionCurrent;
        private readonly Func<bool> _environmentCurrent;
        private readonly Action<Action> _dispatch;
        private readonly Action<string, Param, Action<NativeResponse>> _invoke;
        private readonly Func<Action, TimeSpan, IDisposable> _schedule;
        private readonly int _ownerThread;

        public PopupStoreBackendFunctionTransport()
            : this(new PopupStoreFunctionDevelopmentConfiguration(), () => false, () => false,
                null, null, null) { }

        /// <summary>Call on the Unity main thread after ordinary login/restore. The validation
        /// delegate must represent a separately verified development deployment, not a UI toggle.</summary>
        public static PopupStoreBackendFunctionTransport CreateForCurrentSession(BackendManager owner,
            PopupStoreFunctionDevelopmentConfiguration configuration, Func<bool> environmentStillVerified)
        {
            var context = SynchronizationContext.Current;
            var query = owner == null ? null : owner.CurrentMailReceiveQuery;
            return new PopupStoreBackendFunctionTransport(configuration,
                () => owner != null && query != null && owner.IsLogin && Backend.IsInitialized && Backend.IsLogin
                    && ReferenceEquals(owner.CurrentMailReceiveQuery, query) && owner.IsCurrentGameDataQuery(query),
                environmentStillVerified, context == null ? (Action<Action>)null
                    : action => context.Post(_ => action(), null), InvokeSdk, ScheduleTimeout);
        }

        // The injection seam exercises the real transport state machine without SDK/network calls.
        internal PopupStoreBackendFunctionTransport(PopupStoreFunctionDevelopmentConfiguration configuration,
            Func<bool> sessionCurrent, Func<bool> environmentCurrent, Action<Action> dispatch,
            Action<string, Param, Action<NativeResponse>> invoke,
            Func<Action, TimeSpan, IDisposable> schedule)
        {
            _configuration = configuration ?? new PopupStoreFunctionDevelopmentConfiguration();
            _sessionCurrent = sessionCurrent; _environmentCurrent = environmentCurrent;
            _dispatch = dispatch; _invoke = invoke; _schedule = schedule;
            _ownerThread = Thread.CurrentThread.ManagedThreadId;
        }

        public void Send(string commandJson, Action<PopupStoreFunctionTransportResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (Thread.CurrentThread.ManagedThreadId != _ownerThread)
            {
                Post(() => completed(Result(PopupStoreFunctionTransportOutcome.RejectedBeforeSend, "main_thread_required")));
                return;
            }
            if (!DevelopmentProcess || !_configuration.IsValid || _dispatch == null || _invoke == null
                || _schedule == null || !SafeCheck(_environmentCurrent))
            { completed(Result(PopupStoreFunctionTransportOutcome.Unavailable, "development_unavailable")); return; }
            if (!SafeCheck(_sessionCurrent))
            { completed(Result(PopupStoreFunctionTransportOutcome.RejectedBeforeSend, "session_unavailable")); return; }

            string requestId;
            Param body;
            try
            {
                if (string.IsNullOrWhiteSpace(commandJson) || commandJson.Length > 65536)
                    throw new ArgumentException();
                var command = JObject.Parse(commandJson,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (command["Protocol"]?.Type != JTokenType.String
                    || (string)command["Protocol"] != _configuration.Protocol
                    || command["RequestId"]?.Type != JTokenType.String)
                    throw new ArgumentException();
                requestId = (string)command["RequestId"];
                if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 256) throw new ArgumentException();
                // Installed SDK 5.15 Param.Add(JsonData) throws while traversing null values
                // and coerces integer/array tokens to strings. Use CLR dictionary/scalar/array
                // values instead of its JsonData overload, without relaxing the host types.
                // The host permits these optional members to be absent (their DTO default is
                // null). Omit ONLY known optional nulls in this detached wire object; preserve
                // the durable command/hash, every non-null value and unknown fields for rejection.
                foreach (string name in new[] { "LookupRequestId", "BusinessSessionId", "ExpectedPolicyVersion",
                    "OrderId", "ReceiptId", "Materials" })
                    if (command[name]?.Type == JTokenType.Null) command.Remove(name);
                body = new Param();
                body.Add("request", (Dictionary<string, object>)NativeValue(command));
            }
            catch
            { completed(Result(PopupStoreFunctionTransportOutcome.RejectedBeforeSend, "invalid_request")); return; }

            var pending = new Pending(this, requestId, completed);
            try { pending.Attach(_schedule(() => pending.Finish(null, "timeout"), _configuration.Timeout)); }
            catch
            { completed(Result(PopupStoreFunctionTransportOutcome.RejectedBeforeSend, "timeout_unavailable")); return; }
            // Final checks precede the invocation boundary. No asynchronous work can authorize it.
            if (!SafeCheck(_sessionCurrent) || !SafeCheck(_environmentCurrent))
            { pending.RejectBeforeSend(); return; }
            try { _invoke(_configuration.FunctionName, body, reply => pending.Finish(reply, null)); }
            catch { pending.Finish(null, "invoke_outcome_unknown"); }
        }

        private static bool DevelopmentProcess
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private static bool SafeCheck(Func<bool> check)
        { try { return check != null && check(); } catch { return false; } }

        private static object NativeValue(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Object:
                    var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var property in ((JObject)value).Properties())
                        fields.Add(property.Name, NativeValue(property.Value));
                    return fields;
                case JTokenType.Array:
                    var items = new List<object>();
                    foreach (var item in value.Children()) items.Add(NativeValue(item));
                    return items.ToArray();
                case JTokenType.Integer: return (long)value;
                case JTokenType.Float: return (double)value;
                case JTokenType.String: return (string)value;
                case JTokenType.Boolean: return (bool)value;
                case JTokenType.Null: return null;
                default: throw new ArgumentException("Unsupported JSON value.");
            }
        }

        private void Post(Action action)
        {
            if (_dispatch == null) return;
            try { _dispatch(() => { if (Thread.CurrentThread.ManagedThreadId == _ownerThread) action(); }); }
            catch { /* Failed dispatch cannot be converted into a worker-thread UI callback. */ }
        }

        private static PopupStoreFunctionTransportResult Result(PopupStoreFunctionTransportOutcome outcome, string code)
            => new PopupStoreFunctionTransportResult(outcome, errorCode: code);

        private static void InvokeSdk(string functionName, Param body, Action<NativeResponse> completed)
        {
            Backend.BFunc.InvokeFunction(functionName, body, reply =>
            {
                try { completed(reply == null ? null : new NativeResponse(reply.IsSuccess(), reply.GetReturnValue())); }
                catch { completed(null); }
            });
        }

        private static IDisposable ScheduleTimeout(Action action, TimeSpan delay)
            => new Timer(_ => action(), null, delay, System.Threading.Timeout.InfiniteTimeSpan);

        private sealed class Pending
        {
            private readonly PopupStoreBackendFunctionTransport _owner;
            private readonly string _requestId;
            private readonly Action<PopupStoreFunctionTransportResult> _completed;
            private int _settled;
            private IDisposable _timer;
            internal Pending(PopupStoreBackendFunctionTransport owner, string requestId,
                Action<PopupStoreFunctionTransportResult> completed)
            { _owner = owner; _requestId = requestId; _completed = completed; }
            internal void Attach(IDisposable timer)
            {
                _timer = timer;
                if (Volatile.Read(ref _settled) != 0) timer?.Dispose();
            }
            internal void RejectBeforeSend() => Deliver(Result(PopupStoreFunctionTransportOutcome.RejectedBeforeSend,
                "session_or_environment_changed"));
            internal void Finish(NativeResponse response, string error)
            {
                PopupStoreFunctionTransportResult result = Result(PopupStoreFunctionTransportOutcome.Indeterminate,
                    error ?? "function_outcome_unknown");
                if (response != null && response.Success && response.Json != null && response.Json.Length <= 1048576)
                {
                    try
                    {
                        var outer = JObject.Parse(response.Json,
                            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        var value = outer["result"] as JObject;
                        if (value != null && value["Protocol"]?.Type == JTokenType.String
                            && (string)value["Protocol"] == _owner._configuration.Protocol
                            && value["RequestId"]?.Type == JTokenType.String
                            && (string)value["RequestId"] == _requestId
                            && value["Status"]?.Type == JTokenType.Integer
                            && (long)value["Status"] >= 0 && (long)value["Status"] <= 7)
                            result = new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.ResponseReceived,
                                value.ToString(Formatting.None));
                    }
                    catch { /* Malformed responses may follow a committed operation. */ }
                }
                Deliver(result);
            }
            private void Deliver(PopupStoreFunctionTransportResult result)
            {
                _owner.Post(() =>
                {
                    if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0) return;
                    _timer?.Dispose();
                    // Evaluate only on the captured thread. Stale A -> B -> A results are silent.
                    if (!SafeCheck(_owner._sessionCurrent)) return;
                    _completed(result);
                });
            }
        }
    }
}
