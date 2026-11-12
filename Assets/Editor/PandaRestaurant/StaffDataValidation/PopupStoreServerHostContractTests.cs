#if UNITY_EDITOR
using System;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Production host failure producer -> real SDK Param/transport envelope -> real strict
/// client parser. Only native invocation and durable storage are isolated test doubles. This is
/// not a Function SDK build, authentication test, database test or remote invocation.</summary>
public sealed class PopupStoreServerHostContractTests
{
    private const string AccountId = "isolated-host-account";

    [TestCase(Status.Invalid)]
    [TestCase(Status.Unauthorized)]
    [TestCase(Status.Disabled)]
    public void CorrelatedHostReadFailureIsTypedButNeverUnlocksWrites(Status status)
    {
        var h = new Harness();
        h.HostFailure(status);
        Assert.That(h.Client.QueryAccess(value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed), h.Diagnostics);
        Assert.That(h.Last.Response.Status, Is.EqualTo(status));
        Assert.That(h.Last.Response.Error, Is.EqualTo("host_failure"));
        Assert.That(h.Last.Response.AppliedRequestId, Is.Null);
        Assert.That(h.Last.Response.Wallet, Is.Null);
        Assert.That(h.Last.Response.Grant, Is.Null);
        Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.RecoverFirst));
        Assert.That(h.Client.Submit(Start(), value => h.Last = value), Is.False);
        Assert.That(h.Calls, Is.EqualTo(1));
        Assert.That(h.Journal.Prepared, Is.Zero);
        Assert.That(h.Journal.Completed, Is.Zero);
    }

    [TestCase(Status.Invalid)]
    [TestCase(Status.Unauthorized)]
    [TestCase(Status.Disabled)]
    public void HostFailureCannotAcknowledgeOrEraseAnUnconfirmedWrite(Status status)
    {
        var h = new Harness();
        h.Ready();
        h.HostFailure(status);
        var command = Start();
        Assert.That(h.Client.Submit(command, value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
        Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.ReconciliationRequired));
        Assert.That(h.Journal.Pending, Is.Not.Null);
        Assert.That(h.Journal.Pending.RequestId, Is.EqualTo(command.RequestId));
        Assert.That(h.Journal.Completed, Is.Zero);
        var saved = h.Journal.Pending;

        // A correlated QUERY failure still supplies no committed original-request evidence.
        Assert.That(h.Client.Recover(value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
        Assert.That(h.Journal.Pending, Is.SameAs(saved));
        Assert.That(h.Journal.Completed, Is.Zero);
        Assert.That(h.Calls, Is.EqualTo(3));
    }

    [TestCase(null)]
    [TestCase("different-request")]
    public void HostFailureWithoutMatchingRequestIdentityRemainsUnconfirmed(string requestId)
    {
        var h = new Harness();
        h.Reply = command => SerializeHostFailure(requestId, Status.Invalid);
        Assert.That(h.Client.QueryAccess(value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
        Assert.That(h.Last.Response, Is.Null);
        Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.ReconciliationRequired));
        Assert.That(h.Journal.Completed, Is.Zero);
    }

    [Test]
    public void FormerHostExtensionFieldIsStillRejectedByStrictClientParser()
    {
        var h = new Harness();
        h.Reply = command =>
        {
            var response = SerializeHostFailure(command.RequestId, Status.Disabled);
            response["ServiceStatus"] = 503; // Reproduce the prior host contract defect.
            return response;
        };
        Assert.That(h.Client.QueryAccess(value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
        Assert.That(h.Last.Response, Is.Null);
        Assert.That(h.Journal.Completed, Is.Zero);
    }

    [TestCase(Operation.Access)]
    [TestCase(Operation.Start)]
    [TestCase(Operation.Query)]
    [TestCase(Operation.IssueOrder)]
    [TestCase(Operation.Pack)]
    [TestCase(Operation.Settle)]
    [TestCase(Operation.Claim)]
    [TestCase(Operation.Abandon)]
    public void AllOperationsPreserveCommandMeaningAcrossInstalledSdkNullHandling(Operation operation)
    {
        var h = new Harness();
        h.HostFailure(Status.Disabled);
        var command = new Command { Operation = operation, RequestId = "wire-contract-request",
            BusinessSessionId = operation == Operation.Access || operation == Operation.Start ? null : "server-business",
            LookupRequestId = operation == Operation.Query ? "original-request" : null,
            ExpectedPolicyVersion = operation == Operation.Start ? PopupStoreServerAuthority.PolicyVersion : null,
            OrderId = operation == Operation.Pack ? "server-order" : null,
            ReceiptId = operation == Operation.Pack ? "client-receipt" : null,
            Materials = operation == Operation.Pack ? new[] { "RIBBON_PINK", "STAFF01", "BOX_BLUE", "STAFF01" } : null };
        string originalJson = JsonConvert.SerializeObject(command);
        string originalHash = PopupStoreServerAuthority.CommandHash(command);
        PopupStoreFunctionTransportResult result = null;

        h.Transport.Send(originalJson, value => result = value);

        Assert.That(result.Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.ResponseReceived), h.Diagnostics);
        Assert.That(h.Calls, Is.EqualTo(1));
        Assert.That(h.NativeCommand, Is.Not.Null);
        Assert.That(PopupStoreServerAuthority.CommandHash(h.NativeCommand), Is.EqualTo(originalHash));
        Assert.That(JsonConvert.SerializeObject(command), Is.EqualTo(originalJson), "Original/pending DTO is never rewritten.");
        Assert.That(h.NativeCommand.Materials, Is.EqualTo(command.Materials), "No sorting or duplicate-quantity loss.");
        Assert.That(h.NativeCommand.Operation, Is.EqualTo(operation));
        Assert.That(h.NativeCommand.LookupRequestId, Is.EqualTo(command.LookupRequestId));
        Assert.That(h.NativeCommand.ExpectedPolicyVersion, Is.EqualTo(command.ExpectedPolicyVersion));
        Assert.That(h.NativeCommand.BusinessSessionId, Is.EqualTo(command.BusinessSessionId));
        var request = JObject.Parse(h.SentWire)["request"];
        foreach (string name in new[] { "LookupRequestId", "BusinessSessionId", "ExpectedPolicyVersion", "OrderId", "ReceiptId", "Materials" })
            if (JObject.Parse(originalJson)[name].Type == JTokenType.Null)
                Assert.That(request[name], Is.Null, "Only optional null members are omitted from the SDK wire object.");
    }

    private static Command Start() => new Command { Operation = Operation.Start,
        RequestId = "host-contract-start", ExpectedPolicyVersion = PopupStoreServerAuthority.PolicyVersion };

    private static JObject SerializeHostFailure(string requestId, Status status)
    {
        // Exactly the shared production producer and serializer used by Function.Failure.
        return JObject.Parse(JsonConvert.SerializeObject(
            PopupStoreServerAuthority.CreateHostFailureResponse(requestId, status, "host_failure")));
    }

    private sealed class Harness
    {
        internal readonly Journal Journal = new Journal();
        internal readonly PopupStoreServerClient Client;
        internal readonly PopupStoreBackendFunctionTransport Transport;
        internal Func<Command, JObject> Reply;
        internal PopupStoreServerClientResult Last;
        internal int Calls;
        internal Command NativeCommand;
        internal string SentWire, ReplyWire, InvocationError;
        internal string Diagnostics => "client=" + Last?.Error + "; native=" + InvocationError
            + "; sent=" + SentWire + "; reply=" + ReplyWire;

        internal Harness()
        {
            var session = new PopupStoreRewardAccountSession(AccountId);
            Transport = new PopupStoreBackendFunctionTransport(
                new PopupStoreFunctionDevelopmentConfiguration(true, "development", "dev_offline_host_contract",
                    PopupStoreServerAuthority.Protocol, true, true, true),
                () => true, () => true, action => action(),
                (function, body, callback) =>
                {
                    Calls++;
                    try
                    {
                        SentWire = body.GetJson();
                        var wire = JObject.Parse(SentWire);
                        Assert.That(wire["request"].Type, Is.EqualTo(JTokenType.Object));
                        Assert.That(wire["request"]["Protocol"].Type, Is.EqualTo(JTokenType.String));
                        Assert.That(wire["request"]["RequestId"].Type, Is.EqualTo(JTokenType.String));
                        Assert.That(wire["request"]["Operation"].Type, Is.EqualTo(JTokenType.Integer),
                            "Host requires an integer operation; ToObject coercion is not wire-contract evidence.");
                        if (wire["request"]["Materials"] != null && wire["request"]["Materials"].Type != JTokenType.Null)
                        {
                            Assert.That(wire["request"]["Materials"].Type, Is.EqualTo(JTokenType.Array));
                            foreach (var material in wire["request"]["Materials"])
                                Assert.That(material.Type, Is.EqualTo(JTokenType.String));
                        }
                        NativeCommand = wire["request"].ToObject<Command>();
                        var envelope = new JObject { ["result"] = Reply(NativeCommand) };
                        ReplyWire = envelope.ToString(Formatting.None);
                        callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, ReplyWire));
                    }
                    catch (Exception ex) { InvocationError = ex.ToString(); throw; }
                },
                (callback, delay) => new NoTimer());
            Client = new PopupStoreServerClient(Transport, Journal, () => session);
        }

        internal void HostFailure(Status status)
        { Reply = command => SerializeHostFailure(command.RequestId, status); }

        internal void Ready()
        {
            var now = new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
            Reply = command => JObject.FromObject(new Response
            {
                RequestId = command.RequestId, Status = Status.NotFound,
                Access = new AccessSnapshot { AccountId = AccountId, DailyLimit = 1, Used = 0, Remaining = 1,
                    GameDay = PopupStoreServerAuthority.GameDay(now), PolicyVersion = PopupStoreServerAuthority.PolicyVersion,
                    ServerUtcTicks = now.UtcDateTime.Ticks, NextResetUtcTicks = PopupStoreServerAuthority.NextResetUtcTicks(now),
                    WritesEnabled = true }
            });
            Assert.That(Client.Recover(value => Last = value), Is.True);
            Assert.That(Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed), Diagnostics);
            Assert.That(Client.State, Is.EqualTo(PopupStoreServerClientState.Ready));
        }
    }

    private sealed class Journal : IPopupStoreServerRequestJournal
    {
        internal PopupStorePendingServerRequest Pending;
        internal int Prepared, Completed;
        public PopupStorePendingServerRequest Read(string accountId) => Pending;
        public bool Prepare(string accountId, PopupStorePendingServerRequest request)
        {
            if (Pending != null) return false;
            Pending = request; Prepared++; return true;
        }
        public void Complete(string accountId, PopupStorePendingServerRequest expected)
        {
            if (Pending != null && !Pending.Matches(expected)) throw new InvalidOperationException();
            Pending = null; Completed++;
        }
    }

    private sealed class NoTimer : IDisposable { public void Dispose() { } }
}
#endif
