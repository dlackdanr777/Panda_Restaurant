#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LitJson;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class QuestStaffTutorialStateTests
{
    private const string Account = "quest-staff-schema-account";
    private const string Field = QuestStaffTutorialPolicy.GrantFieldName;

    [Test]
    public void GrantEvidence_InitialMissingValidAndDamagedUseActualSerializerLoaderAndRawRestore()
    {
        string state = GameState(); var random = Random.state;
        var initial = Initial();
        Assert.That(initial[Field].Type, Is.EqualTo(JTokenType.Integer));
        Assert.That((int)initial[Field], Is.Zero);
        Assert.That(initial.Property("StaffAccount"), Is.Null);
        Assert.That((int)initial["Dia"], Is.Zero);
        Assert.That((long)initial["Money"], Is.Zero);
        var catalog = Resources.LoadAll<StaffData>("StaffData");
        foreach (int? mask in new int?[] { null, 0, 1, 3, 7, 15 })
        {
            var flat = Initial();
            if (mask.HasValue) flat[Field] = mask.Value; else flat.Remove(Field);
            var parsed = Parse(flat);
            Assert.That(parsed.IsValid, Is.True);
            Assert.That(parsed.QuestStaffGrantMask, Is.EqualTo(mask));
            var context = new GameDataRestoreContext(() => Account);
            JObject row = Row();
            if (mask.HasValue) row[Field] = new JObject { ["N"] = mask.Value.ToString() };
            string raw = Response(row);
            int apply = 0;
            var result = context.TryRestore(context.BeginQuery(), true, raw, () => catalog,
                () => { apply++; return parsed.IsValid; });
            Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.Ready), result.Reason);
            Assert.That(result.Evidence.QuestStaffGrantMask, Is.EqualTo(mask));
            Assert.That(apply, Is.EqualTo(1));
            Assert.That(result.Evidence.RawJson, Is.EqualTo(raw));
            Assert.That(result.Evidence.StaffAccount.Version, Is.EqualTo(1));
            Assert.That(result.Evidence.StaffAccount.PandaTokens, Is.EqualTo(55));
        }

        foreach (JToken bad in new JToken[] { JValue.CreateNull(), new JValue("0"), new JValue(false),
            new JValue(-1), new JValue(16), new JValue(1.5), new JArray(), new JObject() })
        {
            var flat = Initial(); flat[Field] = bad.DeepClone();
            string before = flat.ToString(Formatting.None);
            Assert.That(Parse(flat).IsValid, Is.False, before);
            Assert.That(flat.ToString(Formatting.None), Is.EqualTo(before));
        }
        foreach (JToken bad in new JToken[] { JValue.CreateNull(), new JObject { ["S"] = "0" },
            new JObject { ["N"] = JValue.CreateNull() }, new JObject { ["N"] = "-1" },
            new JObject { ["N"] = "16" }, new JObject { ["N"] = "0.0" },
            new JObject { ["N"] = "+0" }, new JObject { ["N"] = " 0" },
            new JObject { ["N"] = "0", ["S"] = "0" } })
        {
            var row = Row(); row[Field] = bad.DeepClone();
            string raw = Response(row); int apply = 0;
            var context = new GameDataRestoreContext(() => Account);
            var result = context.TryRestore(context.BeginQuery(), true, raw,
                () => throw new InvalidOperationException("Bad evidence must be rejected before catalog"),
                () => { apply++; return true; });
            Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.InvalidResponse), raw);
            Assert.That(result.Evidence, Is.Null); Assert.That(apply, Is.Zero);
        }
        Assert.That(Random.state, Is.EqualTo(random)); Assert.That(GameState(), Is.EqualTo(state));
    }

    [Test]
    public void LiveCurrentQuest_UsesActualCsvMappingsPredecessorClaimsAndDoneStateRatherThanRequestedId()
    {
        using (var fixture = new Fixture())
        {
            foreach (var mapping in new[] { (1, "STAFF06", 1), (4, "STAFF16", 2), (5, "STAFF01", 4), (19, "STAFF21", 8) })
            {
                fixture.At(mapping.Item1);
                QuestStaffTutorialMemory snapshot = fixture.Read();
                Assert.That(snapshot.CurrentQuestId, Is.EqualTo(Id(mapping.Item1)));
                Assert.That(snapshot.RequiredStaffId, Is.EqualTo(mapping.Item2));
                Assert.That(snapshot.IsEligible, Is.True);
                Assert.That(snapshot.GrantMask, Is.Null, "Old-field absence is distinct from new explicit zero");
                Assert.That(snapshot.TryCreateGrantedMask(out int granted, out string error), Is.True, error);
                Assert.That(granted, Is.EqualTo(mapping.Item3));
                fixture.Done.Add(snapshot.CurrentQuestId);
                Assert.That(fixture.Read().IsEligible, Is.False, "Completed employment is not another grant opportunity");
                fixture.Done.Clear(); fixture.Mask(mapping.Item3);
                Assert.That(fixture.Read().IsEligible, Is.False, "Persisted grant evidence prohibits a second grant");
                fixture.Mask(null);
                if (mapping.Item1 > 1)
                {
                    fixture.Cleared.Remove(Id(mapping.Item1 - 1));
                    Assert.That(fixture.Read().CurrentQuestId, Is.EqualTo(Id(mapping.Item1 - 1)));
                    Assert.That(fixture.Read().Matches(snapshot), Is.False, "An external future ID cannot replace current progression");
                    fixture.Cleared.Add(Id(mapping.Item1 - 1));
                }
                fixture.Cleared.Add(snapshot.CurrentQuestId);
                Assert.That(fixture.Read().Matches(snapshot), Is.False, "Claimed old quests no longer name the current offer");
            }
            fixture.At(12);
            Assert.That(fixture.Read().CurrentQuestId, Is.EqualTo("MainReward12"));
            Assert.That(fixture.Read().IsEligible, Is.False, "TYPE25 item tutorial never qualifies as an employment grant");
            Assert.That(QuestStaffTutorialPolicy.TryGetMapping("MainReward12", out _, out _), Is.False);
            fixture.At(1); UserInfo.IsFirstTutorialClear = false;
            Assert.That(fixture.Read().IsEligible, Is.False, "Basic STAFF11 tutorial is separate");
            UserInfo.IsFirstTutorialClear = true; fixture.Stage(EStage.Stage2);
            Assert.That(fixture.Read().IsEligible, Is.False);
        }
    }

    [Test]
    public void NativeCommit_RechecksLiveSnapshotOnlyChangesGrantEvidenceAndDoesNotNotifyOrRecordPaidGacha()
    {
        using (var fixture = new Fixture())
        {
            fixture.At(4); var before = fixture.Read();
            fixture.Done.Add("MainReward04");
            Assert.That(fixture.Commit(before, 2), Is.False);
            Assert.That(UserInfo.QuestStaffGrantMask, Is.Null);
            fixture.Done.Clear();
            Assert.That(fixture.Commit(before, 15), Is.False, "Caller cannot mark unrelated quests granted");
            int staffEvents = 0; Action callback = () => staffEvents++;
            UserInfo.OnGiveStaffHandler += callback;
            try
            {
                Assert.That(fixture.Commit(before, 2), Is.True);
                Assert.That(UserInfo.QuestStaffGrantMask, Is.EqualTo(2));
                Assert.That(staffEvents, Is.Zero, "Execution publishes only after both core memory states are installed");
                Assert.That(before.GrantMask, Is.Null, "Captured evidence is immutable");
                Assert.That(fixture.Commit(before, 2), Is.False, "Repeated commit is not another completion");
                Assert.That(fixture.Done, Is.Empty, "Grant evidence does not directly complete or collect the quest reward");
            }
            finally { UserInfo.OnGiveStaffHandler -= callback; }
        }
    }

    private static string Id(int ordinal) => "MainReward" + ordinal.ToString("D2");
    private static JObject Initial() => JObject.Parse(LoadUserData.CreateInitialGameData(new DateTime(2026, 9, 15)).GetJson());
    private static LoadUserData Parse(JObject flat) => new LoadUserData(JsonMapper.ToObject(new JArray(flat.DeepClone()).ToString(Formatting.None)));
    private static JObject Row() => new JObject
    {
        ["owner_inDate"] = new JObject { ["S"] = Account }, ["inDate"] = new JObject { ["S"] = "quest-schema-row" },
        ["Dia"] = new JObject { ["N"] = "110" },
        ["StaffAccount"] = new JObject { ["S"] = "{\"Version\":1,\"Staff\":[],\"PandaTokens\":55}" }
    };
    private static string Response(JObject row) => new JObject { ["rows"] = new JArray(row), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
    private static string GameState() => JsonConvert.SerializeObject(new
    { UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount,
        Payments = PaymentInfo.PaymentDatas, GachaPayments = PaymentInfo.GachaPaymentDatas });

    private sealed class Fixture : IDisposable
    {
        private readonly List<(FieldInfo field, object value)> originals = new List<(FieldInfo, object)>();
        private readonly GameObject root;
        private readonly bool clearBefore = UserInfo.IsFirstTutorialClear;
        private readonly string gameBefore = GameState();
        private readonly Random.State randomBefore = Random.state;
        public HashSet<string> Cleared { get; } = new HashSet<string>();
        public HashSet<string> Done { get; } = new HashSet<string>();
        private readonly MethodInfo read = typeof(UserInfo).GetMethod("ReadQuestStaffTutorialMemory", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly MethodInfo commit = typeof(UserInfo).GetMethod("CommitQuestStaffTutorialMemory", BindingFlags.Static | BindingFlags.NonPublic);
        public Fixture()
        {
            root = new GameObject("Detached quest staff state"); root.SetActive(false);
            var manager = root.AddComponent<ChallengeManager>();
            var definitions = new Dictionary<string, ChallengeData>();
            var text = Resources.Load<TextAsset>("Challenge/REWARD_MAIN");
            Assert.That(text, Is.Not.Null);
            foreach (string line in text.text.Split('\n').Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = Utility.SplitCsvLine(line); string id = cells[0].Trim();
                if (!int.TryParse(id.Substring("MainReward".Length), out int ordinal) || ordinal > 20) continue;
                var type = (ChallengeType)Enum.Parse(typeof(ChallengeType), cells[1].Trim());
                definitions.Add(id, type == ChallengeType.TYPE05
                    ? (ChallengeData)new Type05ChallengeData(Challenges.Main, type, id, cells[3], cells[4].Trim(), MoneyType.Gold, 0, 0, null)
                    : new Definition(id, type));
            }
            Swap(typeof(ChallengeManager), "_instance", manager);
            Swap(typeof(ChallengeManager), "_mainChallengeDataDic", definitions);
            Swap(typeof(ChallengeManager), "_challengeDataDic", definitions);
            Swap(typeof(UserInfo), "_doneMainChallengeSet", Done);
            Swap(typeof(UserInfo), "_clearMainChallengeSet", Cleared);
            Swap(typeof(UserInfo), "_currentStage", EStage.Stage1);
            Swap(typeof(UserInfo), "<QuestStaffGrantMask>k__BackingField", null);
            UserInfo.IsFirstTutorialClear = true;
        }
        public void At(int ordinal)
        { Cleared.Clear(); Done.Clear(); Mask(null); Stage(EStage.Stage1); for (int i = 1; i < ordinal; i++) Cleared.Add(Id(i)); }
        public void Mask(int? value) => typeof(UserInfo).GetField("<QuestStaffGrantMask>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        public void Stage(EStage stage) => typeof(UserInfo).GetField("_currentStage", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, stage);
        public QuestStaffTutorialMemory Read() => (QuestStaffTutorialMemory)read.Invoke(null, null);
        public bool Commit(QuestStaffTutorialMemory expected, int mask) => (bool)commit.Invoke(null, new object[] { expected, mask, null });
        private void Swap(Type type, string name, object value)
        { var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic); originals.Add((field, field.GetValue(null))); field.SetValue(null, value); }
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
            for (int i = originals.Count - 1; i >= 0; i--) originals[i].field.SetValue(null, originals[i].value);
            UserInfo.IsFirstTutorialClear = clearBefore;
            Assert.That(GameState(), Is.EqualTo(gameBefore));
            Assert.That(Random.state, Is.EqualTo(randomBefore));
        }
        private sealed class Definition : ChallengeData
        { public Definition(string id, ChallengeType type) { _id = id; _type = type; _challenges = Challenges.Main; } }
    }
}
#endif
