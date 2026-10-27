#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CustomerTableUseRegressionTests;
using Object = UnityEngine.Object;

// Source UI is read in memory. All user state/events are owned and restored by Flow;
// no gameplay commands, account requests, purchases or saved verification assets.
public sealed class UIRefreshRegressionTests
{
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }

    private static void Raise(string name, params object[] args)
        => (Field(typeof(UserInfo), name).GetValue(null) as Delegate)?.DynamicInvoke(args);

    private static int Subscribers(string name, object target)
        => (Field(typeof(UserInfo), name).GetValue(null) as Delegate)?.GetInvocationList()
            .Count(callback => ReferenceEquals(callback.Target, target)) ?? 0;

    private sealed class Views : IDisposable
    {
        internal readonly Flow S = new Flow();
        internal readonly Scene Scene;
        internal readonly UIStaff Staff;
        internal readonly UIPictorialBook Book;
        internal readonly UICustomerPictorialBook Customers;
        internal readonly UICustomerView Detail;
        internal readonly UIPictorialBookCustomerSkin Skins;
        internal readonly MobileUINavigation Navigation;
        internal readonly StaffData[] Catalog;
        internal readonly NormalCustomerData Second;
        internal readonly CustomerSkinData Skin;

        internal Views()
        {
            try
            {
                Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
                Catalog = Resources.LoadAll<StaffData>("StaffData");
                S.Swap(typeof(StaffDataManager), "_instance", S.Create<StaffDataManager>());
                S.Swap(typeof(StaffDataManager), "_staffDataDic", Catalog.ToDictionary(data => data.Id));
                var roles = new List<StaffData>[(int)StaffGroupType.Length];
                for (int i = 0; i < roles.Length; i++) roles[i] = new List<StaffData>();
                foreach (var staff in Catalog) roles[(int)StaffDataManager.GetStaffGroupTypeFromData(staff)].Add(staff);
                S.Swap(typeof(StaffDataManager), "_staffTypeDataList", roles);
                Set(GameManager.Instance, "_addGachaItemStaffSkillTimeDic",
                    Enum.GetValues(typeof(StaffGroupType)).Cast<StaffGroupType>().ToDictionary(type => type, _ => 0f));
                Set(GameManager.Instance, "_upgradeTypeValueDic",
                    Enum.GetValues(typeof(UpgradeType)).Cast<UpgradeType>().ToDictionary(type => type, _ => 0f));
                S.Stage.OnGiveStaffHandler += () => Raise("OnGiveStaffHandler");
                S.Stage.OnChangeStaffHandler += (floor, type) => Raise("OnChangeStaffHandler", floor, type);
                S.Stage.OnChangeStaffSkinHandler += () => Raise("OnChangeStaffSkinHandler");
                S.Stage.OnUpgradeStaffHandler += () => Raise("OnUpgradeStaffHandler");
                S.Swap(typeof(SkinDataManager), "_staffSkinDataDic", new Dictionary<string, StaffSkinData>());
                S.Swap(typeof(UserInfo), "_notificationMessageSet", new HashSet<string>());
                S.Swap(typeof(UserInfo), "_clearNotificationMessageSet", new HashSet<string>());
                S.Swap(typeof(UserInfo), "_giveCustomerSkinSet", new HashSet<string>());
                S.Swap(typeof(UserInfo), "_giveGachaItemLevelDic", new Dictionary<string, int>());
                S.Swap(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int>());
                S.Swap(typeof(UserInfo), "_customerSortType", GradeSortType.None);

                Second = S.Track(Object.Instantiate(S.Data));
                Set(Second, "_visitCountFoodDic", new Dictionary<int, string>());
                Set(Second, "_id", "UI-SECOND-CUSTOMER");
                Set(S.Data, "_thumbnailSprite", S.NewSprite());
                Set(Second, "_thumbnailSprite", S.NewSprite());
                Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[Second.Id] = Second;
                S.Swap(typeof(CustomerDataManager), "_customerDataList", new List<CustomerData> { S.Data, Second });
                var records = (Dictionary<string, SaveCustomerData>)Field(typeof(UserInfo), "_enabledCustomerDic").GetValue(null);
                records[Second.Id] = new SaveCustomerData(Second.Id, "", 0);
                Skin = S.Track(ScriptableObject.CreateInstance<CustomerSkinData>());
                Set(Skin, "_id", "UI-CUSTOMER-SKIN"); Set(Skin, "_sprite", S.NewSprite()); Set(Skin, "_rank", Rank.Rare);
                Set(Skin, "_equipId", S.Data.Id);
                Get<Dictionary<string, CustomerSkinData>>(SkinDataManager.Instance, "_customerSkinDataDic")[Skin.Id] = Skin;
                S.Swap(typeof(SkinDataManager), "_customerSkinDataByLocationDic",
                    new Dictionary<string, List<CustomerSkinData>> {
                        [S.Data.Id] = new List<CustomerSkinData> { Skin }, [Second.Id] = new List<CustomerSkinData>() });
                S.Swap(typeof(ItemManager), "_instance", S.Create<ItemManager>());
                var item = new GachaItemData("UI-ITEM", "UI item", "UI description", 0, 0, 1,
                    UpgradeType.UPGRADE01, 0, 0, 1, S.NewSprite());
                S.Swap(typeof(ItemManager), "_gachaItemDataList", new List<GachaItemData> { item });
                S.Swap(typeof(ItemManager), "_gachaItemDataDic", new Dictionary<string, GachaItemData> { [item.Id] = item });
                S.Swap(typeof(ItemManager), "_upgradeTypeSpriteDic",
                    Enum.GetValues(typeof(UpgradeType)).Cast<UpgradeType>().ToDictionary(type => type, _ => item.Sprite));
                Scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
                Staff = Find<UIStaff>(); Book = Find<UIPictorialBook>();
                Customers = Find<UICustomerPictorialBook>(); Detail = Find<UICustomerView>();
                Skins = Find<UIPictorialBookCustomerSkin>();
                Staff.gameObject.SetActive(false); Book.gameObject.SetActive(false);
                ActivateParents(Staff.transform); ActivateParents(Book.transform);
                var shop = Get<UIRestaurantAdmin>(Staff, "_uiRestaurantAdmin");
                Set(shop, "_tabs", new UIRestaurantAdminTab[] {
                    Get<UIRestaurantAdminTab>(shop, "_furnitureTab"), Get<UIRestaurantAdminTab>(shop, "_staffTab"),
                    Get<UIRestaurantAdminTab>(shop, "_recipeTab"), Get<UIRestaurantAdminTab>(shop, "_kitchenTab") });
                Navigation = S.Create<MobileUINavigation>();
                Staff.OnValidate();
                Staff.ViewInit(Navigation);
                Set(Navigation, "_viewDic", new Dictionary<string, MobileUIView> { ["UIStaff"] = Staff });
                Book.Init();
            }
            catch { Dispose(); throw; }
        }

        private T Find<T>() where T : Component
            => Scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void ActivateParents(Transform child)
        {
            for (var parent = child.parent; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
        }
        internal List<UIRestaurantAdminStaffSlot> Slots => Get<List<UIRestaurantAdminStaffSlot>[]>(Staff, "_slots")
            [(int)Get<EquipStaffType>(Staff, "_currentType")];
        internal List<UICustomerSlot> CustomerSlots => Get<List<UICustomerSlot>>(Customers, "_slotList");
        internal void Open(ERestaurantFloorType floor, EquipStaffType type)
        {
            Staff.ShowUIStaff(floor, type);
            Staff.VisibleState = VisibleState.Appeared;
        }
        internal void CustomerTab() => Invoke(Book, "OnCustomerButtonClicked");
        internal void ReleaseSubscriptions()
        {
            // EditMode source scenes do not dispatch ordinary player lifecycle hooks.
            // Execute the production teardown hooks explicitly before actual destruction.
            if (Customers != null) Invoke(Customers, "OnDestroy");
            if (Book != null) Invoke(Book, "OnDestroy");
            if (Staff != null)
            {
                Invoke(Staff, "OnDestroy");
                Invoke(Get<UIStaffSkin>(Staff, "_uiSkin"), "OnDestroy");
            }
            if (Book != null) Invoke(Get<UIPictorialBookGachaItem>(Book, "_uiGachaItem"), "OnDestroy");
        }
        public void Dispose()
        {
            ReleaseSubscriptions();
            if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            S.Dispose();
        }
    }

    [TestCase(ERestaurantFloorType.Floor1, EquipStaffType.Manager)]
    [TestCase(ERestaurantFloorType.Floor2, EquipStaffType.Waiter)]
    public void StaffEntry_PreparesRequestedSelectionBeforeActivation_AndBindsOnce(
        ERestaurantFloorType floor, EquipStaffType role)
    {
        using (var v = new Views())
        {
            int enables = 0;
            List<StaffData> preparedList = null;
            var probe = v.Staff.gameObject.AddComponent<UIRefreshActivationProbe>();
            probe.OnActivated = () => {
                enables++;
                Assert.That(Get<ERestaurantFloorType>(v.Staff, "_currentFloorType"), Is.EqualTo(floor));
                Assert.That(Get<EquipStaffType>(v.Staff, "_currentType"), Is.EqualTo(role));
                Assert.That(v.Staff.SelectedStaff, Is.Not.Null);
                Assert.That(Get<UIStaffSkin>(v.Staff, "_uiSkin").gameObject.activeSelf, Is.False);
                Assert.That(Get<CanvasGroup>(v.Staff, "_canvasGroup").blocksRaycasts, Is.False);
                preparedList = Get<List<StaffData>>(v.Staff, "_currentTypeDataList");
                Assert.That(v.Slots.Count(slot => slot.gameObject.activeSelf),
                    Is.EqualTo(Get<List<StaffData>>(v.Staff, "_currentTypeDataList").Count));
            };
            v.Open(floor, role);
            Assert.That(enables, Is.EqualTo(1));
            Assert.That(Get<List<StaffData>>(v.Staff, "_currentTypeDataList"), Is.SameAs(preparedList),
                "Push/Show must not rebuild the prepared list after activation.");
            Assert.That(v.Navigation.FirstView, Is.SameAs(v.Staff));
            Assert.That(Get<StaffData>(Get<UIStaffPreview>(v.Staff, "_uiStaffPreview"), "_currentData"),
                Is.SameAs(v.Staff.SelectedStaff));
        }
    }

    [Test]
    public void StaffEntry_DefaultManager_QuestSelection_AndGachaResumePreserveContext()
    {
        using (var v = new Views())
        {
            v.Navigation.Push("UIStaff");
            Assert.That(Get<EquipStaffType>(v.Staff, "_currentType"), Is.EqualTo(EquipStaffType.Manager));
            v.Staff.VisibleState = VisibleState.Appeared;
            foreach (string quest in new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" })
            {
                Assert.That(QuestStaffTutorialPolicy.TryGetMapping(quest, out string id, out _), Is.True);
                var data = v.Catalog.Single(staff => staff.Id == id);
                v.Open(ERestaurantFloorType.Floor1, StaffDataManager.Instance.GetEquipStaffTypeList(data)[0]);
                Assert.That(v.Staff.TrySelectStaff(id), Is.True);
                Assert.That(v.Staff.SelectedStaff, Is.SameAs(data));
            }
            var selected = v.Staff.SelectedStaff;
            var canvas = Get<CanvasGroup>(v.Staff, "_canvasGroup");
            var scroll = Get<ScrollRect>(v.Staff, "_staffScrollRect");
            if (scroll != null) scroll.normalizedPosition = new Vector2(.25f, .6f);
            Vector2 position = scroll == null ? default : scroll.normalizedPosition;
            v.Staff.SuspendForGacha();
            Assert.That(canvas.alpha, Is.Zero);
            Assert.That(v.S.Stage.GiveStaff(selected), Is.True);
            v.Staff.ResumeAfterGacha();
            Assert.That(v.Staff.SelectedStaff, Is.SameAs(selected));
            Assert.That(canvas.alpha, Is.EqualTo(1));
            if (scroll != null) Assert.That(scroll.normalizedPosition, Is.EqualTo(position));
            var preview = Get<UIStaffPreview>(v.Staff, "_uiStaffPreview");
            Assert.That(Get<UIButtonAndText>(preview, "_buyButton").gameObject.activeSelf, Is.False);
            Assert.That(Get<UIButtonAndText>(preview, "_equipButton").gameObject.activeSelf, Is.True);
        }
    }

    [TestCase("MainReward01")]
    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void StaffQuestEntry_PreparesExactQuestSelectionBeforeActivation(string quest)
    {
        using (var v = new Views())
        {
            Assert.That(QuestStaffTutorialPolicy.TryGetMapping(quest, out string id, out _), Is.True);
            var selected = v.Catalog.Single(staff => staff.Id == id);
            var role = StaffDataManager.Instance.GetEquipStaffTypeList(selected)[0];
            int enables = 0;
            v.Staff.gameObject.AddComponent<UIRefreshActivationProbe>().OnActivated = () => {
                enables++;
                Assert.That(v.Staff.SelectedStaff, Is.SameAs(selected));
                Assert.That(Get<StaffData>(Get<UIStaffPreview>(v.Staff, "_uiStaffPreview"), "_currentData"),
                    Is.SameAs(selected));
            };
            Assert.That(v.Staff.ShowUIStaff(ERestaurantFloorType.Floor1, role, selected), Is.True);
            Assert.That(enables, Is.EqualTo(1));
        }
    }

    [Test]
    public void StaffEvents_RefreshAffectedPlacementAcrossFloors_WithoutPulsingUnchangedSlots()
    {
        using (var v = new Views())
        {
            v.Open(ERestaurantFloorType.Floor1, EquipStaffType.Manager);
            var data = Get<List<StaffData>>(v.Staff, "_currentTypeDataList")[0];
            Assert.That(v.Staff.TrySelectStaff(data.Id), Is.True);
            var slot = v.Slots[0];
            Assert.That(v.S.Stage.GiveStaff(data), Is.True);
            v.S.Stage.SetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Manager, data);
            var group = Get<UITextAndText>(slot, "_equipGroup");
            Assert.That(group.gameObject.activeSelf, Is.True);
            int pulses = 0, reorders = 0;
            var probe = group.gameObject.AddComponent<UIRefreshActivationProbe>();
            probe.OnActivated = () => pulses++;
            probe.OnDeactivated = () => pulses++;
            var parentProbe = slot.transform.parent.gameObject.AddComponent<UIRefreshActivationProbe>();
            parentProbe.OnChildrenChanged = () => reorders++;
            v.Staff.OnChangeStaffEvent(ERestaurantFloorType.Floor2, EquipStaffType.Waiter);
            Raise("OnChangeMoneyHandler"); Raise("OnChangeScoreHandler");
            Assert.That(pulses + reorders, Is.Zero);
            v.S.Stage.SetNullEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Manager);
            v.S.Stage.SetEquipStaff(ERestaurantFloorType.Floor2, EquipStaffType.Manager, data);
            var use = Get<UITextAndText>(slot, "_useImage");
            Assert.That(use.Text2.text, Is.EqualTo(Utility.GetFloorStrEngByType(ERestaurantFloorType.Floor2)));
            Assert.That(group.gameObject.activeSelf, Is.True);
            var selected = v.Staff.SelectedStaff;
            v.Open(ERestaurantFloorType.Floor1, EquipStaffType.Manager);
            Assert.That(v.Staff.SelectedStaff, Is.SameAs(selected));
            Assert.That(use.Text2.text, Is.EqualTo(Utility.GetFloorStrEngByType(ERestaurantFloorType.Floor2)));
            Assert.That(Subscribers("OnChangeStaffHandler", v.Staff), Is.EqualTo(1));
            v.Staff.Init();
            Assert.That(Subscribers("OnChangeStaffHandler", v.Staff), Is.EqualTo(1));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CustomerEvents_HiddenOrOtherTabDeferUntilSelected_AndKeepBadges(bool openOtherTab)
    {
        using (var v = new Views())
        {
            if (openOtherTab) v.Book.Show();
            var image = Get<Image>(v.CustomerSlots[0], "_itemImage");
            var oldSprite = image.sprite;
            UserInfo.SetCustomerSkin(v.S.Data, v.Skin);
            UserInfo.AddNotification(v.S.Data.Id);
            Raise("OnVisitedCustomerHandler"); Raise("OnGiveRecipeHandler");
            v.Customers.RefreshVisible();
            Assert.That(image.sprite, Is.SameAs(oldSprite));
            Assert.That(Get<GameObject>(v.Customers, "_alarm").activeSelf, Is.True);
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.Null);
            if (!openOtherTab) v.Book.Show();
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.Null, "Default tab must not render hidden customer detail.");
            v.CustomerTab();
            Assert.That(image.sprite, Is.SameAs(v.Skin.Sprite));
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.S.Data));
            Assert.That(Get<bool>(v.Customers, "_listDirty"), Is.False);
            var revision = Get<Image>(v.Detail, "_npcImage").sprite;
            v.Customers.RefreshVisible();
            Assert.That(Get<Image>(v.Detail, "_npcImage").sprite, Is.SameAs(revision));
        }
    }

    [Test]
    public void StaffSkinAndUpgrade_RefreshSelectedDetailOnly_AndRestoreHiddenChanges()
    {
        using (var v = new Views())
        {
            v.Open(ERestaurantFloorType.Floor1, EquipStaffType.Manager);
            var data = v.Staff.SelectedStaff;
            var other = Get<List<StaffData>>(v.Staff, "_currentTypeDataList").First(staff => staff != data);
            Assert.That(v.S.Stage.GiveStaff(data) && v.S.Stage.GiveStaff(other), Is.True);
            var skins = Get<Dictionary<string, StaffSkinData>>(SkinDataManager.Instance, "_staffSkinDataDic");
            var selectedSkin = v.S.Track(ScriptableObject.CreateInstance<StaffSkinData>());
            Set(selectedSkin, "_id", "UI-STAFF-SKIN"); Set(selectedSkin, "_equipId", data.Id);
            var otherSkin = v.S.Track(ScriptableObject.CreateInstance<StaffSkinData>());
            Set(otherSkin, "_id", "UI-OTHER-STAFF-SKIN"); Set(otherSkin, "_equipId", other.Id);
            skins.Add(selectedSkin.Id, selectedSkin); skins.Add(otherSkin.Id, otherSkin);
            var preview = Get<UIStaffPreview>(v.Staff, "_uiStaffPreview");
            var level = Get<UIImageAndText>(preview, "_levelGroup");
            int detailRefreshes = 0;
            level.gameObject.AddComponent<UIRefreshActivationProbe>().OnDeactivated = () => detailRefreshes++;
            Assert.That(v.S.Stage.TrySetStaffSkin(other, otherSkin), Is.True);
            Assert.That(detailRefreshes, Is.Zero, "Another staff's skin must not rebind selected detail.");
            Assert.That(v.S.Stage.TrySetStaffSkin(data, selectedSkin), Is.True);
            Assert.That(detailRefreshes, Is.EqualTo(1));
            Assert.That(v.S.Stage.TrySetStaffSkin(data, selectedSkin), Is.True);
            Assert.That(detailRefreshes, Is.EqualTo(1), "An unchanged snapshot must not rebind detail.");
            Assert.That(v.S.Stage.UpgradeStaff(data), Is.True);
            Assert.That(detailRefreshes, Is.EqualTo(2));
            Assert.That(level.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo("Lv.2"));
            v.Staff.gameObject.SetActive(false);
            detailRefreshes = 0;
            Assert.That(v.S.Stage.UpgradeStaff(data), Is.True);
            Assert.That(detailRefreshes, Is.Zero);
            v.Staff.gameObject.SetActive(true);
            Invoke(v.Staff, "OnEnable");
            Assert.That(detailRefreshes, Is.EqualTo(1));
            Assert.That(level.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo("Lv.3"));
            Assert.That(v.Staff.SelectedStaff, Is.SameAs(data));
        }
    }

    [Test]
    public void CustomerSkinMode_DefersHiddenListAndDetail_AndNotificationDoesNotRebindEither()
    {
        using (var v = new Views())
        {
            v.Book.Show(); v.CustomerTab();
            var image = Get<Image>(v.CustomerSlots[0], "_itemImage");
            var portrait = Get<Image>(v.Detail, "_npcImage");
            var sprite = image.sprite;
            v.Customers.ShowSkinView(v.S.Data);
            UserInfo.SetCustomerSkin(v.S.Data, v.Skin);
            Raise("OnVisitedCustomerHandler");
            v.Customers.RefreshVisible();
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(portrait.sprite, Is.SameAs(sprite));
            Assert.That(Get<Button>(v.Skins, "_equipButton").gameObject.activeSelf, Is.True,
                "The visible default-skin choice must now offer equip instead of showing in-use.");
            v.Customers.HideSkinView();
            Assert.That(image.sprite, Is.SameAs(v.Skin.Sprite));
            Assert.That(portrait.sprite, Is.SameAs(v.Skin.Sprite));
            int writes = 0;
            portrait.RegisterDirtyVerticesCallback(() => writes++);
            UserInfo.AddNotification(v.Second.Id);
            v.Customers.RefreshVisible();
            Assert.That(writes, Is.Zero);
            Assert.That(Get<GameObject>(v.Customers, "_alarm").activeSelf, Is.True);
        }
    }

    [Test]
    public void CustomerHiddenSort_IsDeferredUntilTabSelection_AndKeepsSelectedCustomer()
    {
        using (var v = new Views())
        {
            Set(v.S.Data, "_name", "UI A"); Set(v.Second, "_name", "UI Z");
            v.Book.Show(); v.CustomerTab();
            Invoke(v.Customers, "OnSlotClicked", v.Second);
            Invoke(v.Book, "OnGachaItemButtonClicked");
            var before = Get<CustomerData>(v.CustomerSlots[0], "_data");
            UserInfo.ChangeCustomerSortType(GradeSortType.NameDescending);
            v.Customers.RefreshVisible();
            Assert.That(Get<CustomerData>(v.CustomerSlots[0], "_data"), Is.SameAs(before));
            v.CustomerTab();
            Assert.That(Get<CustomerData>(v.CustomerSlots[0], "_data"),
                Is.SameAs(CustomerDataManager.Instance.GetSortCustomerList()[0]));
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.Second));
        }
    }

    [Test]
    public void CustomerVisible_UnknownEventsCoalesce_AndUnchangedFrameDoesNotToggle()
    {
        using (var v = new Views())
        {
            v.Book.Show(); v.CustomerTab();
            var slot = v.CustomerSlots[0];
            var frame = Get<GameObject>(slot, "_normalSkinFrame");
            int toggles = 0, imageWrites = 0;
            var probe = frame.AddComponent<UIRefreshActivationProbe>();
            probe.OnActivated = () => toggles++;
            probe.OnDeactivated = () => toggles++;
            Get<Image>(slot, "_itemImage").RegisterDirtyVerticesCallback(() => imageWrites++);
            slot.UpdateUI(); slot.UpdateUI();
            Assert.That(toggles + imageWrites, Is.Zero);
            Invoke(v.Customers, "OnSlotClicked", v.Second);
            UserInfo.SetCustomerSkin(v.S.Data, v.Skin);
            Raise("OnVisitedCustomerHandler"); Raise("OnGiveRecipeHandler"); Raise("OnChangeScoreHandler");
            Assert.That(Get<Image>(slot, "_itemImage").sprite, Is.Not.SameAs(v.Skin.Sprite));
            v.Customers.RefreshVisible();
            Assert.That(Get<Image>(slot, "_itemImage").sprite, Is.SameAs(v.Skin.Sprite));
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.Second), "Data refresh retains selection.");
            Assert.That(Get<bool>(v.Customers, "_listDirty"), Is.False);
            v.Customers.RefreshVisible();
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.Second));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void BookReopen_PreparesDefaultTabAndResetsSelectionWithoutEnablingOldTab(bool fromSkin)
    {
        using (var v = new Views())
        {
            v.Book.Show(); v.CustomerTab();
            Invoke(v.Customers, "OnSlotClicked", v.Second);
            if (fromSkin) v.Customers.ShowSkinView(v.S.Data);
            v.Book.Hide();
            v.Book.gameObject.SetActive(false); // Explicitly advance the animation's final visibility only.
            int enables = 0;
            var probe = v.Customers.gameObject.AddComponent<UIRefreshActivationProbe>();
            probe.OnActivated = () => enables++;
            v.Book.Show();
            Assert.That(enables, Is.Zero);
            Assert.That(v.Customers.gameObject.activeSelf, Is.False);
            Assert.That(v.Skins.gameObject.activeSelf, Is.False);
            Assert.That(Get<UIPictorialBookGachaItem>(v.Book, "_uiGachaItem").gameObject.activeInHierarchy, Is.True);
            Assert.That(Get<CanvasGroup>(v.Book, "_canvasGroup").blocksRaycasts, Is.False);
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.Second), "Hidden detail is not rebound.");
            v.CustomerTab();
            Assert.That(Get<CustomerData>(v.Detail, "_data"), Is.SameAs(v.S.Data));
        }
    }

    [Test]
    public void Subscriptions_RepeatedInitAndSceneReentryDoNotRetainDestroyedViews()
    {
        foreach (int iteration in new[] { 0, 1 })
        {
            object customer, staff, skin, preview;
            using (var v = new Views())
            {
                v.Book.Init(); v.Customers.Init(); v.Staff.Init();
                customer = v.Customers; staff = v.Staff;
                skin = Get<UIStaffSkin>(v.Staff, "_uiSkin");
                preview = Get<UIStaffPreview>(v.Staff, "_uiStaffPreview");
                Assert.That(Subscribers("OnVisitedCustomerHandler", customer), Is.EqualTo(1));
                Assert.That(Subscribers("OnChangeCustomerSkinHandler", customer), Is.EqualTo(1));
                Assert.That(Subscribers("OnChangeCustomerSkinHandler", v.Detail), Is.Zero);
                Assert.That(Subscribers("OnChangeCustomerSkinHandler", v.Skins), Is.Zero);
                Assert.That(Subscribers("OnChangeStaffSkinHandler", staff), Is.EqualTo(1));
                Assert.That(Subscribers("OnChangeStaffSkinHandler", skin), Is.EqualTo(1));
                Assert.That(Subscribers("OnChangeStaffSkinHandler", preview), Is.Zero);
                Assert.That(Subscribers("OnChangeMoneyHandler", staff), Is.Zero);
                v.ReleaseSubscriptions();
                EditorSceneManager.ClosePreviewScene(v.Scene);
                Assert.That(Subscribers("OnVisitedCustomerHandler", customer), Is.Zero);
                Assert.That(Subscribers("OnChangeCustomerSkinHandler", customer), Is.Zero);
                Assert.That(Subscribers("OnChangeStaffSkinHandler", staff), Is.Zero);
                Assert.That(Subscribers("OnChangeStaffSkinHandler", skin), Is.Zero);
                Raise("OnVisitedCustomerHandler"); Raise("OnChangeCustomerSkinHandler"); Raise("OnChangeStaffSkinHandler");
            }
        }
    }
}

