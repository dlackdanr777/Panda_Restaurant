#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Reuses the existing detached account fixtures and the original Stage1 UI.
public partial class StaffStageMigrationCollectionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void QuestStaffShell_FirstFreeEntryHidesAuthoredCatalogPlaceholderWithoutEnableCallback(bool listInitiallyInactive)
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestShellFixture("MainReward01", out var state, out var wallet);
            var account = fixture.Manager.StaffRuntime.Snapshot;
            int diamonds = wallet.DiamondValue;
            using (var ui = new QuestShellPreview(listInitiallyInactive: listInitiallyInactive))
            {
                Assert.That(ui.View.gameObject.activeInHierarchy, Is.False,
                    "Quest preparation happens before the inactive native view is shown");
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.True,
                    "Reproduce the actual Stage1 authored preview, not an already-cleaned test card");
                Assert.That(ui.CatalogCard.transform.IsChildOf(ui.Catalog.transform), Is.False,
                    "The detail preview is a sibling, so hiding the catalog alone cannot hide it");
                Assert.That(ShellRead<TextMeshProUGUI>(ui.CatalogCard, "_nameText").text, Is.EqualTo("삼색이"));
                Assert.That(AssetDatabase.GetAssetPath(ShellRead<Image>(ui.CatalogCard, "_skinImage").sprite),
                    Is.EqualTo("Assets/Sprites/Customer/Customer01_파니.PNG"));
                Assert.That(ShellRead<Image>(ui.CatalogCard, "_star5Frame").gameObject.activeSelf, Is.True);
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False,
                    "An unopened or already-disabled catalog must close its authored card explicitly");
                ui.AssertQuestShell();
                ui.View.SetActiveUIComponents(false);
                ui.View.SetActiveUIComponents(true);
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False);
                Assert.That(ui.View.PrepareItemMachine(), Is.True);
                ui.AssertRestored();
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False,
                    "Restoring normal machines must not resurrect the editor placeholder");
            }
            Assert.That(fixture.Manager.CurrentQuestStaffGrant, Is.Null);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(account));
            Assert.That(wallet.DiamondValue, Is.EqualTo(diamonds));
            Assert.That(state.Notifications, Is.Zero);
            Assert.That(state.Mask, Is.Zero);
        }
    }

    [Test]
    public void QuestStaffShell_PreviousCatalogInspectionIsClosedAndNotRestoredByQuestExit()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestShellFixture("MainReward01", out var state, out _);
            using (var ui = new QuestShellPreview(listInitiallyInactive: true))
            {
                // A real prior catalog preview uses SetData but performs no draw.
                var previous = new GachaItemData("GOTCHA01", "Previously inspected item", "Existing preview", 0, 0,
                    3, default, 0f, 0f, 1, null);
                ui.CatalogCard.SetData(previous);
                ui.CatalogCard.gameObject.SetActive(true);
                Assert.That(ShellRead<TextMeshProUGUI>(ui.CatalogCard, "_nameText").text, Is.EqualTo(previous.Name));
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False);
                Assert.That(ui.View.PrepareStaffMachine(), Is.True);
                ui.AssertRestored();
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False,
                    "A prior preview is not part of the shell visibility snapshot");
                Assert.That(ShellRead<TextMeshProUGUI>(ui.CatalogCard, "_nameText").text, Is.EqualTo(previous.Name),
                    "Hide is display-only and does not substitute or modify product data");
            }
            Assert.That(fixture.Manager.CurrentQuestStaffGrant, Is.Null);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(state.Notifications, Is.Zero);
        }
    }

    [Test]
    public void QuestStaffShell_CatalogInitializationAndVisibilityLifetimesCloseOnlyItsPreview()
    {
        using (var ui = new QuestShellPreview())
        {
            var staffCard = ShellRead<UIGachaCard>(ui.Staff, "_gachaCard");
            bool staffCardWasActive = staffCard.gameObject.activeSelf;
            Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.True);
            ui.Catalog.AddInit();
            Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False);
            foreach (string callback in new[] { "OnEnable", "OnDisable" })
            {
                ui.CatalogCard.gameObject.SetActive(true);
                typeof(UIGachaSlotList).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui.Catalog, null);
                Assert.That(ui.CatalogCard.gameObject.activeSelf, Is.False, callback);
            }
            ui.Catalog.HidePreviewCard();
            Assert.That(staffCard.gameObject.activeSelf, Is.EqualTo(staffCardWasActive),
                "Catalog preview cleanup must not acknowledge or hide the distinct confirmed staff-result card");
        }
    }

    [TestCase("MainReward01")]
    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void QuestStaffShell_FourFreeOffersHideOnlySurroundingsAndRestoreOriginalStates(string quest)
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestShellFixture(quest, out var state, out var wallet);
            var account = fixture.Manager.StaffRuntime.Snapshot;
            int diamonds = wallet.DiamondValue;
            using (var ui = new QuestShellPreview())
            {
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out string error), Is.True, error);
                var parent = (RectTransform)ShellRead<RectTransform>(ui.View, "_machineParent");
                Vector2 originalPosition = parent.anchoredPosition;
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                ui.AssertQuestShell();
                Assert.That(ShellRead<GachaMachineParent>(ui.View, "_requestedInitialMachine"), Is.SameAs(ui.Staff));
                Assert.That(parent.anchoredPosition, Is.EqualTo(originalPosition), "Preparation does not invent a second machine layout");
                // Reentry and the existing result animation's reset calls cannot
                // re-expose the ordinary item machine/catalog behind a free gift.
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                ui.View.SetActiveUIComponents(false);
                ui.View.SetActiveUIComponents(true);
                ui.View.SetActiveGachaMachine(true);
                ui.View.SetStartGacha(true);
                ui.View.SetStartGacha(false);
                ui.AssertQuestShell();
                Assert.That(ui.View.PrepareItemMachine(), Is.True);
                ui.AssertRestored();
                Assert.That(ShellRead<GachaMachineParent>(ui.View, "_requestedInitialMachine"), Is.SameAs(ui.Item));
            }
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(wallet.DiamondValue, Is.EqualTo(diamonds));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(account));
            Assert.That(state.Notifications, Is.Zero);
            Assert.That(state.Mask, Is.Zero);
        }
    }

    [Test]
    public void QuestStaffShell_DisableRestoresMixedAuthoredStatesAndNextGeneralEntryIsUnmasked()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestShellFixture("MainReward01", out _, out _);
            using (var ui = new QuestShellPreview(mixedStates: true))
            {
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                ui.AssertQuestShell();
                typeof(UIGacha).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.View, null);
                ui.AssertRestored();
                Assert.That(ShellRead<bool>(ui.View, "_questStaffEntry"), Is.False);
                Assert.That(ui.View.PrepareStaffMachine(), Is.True);
                ui.AssertRestored();
                Assert.That(ShellRead<bool>(ui.View, "_questPresentationCaptured"), Is.False);
            }
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void QuestStaffShell_ItemTutorialAndRejectedEntriesDoNotHideGeneralGacha()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestShellFixture("MainReward12", out _, out _);
            using (var ui = new QuestShellPreview())
            {
                Assert.That(ui.View.PrepareQuestStaffMachine(null), Is.False);
                Assert.That(ui.View.PrepareQuestStaffMachine(fixture.Manager), Is.False);
                ui.AssertRestored();
                Assert.That(ui.View.PrepareItemMachine(), Is.True);
                ui.View.SetActiveUIComponents(true);
                ui.AssertRestored();
            }
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void QuestStaffShell_NativeShopEntryShowsCentralFreeInputAndExitRestoresGeneralMachine()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture("MainReward01", out var state, out var wallet);
            fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
                fixture.Manager.StaffPurchaseWallet, _ => throw new AssertionException("No draw while displaying"),
                _ => throw new AssertionException("No paid record while displaying"));
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            try
            {
                Authenticate(fixture, fixture.Account);
                RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out var offer, out _), Is.True);
                using (var ui = new QuestShopRecoveryView(fixture.Manager, offer))
                {
                    var item = ui.View.GetComponentInChildren<UIItemGacha>(true);
                    bool initialItemActive = item.gameObject.activeSelf;
                    ui.ClickDetailEntry();
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.View));
                    Assert.That(item.gameObject.activeSelf, Is.False);
                    var claim = ShellRead<Button>(ui.Staff, "_questButton");
                    Assert.That(claim.gameObject.activeInHierarchy && claim.interactable, Is.True);
                    Assert.That(ui.Staff.gameObject.activeInHierarchy, Is.True);
                    Assert.That(ShellRead<RectTransform>(ui.View, "_machineParent").anchoredPosition.x,
                        Is.EqualTo(-1130f).Within(.01f), "Use the existing central staff slot");
                    Assert.That(ShellRead<Button>(ui.Staff, "_singleButton").gameObject.activeSelf, Is.False);
                    Assert.That(ShellRead<Button>(ui.Staff, "_tenButton").gameObject.activeSelf, Is.False);
                    ui.ClickExit();
                    Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff), "Keep the actual return-to-staff-shop flow");
                    Assert.That(item.gameObject.activeSelf, Is.EqualTo(initialItemActive));
                    Assert.That(claim.gameObject.activeSelf, Is.False);
                    Assert.That(ui.View.PrepareItemMachine(), Is.True);
                    ui.Navigation.Push("UIGacha");
                    typeof(QuestShopRecoveryView).GetMethod("CompleteTweens", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                    Assert.That(ui.View.EditorOfflineCurrentMachine, Is.SameAs(item));
                    Assert.That(item.gameObject.activeInHierarchy, Is.True);
                    Assert.That(ShellRead<Button>(ui.View, "_leftButton").gameObject.activeSelf, Is.True);
                    Assert.That(ShellRead<Button>(ui.View, "_rightButton").gameObject.activeSelf, Is.True);
                }
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                Assert.That(state.Mask, Is.Zero);
            }
            finally { fixture.Manager.DestroyEditorOfflineOwner(); }
        }
    }

    private Fixture CreateQuestShellFixture(string quest, out MemoryQuestStaffState state, out MemoryStaffWallet wallet)
    {
        // UI admission checks UnityEngine.Object lifetime. The calculation-only
        // FormatterServices fixture intentionally has no native Unity object;
        // reuse the existing detached MonoBehaviour fixture for this UI boundary.
        var fixture = CreateNativePaidButtonFixture(out wallet);
        RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
        state = new MemoryQuestStaffState { Quest = quest };
        Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
        fixture.Manager.StaffPurchaseDraw = _ => throw new AssertionException("Presentation cannot draw");
        fixture.Manager.StaffPurchaseCommittedEffects = _ => throw new AssertionException("Presentation cannot commit a purchase");
        return fixture;
    }

    private sealed class QuestShellPreview : IDisposable
    {
        private readonly Scene _scene;
        private readonly Dictionary<GameObject, bool> _active = new Dictionary<GameObject, bool>();
        private readonly bool _scrollEnabled;
        private readonly GameObject _exit;
        private readonly Vector3 _staffScale;
        private readonly Vector3 _staffPosition;
        private readonly bool _staffActive;
        public readonly UIGacha View;
        public readonly UIStaffGacha Staff;
        public readonly UIItemGacha Item;
        public readonly UIGachaSlotList Catalog;
        public readonly UIGachaCard CatalogCard;

        public QuestShellPreview(bool mixedStates = false, bool listInitiallyInactive = false)
        {
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            View = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIGacha>(true)).Single();
            Staff = View.GetComponentInChildren<UIStaffGacha>(true);
            Item = View.GetComponentInChildren<UIItemGacha>(true);
            Catalog = ShellRead<UIGachaSlotList>(View, "_gachaItemList");
            CatalogCard = ShellRead<UIGachaCard>(Catalog, "_card");
            if (listInitiallyInactive) Catalog.gameObject.SetActive(false);
            Field(typeof(GachaMachineParent), "_uiGacha").SetValue(Staff, View);
            _exit = View.transform.Find("Anime UI/UI Components/Exit Button").gameObject;
            _staffScale = Staff.transform.localScale; _staffPosition = Staff.transform.localPosition;
            _staffActive = Staff.gameObject.activeSelf;
            var targets = new[] { Item.gameObject, Catalog.gameObject,
                ShellRead<Button>(View, "_leftButton").gameObject, ShellRead<Button>(View, "_rightButton").gameObject };
            for (int i = 0; i < targets.Length; i++)
            {
                if (mixedStates) targets[i].SetActive(i % 2 == 0);
                _active.Add(targets[i], targets[i].activeSelf);
            }
            var scroll = ShellRead<ScrollRect>(View, "_scrollRect");
            if (mixedStates) scroll.enabled = false;
            _scrollEnabled = scroll.enabled;
        }

        public void AssertQuestShell()
        {
            foreach (var entry in _active) Assert.That(entry.Key.activeSelf, Is.False, entry.Key.name);
            Assert.That(ShellRead<ScrollRect>(View, "_scrollRect").enabled, Is.False);
            Assert.That(_exit.activeSelf, Is.True, "Quest masking must preserve the existing exit");
            Assert.That(Staff.gameObject.activeSelf, Is.EqualTo(_staffActive),
                "Preparing an unopened view must preserve the selected machine's authored visibility; native Show activates it");
            Assert.That(Staff.transform.localPosition, Is.EqualTo(_staffPosition));
            Assert.That(Staff.transform.localScale, Is.EqualTo(_staffScale));
        }

        public void AssertRestored()
        {
            foreach (var entry in _active) Assert.That(entry.Key.activeSelf, Is.EqualTo(entry.Value), entry.Key.name);
            Assert.That(ShellRead<ScrollRect>(View, "_scrollRect").enabled, Is.EqualTo(_scrollEnabled));
            Assert.That(_exit.activeSelf, Is.True);
        }

        public void Dispose()
        {
            if (View != null) View.PrepareItemMachine();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }
    }

    private static T ShellRead<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
}
#endif
