// SERVER SOURCE TEMPLATE ONLY. Not a Unity component; no server project, SDK bundle,
// build, deployment, credentials or activation is supplied. The separate Database store
// source candidate still requires the actual SDK and development verification.
// Target: official BACKND Function 1.1.1 template (net8.0, bundled Base 5.18.17).
using System;
using System.IO;
using Amazon.Lambda.Core;
using BackEnd;
using Newtonsoft.Json;
using PandaRestaurant.PopupStoreServer;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BackendFunction
{
    public sealed class PopupStoreHostDependencies
    {
        public bool DevelopmentEnvironmentVerified;
        public bool FunctionProtocolVerified;
        // gamerId may identify a character when multi-character is enabled. Verify the
        // project's identity mode before using it as the account-wide daily/wallet key.
        // This must not be supplied by the command or inferred from a dev_ function name.
        public bool AuthenticatedAccountIdentityVerified;
        public IAtomicAccountStore Store;
        public Func<AuthenticatedPrincipal, IAtomicAccountStore> CreateStoreForCaller;
        public IServerClock Clock;
        public IServerOrderLifecycle Lifecycle;
    }

    public class BFunc
    {
        // Only trusted server startup code may supply this. Its default is unavailable.
        // Never construct dependencies, identifiers, capability flags or a principal from input.
        public static Func<PopupStoreHostDependencies> ResolveTrustedDependencies;

        public Stream Function(Stream stream, ILambdaContext context)
        {
            string requestId = null;
            Command command;
            try { Backend.Initialize(ref stream); }
            catch { return Failure(null, Status.Unauthorized, "authentication_unavailable"); }

            try
            {
                if (!Backend.HasKey("request")) return Failure(null, Status.Invalid, "request_required");
                string json = Backend.Content["request"].ToJson();
                Response failure;
                if (!PopupStoreServerCommandCodec.TryDecode(json, out command, out failure))
                    return Backend.StringToStream(JsonConvert.SerializeObject(failure));
                requestId = command.RequestId;
            }
            catch { return Failure(requestId, Status.Invalid, "invalid_request"); }

            PopupStoreHostDependencies dependencies;
            try { dependencies = ResolveTrustedDependencies?.Invoke(); }
            catch { return Failure(requestId, Status.Disabled, "development_dependencies_unavailable"); }
            if (dependencies == null || !dependencies.DevelopmentEnvironmentVerified
                || !dependencies.FunctionProtocolVerified
                || !dependencies.AuthenticatedAccountIdentityVerified
                || (dependencies.Store == null && dependencies.CreateStoreForCaller == null) || dependencies.Clock == null
                || dependencies.Lifecycle == null)
                return Failure(requestId, Status.Disabled, "development_dependencies_unavailable");
            try
            {
                if (!dependencies.Clock.GameplayProtocolVerified || string.IsNullOrWhiteSpace(dependencies.Clock.ProtocolVersion)
                    || !dependencies.Lifecycle.ProtocolVerified)
                    return Failure(requestId, Status.Disabled, "atomic_wallet_and_clock_not_verified");
            }
            catch { return Failure(requestId, Status.Disabled, "development_dependencies_unavailable"); }

            string gamerId;
            try
            {
                // Function authentication uses the SDK's caller context. No account ID in the
                // command, local journal, Unity UserInDate or request header is trusted here.
                var authenticated = Backend.BMember.GetUserInfoV2();
                if (authenticated == null || !authenticated.IsSuccess())
                    return Failure(requestId, Status.Unauthorized, "caller_not_authenticated");
                gamerId = authenticated.GetReturnValuetoJSON()["row"]["gamerId"].ToString();
                if (string.IsNullOrWhiteSpace(gamerId))
                    return Failure(requestId, Status.Unauthorized, "caller_not_authenticated");
            }
            catch { return Failure(requestId, Status.Unauthorized, "caller_not_authenticated"); }

            var principal = new AuthenticatedPrincipal(gamerId);
            IAtomicAccountStore store;
            try
            {
                // A DatabaseAccountStore must be bound to this authenticated invocation, while
                // the underlying Database Client can be reused by trusted server startup.
                store = dependencies.CreateStoreForCaller != null
                    ? dependencies.CreateStoreForCaller(principal) : dependencies.Store;
                if (store == null || !store.AtomicAccountTransactionsVerified || !store.FullWalletMigrationVerified)
                    return Failure(requestId, Status.Disabled, "atomic_wallet_not_verified");
            }
            catch { return Failure(requestId, Status.Disabled, "development_dependencies_unavailable"); }

            try
            {
                using (var random = new SystemSecureRandom())
                {
                    var authority = new PopupStoreServerAuthority(store, dependencies.Clock, random, dependencies.Lifecycle);
                    var result = authority.Execute(principal, command);
                    return Backend.StringToStream(JsonConvert.SerializeObject(result));
                }
            }
            catch
            {
                // The store may have committed. Deliberately omit the successful protocol
                // envelope: the client transport must retain an indeterminate outcome and query.
                return Backend.StringToStream("{\"TransportOutcome\":\"Indeterminate\",\"Error\":\"authority_outcome_unknown\"}");
            }
        }

        private static Stream Failure(string requestId, Status status, string error)
        {
            // SDK transport may still be HTTP 200. Application failure stays in the exact shared
            // Response contract; unknown extension fields are deliberately rejected by clients.
            return Backend.StringToStream(JsonConvert.SerializeObject(
                PopupStoreServerAuthority.CreateHostFailureResponse(requestId, status, error)));
        }
    }
}
