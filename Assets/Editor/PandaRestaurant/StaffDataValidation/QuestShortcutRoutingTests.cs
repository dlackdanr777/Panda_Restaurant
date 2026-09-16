#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.DataBind;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Definition and native-selection tests only: no purchase/equip input, account SDK,
// gameplay scene execution, save, or writes to the user's progression.
public sealed class QuestShortcutRoutingTests
{
    [Test]
    public void All322MainDefinitions_UseTheirExpectedLiveShortcutBinding()
    {
        var host = new GameObject("Inactive shortcut binding audit"); host.SetActive(false);
        var bindingField = Field(typeof(DataBind), "_unityActionBindDic");
        object originalBindings = bindingField.GetValue(null);
        try
        {
            bindingField.SetValue(null, new DataBindContainer<UnityAction>());
            var manager = host.AddComponent<ChallengeManager>();
            var resolve = typeof(ChallengeManager).GetMethod("GetShortCutAction", BindingFlags.Instance | BindingFlags.NonPublic);
            var rows = Rows("Challenge/REWARD_MAIN");
            Assert.That(rows.Count, Is.EqualTo(322));
            Assert.That(rows.Select(row => row[0]).Distinct().Count(), Is.EqualTo(rows.Count));
            foreach (var row in rows)
            {
                string expected = ExpectedBinding(row);
                Assert.That(expected, Is.Not.Null, row[0] + " has an unsupported shortcut");
                var actual = resolve.Invoke(manager, new object[] { row[2] });
                Assert.That(actual, Is.SameAs(DataBind.GetUnityActionBindData(expected)), row[0]);
            }
        }
        finally { bindingField.SetValue(null, originalBindings); Object.DestroyImmediate(host); }
    }

    [TestCase("TYPE01", 128, "FurnitureData/CSVData")]
    [TestCase("TYPE02", 118, "KitchenUtensilData/CSVData")]
    [TestCase("TYPE03", 27, "FoodData/FoodDataList")]
    public void All273ProductDefinitions_KeepExactRegisteredTarget(string kind, int count, string resource)
    {
        var catalog = new HashSet<string>(Resources.LoadAll<TextAsset>(resource)
            .SelectMany(asset => ReadRows(asset.text)).Select(row => row[0]), StringComparer.Ordinal);
        var rows = Rows("Challenge/REWARD_MAIN").Where(row => row[1] == kind).ToList();
        Assert.That(rows.Count, Is.EqualTo(count));
        foreach (var row in rows)
        {
            Assert.That(catalog.Contains(row[4]), Is.True, row[0] + " target is missing from the runtime catalog");
            var quest = ProductQuest(row);
            Assert.That(ProductId(quest), Is.EqualTo(row[4]), row[0] + " must not fall back to a category's first product");
        }
    }

    [Test]
    public void Wallpaper01_HasFivePlacementScoreWithoutPurchaseOrUnlockCost()
    {
        var row = Rows("FurnitureData/CSVData/WallpaperData").Single(item => item[0] == "WALLPAPER01");
        Assert.That(int.Parse(row[4]), Is.EqualTo(5));
        Assert.That(int.Parse(row[6]), Is.Zero);
        Assert.That(int.Parse(row[8]), Is.Zero);
    }

    [Test]
    public void AmbiguousProductTargets_DoNotGuessTheFirstItem()
    {
        var quest = new Type01ChallengeData(Challenges.Main, ChallengeType.TYPE01, "MainReward02", "",
            new[] { "TABLE01_01", "TABLE02_01" }, MoneyType.Gold, 0, 0, null);
        Assert.That(ProductId(quest), Is.Null);
        Assert.That(ProductId(null), Is.Null);
    }