public sealed class UIDeferredRefreshRegressionTests
{
    private sealed class Views : IDisposable
    {
        internal readonly Flow S = new Flow();
        internal readonly StaffGachaOfflineFonts Fonts = new StaffGachaOfflineFonts();
        internal readonly Scene Scene;
        internal readonly MailManager Mail;
        internal readonly ChallengeManager Challenges;
        internal readonly UIMailbox Mailbox;
        internal readonly UIChallenge Challenge;
        internal readonly List<ChallengeData> Daily = new List<ChallengeData>();
        internal readonly List<ChallengeData> Weekly = new List<ChallengeData>();
        internal readonly List<ChallengeData> AllTime = new List<ChallengeData>();

        internal Views()
        {
            try
            {
                Mail = S.Create<MailManager>();
                S.Swap(typeof(MailManager), "_instance", Mail);
                Set(Mail, "_isReceiveExpired", (Func<MailData, bool>)(_ => false));
                Challenges = S.Create<ChallengeManager>();
                S.Swap(typeof(ChallengeManager), "_instance", Challenges);
                Set(Challenges, "_dailyChallengeList", Daily);
                Set(Challenges, "_weeklyChallengeList", Weekly);
                Set(Challenges, "_allTimeChallengeList", AllTime);
                Set(Challenges, "_challengeDataDic", new Dictionary<string, ChallengeData>());
                foreach (string period in new[] { "Daily", "Weekly", "AllTime" })
                {
                    S.Swap(typeof(UserInfo), "_done" + period + "ChallengeSet", new HashSet<string>());
                    S.Swap(typeof(UserInfo), "_clear" + period + "ChallengeSet", new HashSet<string>());
                }
                S.Swap(typeof(UserInfo), "_totalAddMoney", 0L);
                S.Swap(typeof(UserInfo), "_totalCumulativeCustomerCount", 0);
                Scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
                Mailbox = Find<UIMailbox>();
                Challenge = Find<UIChallenge>();
                Mailbox.gameObject.SetActive(false);
                Challenge.gameObject.SetActive(false);
                Fonts.BindBeforeActivation(Mailbox.gameObject, Challenge.gameObject);
                Mailbox.Init();
                Challenge.Init();
                ActivateParents(Mailbox.transform);
                ActivateParents(Challenge.transform);
            }
            catch { Dispose(); throw; }
        }

