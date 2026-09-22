#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Muks.Tween;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Existing detached account/Stage fixtures; no SDK, account grant, or saved scene changes.
public partial class StaffStageMigrationCollectionTests
{
    [TestCase("MainReward01")]
    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void IntegrationNavigation_QuestRequestReentryKeepsPendingAndConfirmedRequestWithoutSendingAgain(string quest)
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture(quest, out var state, out var wallet);
            var singleton = Field(typeof(BackendManager), "_instance", true).GetValue(null);
            bool sdk = BackEnd.Backend.IsInitialized;
            var diamondWallet = fixture.Manager.StaffPurchaseWallet;
            int draws = 0, records = 0;
            fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage, diamondWallet,
                _ => { draws++; return null; }, _ => records++);
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            try
            {
                Authenticate(fixture, fixture.Account);
                RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out var original, out _), Is.True);
                Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out _, out _), Is.False,
                    "An eligible new offer is not an already accepted request");
                using (var ui = new QuestShopRecoveryView(fixture.Manager, original))
                {
                    ui.ClickDetailEntry();
                    Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out string error), Is.True, error);
                    Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                    ui.ClickExit(); // Native X is available while the accepted save is still pending.
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff));
                    Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.False);
                    Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out var pending, out var retained), Is.True);
                    Assert.That(retained, Is.SameAs(operation));
                    Assert.That(QuestShopEntryCurrent(fixture.Manager, original), Is.True);
                    ui.BindContext(fixture.Manager, pending);
                    ui.ResumeRequest(fixture.Manager, retained);
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.View));
                    Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                    ui.Staff.OnSingleGachaButtonClicked();
                    ui.Staff.OnTenGachaButtonClicked();
                    Assert.That(fixture.Game.Writes, Is.EqualTo(1), "Reentry cannot re-send or fall through to paid purchase");
                    ui.ClickExit();

                    state.OnNotify = () => state.Done = true;
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    fixture.Game.WriteReplies[1](Bro("204", ""));
                    Assert.That(operation.LocalCompleted, Is.True);
                    Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.False);
                    Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out var completed, out retained), Is.True);
                    Assert.That(retained, Is.SameAs(operation));
                    // Owned detail normally hides this existing purchase control. The explicit
                    // quest shortcut continuation must not depend on or add another button.
                    ui.DetailEntry.gameObject.SetActive(false);
                    ui.BindContext(fixture.Manager, completed);
                    ui.ResumeRequest(fixture.Manager, retained);
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.View));
                    Assert.That(ui.Display.TryShowCompleted(false, out error), Is.True, error);
                    Assert.That(ui.Display.EditorCurrentItem.StaffId, Is.EqualTo(completed.StaffId));
                    ui.Display.AcknowledgeResult(false);
                    ui.ClickExit();
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff));
                    ui.ResumeRequest(fixture.Manager, retained);
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff),
                        "A later shortcut must not automatically reopen an acknowledged result");
                    state.Claimed = true;
                    Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out _, out _), Is.False);
                    Assert.That(QuestShopEntryCurrent(fixture.Manager, completed), Is.False);
                    state.Claimed = false;
                    state.Quest = "MainReward12";
                    Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out _, out _), Is.False);
                    Assert.That(QuestShopEntryCurrent(fixture.Manager, completed), Is.False);
                    Assert.That(fixture.Game.Writes, Is.EqualTo(2), "Only the original grant and its existing completion autosave");
                    Assert.That(draws + records, Is.Zero);
                    Assert.That(operation.CompletionCount, Is.EqualTo(1));
                    Assert.That(state.Notifications, Is.EqualTo(1));
                    Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                    Assert.That(wallet.GoldValue, Is.EqualTo(1000000));
                    Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
                    AssertAccountLevels(fixture, completed.StaffId, 1);
                    AssertAccountLevels(fixture, "STAFF03", 4);
                }
                Assert.That(Field(typeof(BackendManager), "_instance", true).GetValue(null), Is.SameAs(singleton));
                Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(sdk));
            }
            finally { fixture.Manager.DestroyEditorOfflineOwner(); }
        }
    }

    [TestCase("query")]
    [TestCase("account")]
    public void IntegrationNavigation_QuestRequestReentryRejectsChangedAccountOrQuery(string change)
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture("MainReward01", out _, out _);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out _), Is.True);
            Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out var retained, out _), Is.True);
            if (change == "query") fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            else Authenticate(fixture, Unique("quest-reentry-account"));
            Assert.That(fixture.Manager.TryGetRetainedQuestStaffEntry(out _, out _), Is.False);
            Assert.That(QuestShopEntryCurrent(fixture.Manager, retained), Is.False);
            Assert.That(operation.LocalCompleted, Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1), "Navigation does not retransmit an invalidated request");
        }
    }

    private static bool QuestShopEntryCurrent(BackendManager owner, QuestStaffOffer offer)
        => (bool)typeof(UIMainCanvas).GetMethod("IsQuestShopOfferCurrent", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { owner, offer });

    private sealed class QuestShopRecoveryView : IDisposable
    {
        private readonly Scene _scene;
        private readonly GameObject _root;
        private readonly StaffGachaOfflineFonts _fonts;
        private readonly UIMainCanvas _canvas;
        public readonly UIGacha View;
        public readonly UIStaffGacha Staff;
        public readonly UIStaff ShopStaff;
        public readonly MobileUINavigation Navigation;
        public readonly Button DetailEntry;
        public StaffGachaPurchaseDisplay Display => Staff == null ? null :
            (StaffGachaPurchaseDisplay)Field(typeof(UIStaffGacha), "_questDisplay").GetValue(Staff);

        public QuestShopRecoveryView(BackendManager owner, QuestStaffOffer offer)
        {
            _scene = EditorSceneManager.NewPreviewScene();
            try
            {
                _root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(_scene, out View, out Staff, includeNavigation: true);
                _fonts = new StaffGachaOfflineFonts();
                _fonts.BindBeforeActivation(_root);
                StaffGachaOfflineViewFactory.ConfigureNavigation(View, Staff, owner);
                _fonts.BindBeforeActivation(_root);
                _canvas = _root.GetComponent<UIMainCanvas>();
                Navigation = _root.GetComponent<MobileUINavigation>();
                ShopStaff = _root.GetComponentInChildren<UIStaff>(true);
                DetailEntry = StaffGachaOfflineViewFactory.NavigationEntry(View);
                Invoke(_canvas, "Awake");
                Invoke(Navigation, "Start");
                _root.SetActive(true);
                StaffGachaOfflineViewFactory.BeginNavigation(View);
                BindContext(owner, offer);
            }
            catch { Dispose(); throw; }
        }

        public void BindContext(BackendManager owner, QuestStaffOffer offer)
        {
            // The existing exact-selection integration test covers ShowQuestStaff. Here
            // bind that selected context to the actual copied native return stack.
            Field(typeof(UIStaff), "_previewStaffData").SetValue(ShopStaff, offer.StaffData);
            Field(typeof(UIMainCanvas), "_questShopOwner").SetValue(_canvas, owner);
            Field(typeof(UIMainCanvas), "_questShopOffer").SetValue(_canvas, offer);
            Field(typeof(UIMainCanvas), "_questShopSelectionRevision").SetValue(_canvas, ShopStaff.SelectionRevision);
        }

        public void ClickDetailEntry() { DetailEntry.onClick.Invoke(); CompleteTweens(); }

        public void ResumeRequest(BackendManager owner, QuestStaffGrantExecution operation)
        {
            var routine = (IEnumerator)typeof(UIMainCanvas).GetMethod("ResumeQuestStaffRequestWhenReady",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_canvas, new object[] { owner, operation });
            Assert.That(routine.MoveNext(), Is.False, "The selected copied shop/detail is already stable");
            CompleteTweens();
        }

        public void ClickExit()
        {
            var exit = View.transform.Find("Anime UI/UI Components/Exit Button").GetComponent<Button>();
            Assert.That(exit.gameObject.activeInHierarchy && exit.interactable, Is.True);
            exit.onClick.Invoke();
        }

        private void CompleteTweens()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            for (int pass = 0; pass < 3; pass++)
            foreach (TweenData tween in _root.GetComponentsInChildren<TweenData>(true))
            {
                if (!tween.enabled) continue;
                if (typeof(TweenData).GetField("_percentHandler", flags).GetValue(tween) == null) Invoke(tween, "Awake");
                Invoke(tween, "Update");
                typeof(TweenData).GetField("ElapsedDuration", flags).SetValue(tween, float.MaxValue);
                Invoke(tween, "Update");
            }
        }

        private static void Invoke(object owner, string method)
        {
            // Tween implementations inherit a private Awake from TweenData; a lookup
            // only on the concrete type cannot find that native initialization step.
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                var callback = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (callback == null) continue;
                callback.Invoke(owner, null);
                return;
            }
            throw new MissingMethodException(owner.GetType().Name, method);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Display?.Dispose();
                foreach (ScrollingImage image in _root.GetComponentsInChildren<ScrollingImage>(true))
                {
                    var material = (Material)Field(typeof(ScrollingImage), "_material").GetValue(image);
                    if (material != null && !EditorUtility.IsPersistent(material)) UnityEngine.Object.DestroyImmediate(material);
                }
                UnityEngine.Object.DestroyImmediate(_root);
            }
            _fonts?.Dispose();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }
    }

    [Test]
    public void IntegrationNavigation_QuestOfferIsBoundToExactQuestStaffAndRestoreGeneration()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string quest in new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" })
        {
            var fixture = CreateQuestStaffFixture(quest, out var state, out _);
            var oldManager = fixture.Manager;
            fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
                oldManager.StaffPurchaseWallet, _ => throw new AssertionException("Navigation must not draw"),
                _ => throw new AssertionException("Navigation must not record a paid purchase"));
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            try
            {
                Authenticate(fixture, fixture.Account);
                RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out var offer, out string error), Is.True, error);
                QuestStaffTutorialPolicy.TryGetMapping(quest, out string expectedStaff, out _);
                Assert.That(offer.StaffId, Is.EqualTo(expectedStaff));
                var check = typeof(UIMainCanvas).GetMethod("IsQuestShopOfferCurrent", BindingFlags.Static | BindingFlags.NonPublic);
                bool Valid() => (bool)check.Invoke(null, new object[] { fixture.Manager, offer });
                Assert.That(Valid(), Is.True);
                state.Quest = "MainReward12";
                Assert.That(Valid(), Is.False, "A shop-open offer does not survive a quest change");
                state.Quest = quest;
                Assert.That(Valid(), Is.True);
                RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
                Assert.That(Valid(), Is.False, "Even the same account and quest need a fresh shortcut after requery");
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out var renewed, out _), Is.True);
                Assert.That(check.Invoke(null, new object[] { fixture.Manager, renewed }), Is.True);
                Authenticate(fixture, Unique("new-shop-account"));
                Assert.That(check.Invoke(null, new object[] { fixture.Manager, renewed }), Is.False);
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
            finally { fixture.Manager.DestroyEditorOfflineOwner(); }
        }
    }

    [Test]
    public void IntegrationNavigation_ProductStaffDetailSeparatesPlacementAndUpgradeAndUsesCurrentFloor()
    {
        var catalog = Catalog();
        using (var scope = new RuntimeStaffScope(catalog))
        {
            var roleCacheField = Field(typeof(StaffDataManager), "_staffTypeDataList", true);
            var oldRoleCache = roleCacheField.GetValue(null);
            var fixture = CreateAccountRuntimeFixture(out _);
            var stage = fixture.Stage.Runtime[EStage.Stage1];
            var oldStages = Field(typeof(UserInfo), "_stageInfos", true).GetValue(null);
            var oldStage = Field(typeof(UserInfo), "_currentStage", true).GetValue(null);
            var oldManager = Field(typeof(GameManager), "_instance", true).GetValue(null);
            var gameHost = new GameObject("Inactive shop detail test manager"); gameHost.SetActive(false);
            var manager = gameHost.AddComponent<GameManager>();
            Field(typeof(GameManager), "_addGachaItemStaffSkillTimeDic").SetValue(manager,
                Enum.GetValues(typeof(StaffGroupType)).Cast<StaffGroupType>().ToDictionary(type => type, _ => 0f));
            Scene scene = default;
            UIStaffPreview preview = null;
            try
            {
                var roleCache = new List<StaffData>[(int)StaffGroupType.Length];
                for (int index = 0; index < roleCache.Length; index++)
                    roleCache[index] = new List<StaffData>();
                foreach (var employee in catalog)
                    roleCache[(int)StaffDataManager.GetStaffGroupTypeFromData(employee)].Add(employee);
                roleCacheField.SetValue(null, roleCache);

                Field(typeof(GameManager), "_instance", true).SetValue(null, manager);
                Field(typeof(UserInfo), "_stageInfos", true).SetValue(null,
                    new[] { stage, fixture.Stage.Runtime[EStage.Stage2], fixture.Stage.Runtime[EStage.Stage3] });
                Field(typeof(UserInfo), "_currentStage", true).SetValue(null, EStage.Stage1);
                scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
                preview = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIStaffPreview>(true)).Single();
                int upgradeOpens = 0;
                preview.Init((floor, role, employee) => stage.SetEquipStaff(floor, role, employee),
                    (floor, employee) => stage.SetNullEquipStaff(floor, employee), _ => upgradeOpens++);
                var staff = scope.Staff("STAFF06");
                var equip = (UIButtonAndText)Field(typeof(UIStaffPreview), "_equipButton").GetValue(preview);
                var buy = (UIButtonAndText)Field(typeof(UIStaffPreview), "_buyButton").GetValue(preview);
                var usingButton = (UI3LayerButton)Field(typeof(UIStaffPreview), "_usingButton").GetValue(preview);
                var select = (UIStaffSelectSlot)Field(typeof(UIStaffPreview), "_selectGroup").GetValue(preview);
                var upgrade = (Image)Field(typeof(UIStaffSelectSlot), "_upgradeImage").GetValue(select);
                preview.SetData(ERestaurantFloorType.Floor1, EquipStaffType.Manager, staff);
                Assert.That(buy.gameObject.activeSelf, Is.True);
                Assert.That(equip.gameObject.activeSelf, Is.False);
                Assert.That(stage.GiveStaff(staff), Is.True);
                preview.SetData(ERestaurantFloorType.Floor1, EquipStaffType.Manager, staff);
                Assert.That(buy.gameObject.activeSelf, Is.False);
                Assert.That(equip.gameObject.activeSelf, Is.True);
                Assert.That(upgrade.gameObject.activeSelf, Is.True);
                var equipRect = (RectTransform)equip.transform;
                var upgradeRect = upgrade.rectTransform;
                Assert.That(equipRect.parent, Is.SameAs(upgradeRect.parent), "Assert the actual product scene overlap risk");
                Assert.That(upgradeRect.anchoredPosition.x - equipRect.anchoredPosition.x,
                    Is.GreaterThanOrEqualTo(250f), "Placement and upgrade must have separate hit regions");
                upgrade.GetComponent<Button>().onClick.Invoke();
                Assert.That(upgradeOpens, Is.EqualTo(1), "The visible upgrade action keeps the original upgrade callback");
                ((Button)Field(typeof(UIButtonAndText), "_button").GetValue(equip)).onClick.Invoke();
                Assert.That(stage.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Manager), Is.SameAs(staff));
                preview.SetData(ERestaurantFloorType.Floor1, EquipStaffType.Manager, staff);
                Assert.That(usingButton.gameObject.activeSelf, Is.True);
                Assert.That(equip.gameObject.activeSelf, Is.False);
                typeof(UIStaffPreview).GetMethod("OnUsingButtonClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(preview, null);
                Assert.That(stage.IsEquipStaff(staff), Is.False);
                preview.SetData(ERestaurantFloorType.Floor2, EquipStaffType.Manager, staff);
                ((Button)Field(typeof(UIButtonAndText), "_button").GetValue(equip)).onClick.Invoke();
                Assert.That(stage.GetEquipStaff(ERestaurantFloorType.Floor2, EquipStaffType.Manager), Is.SameAs(staff));
                Assert.That(stage.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Manager), Is.Null);
                Assert.That(fixture.Manager.StaffRuntime.GetLevel(staff.Id), Is.EqualTo(1));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
                UIStaff detail = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIStaff>(true)).Single();

                var sourceOrder = new List<StaffData>
                {
                    scope.Staff("STAFF01"),
                    scope.Staff("STAFF41"),
                    scope.Staff("STAFF35"),
                    scope.Staff("STAFF39"),
                    scope.Staff("STAFF33")
                };
                string[] unchangedSourceIds = sourceOrder.Select(data => data.Id).ToArray();
                var createDisplayDataList = typeof(UIStaff).GetMethod(
                    "CreateDisplayDataList", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(createDisplayDataList, Is.Not.Null);
                var displayOrder = (List<StaffData>)createDisplayDataList.Invoke(null, new object[] { sourceOrder });
                Assert.That(displayOrder.Select(data => data.Id), Is.EqualTo(new[]
                {
                    "STAFF39", "STAFF35", "STAFF33", "STAFF41", "STAFF01"
                }), "Employee display order is Special, Unique, Rare, Normal, then numeric STAFF ID");
                Assert.That(sourceOrder.Select(data => data.Id), Is.EqualTo(unchangedSourceIds),
                    "Display sorting must not mutate the manager catalog input");
                Field(typeof(UIStaff), "_currentTypeDataList").SetValue(detail, displayOrder);
                var findDisplayStaff = typeof(UIStaff).GetMethod(
                    "FindDisplayStaff", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(findDisplayStaff, Is.Not.Null);
                Assert.That(findDisplayStaff.Invoke(detail, new object[] { scope.Staff("STAFF35") }),
                    Is.SameAs(scope.Staff("STAFF35")), "A selected employee remains selected after display sorting");

                var content = (RectTransform)Field(typeof(UIStaff), "_slotParnet").GetValue(detail);
                var grid = content.GetComponent<GridLayoutGroup>();
                var fitter = content.GetComponent<ContentSizeFitter>();
                Assert.That(grid, Is.Not.Null);
                Assert.That(fitter, Is.Not.Null);
                Assert.That(content.childCount, Is.Zero, "The scene contains no placeholder cards");
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.EqualTo(3));
                Assert.That(grid.startCorner, Is.EqualTo(GridLayoutGroup.Corner.UpperLeft));
                Assert.That(grid.startAxis, Is.EqualTo(GridLayoutGroup.Axis.Horizontal));
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.UpperLeft));
                Assert.That(content.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(content.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(content.pivot, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(content.sizeDelta.x, Is.EqualTo(603.898f).Within(0.001f));
                Assert.That(grid.cellSize, Is.EqualTo(new Vector2(167.873f, 175.3194f)));
                Assert.That(grid.spacing, Is.EqualTo(new Vector2(33.47f, 16.51f)));
                float threeColumnWidth = grid.padding.horizontal + 3f * grid.cellSize.x + 2f * grid.spacing.x;
                Assert.That(content.rect.width, Is.EqualTo(threeColumnWidth).Within(1f));
                Assert.That(fitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.Unconstrained));
                Assert.That(fitter.verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                var scroll = content.GetComponentInParent<ScrollRect>(true);
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);

                var nameText = (TextMeshProUGUI)Field(typeof(UIStaffSelectSlot), "_nameText").GetValue(select);
                Assert.That(nameText.enableAutoSizing, Is.True);
                Assert.That(nameText.fontSizeMin, Is.EqualTo(20f));
                Assert.That(nameText.fontSizeMax, Is.EqualTo(34f));
                Assert.That(nameText.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(nameText.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Center));
                Assert.That(nameText.margin.x, Is.EqualTo(20f));
                Assert.That(nameText.margin.z, Is.EqualTo(20f));
                Assert.That(nameText.rectTransform.rect.width - nameText.margin.x - nameText.margin.z,
                    Is.EqualTo(185f).Within(0.001f), "The gold nameplate keeps its safe inner width");
                select.SetText("차이나 걸 토루루");
                Assert.That(nameText.text, Is.EqualTo("차이나 걸 토루루"));
                select.SetText("와부");
                Assert.That(nameText.fontSizeMax, Is.EqualTo(34f), "Short names retain the original maximum size");

                foreach (string quest in new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" })
                {
                    QuestStaffTutorialPolicy.TryGetMapping(quest, out string id, out _);
                    var employee = scope.Staff(id);
                    var role = StaffDataManager.Instance.GetEquipStaffTypeList(employee)[0];
                    Field(typeof(UIStaff), "_currentType").SetValue(detail, role);
                    Field(typeof(UIStaff), "_currentFloorType").SetValue(detail, ERestaurantFloorType.Floor1);
                    Field(typeof(UIStaff), "_currentTypeDataList").SetValue(detail, catalog.Where(item =>
                        StaffDataManager.Instance.GetEquipStaffTypeList(item).Contains(role)).ToList());
                    Assert.That(detail.TrySelectStaff(id), Is.True, quest);
                    Assert.That(detail.SelectedStaff, Is.SameAs(employee), "Shortcut selects its exact employee, not the role's first slot");
                }
                int selectionVersion = detail.SelectionRevision;
                Assert.That(detail.TrySelectStaff("STAFF21"), Is.True);
                Assert.That(detail.SelectionRevision, Is.EqualTo(selectionVersion), "Selecting the same staff is not a new context");
                Assert.That(detail.TrySelectStaff("NOT_REGISTERED"), Is.False);

                StaffData retainedStaff = detail.SelectedStaff;
                int retainedRevision = detail.SelectionRevision;
                EquipStaffType retainedRole = StaffDataManager.Instance.GetEquipStaffTypeList(retainedStaff)[0];
                string[] managerCatalogBefore = StaffDataManager.Instance.GetStaffDataList(retainedRole)
                    .Select(data => data.Id).ToArray();
                var floorSource = StaffDataManager.Instance.GetStaffDataList(
                    retainedRole, ERestaurantFloorType.Floor1);
                var expectedRefreshed = (List<StaffData>)createDisplayDataList.Invoke(
                    null, new object[] { floorSource });
                detail.gameObject.SetActive(false);
                var refreshDisplay = typeof(UIStaff).GetMethod(
                    "SetStaffDataOptimized", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(refreshDisplay, Is.Not.Null);
                refreshDisplay.Invoke(detail, new object[] { retainedRole });
                Assert.That(detail.SelectedStaff, Is.SameAs(retainedStaff),
                    "Refreshing the same role keeps the current selected employee");
                Assert.That(detail.SelectionRevision, Is.EqualTo(retainedRevision),
                    "A retained selection does not create a new selection context");
                var refreshed = (List<StaffData>)Field(typeof(UIStaff), "_currentTypeDataList").GetValue(detail);
                Assert.That(refreshed.Select(data => data.Id),
                    Is.EqualTo(expectedRefreshed.Select(data => data.Id)));
                Assert.That(StaffDataManager.Instance.GetStaffDataList(retainedRole).Select(data => data.Id),
                    Is.EqualTo(managerCatalogBefore), "UI refresh must not reorder the manager catalog");
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
            finally
            {
                roleCacheField.SetValue(null, oldRoleCache);
                if (preview != null) typeof(UIStaffPreview).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(preview, null);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                Field(typeof(UserInfo), "_stageInfos", true).SetValue(null, oldStages);
                Field(typeof(UserInfo), "_currentStage", true).SetValue(null, oldStage);
                Field(typeof(GameManager), "_instance", true).SetValue(null, oldManager);
                UnityEngine.Object.DestroyImmediate(gameHost);
            }
        }
    }
}

public sealed class StaffShopNavigationOrderTests
{
    [Test]
    public void Shortcut_ClosesOnlyQuestThenCompletesNativeShopShowBeforeOpeningDetail()
    {
        var root = new GameObject("Inactive native shortcut order test"); root.SetActive(false);
        try
        {
            var nav = root.AddComponent<MobileUINavigation>();
            var canvas = root.AddComponent<UIMainCanvas>();
            var questHost = new GameObject("Quest"); questHost.transform.SetParent(root.transform);
            var quest = questHost.AddComponent<StaffShopTestView>();
            var shopHost = new GameObject("Shop"); shopHost.transform.SetParent(root.transform);
            var shop = shopHost.AddComponent<StaffShopTestAdmin>();
            var group = shopHost.AddComponent<CanvasGroup>(); group.alpha = 1f; group.interactable = group.blocksRaycasts = true;
            var gate = new GameObject("Native shop gate"); gate.transform.SetParent(shopHost.transform); gate.SetActive(false);
            Set(shop, typeof(UIRestaurantAdmin), "_canvasGroup", group);
            Set(canvas, typeof(UIMainCanvas), "_uiNav", nav);
            Set(canvas, typeof(UIMainCanvas), "_uiAdmin", shop);
            Set(nav, typeof(MobileUINavigation), "_viewDic", new Dictionary<string, MobileUIView>
                { ["UIMainChallenge"] = quest, ["RestaurantAdminUI"] = shop });
            Set(nav, typeof(MobileUINavigation), "_activeViewList", new List<MobileUIView> { quest });
            quest.VisibleState = VisibleState.Appeared;
            shop.VisibleState = VisibleState.Disappeared;
            // No Unity Update/SDK initialization is needed to step the product navigation coroutine.
            int opened = 0;
            var method = typeof(UIMainCanvas).GetMethod("OpenShopShortcutRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            var routine = (IEnumerator)method.Invoke(canvas, new object[] { (Action)(() => opened++) });
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(quest.Hides, Is.EqualTo(1));
            Assert.That(shop.Shows, Is.Zero);
            Assert.That(opened, Is.Zero);
            quest.VisibleState = VisibleState.Disappeared;
            root.SetActive(true);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(shop.Shows, Is.EqualTo(1));
            Assert.That(nav.FirstView, Is.SameAs(shop));
            Assert.That(opened, Is.Zero, "Do not open a child while the shop background is still appearing");
            shop.VisibleState = VisibleState.Appeared;
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(nav.CheckActiveView("UIMainChallenge"), Is.False);
            Assert.That(typeof(UIMainCanvas).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null,
                "No polling path may automatically open a quest machine after tutorial/login");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Set(object target, Type type, string name, object value)
        => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}

public sealed class StaffShopTestView : MobileUIView
{
    public int Hides;
    public override void Init() { }
    public override void Show() { VisibleState = VisibleState.Appeared; }
    public override void Hide() { Hides++; VisibleState = VisibleState.Disappearing; }
}

public sealed class StaffShopTestAdmin : UIRestaurantAdmin
{
    public int Shows;
    public override void Init() { }
    public override void Show() { Shows++; gameObject.SetActive(true); VisibleState = VisibleState.Appearing; }
    public override void Hide() { VisibleState = VisibleState.Disappeared; }
}
#endif