    [TestCase("furniture")]
    [TestCase("kitchen")]
    [TestCase("recipe")]
    public void NativeDetail_SelectsLaterProductNotFirstAndRejectsUnknownTarget(string kind)
    {
        using (var scope = new NativeSelectionScope())
        {
            if (kind == "furniture")
            {
                var view = scope.Find<UIFurniture>();
                var first = scope.Data<FurnitureData>("TABLE01_01");
                var target = scope.Data<FurnitureData>("TABLE02_01");
                Set(first, "_type", FurnitureType.Table1); Set(target, "_type", FurnitureType.Table1);
                Set(view, "_isInitialized", true); Set(view, "_currentType", FurnitureType.Table1);
                Set(view, "_currentFloorType", ERestaurantFloorType.Floor1);
                Set(view, "_currentTypeDataList", new List<FurnitureData> { first, target });
                Assert.That(view.TrySelectFurniture(first.Id), Is.True);
                Assert.That(view.TrySelectFurniture(target.Id), Is.True);
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(view.TrySelectFurniture("NOT_REGISTERED"), Is.False);
                Set(first, "_floorType", ERestaurantFloorType.Floor2);
                Assert.That(view.TrySelectFurniture(first.Id), Is.False, "A selection cannot borrow another floor's item");
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(view.BuyButtonRect, Is.Not.Null);
                Assert.That(view.EquipButtonRect, Is.Not.Null);
            }
            else if (kind == "kitchen")
            {
                var view = scope.Find<UIKitchen>();
                var first = scope.Data<KitchenUtensilData>("COOKER01_01");
                var target = scope.Data<KitchenUtensilData>("COOKER02_01");
                Set(first, "_type", KitchenUtensilType.Burner1); Set(target, "_type", KitchenUtensilType.Burner1);
                Set(view, "_isInitialized", true); Set(view, "_currentType", KitchenUtensilType.Burner1);
                Set(view, "_currentFloorType", ERestaurantFloorType.Floor1);
                Set(view, "_currentTypeDataList", new List<KitchenUtensilData> { first, target });
                Assert.That(view.TrySelectKitchen(first.Id), Is.True);
                Assert.That(view.TrySelectKitchen(target.Id), Is.True);
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(view.TrySelectKitchen("NOT_REGISTERED"), Is.False);
                Set(first, "_type", KitchenUtensilType.Burner2);
                Assert.That(view.TrySelectKitchen(first.Id), Is.False, "A selection cannot borrow another category's item");
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(view.BuyButtonRect, Is.Not.Null);
            }
            else
            {
                var view = scope.Find<UIRecipeTab>();
                var first = scope.Data<FoodData>("FOOD01");
                var target = scope.Data<FoodData>("FOOD02");
                Set(view, "_isInitialized", true);
                Set(view, "_foodDataList", new List<FoodData> { first, target });
                Assert.That(view.TrySelectRecipe(first.Id), Is.True);
                Assert.That(view.TrySelectRecipe(target.Id), Is.True);
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(Field(typeof(UIRecipeUpgrade), "_currentData").GetValue(scope.Find<UIRecipeUpgrade>()), Is.SameAs(target),
                    "Preview and upgrade entry must refer to the same recipe");
                Assert.That(view.TrySelectRecipe("NOT_REGISTERED"), Is.False);
                Assert.That(view.SelectedData, Is.SameAs(target));
                Assert.That(view.BuyButtonRect, Is.Not.Null);
            }
            scope.AssertNoMutation();
        }
    }

