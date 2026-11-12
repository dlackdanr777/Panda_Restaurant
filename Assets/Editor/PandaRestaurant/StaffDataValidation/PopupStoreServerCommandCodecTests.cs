#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

public sealed class PopupStoreServerCommandCodecTests
{
    private static JObject Pack()
    {
        return JObject.FromObject(new Command { Operation = Operation.Pack, RequestId = "request-1",
            BusinessSessionId = "business-1", OrderId = "order-1", ReceiptId = "receipt-1",
            Materials = new[] { "RIBBON_PINK", "STAFF01", "BOX_BLUE", "STAFF01" } });
    }

    [TestCase("Score")]
    [TestCase("TotalScore")]
    [TestCase("Grade")]
    [TestCase("HighestClearedGrade")]
    [TestCase("Diamonds")]
    [TestCase("GrantKey")]
    [TestCase("AccountId")]
    [TestCase("WalletId")]
    [TestCase("GameplaySeconds")]
    [TestCase("ArrivalConfirmed")]
    [TestCase("WalletMigrationComplete")]
    [TestCase("$type")]
    [TestCase("requestId")]
    public void ClientSuppliedAuthorityAndCasingAliasesAreRejected(string field)
    {
        var input = Pack(); input[field] = "attacker-supplied";
        AssertRejected(input.ToString(Formatting.None), "request-1");
    }

    [TestCase("\"Pack\"")]
    [TestCase("\"4\"")]
    [TestCase("4.0")]
    [TestCase("-1")]
    [TestCase("8")]
    [TestCase("9223372036854775808")]
    [TestCase("null")]
    [TestCase("true")]
    public void OperationMustBeBoundedIntegerRatherThanCoercedJson(string value)
    {
        var input = Pack(); input["Operation"] = JToken.Parse(value);
        AssertRejected(input.ToString(Formatting.None), "request-1");
    }

    [TestCase("[\"STAFF01\",null,\"BOX_BLUE\"]")]
    [TestCase("[\"STAFF01\",1,\"BOX_BLUE\"]")]
    [TestCase("{\"0\":\"STAFF01\"}")]
    [TestCase("\"[STAFF01,BOX_BLUE,RIBBON_PINK]\"")]
    public void MalformedMaterialsCannotReachTheAuthority(string value)
    {
        var input = Pack(); input["Materials"] = JToken.Parse(value);
        AssertRejected(input.ToString(Formatting.None), "request-1");
    }

    [Test]
    public void DuplicatePropertiesAreRejectedInsteadOfLastValueWinning()
    {
        string json = Pack().ToString(Formatting.None);
        AssertRejected(json.Insert(1, "\"Operation\":6,"), null);
        AssertRejected(json.Insert(1, "\"RequestId\":\"other\","), null);
    }

    [Test]
    public void OversizedOrNonObjectInputProducesNoAppliedEvidence()
    {
        AssertRejected(null, null);
        AssertRejected(new string(' ', PopupStoreServerCommandCodec.MaximumCharacters + 1), null);
        AssertRejected("[]", null);
        AssertRejected("{", null);
        AssertRejected("null", null);
    }

    [Test]
    public void RequiredEnvelopeAndProtocolCannotBeOmittedOrSubstituted()
    {
        foreach (string name in new[] { "Protocol", "RequestId", "Operation" })
        {
            var input = Pack(); input.Remove(name);
            AssertRejected(input.ToString(Formatting.None), name == "RequestId" ? null : "request-1");
        }
        var mismatch = Pack(); mismatch["Protocol"] = "future-unapproved-policy";
        AssertRejected(mismatch.ToString(Formatting.None), "request-1");
    }

    [Test]
    public void ValidPackPreservesOriginalOrderingAndDuplicateQuantities()
    {
        var input = Pack(); string original = input.ToString(Formatting.None);
        Assert.That(PopupStoreServerCommandCodec.TryDecode(original, out var command, out var failure), Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(command.Materials, Is.EqualTo(new[] { "RIBBON_PINK", "STAFF01", "BOX_BLUE", "STAFF01" }));
        Assert.That(PopupStoreServerAuthority.CommandHash(command), Is.EqualTo(
            PopupStoreServerAuthority.CommandHash(input.ToObject<Command>())));
        command.Materials[0] = "mutated-after-decode";
        Assert.That(input.ToString(Formatting.None), Is.EqualTo(original));
    }

    [TestCase("RequestId")]
    [TestCase("OrderId")]
    [TestCase("ReceiptId")]
    [TestCase("BusinessSessionId")]
    public void DateShapedIdentifiersRetainTheJsonStringType(string field)
    {
        var input = Pack(); input[field] = "2026-11-09T05:00:00Z";
        Assert.That(PopupStoreServerCommandCodec.TryDecode(input.ToString(Formatting.None), out var command, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(JObject.FromObject(command)[field].Value<string>(), Is.EqualTo("2026-11-09T05:00:00Z"));
        Assert.That(PopupStoreServerAuthority.CommandHash(command), Is.Not.Empty);
    }

    [Test]
    public void TrailingSecondCommandCannotBeIgnored()
    { AssertRejected(Pack().ToString(Formatting.None) + "{}", null); }

    [TestCase(Operation.Access)]
    [TestCase(Operation.Start)]
    [TestCase(Operation.Query)]
    [TestCase(Operation.IssueOrder)]
    [TestCase(Operation.Pack)]
    [TestCase(Operation.Settle)]
    [TestCase(Operation.Claim)]
    [TestCase(Operation.Abandon)]
    public void InstalledSdkNativeBodyAndFunctionDecoderAgree(Operation operation)
    {
        var source = operation == Operation.Pack ? Pack().ToObject<Command>() : new Command
        {
            Operation = operation, RequestId = "roundtrip-1",
            ExpectedPolicyVersion = operation == Operation.Start ? PopupStoreServerAuthority.PolicyVersion : null,
            BusinessSessionId = operation == Operation.Access || operation == Operation.Start ? null : "business-1"
        };
        // This is the actual installed SDK envelope shape; optional nulls are omitted as by the transport.
        var native = JObject.FromObject(source).Properties().Where(p => p.Value.Type != JTokenType.Null)
            .ToDictionary(p => p.Name, p => p.Name == "Materials" ? (object)source.Materials
                : p.Name == "Operation" ? (object)(int)operation : (string)p.Value);
        var body = new BackEnd.Param(); body.Add("request", native);
        var wire = JObject.Parse(body.GetJson())["request"].ToString(Formatting.None);
        Assert.That(PopupStoreServerCommandCodec.TryDecode(wire, out var decoded, out var error), Is.True,
            error == null ? wire : error.Error);
        Assert.That(PopupStoreServerAuthority.CommandHash(decoded), Is.EqualTo(PopupStoreServerAuthority.CommandHash(source)));
    }

    private static void AssertRejected(string json, string requestId)
    {
        Assert.That(PopupStoreServerCommandCodec.TryDecode(json, out var command, out var error), Is.False);
        Assert.That(command, Is.Null);
        Assert.That(error.Status, Is.EqualTo(Status.Invalid));
        Assert.That(error.RequestId, Is.EqualTo(requestId));
        Assert.That(error.AppliedRequestId, Is.Null);
        Assert.That(error.Business, Is.Null);
        Assert.That(error.Wallet, Is.Null);
        Assert.That(error.Grant, Is.Null);
    }
}
#endif
