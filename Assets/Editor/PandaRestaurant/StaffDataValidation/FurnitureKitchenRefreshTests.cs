#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using static CustomerTableUseRegressionTests;

public sealed class FurnitureKitchenRefreshTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type Flags(Type view) => view.GetNestedType("DirtyFlags", BindingFlags.NonPublic);
    private static object Flag(Type view, int value) => Enum.ToObject(Flags(view), value);
    private static object State(Type view, BasicData data, int state)
        => Activator.CreateInstance(view.GetNestedType("SlotState", BindingFlags.NonPublic),
            new object[] { data, state, 0, 0 });
    private static int StateCode(object state) => (int)state.GetType().GetField("State").GetValue(state);
    private static bool Affected(Type view, BasicData data, int state, int flags)
        => (bool)view.GetMethod("ShouldRefreshSlot", Private).Invoke(null,
            new[] { data, State(view, data, state), Flag(view, flags) });

    [Test]
    public void Catalog_RecordsActualSlotInstantiationCountsWithoutChangingUserScenes()
    {
        foreach (string resource in new[] {
            "FurnitureData/CSVData/TableData", "FurnitureData/CSVData/CounterData", "FurnitureData/CSVData/RackData",
            "FurnitureData/CSVData/FrameData", "FurnitureData/CSVData/FlowerData", "FurnitureData/CSVData/AccData",
            "FurnitureData/CSVData/WallpaperData", "KitchenUtensilData/CSVData/CookerData",
            "KitchenUtensilData/CSVData/FridgeData", "KitchenUtensilData/CSVData/CabinetData",
            "KitchenUtensilData/CSVData/WindowData", "KitchenUtensilData/CSVData/CooktoolData",
            "KitchenUtensilData/CSVData/KitchenrackData", "KitchenUtensilData/CSVData/SinkData" })
        {
            var csv = Resources.Load<TextAsset>(resource);
            Assert.That(csv, Is.Not.Null, resource);
            foreach (string row in csv.text.Split('\n').Skip(1))
                if (string.IsNullOrWhiteSpace(row.Split(',')[0]))
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Id값이 이상합니다: " + row.Split(',')[0].Trim());
        }
        using (var fonts = new StaffGachaOfflineFonts())
            UiSpriteOptimizationAudit.CaptureSlotInventory(view =>
            {
                fonts.BindBeforeActivation(view.gameObject);
                var slots = Get<List<UIRestaurantAdminFoodTypeSlot>>(view, "_slots");
                var original = slots.ToArray();
                bool kitchen = view is UIKitchen;
                var catalog = kitchen ? KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList().Cast<BasicData>()
                    : FurnitureDataManager.Instance.GetFurnitureDataList().Cast<BasicData>();
                var categories = catalog.Where(data => (kitchen ? ((KitchenUtensilData)data).UnlockData
                        : ((FurnitureData)data).UnlockData).UnlockType == UnlockConditionType.None)
                    .GroupBy(data => kitchen ? (int)((KitchenUtensilData)data).Type : (int)((FurnitureData)data).Type)
                    .Where(group => group.Count() >= 2).Take(2).ToArray();
                Assert.That(categories.Length, Is.EqualTo(2));
                foreach (int category in new[] { 0, 1, 0 })
                {
                    var data = categories[category].Take(category == 0 ? 2 : 1).ToArray();
                    Set(view, "_currentType", Enum.ToObject(kitchen ? typeof(KitchenUtensilType) : typeof(FurnitureType),
                        categories[category].Key));
                    Set(view, "_currentTypeDataList", kitchen
                        ? (object)data.Cast<KitchenUtensilData>().ToList() : data.Cast<FurnitureData>().ToList());
                    Invoke(view, "RefreshSlots", Flag(view.GetType(), 0));
                    Assert.That(slots.Count(slot => slot.gameObject.activeSelf), Is.EqualTo(data.Length));
                    Get<UnityEngine.UI.Button>(slots[0], "_button").onClick.Invoke();
                    Assert.That(kitchen ? ((UIKitchen)view).SelectedData : (BasicData)((UIFurniture)view).SelectedData,
                        Is.SameAs(data[0]));
                    CollectionAssert.AreEqual(original, Get<List<UIRestaurantAdminFoodTypeSlot>>(view, "_slots"));
                }
            });
    }

    [Test]
    public void ConsolidatedWallpapers_KeepCanonicalGuidFileIdAndRuntimeResourcesPath()
    {
        foreach (int id in Enumerable.Range(19, 17))
        {
            string folder = "Assets/Resources/FurnitureData/Sprites/" + id;
            string path = Directory.GetFiles(folder, "WALLPAPER" + id + "_*.png").Single().Replace('\\', '/');
            Assert.That(Directory.GetFiles("Assets/Resources/FurnitureData/Sprites/벽지", "WALLPAPER" + id + "_*.png"), Is.Empty);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(fileId, Is.EqualTo(21300000));
            string resource = path.Substring("Assets/Resources/".Length);
            resource = resource.Substring(0, resource.Length - ".png".Length);
            Assert.That(Resources.Load<Sprite>(resource), Is.SameAs(sprite), resource);
            Assert.That(sprite.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DirtyUnion_TargetsCurrencyScoreOwnershipUnlockAndPlacement(bool kitchen)
    {
        using (var s = new Flow())
        {
            Type view = kitchen ? typeof(UIKitchen) : typeof(UIFurniture);
            BasicData data = s.Track(ScriptableObject.CreateInstance(
                kitchen ? typeof(KitchenUtensilData) : typeof(FurnitureData))) as BasicData;
            foreach (var currency in new[] { MoneyType.Gold, MoneyType.Dia })
            {
                Set(data, "_moneyType", currency);
                for (int state = 1; state <= 7; state++)
                for (int flags = 1; flags < 64; flags++)
                {
                    bool owned = state == 1 || state == 2;
                    bool priced = state >= 5;
                    bool expected = (flags & 8) != 0
                        || (owned ? (flags & 32) != 0
                            : (flags & 16) != 0 || ((flags & 4) != 0 && state != 3)
                                || (priced && (flags & (currency == MoneyType.Gold ? 1 : 2)) != 0));
                    Assert.That(Affected(view, data, state, flags), Is.EqualTo(expected),
                        currency + "/" + state + "/" + flags);
                }
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealState_CrossesPriceScoreAndOtherFloorPlacementInBothDirections(bool kitchen)
    {
        using (var s = new Flow())
        {
            BasicData data = s.Track(ScriptableObject.CreateInstance(
                kitchen ? typeof(KitchenUtensilData) : typeof(FurnitureData))) as BasicData;
            Set(data, "_id", "UI-OPTIMIZATION-ITEM");
            Set(data, "_moneyType", MoneyType.Gold);
            Set(data, "_buyPrice", 100);
            Set(data, "_buyScore", 10);
            Set(data, "_unlockData", new UnlockConditionData(UnlockConditionType.None, "", 0));
            object view = kitchen ? (object)s.Create<UIKitchen>() : s.Create<UIFurniture>();
            if (kitchen) Set(data, "_type", KitchenUtensilType.Burner1);
            else Set(data, "_type", FurnitureType.Table1);
            s.Swap(typeof(UserInfo), "_score", 10);
            s.Swap(typeof(UserInfo), "_money", 99L);
            s.Swap(typeof(UserInfo), "_dia", 99);
            Func<object> compute = () => Invoke(view, "ComputeSlotState", data);
            Assert.That(StateCode(compute()), Is.EqualTo(6));
            s.Swap(typeof(UserInfo), "_money", 100L);
            Assert.That(StateCode(compute()), Is.EqualTo(5));
            s.Swap(typeof(UserInfo), "_money", 99L);
            Assert.That(StateCode(compute()), Is.EqualTo(6));
            Set(data, "_moneyType", MoneyType.Dia);
            Assert.That(StateCode(compute()), Is.EqualTo(7));
            s.Swap(typeof(UserInfo), "_dia", 100);
            Assert.That(StateCode(compute()), Is.EqualTo(5));
            s.Swap(typeof(UserInfo), "_dia", 99);
            Assert.That(StateCode(compute()), Is.EqualTo(7));
            s.Swap(typeof(UserInfo), "_score", 9);
            Assert.That(StateCode(compute()), Is.EqualTo(4));
            s.Swap(typeof(UserInfo), "_score", 10);
            Assert.That(StateCode(compute()), Is.EqualTo(7));
            if (kitchen)
            {
                Get<List<string>>(s.Stage, "_giveKitchenUtensilList").Add(data.Id);
                Get<KitchenUtensilData[,]>(s.Stage, "_equipKitchenUtensilDatas")[1, 0] = (KitchenUtensilData)data;
            }
            else
            {
                Get<List<string>>(s.Stage, "_giveFurnitureList").Add(data.Id);
                Get<FurnitureData[,]>(s.Stage, "_equipFurnitureDatas")[1, 0] = (FurnitureData)data;
            }
            var placed = compute();
            Assert.That(StateCode(placed), Is.EqualTo(1));
            Assert.That((int)placed.GetType().GetField("A").GetValue(placed), Is.EqualTo(1));
            if (kitchen) Get<KitchenUtensilData[,]>(s.Stage, "_equipKitchenUtensilDatas")[1, 0] = null;
            else Get<FurnitureData[,]>(s.Stage, "_equipFurnitureDatas")[1, 0] = null;
            Assert.That(StateCode(compute()), Is.EqualTo(2));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualSlots_ApplyThresholdsFromComputedState_AndKeepUnaffectedCurrencyUntouched(bool kitchen)
    {
        using (var s = new Flow())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            try
            {
                Component view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Single(item => item != null && item.GetType() == (kitchen ? typeof(UIKitchen) : typeof(UIFurniture)));
                view.gameObject.SetActive(false);
                Type type = view.GetType();
                var data = new List<BasicData>();
                var slots = new List<UIRestaurantAdminFoodTypeSlot>();
                for (int i = 0; i < 2; i++)
                {
                    var item = (BasicData)s.Track(ScriptableObject.CreateInstance(
                        kitchen ? typeof(KitchenUtensilData) : typeof(FurnitureData)));
                    Set(item, "_id", "UI-OPT-" + i); Set(item, "_name", "UI item " + i);
                    Set(item, "_foodType", FoodType.Natural);
                    Set(item, "_moneyType", i == 0 ? MoneyType.Gold : MoneyType.Dia);
                    Set(item, "_buyPrice", 100); Set(item, "_buyScore", 10);
                    Set(item, "_unlockData", new UnlockConditionData(UnlockConditionType.None, "", 0));
                    if (kitchen) Set(item, "_type", KitchenUtensilType.Burner1);
                    else Set(item, "_type", FurnitureType.Table1);
                    data.Add(item);
                    slots.Add(UnityEngine.Object.Instantiate(Get<UIRestaurantAdminFoodTypeSlot>(view, "_slotPrefab"),
                        Get<Transform>(view, "_slotParnet")));
                }
                fonts.BindBeforeActivation(view.gameObject);
                Set(view, "_slots", slots);
                Set(view, "_slotStates", Array.CreateInstance(type.GetNestedType("SlotState", BindingFlags.NonPublic), 2));
                Set(view, "_currentTypeDataList", kitchen
                    ? (object)data.Cast<KitchenUtensilData>().ToList() : data.Cast<FurnitureData>().ToList());
                s.Swap(typeof(UserInfo), "_score", 10);
                s.Swap(typeof(UserInfo), "_money", 99L); s.Swap(typeof(UserInfo), "_dia", 99);
                Action<int> refresh = flags => Invoke(view, "RefreshSlots", Flag(type, flags));
                refresh(0);
                Assert.That(Get<UIImageAndText>(slots[0], "_notEnoughPriceImage").gameObject.activeSelf, Is.True);
                Assert.That(Get<UIImageAndText>(slots[1], "_notEnoughDiaPriceImage").gameObject.activeSelf, Is.True);
                Get<TextMeshProUGUI>(slots[1], "_nameText").text = "unaffected diamond";
                s.Swap(typeof(UserInfo), "_money", 100L);
                refresh(1);
                Assert.That(Get<UIImageAndText>(slots[0], "_priceImage").gameObject.activeSelf, Is.True);
                Assert.That(Get<TextMeshProUGUI>(slots[1], "_nameText").text, Is.EqualTo("unaffected diamond"));
                s.Swap(typeof(UserInfo), "_money", 99L); s.Swap(typeof(UserInfo), "_dia", 100);
                refresh(3);
                Assert.That(Get<UIImageAndText>(slots[0], "_notEnoughPriceImage").gameObject.activeSelf, Is.True);
                Assert.That(Get<UIImageAndText>(slots[1], "_priceImage").gameObject.activeSelf, Is.True);
                s.Swap(typeof(UserInfo), "_score", 9);
                refresh(4);
                foreach (var slot in slots) Assert.That(Get<UIImageAndText>(slot, "_scoreImage").gameObject.activeSelf, Is.True);
                s.Swap(typeof(UserInfo), "_score", 10);
                refresh(7);
                if (kitchen) Get<List<string>>(s.Stage, "_giveKitchenUtensilList").Add(data[0].Id);
                else Get<List<string>>(s.Stage, "_giveFurnitureList").Add(data[0].Id);
                refresh(8);
                Assert.That(Get<UIImageAndText>(slots[0], "_operateImage").gameObject.activeSelf, Is.True);
                Assert.That(Get<UITextAndText>(slots[0], "_useImage").gameObject.activeSelf, Is.False);
                if (kitchen) Get<KitchenUtensilData[,]>(s.Stage, "_equipKitchenUtensilDatas")[1, 0] = (KitchenUtensilData)data[0];
                else Get<FurnitureData[,]>(s.Stage, "_equipFurnitureDatas")[1, 0] = (FurnitureData)data[0];
                refresh(32);
                Assert.That(Get<UITextAndText>(slots[0], "_useImage").gameObject.activeSelf, Is.True);
                Assert.That(Get<UIImageAndText>(slots[0], "_operateImage").gameObject.activeSelf, Is.False);

                Component preview;
                if (kitchen)
                {
                    var detail = Get<UIKitchenPreview>(view, "_uikitchenPreview");
                    detail.SetData(ERestaurantFloorType.Floor1, (KitchenUtensilData)data[1]);
                    preview = detail;
                }
                else
                {
                    var detail = Get<UIFurniturePreview>(view, "_uiFurniturePreview");
                    detail.SetData(ERestaurantFloorType.Floor1, (FurnitureData)data[1]);
                    preview = detail;
                }
                for (Transform parent = view.transform.parent; parent != null; parent = parent.parent)
                    parent.gameObject.SetActive(true);
                view.gameObject.SetActive(true);
                s.Swap(typeof(UserInfo), "_dia", 99);
                Invoke(view, "OnChangeMoneyEvent");
                Invoke(view, "OnChangeDiaEvent");
                Invoke(view, "LateUpdate");
                Assert.That(Get<UIButtonAndText>(preview, "_buyButton").gameObject.activeSelf, Is.False);
                Assert.That(Get<UIButtonAndText>(preview, "_notEnoughMoneyButton").gameObject.activeSelf, Is.True);
                s.Swap(typeof(UserInfo), "_dia", 100);
                Invoke(view, "OnChangeDiaEvent"); Invoke(view, "LateUpdate");
                Assert.That(Get<UIButtonAndText>(preview, "_buyButton").gameObject.activeSelf, Is.True);
                s.Swap(typeof(UserInfo), "_score", 9);
                Invoke(view, kitchen ? "UpdateUI" : "UpdateUIOptimized"); Invoke(view, "LateUpdate");
                Assert.That(Get<UIButtonAndText>(preview, "_scoreButton").gameObject.activeSelf, Is.True);
                s.Swap(typeof(UserInfo), "_score", 10);
                Invoke(view, kitchen ? "UpdateUI" : "UpdateUIOptimized"); Invoke(view, "LateUpdate");
                Assert.That(Get<UIButtonAndText>(preview, "_buyButton").gameObject.activeSelf, Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Subscriptions_MergeDependencyEvents_IgnoreOtherCategories_AndRelease(bool kitchen)
    {
        using (var s = new Flow())
        {
            Component view = kitchen ? (Component)s.Create<UIKitchen>() : s.Create<UIFurniture>();
            Type type = view.GetType();
            Set(view, "_currentTypeDataList", kitchen ? (object)new List<KitchenUtensilData>() : new List<FurnitureData>());
            Invoke(view, "SubscribeEvents");
            view.gameObject.SetActive(true);
            foreach (string name in new[] { "OnGiveFurnitureHandler", "OnGiveKitchenUtensilHandler",
                "OnGiveRecipeHandler", "OnGiveGachaItemHandler", "OnChangeFloorHandler" })
            {
                Set(view, "_dirtyFlags", Flag(type, 0));
                ((Action)typeof(UserInfo).GetField(name, Private).GetValue(null))();
                Assert.That(Convert.ToInt32(Get<object>(view, "_dirtyFlags")), Is.Not.Zero, name);
            }
            Set(view, "_dirtyFlags", Flag(type, 0));
            string method = kitchen ? "OnChangeKitchenEvent" : "OnChangeFurnitureEvent";
            Invoke(view, method, ERestaurantFloorType.Floor2,
                kitchen ? (object)KitchenUtensilType.Burner2 : FurnitureType.Table2);
            Assert.That(Convert.ToInt32(Get<object>(view, "_dirtyFlags")), Is.Zero);
            Invoke(view, method, ERestaurantFloorType.Floor2,
                kitchen ? (object)KitchenUtensilType.Burner1 : FurnitureType.Table1);
            Assert.That(Convert.ToInt32(Get<object>(view, "_dirtyFlags")), Is.EqualTo(32));
            view.gameObject.SetActive(false);
            Set(view, "_dirtyFlags", Flag(type, 0));
            Invoke(view, "OnChangeMoneyEvent");
            Assert.That(Convert.ToInt32(Get<object>(view, "_dirtyFlags")), Is.Zero);
            Invoke(view, "OnDestroy");
            foreach (string name in new[] { "OnGiveFurnitureHandler", "OnGiveKitchenUtensilHandler",
                "OnGiveRecipeHandler", "OnGiveGachaItemHandler", "OnChangeFloorHandler" })
                Assert.That((typeof(UserInfo).GetField(name, Private).GetValue(null) as Delegate)
                    ?.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, view)) ?? 0, Is.Zero);
        }
    }
}
#endif