    private static string ExpectedBinding(string[] row)
    {
        if (row[1] == "TYPE05") return "ShowStaffTab";
        if (row[1] == "TYPE03") return "ShowRecipeTab";
        if (row[1] == "TYPE25") return "ShortCut10";
        if (row[1] != "TYPE01" && row[1] != "TYPE02") return "ShowRestaurant";
        string id = row[4];
        if (id.StartsWith("TABLE", StringComparison.Ordinal)) return "ShowFurnitureTable" + int.Parse(id.Substring(id.LastIndexOf('_') + 1));
        if (id.StartsWith("COOKER", StringComparison.Ordinal)) return "ShowKitchenBurner" + int.Parse(id.Substring(id.LastIndexOf('_') + 1));
        var prefixes = new Dictionary<string, string>
        {
            ["COUNTER"] = "ShowFurnitureCounter", ["RACK"] = "ShowFurnitureRack", ["FRAME"] = "ShowFurnitureFrame",
            ["FLOWER"] = "ShowFurnitureFlower", ["ACC"] = "ShowFurnitureAcc", ["WALLPAPER"] = "ShowFurnitureWallpaper",
            ["FRIDGE"] = "ShowKitchenFridge", ["CABINET"] = "ShowKitchenCabinet", ["WINDOW"] = "ShowKitchenWindow",
            ["SINK"] = "ShowKitchenSink", ["KITCHENRACK"] = "ShowKitchenKitchenrack", ["COOKTOOL"] = "ShowKitchenCookingTools"
        };
        return prefixes.FirstOrDefault(pair => id.StartsWith(pair.Key, StringComparison.Ordinal)).Value;
    }

    private static ChallengeData ProductQuest(string[] row)
    {
        if (row[1] == "TYPE01") return new Type01ChallengeData(Challenges.Main, ChallengeType.TYPE01,
            row[0], row[3], new[] { row[4] }, MoneyType.Gold, 0, 0, null);
        if (row[1] == "TYPE02") return new Type02ChallengeData(Challenges.Main, ChallengeType.TYPE02,
            row[0], row[3], new[] { row[4] }, MoneyType.Gold, 0, 0, null);
        return new Type03ChallengeData(Challenges.Main, ChallengeType.TYPE03,
            row[0], row[3], row[4], MoneyType.Gold, 0, 0, null);
    }

    private static string ProductId(ChallengeData quest) => (string)typeof(UIMainCanvas)
        .GetMethod("GetQuestProductId", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { quest });
    private static List<string[]> Rows(string resource)
    {
        var asset = Resources.Load<TextAsset>(resource);
        Assert.That(asset, Is.Not.Null, resource);
        return ReadRows(asset.text).ToList();
    }
    private static IEnumerable<string[]> ReadRows(string text) => text.Split('\n').Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line)).Select(Utility.SplitCsvLine)
        .Where(row => row.Length > 0 && !string.IsNullOrWhiteSpace(row[0]))
        .Select(row => row.Select(value => value.Trim()).ToArray());
    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            var result = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (result != null) return result;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

    private sealed class NativeSelectionScope : IDisposable
    {
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<Object> _created = new List<Object>();
        private readonly Scene _preview;
        private readonly long _money = UserInfo.Money;
        private readonly int _dia = UserInfo.Dia;
        private readonly bool _sdk = BackEnd.Backend.IsInitialized;
        private readonly UnityEngine.Random.State _random = UnityEngine.Random.state;
        public NativeSelectionScope()
        {
            Replace(typeof(UserInfo), "_stageInfos", new[] { new StageInfo(), new StageInfo(), new StageInfo() });
            Replace(typeof(UserInfo), "_giveRecipeLevelDic", new Dictionary<string, int>());
            Replace(typeof(GameManager), "_instance", Inactive<GameManager>());
            Replace(typeof(TimeManager), "_instance", Inactive<TimeManager>());
            _preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        }
        public T Find<T>() where T : Component => _preview.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private T Inactive<T>() where T : Component
        {
            var host = new GameObject("Inactive native selection dependency " + typeof(T).Name); host.SetActive(false);
            _created.Add(host); return host.AddComponent<T>();
        }
        public T Data<T>(string id) where T : BasicData
        {
            var data = ScriptableObject.CreateInstance<T>(); _created.Add(data);
            // Preview controls validate the recipe type even for furniture
            // detail fixtures; use a valid enum value so the test reaches the
            // native exact-selection path rather than failing in setup.
            Set(data, "_id", id); Set(data, "_name", id); Set(data, "_foodType", FoodType.Natural);
            Set(data, "_moneyType", MoneyType.Gold); Set(data, "_buyPrice", 0); Set(data, "_buyScore", 0);
            if (data is FurnitureData || data is KitchenUtensilData)
            {
                Set(data, "_floorType", ERestaurantFloorType.Floor1);
                Set(data, "_unlockData", new UnlockConditionData(UnlockConditionType.None, "", 0));
            }
            return data;
        }
        private void Replace(Type type, string name, object value)
        {
            var field = Field(type, name); object original = field.GetValue(null);
            _restore.Add(() => field.SetValue(null, original)); field.SetValue(null, value);
        }
        public void AssertNoMutation()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(_random)));
        }
        public void Dispose()
        {
            if (_preview.IsValid()) EditorSceneManager.ClosePreviewScene(_preview);
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            for (int i = _restore.Count - 1; i >= 0; i--) _restore[i]();
            UnityEngine.Random.state = _random;
        }
    }
}

