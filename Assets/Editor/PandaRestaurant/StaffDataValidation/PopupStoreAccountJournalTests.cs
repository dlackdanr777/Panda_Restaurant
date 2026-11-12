#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class PopupStoreAccountJournalTests
{
    private string _directory;
    private string _savedAccountField;
    private static FieldInfo AccountField => typeof(UserInfo).GetField("_popupStoreAccountJson", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly GameDataSaveTarget Target = new GameDataSaveTarget("TEST_ONLY-account-A", "TEST_ONLY-row-A");

    [SetUp]
    public void Before()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PandaPopupJournalTests-" + Guid.NewGuid().ToString("N"));
        _savedAccountField = (string)AccountField.GetValue(null);
    }
    [TearDown]
    public void After()
    {
        AccountField.SetValue(null, _savedAccountField);
        // Only this test's unique directory and direct files are owned. Never recursively delete.
        string path = Path.GetFullPath(_directory);
        Assert.That(Path.GetDirectoryName(path).TrimEnd(Path.DirectorySeparatorChar), Is.EqualTo(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
        Assert.That(Path.GetFileName(path).StartsWith("PandaPopupJournalTests-", StringComparison.Ordinal), Is.True);
        if (Directory.Exists(path))
        {
            Assert.That(Directory.GetDirectories(path), Is.Empty);
            foreach (string file in Directory.GetFiles(path)) File.Delete(file);
            Directory.Delete(path, false);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void RestartPreservesExactRecordPayloadIdentityAndSendUncertainty(bool notSent)
    {
        var intent = Claim(notSent); var first = new PopupStoreAccountFileJournal(_directory);
        Assert.That(first.TryWrite(intent, out var error), Is.True, error);
        var restart = new PopupStoreAccountFileJournal(_directory);
        Assert.That(restart.TryRead(Target, out var restored, out error), Is.True, error);
        Assert.That(restored.ToJson(), Is.EqualTo(intent.ToJson()));
        Assert.That(restored.Candidate.State, Is.EqualTo(PopupStoreGameDataState.Claimed));
        Assert.That(restored.PayloadJson, Is.EqualTo(intent.PayloadJson));
        Assert.That(restored.DefinitelyNotSent, Is.EqualTo(notSent));
        Assert.That(Directory.GetFiles(_directory, "*.pending"), Is.Empty);
    }

    [Test]
    public void MissingJournalDoesNotCreateFilesOrAuthorizeAnyReward()
    {
        var store = new PopupStoreAccountFileJournal(_directory);
        Assert.That(store.TryRead(Target, out var pending, out var error), Is.True, error);
        Assert.That(pending, Is.Null); Assert.That(Directory.Exists(_directory), Is.False);
    }

    [TestCase("Version")] [TestCase("NotSent")] [TestCase("Extra")]
    [TestCase("Source")] [TestCase("Candidate")] [TestCase("Request")]
    public void CorruptEnvelopeCannotBecomeAnEmptyOrRetryableIntent(string field)
    {
        var value = JObject.Parse(Claim().ToJson());
        if (field == "Version") value[field] = 2;
        else if (field == "NotSent") value[field] = "true";
        else if (field == "Extra") value[field] = true;
        else if (field == "Request") value[field] = "other-logical-request";
        else value[field] = "{}";
        Assert.That(() => PopupStoreAccountIntent.Read(value.ToString(Formatting.None)), Throws.Exception);
    }

    [TestCase(false)] [TestCase(true)]
    public void DuplicateOrTrailingEnvelopeIsRejected(bool trailing)
    {
        string json = Claim().ToJson();
        json = trailing ? json + " {}" : json.Insert(1, "\"Version\":1,");
        Assert.That(() => PopupStoreAccountIntent.Read(json), Throws.Exception);
    }

    [TestCase("duplicate")] [TestCase("trailing")] [TestCase("wrongRecord")]
    [TestCase("extra")] [TestCase("missingDia")] [TestCase("negativeDia")]
    [TestCase("fractionDia")] [TestCase("overflowDia")]
    public void ExactClaimPayloadRejectsAmbiguityAndInvalidBalance(string mutation)
    {
        var intent = Claim(); var payload = JObject.Parse(intent.PayloadJson); string json;
        if (mutation == "duplicate") json = intent.PayloadJson.Insert(1, "\"Dia\":100,");
        else if (mutation == "trailing") json = intent.PayloadJson + " {}";
        else
        {
            if (mutation == "wrongRecord") payload[PopupStoreGameDataRecord.FieldName] = intent.Source.ToJson();
            if (mutation == "extra") payload["Gold"] = 10;
            if (mutation == "missingDia") payload.Remove("Dia");
            if (mutation == "negativeDia") payload["Dia"] = -1;
            if (mutation == "fractionDia") payload["Dia"] = 101.5;
            if (mutation == "overflowDia") payload["Dia"] = (long)int.MaxValue + 1;
            json = payload.ToString(Formatting.None);
        }
        Assert.That(() => new PopupStoreAccountIntent(Target, intent.RequestId, intent.Source, intent.Candidate, json), Throws.Exception);
    }

    [Test]
    public void StartIntentCannotContainWalletField()
    {
        var next = PopupStoreGameDataTestData.Started(); var json = Payload(next, 100);
        Assert.That(() => new PopupStoreAccountIntent(Target, next.LastRequestId, PopupStoreGameDataRecord.Empty, next, json), Throws.ArgumentException);
    }

    [Test]
    public void UnknownIntentCannotBeReplacedByAnotherAttemptOrBusiness()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var original = Claim();
        Assert.That(store.TryWrite(original, out _), Is.True);
        var retry = new PopupStoreAccountIntent(Target, Attempt(original.Candidate), original.Source, original.Candidate, original.PayloadJson);
        Assert.That(store.TryWrite(retry, out _), Is.False);
        var start = Start(Target);
        Assert.That(store.TryWrite(start, out _), Is.False);
        Assert.That(store.TryRead(Target, out var current, out _), Is.True);
        Assert.That(current.ToJson(), Is.EqualTo(original.ToJson()));
    }

    [Test]
    public void ProvenNotSentRetryKeepsDecisionAndUpdatesOnlyAttemptAndCurrentBalance()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var original = Claim();
        Assert.That(store.TryWrite(original, out _), Is.True);
        var rejected = new PopupStoreAccountIntent(Target, original.RequestId, original.Source, original.Candidate, original.PayloadJson, true);
        Assert.That(store.TryWrite(rejected, out _), Is.True);
        var retry = new PopupStoreAccountIntent(Target, Attempt(original.Candidate), original.Source, original.Candidate, Payload(original.Candidate, 108));
        Assert.That(store.TryWrite(retry, out var error), Is.True, error);
        Assert.That(store.TryRead(Target, out var current, out _), Is.True);
        Assert.That(current.Candidate.Matches(original.Candidate), Is.True);
        Assert.That(current.Candidate.RewardRoll, Is.EqualTo(original.Candidate.RewardRoll));
        Assert.That(current.RequestId, Is.Not.EqualTo(original.RequestId));
        Assert.That(current.DefinitelyNotSent, Is.False);
        Assert.That((int)JObject.Parse(current.PayloadJson)["Dia"], Is.EqualTo(108));
        Assert.That(store.TryWrite(rejected, out _), Is.False, "An older rejection cannot reset the sent retry.");
    }

    [Test]
    public void SameAttemptCannotRefreshBalanceAndNotSentCannotReplaceDecision()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var original = Claim(true);
        Assert.That(store.TryWrite(original, out _), Is.True);
        var changed = new PopupStoreAccountIntent(Target, original.RequestId, original.Source, original.Candidate, Payload(original.Candidate, 108), true);
        Assert.That(store.TryWrite(changed, out _), Is.False);
        Assert.That(store.TryWrite(Start(Target), out _), Is.False);
    }

    [Test]
    public void ClearingRequiresExactLatestTransportAttempt()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var intent = Claim();
        store.TryWrite(intent, out _);
        Assert.That(store.TryClear(Target, "other", out _), Is.False);
        Assert.That(store.TryRead(Target, out var stillPresent, out _), Is.True); Assert.That(stillPresent, Is.Not.Null);
        Assert.That(store.TryClear(Target, intent.RequestId, out var error), Is.True, error);
        Assert.That(store.TryRead(Target, out var cleared, out _), Is.True); Assert.That(cleared, Is.Null);
    }

    [Test]
    public void AccountsAndRowsAreIsolatedEvenWithDelimiterLikeNames()
    {
        var store = new PopupStoreAccountFileJournal(_directory);
        var targets = new[] { new GameDataSaveTarget("A:B", "C"), new GameDataSaveTarget("A", "B:C"), new GameDataSaveTarget("A", "C") };
        foreach (var target in targets) Assert.That(store.TryWrite(Start(target), out _), Is.True);
        Assert.That(Directory.GetFiles(_directory, "*.json").Length, Is.EqualTo(3));
        foreach (var target in targets)
        {
            Assert.That(store.TryRead(target, out var value, out _), Is.True);
            Assert.That(value.Target.Matches(target), Is.True);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CorruptOrWrongOwnerFileIsPreservedAndBlocksOverwriteAndClear(bool wrongOwner)
    {
        var store = new PopupStoreAccountFileJournal(_directory); var intent = Claim(); store.TryWrite(intent, out _);
        string path = Directory.GetFiles(_directory, "*.json").Single();
        string raw = wrongOwner ? Claim(target: new GameDataSaveTarget("other", "row")).ToJson() : "{corrupt";
        File.WriteAllText(path, raw);
        Assert.That(store.TryRead(Target, out var invalid, out var error), Is.False); Assert.That(invalid, Is.Null); Assert.That(error, Is.Not.Empty);
        Assert.That(store.TryWrite(intent, out _), Is.False); Assert.That(store.TryClear(Target, intent.RequestId, out _), Is.False);
        Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
    }

    [Test]
    public void ExistingCrossProcessWriteLockBlocksMutationWithoutLosingIntent()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var intent = Claim(); store.TryWrite(intent, out _);
        string lockPath = Directory.GetFiles(_directory, "*.lock").Single();
        using (var held = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.That(store.TryWrite(intent, out _), Is.False);
            Assert.That(store.TryClear(Target, intent.RequestId, out _), Is.False);
        }
        Assert.That(store.TryRead(Target, out var kept, out _), Is.True); Assert.That(kept.ToJson(), Is.EqualTo(intent.ToJson()));
    }

    [Test]
    public void InaccessibleRecoveryPathCannotBeInterpretedAsAnAbsentIntent()
    {
        var store = new PopupStoreAccountFileJournal(_directory); var intent = Claim(); store.TryWrite(intent, out _);
        string ownedPath = Directory.GetFiles(_directory, "*.json").Single();
        File.Delete(ownedPath); Directory.CreateDirectory(ownedPath);
        try
        {
            Assert.That(store.TryRead(Target, out var unknown, out var error), Is.False);
            Assert.That(unknown, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(store.TryWrite(intent, out _), Is.False);
        }
        finally { Directory.Delete(ownedPath, false); }
    }

    [Test]
    public void OriginalUserInfoPreservesValidAndMalformedStringWithoutResettingIt()
    {
        foreach (string raw in new[] { PopupStoreGameDataTestData.Started().ToJson(), "{invalid" })
        {
            LoadRaw(new JArray(new JObject { [PopupStoreGameDataRecord.FieldName] = raw }));
            var saved = SavedFields();
            Assert.That((string)saved[PopupStoreGameDataRecord.FieldName], Is.EqualTo(raw));
            Assert.That(PopupStoreGameDataRecord.TryRead(raw, true, out _, out _), Is.EqualTo(raw != "{invalid"));
        }
    }

    [TestCase("missing")] [TestCase("null")] [TestCase("number")]
    public void OldOrNonStringFieldNeverInventsAnEmptyRecordOrOverwritesServerColumn(string kind)
    {
        var row = new JObject();
        if (kind == "null") row[PopupStoreGameDataRecord.FieldName] = JValue.CreateNull();
        if (kind == "number") row[PopupStoreGameDataRecord.FieldName] = 123;
        AccountField.SetValue(null, "previous-account-data");
        LoadRaw(new JArray(row));
        Assert.That(SavedFields().Property(PopupStoreGameDataRecord.FieldName), Is.Null);
        Assert.That(AccountField.GetValue(null), Is.Null, "Previous account data must not leak to this account.");
    }

    private static void LoadRaw(JArray rows) => typeof(UserInfo).GetMethod("LoadPopupStoreAccountField", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { LitJson.JsonMapper.ToObject(rows.ToString(Formatting.None)) });
    private static JObject SavedFields()
    {
        var values = new Param();
        typeof(UserInfo).GetMethod("AddPopupStoreAccountSaveField", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { values });
        return JObject.Parse(values.GetJson());
    }
    private static string Attempt(PopupStoreGameDataRecord candidate) => candidate.LastRequestId + ":attempt:" + Guid.NewGuid().ToString("N");
    private static string Payload(PopupStoreGameDataRecord candidate, int? balance = null)
    {
        var value = new JObject { [PopupStoreGameDataRecord.FieldName] = candidate.ToJson() };
        if (balance.HasValue) value["Dia"] = balance.Value;
        return value.ToString(Formatting.None);
    }
    private static PopupStoreAccountIntent Start(GameDataSaveTarget target)
    {
        var next = PopupStoreGameDataTestData.Started();
        return new PopupStoreAccountIntent(target, next.LastRequestId, PopupStoreGameDataRecord.Empty, next, Payload(next));
    }
    private static PopupStoreAccountIntent Claim(bool notSent = false, GameDataSaveTarget target = null)
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
        Assert.That(source.TryClaim("TEST_ONLY-claim", out var next, out _), Is.True);
        return new PopupStoreAccountIntent(target ?? Target, next.LastRequestId, source, next, Payload(next, 101), notSent);
    }
}
#endif