        private T Find<T>() where T : Component => Scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static void ActivateParents(Transform child)
        {
            for (var parent = child.parent; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
        }

        internal UIChallengeTab Tab(string name) => Get<UIChallengeTab>(Challenge, "_ui" + name);
        internal List<UIChallengeTabSlot> Slots(string name) => Get<List<UIChallengeTabSlot>>(Tab(name), "_uiChallengeTabSlotList");
        internal List<UIMailSlot> MailSlots => Get<List<UIMailSlot>>(Mailbox, "_slotPool");
        internal List<MailData> Mails => Get<List<MailData>>(Mail, "_mailList");
        internal bool Alarm(string name) => Get<GameObject>(Challenge, "_ui" + name + "Alarm").activeSelf;

        internal ChallengeData Add(string id, Challenges period, bool customers = false)
        {
            ChallengeData data = customers
                ? (ChallengeData)new Type09ChallengeData(period, ChallengeType.TYPE09, id, id, 100, MoneyType.Gold, 0, 0, null)
                : new Type11ChallengeData(period, ChallengeType.TYPE11, id, id, 100, MoneyType.Gold, 0, 0, null);
            (period == global::Challenges.Daily ? Daily : period == global::Challenges.Weekly ? Weekly : AllTime).Add(data);
            Get<Dictionary<string, ChallengeData>>(Challenges, "_challengeDataDic").Add(id, data);
            return data;
        }

        internal void OpenMailWithoutNetwork()
        {
            // Exercise the rendering boundary without issuing LoadMailListAsync/account requests.
            Mailbox.gameObject.SetActive(true);
            Set(Mailbox, "_isShown", true);
            Invoke(Mailbox, "RefreshUI");
            Invoke(Mailbox, "LateUpdate");
        }

        internal void ReleaseSubscriptions()
        {
            if (Mailbox != null) Invoke(Mailbox, "OnDestroy");
            if (Challenge != null)
            {
                Invoke(Challenge, "OnDestroy");
                foreach (string name in new[] { "Daily", "Weekly", "AllTime" })
                    Invoke(Tab(name), "OnDestroy");
            }
        }

        public void Dispose()
        {
            ReleaseSubscriptions();
            if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            Fonts.Dispose();
            S.Dispose();
        }
    }