public partial class StaffStageMigrationCollectionTests
{
    [TestCase("MainReward01")]
    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void QuestShortcut_OwnedEmployeeHasReadOnlyDetailButNeverNewGrant(string quest)
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture(quest, out var state, out var wallet);
            QuestStaffTutorialPolicy.TryGetMapping(quest, out var id, out int bit);
            // The static current-detail guard is a real UI-boundary check. Use
            // the native detached owner used by the integration tests instead
            // of the FormatterServices fake MonoBehaviour returned by the
            // lower-level fixture; Unity's fake-null operator would reject it.
            var oldManager = fixture.Manager;
            fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
                oldManager.StaffPurchaseWallet,
                _ => throw new AssertionException("Owned detail must not draw"),
                _ => throw new AssertionException("Owned detail must not record a paid purchase"));
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            Authenticate(fixture, fixture.Account);
            RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"" + id + "\",\"Level\":1}],\"PandaTokens\":55}");
            state.Done = true; state.Mask = bit;
            var source = fixture.Manager.StaffRuntime.Snapshot;
            try
            {
                Assert.That(fixture.Manager.TryGetCurrentQuestOwnedStaffDetail(out var detail), Is.True);
                Assert.That(detail.QuestId, Is.EqualTo(quest)); Assert.That(detail.StaffData.Id, Is.EqualTo(id));
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.False);
                Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
                Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out _, out _), Is.False);
                var host = new GameObject("Inactive owned quest detail input"); host.SetActive(false);
                try
                {
                    var canvas = host.AddComponent<UIMainCanvas>();
                    Field(typeof(UIMainCanvas), "_questOwnedStaffDetail").SetValue(canvas, detail);
                    typeof(UIMainCanvas).GetMethod("OnShowStaffGachaUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, null);
                    Assert.That(fixture.Game.Writes, Is.Zero, "An unexpected stale gacha input from owned-only detail cannot become paid");
                }
                finally { Object.DestroyImmediate(host); }
                var current = typeof(UIMainCanvas).GetMethod("IsOwnedQuestStaffDetailCurrent", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(current.Invoke(null, new object[] { fixture.Manager, detail }), Is.True);
                state.Claimed = true;
                Assert.That(fixture.Manager.TryGetCurrentQuestOwnedStaffDetail(out _), Is.False);
                state.Claimed = false; state.Quest = "MainReward02";
                Assert.That(current.Invoke(null, new object[] { fixture.Manager, detail }), Is.False);
                state.Quest = quest;
                fixture.Manager.InvalidateGameDataRestore();
                Assert.That(current.Invoke(null, new object[] { fixture.Manager, detail }), Is.False);
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(fixture.Manager.CurrentQuestStaffGrant, Is.Null);
                Assert.That(source.PandaTokens, Is.EqualTo(55));
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                Assert.That(wallet.GoldValue, Is.EqualTo(1000000));
                Assert.That(wallet.CostCommits, Is.Zero);
                Assert.That(state.Notifications, Is.Zero);
            }
            finally { fixture.Manager.DestroyEditorOfflineOwner(); }
        }
    }
}
#endif
