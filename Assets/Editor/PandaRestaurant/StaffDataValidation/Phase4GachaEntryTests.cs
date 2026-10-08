#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using Muks.Tween;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class StaffGachaOfflineSessionTests
{
    [Test]
    public void Phase4Gacha_StaffShopFirstEntryCompletesPaidControlsBeforeAnyUpdateOrArrow()
    {
        using (var scope = new Phase4EntryScope())
        {
            var ui = scope.Ui;
            StaffGachaOfflineViewFactory.NavigationEntry(ui.View).onClick.Invoke();
            Assert.That(ui.View.VisibleState, Is.EqualTo(VisibleState.Appearing));
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
            Assert.That(ui.Staff.SingleButton.IsInteractable(), Is.False);
            Assert.That(ui.Staff.TenButton.IsInteractable(), Is.False);
            ui.Staff.SingleButton.onClick.Invoke();
            ui.Staff.TenButton.onClick.Invoke();
            Assert.That(ui.View.IsChoosingPayment, Is.False, "Opening animation cannot admit a purchase");
            Phase4FinishTweens(ui.Root);
            Phase4AssertPaidControls(ui);
            ui.View.Show(); // A repeated open must not reset the selected machine to Item.
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
            Assert.That(scope.Store.SaveAttempts, Is.Zero);
            ui.Staff.SingleButton.onClick.Invoke();
            Assert.That(ui.View.IsChoosingPayment, Is.True);
            Assert.That(RuntimeReference<TMPro.TextMeshProUGUI>(ui.View.CollectionPayment, "_product").text, Does.Contain("스텝 1회"));
            ui.View.CollectionPayment.Close();
            Assert.That(scope.Store.SaveAttempts, Is.Zero);
        }
    }

    [Test]
    public void Phase4Gacha_StaffShopReentryAndBothArrowsPreserveOnePaymentListener()
    {
        using (var scope = new Phase4EntryScope())
        {
            var ui = scope.Ui;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                ui.ClickEntry();
                Phase4AssertPaidControls(ui);
                ui.ClickArrow("_leftButton");
                Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));
                Assert.That(ui.Staff.SingleButton.gameObject.activeSelf, Is.False);
                ui.ClickArrow("_rightButton");
                Phase4AssertPaidControls(ui);
                ui.Staff.TenButton.onClick.Invoke();
                Assert.That(ui.View.IsChoosingPayment, Is.True);
                Assert.That(RuntimeReference<TMPro.TextMeshProUGUI>(ui.View.CollectionPayment, "_product").text, Does.Contain("스텝 10+1회"));
                Assert.That(ui.Root.GetComponentsInChildren<GachaPaymentChoiceView>(true).Length, Is.EqualTo(1));
                ui.View.CollectionPayment.Close();
                ui.ClickExit();
                Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff));
            }
            Assert.That(scope.Store.SaveAttempts, Is.Zero);
        }
    }

    [Test]
    public void Phase4Gacha_DefaultEntryStillSelectsItemAndCloseDuringOpeningCannotReviveStaff()
    {
        using (var scope = new Phase4EntryScope())
        {
            var ui = scope.Ui;
            StaffGachaOfflineViewFactory.NavigationEntry(ui.View).onClick.Invoke();
            ui.View.Hide();
            Phase4FinishTweens(ui.Root);
            Assert.That(ui.View.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(ui.View.gameObject.activeSelf, Is.False);
            ui.View.Show(); // No requested override: the ordinary entry is Item.
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));
            Phase4FinishTweens(ui.Root);
            Assert.That(ui.View.VisibleState, Is.EqualTo(VisibleState.Appeared));
            Assert.That(ui.Item.SingleButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(ui.Staff.SingleButton.gameObject.activeSelf, Is.False);
            ui.Item.SingleButton.onClick.Invoke();
            Assert.That(ui.View.IsChoosingPayment, Is.True);
            Assert.That(RuntimeReference<TMPro.TextMeshProUGUI>(ui.View.CollectionPayment, "_product").text, Does.Contain("아이템 1회"));
            Assert.That(scope.Store.SaveAttempts, Is.Zero);
        }
    }

    [Test]
    public void Phase4Gacha_FastShopEntryInterruptsActualPrewarmIteratorWithoutLosingControls()
    {
        using (var scope = new Phase4EntryScope())
        {
            var ui = scope.Ui;
            typeof(UIGacha).GetField("_requestedInitialMachine", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(ui.View, null);
            var prewarm = (IEnumerator)typeof(UIGacha).GetMethod("PrewarmFirstScreen",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.View, null);
            Assert.That(prewarm.MoveNext(), Is.True); // Initial loading yield.
            Assert.That(prewarm.MoveNext(), Is.True); // Item is now warming invisibly.
            Assert.That(RuntimeReference<bool>(ui.View, "_isPrewarming"), Is.True);
            StaffGachaOfflineViewFactory.NavigationEntry(ui.View).onClick.Invoke();
            Assert.That(RuntimeReference<bool>(ui.View, "_isPrewarming"), Is.False);
            Phase4FinishTweens(ui.Root);
            Phase4AssertPaidControls(ui);
            // Production stops the hosted coroutine; even a queued continuation must
            // observe Appeared and cannot deactivate the user's already-open screen.
            Assert.That(prewarm.MoveNext(), Is.False);
            Phase4AssertPaidControls(ui);
            Assert.That(ui.Navigation.Count, Is.EqualTo(3));
            Assert.That(scope.Store.SaveAttempts, Is.Zero);
        }
    }

    [Test]
    public void Phase4Gacha_ConfirmedResultCloseAndShopReentryRestorePaidControlsWithoutReplay()
    {
        using (var scope = new Phase4EntryScope())
        {
            var ui = scope.Ui;
            ui.ClickEntry();
            Assert.That(scope.Economy.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle,
                null, out string error), Is.True, error);
            Assert.That(ui.Display.TryShowCompleted(false, out error), Is.True, error);
            Assert.That(ui.Display.EditorIsResultVisible, Is.True);
            ui.Display.AcknowledgeResult();
            ui.ClickExit();
            ui.ClickEntry();
            ui.Display.Tick();
            Phase4AssertPaidControls(ui);
            Assert.That(ui.Display.EditorIsResultVisible, Is.False);
            Assert.That(ui.View.IsStartGacha, Is.False);
            Assert.That(scope.Store.CommitCount, Is.EqualTo(1));
            Assert.That(scope.Economy.Snapshot.Account.GachaEconomy.StaffCounter, Is.EqualTo(1));
        }
    }

    private static void Phase4AssertPaidControls(OfflineNavigationView ui)
    {
        Assert.That(ui.View.VisibleState, Is.EqualTo(VisibleState.Appeared));
        Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
        foreach (var button in new[] { ui.Staff.SingleButton, ui.Staff.TenButton })
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.IsInteractable(), Is.True, button.name + " must be ready in the tween completion callback");
            Assert.That(button.transform.localScale.x, Is.GreaterThan(.1f));
            Assert.That(button.transform.localScale.y, Is.GreaterThan(.1f));
            var group = button.GetComponent<CanvasGroup>();
            Assert.That(group == null || group.alpha > .9f, Is.True);
        }
    }

    private static void Phase4FinishTweens(GameObject root)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (TweenData tween in root.GetComponentsInChildren<TweenData>(true))
        {
            if (!tween.enabled) continue;
            if (typeof(TweenData).GetField("_percentHandler", flags).GetValue(tween) == null)
                typeof(TweenData).GetMethod("Awake", flags).Invoke(tween, null);
            var update = tween.GetType().GetMethod("Update", flags);
            update.Invoke(tween, null);
            typeof(TweenData).GetField("ElapsedDuration", flags).SetValue(tween, float.MaxValue);
            update.Invoke(tween, null);
        }
    }

    private sealed class Phase4EntryScope : IDisposable
    {
        internal readonly StaffGachaOfflineSession Session;
        internal readonly OfflineNavigationView Ui;
        internal readonly GachaEconomyMemoryStore Store;
        internal readonly GachaEconomyService Economy;
        private readonly GachaStaffData _staffData;
        private readonly GachaItemData _itemData;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        private readonly UnityEngine.Random.State _random = UnityEngine.Random.state;

        internal Phase4EntryScope()
        {
            try
            {
            Session = new StaffGachaOfflineSession(Catalog());
            Ui = new OfflineNavigationView(Session.Owner, false, includeItemPresentation: true);
            _staffData = GachaStaffData.Create(Resources.Load<StaffData>("StaffData/STAFF23"));
            _itemData = ScriptableObject.CreateInstance<GachaItemData>();
            EnhancementFairyStage1Host.Set(_itemData, "_id", "PHASE4-ENTRY-ITEM");
            EnhancementFairyStage1Host.Set(_itemData, "_rank", Rank.Normal2);
            EnhancementFairyStage1Host.Set(_itemData, "_upgradeType", UpgradeType.UPGRADE01);
            Store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("phase4-entry",
                new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                    Array.Empty<StaffAccountStaffRecord>(), 1000, new GachaEconomySaveData(20, 20, 0, 0)), 1000));
            Economy = new GachaEconomyService(Store, new GachaData[] { _staffData, _itemData },
                Resources.Load<GachaEconomySettings>("GachaEconomySettings"), (machine, pool, guaranteed) => pool[0]);
            Ui.Staff.ConfigureCollectionOffline(Ui.View, Economy);
            // OfflineNavigationView already configured the item's presentation once.
            // Bind only the collection service/listeners; repeating presentation setup
            // is rightly rejected and would duplicate slots and the audio owner.
            Ui.Item.BindCollectionEconomy(Ui.View, Economy);
            Ui.Item.SingleButton.onClick.AddListener(Ui.Item.OnSingleGachaButtonClicked);
            Ui.Item.TenButton.onClick.AddListener(Ui.Item.OnTenGachaButtonClicked);
            Ui.View.BindCollectionEconomy(Economy);
            _fonts.BindBeforeActivation(Ui.Root);
            InvokePrivate(Ui.Root.GetComponent<UIMainCanvas>(), "Awake");
            InvokePrivate(Ui.Navigation, "Start");
            Ui.Root.SetActive(true);
            StaffGachaOfflineViewFactory.BeginNavigation(Ui.View);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            Ui?.Dispose();
            _fonts.Dispose();
            Session?.Dispose();
            if (_staffData != null) Object.DestroyImmediate(_staffData);
            if (_itemData != null) Object.DestroyImmediate(_itemData);
            UnityEngine.Random.state = _random;
        }
    }
}

public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void Phase4Gacha_OpeningCompletionNeverExposesPaidControlsInFreeQuestEntry()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Field(typeof(Muks.BackEnd.BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager,
                new MemoryQuestStaffState { Quest = "MainReward01" });
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                typeof(UIStaffGacha).GetMethod("PrepareQuestEntry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui.Staff, new object[] { fixture.Manager, "MainReward01", null });
                ui.Staff.Show();
                ui.View.VisibleState = VisibleState.Appeared;
                typeof(UIStaffGacha).GetMethod("CompleteOpening", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui.Staff, null);
                Assert.That(ui.Single.gameObject.activeSelf || ui.Multi.gameObject.activeSelf, Is.False);
                Assert.That(PaidField<Button>(ui.Staff, "_questButton").gameObject.activeInHierarchy, Is.True);
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
            }
        }
    }
}
#endif
