#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ItemGachaTutorialConnectionTests
{
    [Test]
    public void Main12Entry_UsesActualCurrentCsvProgressionWithoutEditorUnlockAndKeepsSceneReferences()
    {
        using (var f = new Fixture())
        {
            f.At(11); Assert.That(UIGacha.IsProgressionEntryUnlocked(), Is.False);
            f.At(12); Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.True);
            Assert.That(UIGacha.IsProgressionEntryUnlocked(), Is.True);
            Assert.That(f.Definitions["MainReward12"].Type, Is.EqualTo(ChallengeType.TYPE25));
            f.Cleared.Remove("MainReward10"); Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.False);
            f.At(12); f.Swap(typeof(UserInfo), "_currentStage", EStage.Stage2);
            Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.False);
            f.Swap(typeof(UserInfo), "_currentStage", EStage.Stage1);
            UserInfo.IsFirstTutorialClear = false; Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.False);
            UserInfo.IsFirstTutorialClear = true; UserInfo.IsMiniGameTutorialClear = true;
            Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.False);
            UserInfo.IsMiniGameTutorialClear = false; f.At(13);
            Assert.That(GachaTutorial.IsCurrentItemTutorialQuest(), Is.False);
            Assert.That(UIGacha.IsProgressionEntryUnlocked(), Is.True, "Claimed Main12 keeps the established general entry policy");
            Assert.That((bool)Field(typeof(UIStaffGacha), "IsGachaExecutionEnabled").GetValue(null), Is.True,
                "Paid execution is enabled independently; Main12 still uses only its verified item tutorial entry");
        }

        string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/Stage1.unity"));
        Assert.That(scene, Does.Contain("_miniGameTutorial: {fileID: 1826259372}"));
        Assert.That(scene, Does.Contain("_itemGacha: {fileID: 110240286}"));
        Assert.That(scene, Does.Contain("_uiGacha: {fileID: 771534404328290643}"));
        Assert.That(scene, Does.Contain("_shopButton: {fileID: 4521768}"));
        Assert.That(scene, Does.Contain("_recipeButton: {fileID: 1432119638}"));
        string animations = string.Join("\n", Directory.GetFiles(Path.Combine(Application.dataPath,
            "Animation/GachaMachine/ItemGacha"), "*.anim").Select(File.ReadAllText));
        Assert.That(animations, Does.Contain("functionName: SetStep"));
        Assert.That(animations, Does.Not.Contain("functionName: GetItem"));
        Assert.That(animations, Does.Not.Contain("functionName: StartAddItem"));
        Assert.That(scene, Does.Not.Contain("m_MethodName: StartAddItem"));
    }

    [Test]
    public void FixedItemInput_GrantsOnceCountsOnceAndWaitsForActualCoordinatorReceiptsBeforeNextGuidance()
    {
        using (var f = new Fixture())
        {
            f.At(12);
            var view = f.Tutorial();
            var machine = f.Machine(); Set(view, "_itemGacha", machine);
            Set(view, "_tutorialRunActive", true);
            int itemEvents = 0, countEvents = 0;
            UserInfo.OnGiveGachaItemHandler += () => { itemEvents++; Assert.That(Accept(view), Is.False); };
            UserInfo.OnUseGachaMachineHandler += () =>
            {
                countEvents++;
                Invoke(typeof(ChallengeManager), f.Challenges, "Type25ChallengeCheck");
                Assert.That(Accept(view), Is.False);
            };
            Assert.That(Accept(view), Is.True);
            Assert.That(Accept(view), Is.False, "The duplicated HoleClickHandler pointer-up is not another grant");
            Assert.That(UserInfo.GetGiveGachaItemCountDic()["GOTCHA91"], Is.EqualTo(1));
            Assert.That(UserInfo.GetGachaItemLevel("GOTCHA91"), Is.EqualTo(1));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(1));
            Assert.That(f.Done, Does.Contain("MainReward12"));
            Assert.That(f.Cleared, Does.Not.Contain("MainReward12"), "No automatic quest-reward collection");
            Assert.That(itemEvents, Is.EqualTo(1)); Assert.That(countEvents, Is.EqualTo(1));
            Assert.That(machine.Presentations, Is.EqualTo(1));
            Assert.That(f.Transport.Calls.Count, Is.EqualTo(1));
            var itemSave = (GameDataSaveRequest)Get(view, "_itemSave");
            Assert.That(Confirmed(view, itemSave), Is.False);
            Assert.That(Invoke(typeof(GachaTutorial), view, "BeginCompletionSave"), Is.Null);
            Assert.That(UserInfo.IsMiniGameTutorialClear, Is.False);
            Assert.That((int)f.Transport.Calls[0].Values["TotalUseGachaMachineCount"], Is.EqualTo(1));
            Assert.That((bool)f.Transport.Calls[0].Values["IsMiniGameTutorialClear"], Is.False);
            f.Transport.Respond(0, true); f.Transport.Respond(0, true);
            Assert.That(Confirmed(view, itemSave), Is.True);
            Assert.That(f.Transport.Calls.Count, Is.EqualTo(1));

            var replacementMachine = f.Machine();
            Assert.That(replacementMachine.StartAddItem(f.Item), Is.False, "Recreated views cannot replay the persisted item");
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(1));
            var completion = (GameDataSaveRequest)Invoke(typeof(GachaTutorial), view, "BeginCompletionSave");
            Assert.That(Invoke(typeof(GachaTutorial), view, "BeginCompletionSave"), Is.SameAs(completion));
            Assert.That(Confirmed(view, completion), Is.False, "Completion memory flag is not a stored receipt");
            Assert.That(f.Transport.Calls.Count, Is.EqualTo(2));
            Assert.That((bool)f.Transport.Calls[1].Values["IsMiniGameTutorialClear"], Is.True);
            f.Transport.Respond(1, true); f.Transport.Respond(1, true);
            Assert.That(Confirmed(view, completion), Is.True);
            Assert.That(f.Transport.Calls.Count, Is.EqualTo(2));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(1));
            Assert.That(UserInfo.GetGiveGachaItemCountDic()["GOTCHA91"], Is.EqualTo(1));
            f.AssertUnrelatedUnchanged();
        }
    }

    [Test]
    public void InvalidSessionAndUnknownSave_DoNotGrantOrAdvanceOrResetAndKeepFixedItemOnLateResponses()
    {
        foreach (string scenario in new[] { "wrong-quest", "unavailable", "old-query", "busy", "unknown", "query-changed-after-send", "completion-unknown" })
        using (var f = new Fixture())
        {
            f.At(scenario == "wrong-quest" ? 11 : 12);
            var view = f.Tutorial(); Set(view, "_itemGacha", f.Machine()); Set(view, "_tutorialRunActive", true);
            if (scenario == "unavailable") view.MutationAllowed = false;
            if (scenario == "old-query") view.Current = false;
            if (scenario == "busy") f.Coordinator.RequestAutosave();
            int beforeCalls = f.Transport.Calls.Count;
            bool shouldGrant = scenario == "unknown" || scenario == "query-changed-after-send" || scenario == "completion-unknown";
            Assert.That(Accept(view), Is.EqualTo(shouldGrant), scenario);
            if (!shouldGrant)
            {
                Assert.That(f.Transport.Calls.Count, Is.EqualTo(beforeCalls), scenario);
                Assert.That(UserInfo.IsGiveGachaItem("GOTCHA91"), Is.False);
                Assert.That(UserInfo.TotalUseGachaMachineCount, Is.Zero);
            }
            else
            {
                var itemSave = (GameDataSaveRequest)Get(view, "_itemSave");
                if (scenario == "query-changed-after-send")
                {
                    view.Current = false; f.SessionCurrent = false; f.Transport.Respond(0, true);
                }
                else if (scenario == "completion-unknown")
                {
                    f.Transport.Respond(0, true);
                    var completion = (GameDataSaveRequest)Invoke(typeof(GachaTutorial), view, "BeginCompletionSave");
                    f.Transport.Respond(1, false);
                    Assert.That(Confirmed(view, completion), Is.False);
                    Assert.That(f.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
                }
                else f.Transport.Respond(0, false);
                int calls = f.Transport.Calls.Count;
                Assert.That(Accept(view), Is.False);
                if (scenario != "completion-unknown")
                {
                    Assert.That(Confirmed(view, itemSave), Is.False);
                    Assert.That(Invoke(typeof(GachaTutorial), view, "BeginCompletionSave"), Is.Null);
                    Assert.That(UserInfo.IsMiniGameTutorialClear, Is.False);
                }
                Assert.That(f.Transport.Calls.Count, Is.EqualTo(calls));
                Assert.That(UserInfo.GetGiveGachaItemCountDic()["GOTCHA91"], Is.EqualTo(1));
                Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(1));
            }
            f.AssertUnrelatedUnchanged();
        }
    }

    private static bool Accept(GachaTutorial value) => (bool)Invoke(typeof(GachaTutorial), value, "TryAcceptTutorialItem");
    private static bool Confirmed(GachaTutorial value, GameDataSaveRequest request) =>
        (bool)Invoke(typeof(GachaTutorial), value, "IsSaveConfirmed", request);
    private static object Invoke(Type type, object value, string name, params object[] arguments)
    {
        try { return type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }
    private static FieldInfo Field(Type type, string name)
    {
        for (var next = type; next != null; next = next.BaseType)
        {
            var field = next.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    private static void Set(object value, string name, object contents) => Field(value.GetType(), name).SetValue(value, contents);
    private static object Get(object value, string name) => Field(value.GetType(), name).GetValue(value);

    private sealed class Definition : ChallengeData
    { public Definition(string id, ChallengeType type) { _id = id; _type = type; _challenges = Challenges.Main; } }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Action> restore = new List<Action>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly UnityEngine.Random.State random = UnityEngine.Random.state;
        private readonly string unrelated;
        public readonly HashSet<string> Cleared = new HashSet<string>();
        public readonly HashSet<string> Done = new HashSet<string>();
        public readonly Dictionary<string, ChallengeData> Definitions = new Dictionary<string, ChallengeData>();
        public ChallengeManager Challenges { get; }
        public GachaItemData Item { get; }
        public Transport Transport { get; } = new Transport();
        public GameDataSaveCoordinator Coordinator { get; }
        public bool SessionCurrent = true;
        private static string Unrelated() => JsonConvert.SerializeObject(new
        { UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.QuestStaffGrantMask,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas });

        public Fixture()
        {
            unrelated = Unrelated();
            foreach (var field in typeof(UserInfo).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) Swap(typeof(UserInfo), field.Name, null);
            Swap(typeof(UserInfo), "IsFirstTutorialClear", true);
            Swap(typeof(UserInfo), "IsTutorialStart", true);
            Swap(typeof(UserInfo), "IsMiniGameTutorialClear", false);
            Swap(typeof(UserInfo), "_currentStage", EStage.Stage1);
            Swap(typeof(UserInfo), "_doneMainChallengeSet", Done);
            Swap(typeof(UserInfo), "_clearMainChallengeSet", Cleared);
            Swap(typeof(UserInfo), "_totalUseGachaMachineCount", 0);
            Swap(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int>());
            Swap(typeof(UserInfo), "_giveGachaItemLevelDic", new Dictionary<string, int>());
            Swap(typeof(UserInfo), "_notificationMessageSet", new HashSet<string>());
            Challenges = Create<ChallengeManager>(); Swap(typeof(ChallengeManager), "_instance", Challenges);
            foreach (string line in Resources.Load<TextAsset>("Challenge/REWARD_MAIN").text.Split('\n').Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = Utility.SplitCsvLine(line); string id = cells[0].Trim();
                if (!int.TryParse(id.Substring("MainReward".Length), out int ordinal) || ordinal > 20) continue;
                var type = (ChallengeType)Enum.Parse(typeof(ChallengeType), cells[1].Trim());
                Definitions.Add(id, type == ChallengeType.TYPE25
                    ? (ChallengeData)new Type25ChallengeData(global::Challenges.Main, type, id, cells[3], int.Parse(cells[5]), MoneyType.Dia, 100, 0, null)
                    : new Definition(id, type));
            }
            Swap(typeof(ChallengeManager), "_mainChallengeDataDic", Definitions);
            Swap(typeof(ChallengeManager), "_challengeDataDic", Definitions);
            Swap(typeof(ChallengeManager), "_type25ChallengeDataDic", new Dictionary<string, Type25ChallengeData>
            { ["MainReward12"] = (Type25ChallengeData)Definitions["MainReward12"] });
            string row = Resources.Load<TextAsset>("ItemData/GachaItemData/GachaItemList").text.Split('\n')
                .Single(line => line.StartsWith("GOTCHA91,"));
            var itemCells = row.Split(',');
            Item = new GachaItemData(itemCells[0], itemCells[1], itemCells[2], int.Parse(itemCells[4]), int.Parse(itemCells[5]),
                int.Parse(itemCells[6]), default, 0, 0, 1, null);
            Swap(typeof(ItemManager), "_instance", Create<ItemManager>());
            Swap(typeof(ItemManager), "_gachaItemDataDic", new Dictionary<string, GachaItemData> { [Item.Id] = Item });
            var target = new GameDataSaveTarget("detached-item-tutorial", Guid.NewGuid().ToString("N"));
            bool initialized = false;
            object[] args = { (Func<GameDataSdkRetrySettings>)(() => new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true)),
                (Func<bool>)(() => initialized), (Func<GameDataRawResponse>)(() => { initialized = true; return new GameDataRawResponse(true, "200", "", ""); }), null };
            typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            var policy = (GameDataSdkInitializationPolicy)args[3];
            var sender = new GameDataSingleUpdate(() => new GameDataSaveReadiness(true, true, true, true,
                target.AccountInDate, target, initializationPolicy: policy), Transport, () => SessionCurrent);
            Coordinator = new GameDataSaveCoordinator(target, sender, Latest, isSessionCurrent: () => SessionCurrent);
        }
        private Param Latest()
        {
            var values = new Param();
            values.Add("GiveGachaItemCountDic", new Dictionary<string, int>(UserInfo.GetGiveGachaItemCountDic()));
            values.Add("TotalUseGachaMachineCount", UserInfo.TotalUseGachaMachineCount);
            values.Add("IsMiniGameTutorialClear", UserInfo.IsMiniGameTutorialClear);
            values.Add("DoneMainChallengeList", Done.ToArray());
            return values;
        }
        public void At(int ordinal) { Cleared.Clear(); Done.Clear(); for (int i = 1; i < ordinal; i++) Cleared.Add("MainReward" + i.ToString("D2")); }
        public T Create<T>() where T : Component
        { var go = new GameObject("Detached item tutorial " + typeof(T).Name); go.SetActive(false); objects.Add(go); return go.AddComponent<T>(); }
        public TutorialView Tutorial() { var view = Create<TutorialView>(); view.Owner = Coordinator; return view; }
        public ItemView Machine() => Create<ItemView>();
        public void Swap(Type type, string name, object value)
        { var field = Field(type, name); var old = field.GetValue(null); restore.Add(() => field.SetValue(null, old)); field.SetValue(null, value); }
        public void AssertUnrelatedUnchanged()
        { Assert.That(Unrelated(), Is.EqualTo(unrelated)); Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); }
        public void Dispose()
        {
            ((HashSet<GameDataSaveCoordinator>)typeof(GameDataSaveCoordinator).GetField("BlockedCoordinators",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Remove(Coordinator);
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
            UnityEngine.Random.state = random;
        }
    }

    public sealed class TutorialView : GachaTutorial
    {
        public GameDataSaveCoordinator Owner;
        public bool Current = true, MutationAllowed = true;
        public int Errors;
        protected override bool CaptureSaveSession() => Current && CanBeginItemMutation();
        protected override bool IsCapturedSessionCurrent() => Current;
        protected override bool CanBeginItemMutation() => Current && MutationAllowed && Owner.CanStartPurchase;
        protected override GameDataSaveRequest SaveTutorialProgress() => Owner.RequestAutosave();
        protected override void ReportProgressError(string message) { Errors++; }
    }
    public sealed class ItemView : UIItemGacha
    {
        public int Presentations;
        protected override void PreparePurchasePresentation() { }
        protected override void StartTutorialPresentation() { Presentations++; }
    }
    public sealed class Transport : IGameDataUpdateTransport
    {
        public sealed class Call { public GameDataSaveIdentity Identity; public JObject Values; public Action<GameDataSaveIdentity, GameDataRawResponse> Callback; }
        public readonly List<Call> Calls = new List<Call>();
        public void UpdateOnce(GameDataSaveIdentity identity, Param values, Action<GameDataSaveIdentity, GameDataRawResponse> response) =>
            Calls.Add(new Call { Identity = identity, Values = JObject.Parse(values.GetJson()), Callback = response });
        public void Respond(int index, bool success) => Calls[index].Callback(Calls[index].Identity,
            success ? new GameDataRawResponse(true, "204", "", "") : null);
    }
}
#endif