    private static MailData Mail(string date, bool received = false) => new MailData(BackEnd.PostType.Admin,
        LitJson.JsonMapper.ToObject("{\"inDate\":\"" + date +
            "\",\"title\":\"Test mail\",\"content\":\"Preview\",\"author\":\"Test\",\"items\":[],\"isRead\":\"" +
            (received ? "1" : "0") + "\"}"));

    private static FieldInfo EventField(object owner, string name) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static void Raise(object owner, string name, params object[] args)
        => (EventField(owner, name).GetValue(owner) as Delegate)?.DynamicInvoke(args);

    private static int Subscribers(object owner, string name)
        => (EventField(owner, name).GetValue(owner) as Delegate)?.GetInvocationList().Length ?? 0;

    [Test]
    public void Mail_HiddenChangesCoalesce_SortAndPoolOrderRemainStable()
    {
        using (var v = new Views())
        {
            var old = Mail("2026-10-01");
            var newest = Mail("2026-10-03", true);
            var equal = Mail("2026-10-03");
            v.Mails.AddRange(new[] { old, newest, equal });
            for (int i = 0; i < 3; i++) Raise(v.Mail, "OnMailListRefreshed");
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(v.MailSlots, Is.Empty);
            v.OpenMailWithoutNetwork();
            CollectionAssert.AreEqual(new[] { newest, equal, old }, v.MailSlots.Select(slot => Get<MailData>(slot, "_data")));
            Assert.That(Get<TextMeshProUGUI>(v.Mailbox, "_mailCountText").text, Is.EqualTo("3/100"));
            Assert.That(Get<Button>(v.Mailbox, "_deleteReadButton").gameObject.activeSelf, Is.True);
            var first = v.MailSlots[0];
            var text = Get<TextMeshProUGUI>(first, "_contentText");
            int disables = 0;
            first.gameObject.AddComponent<UIRefreshActivationProbe>().OnDeactivated = () => disables++;
            text.text = "unbound";
            for (int i = 0; i < 3; i++) Raise(v.Mail, "OnMailListRefreshed");
            Assert.That(text.text, Is.EqualTo("unbound"));
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(text.text, Is.Not.EqualTo("unbound"));
            text.text = "one bind only";
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(text.text, Is.EqualTo("one bind only"));
            Assert.That(disables, Is.Zero, "Existing active slots must not be toggled off for a refresh.");
            Invoke(v.Mailbox, "OnSortToggled");
            Invoke(v.Mailbox, "LateUpdate");
            CollectionAssert.AreEqual(new[] { old, newest, equal }, v.MailSlots.Select(slot => Get<MailData>(slot, "_data")));
            Get<Button>(v.MailSlots[1], "_slotButton").onClick.Invoke();
            var popup = Get<UIMailDetailPopup>(v.Mailbox, "_detailPopup");
            Assert.That(popup.gameObject.activeSelf, Is.True);
            Assert.That(Get<MailData>(popup, "_currentMail"), Is.SameAs(newest));
            Get<Button>(popup, "_overlayButton").onClick.Invoke();
            Assert.That(popup.gameObject.activeSelf, Is.False);
            v.Mailbox.Hide();
            v.Mailbox.gameObject.SetActive(false);
            v.Mails.Remove(newest);
            Raise(v.Mail, "OnMailListRefreshed");
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(Get<TextMeshProUGUI>(v.Mailbox, "_mailCountText").text, Is.EqualTo("3/100"));
            v.OpenMailWithoutNetwork();
            Assert.That(Get<TextMeshProUGUI>(v.Mailbox, "_mailCountText").text, Is.EqualTo("2/100"));
            Assert.That(v.MailSlots[2].gameObject.activeSelf, Is.False);
            Assert.That(Get<Button>(v.Mailbox, "_deleteReadButton").gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void Mail_OldLoadCompletionCannotReleaseNewLoad_AndBindingReadsLatestManagerList()
    {
        using (var v = new Views())
        {
            v.OpenMailWithoutNetwork();
            Set(v.Mailbox, "_loadVersion", 2);
            Set(v.Mail, "<IsMailListLoading>k__BackingField", true);
            Invoke(v.Mailbox, "SetLoading", true);
            v.Mails.Add(Mail("2026-10-01"));
            Raise(v.Mail, "OnMailListRefreshed");
            Invoke(v.Mailbox, "CompleteMailLoad", 1);
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(v.MailSlots, Is.Empty, "A partially loaded list must not be bound.");
            Assert.That(v.Mail.IsMailListLoading, Is.True);
            var latest = Mail("2026-10-04", true);
            v.Mails.Clear(); v.Mails.Add(latest);
            Set(v.Mail, "_mailListLoadSerial", 2);
            Invoke(v.Mail, "CompleteMailListLoad", 2);
            Invoke(v.Mailbox, "CompleteMailLoad", 2);
            Raise(v.Mail, "OnMailListRefreshed");
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(Get<MailData>(v.MailSlots.Single(), "_data"), Is.SameAs(latest));
            Assert.That(Get<bool>(v.Mailbox, "_isDirty"), Is.False);
            Set(v.Mail, "<IsMailListLoading>k__BackingField", true);
            Set(v.Mail, "_mailListLoadSerial", 4);
            v.Mails.Clear();
            Raise(v.Mail, "OnMailListRefreshed");
            Invoke(v.Mail, "CompleteMailListLoad", 3);
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(v.Mail.IsMailListLoading, Is.True);
            Assert.That(v.MailSlots.Single().gameObject.activeSelf, Is.True);
            v.Mails.Add(latest);
            Invoke(v.Mail, "CompleteMailListLoad", 4);
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(v.Mail.IsMailListLoading, Is.False, "A superseding alarm load releases the window too.");
            Assert.That(Get<bool>(v.Mailbox, "_isLoading"), Is.False);
            Set(v.Mail, "_readReceiveQuery", (Func<Muks.BackEnd.GameDataRestoreQuery>)(() => null));
            int failures = 0;
            v.Mail.LoadMailListAsync(onFail: () => failures++);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(v.Mail.IsMailListLoading, Is.False);
            Assert.That(v.Mails.Single(), Is.SameAs(latest));
            latest.SetReceived();
            Raise(v.Mail, "OnMailListRefreshed");
            v.Mailbox.Hide();
            Invoke(v.Mailbox, "LateUpdate");
            Assert.That(Get<bool>(v.Mailbox, "_isDirty"), Is.True);
            v.OpenMailWithoutNetwork();
            Assert.That(Get<bool>(v.Mailbox, "_isDirty"), Is.False);
        }
    }

    [Test]
    public void Challenge_HiddenCompletionAndAddMaintainBadges_OnlySelectedTabBinds()
    {
        using (var v = new Views())
        {
            var progress = v.Add("DailyProgress", global::Challenges.Daily);
            var done = v.Add("DailyDone", global::Challenges.Daily);
            var claimed = v.Add("DailyClaimed", global::Challenges.Daily);
            var repeat = v.Add("DailyRepeat", global::Challenges.Daily);
            var weekly = v.Add("WeeklyDone", global::Challenges.Weekly);
            UserInfo.DoneChallenge(done); UserInfo.DoneChallenge(claimed); UserInfo.ClearChallenge(claimed);
            UserInfo.DoneChallenge(repeat); UserInfo.ClearChallenge(repeat);
            UserInfo.DoneChallenge(weekly);
            Raise(v.Challenges, "OnAddChallengeHandler");
            Assert.That(v.Alarm("Daily"), Is.True);
            Assert.That(v.Alarm("Weekly"), Is.True);
            foreach (string name in new[] { "Daily", "Weekly", "AllTime" })
            {
                Invoke(v.Tab(name), "LateUpdate");
                Assert.That(v.Slots(name), Is.Empty);
            }
            v.Challenge.Show();
            CollectionAssert.AreEqual(new[] { done, progress, claimed }, v.Slots("Daily").Select(slot => slot.Data));
            Assert.That(v.Slots("Weekly"), Is.Empty);
            Invoke(v.Challenge, "OnWeeklyButtonClicked");
            Assert.That(v.Slots("Weekly").Single().Data, Is.SameAs(weekly));
            Assert.That(Get<RectTransform>(v.Tab("Daily"), "_contentRect").gameObject.activeSelf, Is.False);
            UserInfo.ClearChallenge(weekly);
            var weeklyText = Get<TextMeshProUGUI>(v.Slots("Weekly")[0], "_descriptionText");
            weeklyText.text = "pending bind";
            v.Challenges.UpdateChallengeByChallenges(global::Challenges.Weekly);
            v.Challenges.UpdateChallengeByChallenges(global::Challenges.Weekly);
            Assert.That(weeklyText.text, Is.EqualTo("pending bind"));
            Invoke(v.Tab("Weekly"), "LateUpdate");
            Assert.That(weeklyText.text, Is.EqualTo(weekly.Description));
            weeklyText.text = "one bind only";
            Invoke(v.Tab("Weekly"), "LateUpdate");
            Assert.That(weeklyText.text, Is.EqualTo("one bind only"));
            Assert.That(v.Alarm("Weekly"), Is.False);
            Assert.That(Get<Button>(v.Slots("Weekly")[0], "_clearButton").gameObject.activeSelf, Is.True);
            v.Challenge.Hide(); v.Challenge.gameObject.SetActive(false);
            UserInfo.ClearChallenge(done);
            Assert.That(v.Alarm("Daily"), Is.False);
            var added = v.Add("DailyNew", global::Challenges.Daily);
            UserInfo.DoneChallenge(added);
            Raise(v.Challenges, "OnAddChallengeHandler");
            Assert.That(v.Alarm("Daily"), Is.True);
            Assert.That(v.Slots("Daily").Count, Is.EqualTo(3));
            v.Challenge.Show();
            CollectionAssert.AreEqual(new[] { added, progress, done, claimed }, v.Slots("Daily").Select(slot => slot.Data));
        }
    }

    [Test]
    public void Challenge_ProgressTargetsOnlyEventType_HiddenAndInactiveSlotsDoNoWork()
    {
        using (var v = new Views())
        {
            v.Add("DailyMoney", global::Challenges.Daily);
            v.Add("DailyCustomers", global::Challenges.Daily, true);
            v.Add("WeeklyMoney", global::Challenges.Weekly);
            v.Challenge.Show();
            var money = v.Slots("Daily")[0];
            var other = v.Slots("Daily")[1];
            var otherText = Get<TextMeshProUGUI>(other, "_countText");
            otherText.text = "unchanged type";
            v.S.Swap(typeof(UserInfo), "_totalAddMoney", 50L);
            Raise(v.Challenges, "OnChallengePercentUpdateHandler", ChallengeType.TYPE11);
            Assert.That(Get<Image>(money, "_percentBar").fillAmount, Is.EqualTo(.5f));
            Assert.That(Get<TextMeshProUGUI>(money, "_countText").text, Is.EqualTo("50 / 100"));
            Assert.That(otherText.text, Is.EqualTo("unchanged type"));
            Assert.That(Subscribers(v.Challenges, "OnChallengePercentUpdateHandler"), Is.EqualTo(3),
                "Only tabs subscribe, never individual pooled slots.");
            Invoke(v.Challenge, "OnWeeklyButtonClicked");
            v.S.Swap(typeof(UserInfo), "_totalAddMoney", 75L);
            Raise(v.Challenges, "OnChallengePercentUpdateHandler", ChallengeType.TYPE11);
            Assert.That(Get<Image>(money, "_percentBar").fillAmount, Is.EqualTo(.5f));
            Invoke(v.Challenge, "OnDailyButtonClicked");
            Assert.That(Get<Image>(money, "_percentBar").fillAmount, Is.EqualTo(.75f));
            v.Daily.RemoveAt(1);
            Raise(v.Challenges, "OnAddChallengeHandler");
            Invoke(v.Tab("Daily"), "LateUpdate");
            Assert.That(other.gameObject.activeSelf, Is.False);
            otherText.text = "inactive";
            Raise(v.Challenges, "OnChallengePercentUpdateHandler", ChallengeType.TYPE09);
            Assert.That(otherText.text, Is.EqualTo("inactive"));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Subscriptions_SceneReentryReleasesMailAndTabEvents(int iteration)
    {
        using (var v = new Views())
        {
            v.Add("DailyMoney", global::Challenges.Daily);
            v.Challenge.Show();
            Assert.That(Subscribers(v.Mail, "OnMailListRefreshed"), Is.EqualTo(1));
            Assert.That(Subscribers(v.Challenges, "OnAddChallengeHandler"), Is.EqualTo(1));
            v.ReleaseSubscriptions();
            Assert.That(Subscribers(v.Mail, "OnMailListRefreshed"), Is.Zero);
            Assert.That(Subscribers(v.Mail, "OnMailListLoadCompleted"), Is.Zero);
            Assert.That(Subscribers(v.Challenges, "OnAddChallengeHandler"), Is.Zero);
            Assert.That(Subscribers(v.Challenges, "OnChallengePercentUpdateHandler"), Is.Zero);
            Raise(v.Mail, "OnMailListRefreshed");
            Raise(v.Challenges, "OnAddChallengeHandler");
            Raise(v.Challenges, "OnChallengePercentUpdateHandler", ChallengeType.TYPE11);
        }
    }
}

[ExecuteAlways]
public sealed class UIRefreshActivationProbe : MonoBehaviour
{
    internal Action OnActivated, OnDeactivated, OnChildrenChanged;
    private void OnEnable() => OnActivated?.Invoke();
    private void OnDisable() => OnDeactivated?.Invoke();
    private void OnTransformChildrenChanged() => OnChildrenChanged?.Invoke();
}
#endif
