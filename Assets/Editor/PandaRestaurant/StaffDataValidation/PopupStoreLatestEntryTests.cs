#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual authored latest tutorial and Navigation with no account authority or SDK execution.</summary>
public sealed class PopupStoreLatestEntryTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string TutorialPath = "Assets/Resources/UI/PopupStore/Account/PopupStorePhasePreviewView.prefab";
    private Func<UIPopupStore, IPopupStoreAccountPhaseBridge> _factory;
    private Func<bool> _pending;
    private GachaCollectionPreviewSceneWitness _workspace;
    private int _factoryCalls;

    [SetUp] public void PreserveOwners()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _factory = PopupStoreAccountPhaseBootstrap.BridgeFactory;
        _pending = PopupStoreStartController.EditorPhasePreviewEntryPending;
        _factoryCalls = 0;
        PopupStoreAccountPhaseBootstrap.BridgeFactory = _ => { _factoryCalls++; return null; };
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => false;
        Assert.That(Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource).EnableAccountMode, Is.False);
    }
    [TearDown] public void RestoreOwners()
    {
        PopupStoreAccountPhaseBootstrap.BridgeFactory = _factory;
        PopupStoreStartController.EditorPhasePreviewEntryPending = _pending;
        Assert.That(_factoryCalls, Is.Zero, "The authored OFF setting must not even construct an account bridge.");
        _workspace.AssertUnchanged("latest popup entry isolated navigation tests");
    }

    [Test]
    public void AuthoredEntryUsesApprovedRuntimeTutorialAndStartsWithoutTheLegacyOverlay()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var controller = root.GetComponent<PopupStoreStartController>();
        Assert.That(Read<GameObject>(controller, "_unavailableTutorialPrefab"), Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(TutorialPath)));
        Assert.That(Read<string>(controller, "_unavailableTicketText"), Is.Not.Null.And.Not.Empty);
        Assert.That(Read<GameObject>(root.GetComponent<PopupStoreStartPanel>(), "_overlay").activeSelf, Is.False);
        Assert.That(Read<string>(root.GetComponent<PopupStoreBusinessClock>(), "_notStartedText"), Is.EqualTo("—"));
        foreach (string dependency in AssetDatabase.GetDependencies(TutorialPath, true))
            Assert.That(dependency.Replace('\\', '/').Contains("/Editor/"), Is.False, dependency);
    }

    [TestCase(true)] [TestCase(false)]
    public void EitherEnableOrderKeepsTheOldOverlayHiddenBeforeAndAfterShow(bool panelFirst)
    {
        using (var h = new Harness(open: false))
        {
            Assert.That(h.LegacyOverlay.activeSelf, Is.False);
            h.Root.SetActive(true);
            Invoke(panelFirst ? (object)h.Panel : h.Controller, "OnEnable");
            Assert.That(h.LegacyOverlay.activeSelf, Is.False);
            Invoke(panelFirst ? (object)h.Controller : h.Panel, "OnEnable");
            Assert.That(h.LegacyOverlay.activeSelf, Is.False);
            h.Root.SetActive(false);
            h.Open();
            h.AssertLatestUnavailable();
            h.AssertUntouched();
        }
    }

    [Test]
    public void AccountOffEntryDisplaysFivePagesAndUnknownTicketsWithoutCreatingAnyPhaseOrHost()
    {
        using (var h = new Harness())
        {
            h.AssertLatestUnavailable();
            Assert.That(h.Tutorial.PageCount, Is.EqualTo(5));
            Assert.That(h.Root.GetComponent<PopupStorePhasePreview>(), Is.Null);
            Assert.That(h.Controller.Host, Is.Null);
            Assert.That(h.Panel.Remaining, Is.EqualTo(-1)); Assert.That(h.Panel.Limit, Is.EqualTo(-1));
            Assert.That(Read<TMP_Text>(h.Clock, "_timeText").text, Is.EqualTo("—"));
            for (int page = 0; page < 5; page++)
            {
                Assert.That(h.Tutorial.CurrentPage, Is.EqualTo(page));
                Assert.That(h.Tutorial.TryPlay(), Is.False);
                if (page < 4) Assert.That(h.Tutorial.TryNext(), Is.True);
            }
            Assert.That(h.PlayButton.interactable, Is.False);
            Assert.That(h.Ticket.text, Is.EqualTo(Read<string>(h.Controller, "_unavailableTicketText")));
            Assert.That(h.Ticket.text, Does.Not.Contain("0/1").And.Not.Contain("1/1"));
            h.AssertUntouched();
        }
    }

    [Test]
    public void DisabledLatestEntryRejectsRealStartPackingCounterAndTimeApisEvenAfterRepeatedClicks()
    {
        using (var h = new Harness())
        {
            while (h.Tutorial.TryNext()) { }
            for (int i = 0; i < 4; i++)
            {
                h.PlayButton.onClick.Invoke();
                Read<Button>(h.Panel, "_startButton").onClick.Invoke();
                Assert.That(h.Tutorial.TryPlay(), Is.False);
                Assert.That(h.Controller.RequestStart(), Is.False);
                Assert.That(h.Controller.RefreshAccess(), Is.False);
                Assert.That(h.Clock.TryStartSession(), Is.False);
                Assert.That(h.Clock.AdvanceTime(90), Is.False);
            }
            var counter = h.Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
            var catalog = Read<PopupStoreMaterialCatalog>(counter, "_catalog");
            Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var items, out _), Is.True);
            Assert.That(counter.TryAdd(items[0]), Is.False);
            Assert.That(h.Root.GetComponent<PopupStorePacking>().TryPack().Success, Is.False);
            h.Root.GetComponent<PopupStoreCustomerEntrance>().TickEntrance(100);
            Assert.That(h.Tutorial.PlayRequestCount, Is.Zero);
            h.AssertUntouched();
        }
    }

    [Test]
    public void InspectorUnknownTicketCopyAndApprovedLayoutRemainUntouchedByRepeatedRefresh()
    {
        const string message = "이용권 조회 대기";
        using (var h = new Harness(unavailableText: message))
        {
            while (h.Tutorial.TryNext()) { }
            var ticketPosition = Read<Vector2>(h.Tutorial, "_ticketPosition");
            var playPosition = Read<Vector2>(h.Tutorial, "_playPosition");
            for (int i = 0; i < 5; i++) h.Controller.OnViewShown();
            Assert.That(h.Ticket.text, Is.EqualTo(message));
            Assert.That(Read<Vector2>(h.Tutorial, "_ticketPosition"), Is.EqualTo(ticketPosition));
            Assert.That(Read<Vector2>(h.Tutorial, "_playPosition"), Is.EqualTo(playPosition));
            Assert.That(h.Root.GetComponentsInChildren<PopupStorePhasePreviewView>(true).Length, Is.EqualTo(1));
            Assert.That(h.Tutorial.CurrentPage, Is.EqualTo(4));
            h.AssertLatestUnavailable(); h.AssertUntouched();
        }
    }

    [Test]
    public void RepeatedCloseReentryAndBindingKeepOneTutorialOneNavigationActionAndNoStaleInput()
    {
        using (var h = new Harness())
        {
            var same = h.Tutorial;
            for (int visit = 1; visit <= 3; visit++)
            {
                for (int n = 0; n < 4; n++) h.Controller.OnViewShown();
                int page = same.CurrentPage;
                Read<Button>(same, "_nextButton").onClick.Invoke();
                Assert.That(same.CurrentPage, Is.EqualTo(page + 1), "Repeated binding must not multiply the next-page listener.");
                Read<Button>(same, "_previousButton").onClick.Invoke();
                Assert.That(same.CurrentPage, Is.EqualTo(page));
                Read<Button>(same, "_closeButton").onClick.Invoke();
                Read<Button>(same, "_closeButton").onClick.Invoke();
                Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
                Assert.That(same.CloseRequestCount, Is.EqualTo(visit));
                Assert.That(h.Root.activeSelf, Is.False);
                Assert.That(Read<CanvasGroup>(same, "_inputGroup").blocksRaycasts, Is.False);
                h.Open();
                Assert.That(h.Tutorial, Is.SameAs(same));
                Assert.That(h.Root.GetComponentsInChildren<PopupStorePhasePreviewView>(true).Length, Is.EqualTo(1));
                Assert.That(Read<CanvasGroup>(same, "_inputGroup").blocksRaycasts, Is.True);
                Assert.That(h.Root.GetComponent<CanvasGroup>().interactable, Is.True, "The hidden legacy panel must release its input lock.");
                h.AssertLatestUnavailable(); h.AssertUntouched();
            }
        }
    }

    [Test]
    public void MissingLatestPrefabDoesNotFallBackToLegacyAdmissionOrLeaveAnUnclosableView()
    {
        using (var h = new Harness(open: false))
        {
            Write(h.Controller, "_unavailableTutorialPrefab", null);
            h.Open();
            Assert.That(h.Panel.IsVisible, Is.False); Assert.That(h.LegacyOverlay.activeSelf, Is.False);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.View.TryClose(), Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            h.AssertUntouched();
        }
    }

    [Test]
    public void NonReviewAdmissionWithoutItsAccountPhaseCannotUseTheNinetySecondFallback()
    {
        using (var h = new Harness())
        {
            var store = new PopupStoreBusinessStartFakeStore(() => PopupStoreApprovedTestSession.AuthorityTime,
                new PopupStoreBusinessAccessPolicy("latest-entry-fake-account", 1));
            store.RegisterAccount("latest-entry-contract-only");
            var session = store.OpenSession("latest-entry-contract-only");
            var repository = new PopupStoreBusinessStartFakeRepository(store);
            using (var host = new PopupStoreAdmissionHost(repository, repository, () => session, editorReview: false))
            {
                Assert.That(h.Controller.BindHost(host), Is.True);
                h.AssertLatestUnavailable();
                while (h.Tutorial.TryNext()) { }
                Assert.That(h.Tutorial.TryPlay(), Is.False);
                Assert.That(h.Controller.RequestStart(), Is.False);
                Assert.That(h.Clock.TryStartSession(), Is.False);
                Assert.That(repository.SubmitCalls, Is.Zero);
                Assert.That(host.Transaction, Is.Null); Assert.That(host.LocalStartCount, Is.Zero);
                Assert.That(host.Access.Remaining, Is.EqualTo(1));
                Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
                Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            }
        }
    }

    [Test]
    public void ExplicitNinetySecondReviewCanReplaceOnlyTheUnavailablePresentationAndStillConsumesOnce()
    {
        using (var h = new Harness())
        {
            var standby = h.Tutorial;
            var host = h.BindReview();
            Assert.That(h.Panel.IsVisible, Is.True);
            Assert.That(standby == null || !standby.IsTutorialVisible, Is.True);
            Assert.That(h.Root.GetComponent<PopupStorePhasePreview>(), Is.Null);
            Assert.That(h.Controller.RequestStart(), Is.True, h.Controller.LastError);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.LocalStartCount, Is.EqualTo(1)); Assert.That(host.Access.Remaining, Is.Zero);
            Assert.That(h.Controller.RequestStart(), Is.False);
        }
    }

    [Test]
    public void SavedRecoveryTakesOverTheOriginalCloseControlBeforeUnavailableViewIsReleased()
    {
        using (var h = new Harness()) using (var bridge = new RecoveryBridge())
        {
            Assert.That(h.Controller.BindHost(bridge.AdmissionHost), Is.True);
            var standby = h.Tutorial;
            Assert.That(standby.IsTutorialVisible, Is.True);
            var originalX = Read<Button>(h.View, "_closeButton");
            Assert.That(originalX.gameObject.activeSelf, Is.False);
            var reward = h.CreateRewardView();
            var recovery = h.Root.AddComponent<PopupStoreAccountRecoveryPresentation>();
            var configuration = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
            Assert.That(recovery.Bind(bridge, bridge, reward, configuration.RewardTable), Is.True);
            for (int i = 0; i < 4; i++) h.Controller.OnViewShown();
            Assert.That(originalX.gameObject.activeSelf, Is.False, "Retiring the old tutorial must not restore X over the recovery owner's guard.");
            Assert.That(h.Tutorial, Is.Null);
            Assert.That(standby == null || !standby.IsTutorialVisible, Is.True);
            Assert.That(h.Root.GetComponentsInChildren<PopupStorePhasePreviewView>(false).Any(x => x.IsTutorialVisible), Is.False);
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(recovery.Snapshot, Is.SameAs(bridge.Recovery));
            Assert.That(h.View.TryRequestExit(), Is.False);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(bridge.Repository.SubmitCalls, Is.Zero);
            Assert.That(bridge.AdmissionHost.LocalStartCount, Is.Zero);
            Assert.That(bridge.Prepares + bridge.Claims, Is.Zero);
            Assert.That(reward.BeginCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStorePhasePreview>(), Is.Null);
        }
    }

    [Test]
    public void LatestPhaseOwnerReplacesUnavailableViewWithoutDuplicateTutorialOrBypassingFinalPage()
    {
        using (var h = new Harness())
        {
            var standby = h.Tutorial;
            PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
            var admission = h.BindReview();
            Write(h.Clock, "_notStartedText", "대기값");
            var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
            Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().TryCreateSnapshot(catalog, out var profile, out var error), Is.True, error);
            var phase = h.Root.AddComponent<PopupStorePhasePreview>();
            Assert.That(phase.Initialize(profile, PopupStoreScorePolicySnapshot.Ordinary, out error), Is.True, error);
            var actual = h.CreatePhaseView();
            Assert.That(phase.AttachView(actual, Read<Button>(h.View, "_closeButton")), Is.True);
            var bindPolish = typeof(PopupStoreVerificationRunner).GetMethod("BindPhasePreviewPolish", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bindPolish, Is.Not.Null);
            bindPolish.Invoke(null, new object[] { h.View, actual, phase });
            Assert.That(Read<string>(h.Clock, "_notStartedText"), Is.EqualTo("대기값"));
            Assert.That(Read<TMP_Text>(h.Clock, "_timeText").text, Is.EqualTo("대기값"));
            Invoke(h.Clock, "UsePhaseTimePlaceholder");
            Assert.That(Read<string>(h.Clock, "_notStartedText"), Is.EqualTo("대기값"));
            Assert.That(Read<TMP_Text>(h.Clock, "_timeText").text, Is.EqualTo("대기값"));
            Assert.That(standby == null || !standby.IsTutorialVisible, Is.True);
            Assert.That(h.Root.GetComponentsInChildren<PopupStorePhasePreviewView>(false).Count(x => x.IsTutorialVisible), Is.EqualTo(1));
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(actual.IsTutorialVisible, Is.True);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(actual.TryPlay(), Is.False);
            while (actual.TryNext()) { }
            Assert.That(actual.TryPlay(), Is.True); Assert.That(actual.TryPlay(), Is.False);
            Assert.That(admission.LocalStartCount, Is.EqualTo(1)); Assert.That(admission.Access.Remaining, Is.Zero);
            Assert.That(phase.PresentationCue, Is.EqualTo("Stage"));
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    private sealed class Harness : IDisposable
    {
        internal readonly GameObject Scope, Root;
        internal readonly UIPopupStore View;
        internal readonly MobileUINavigation Navigation;
        internal readonly PopupStoreAdmissionMapView Map;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        private readonly GameObject _tutorialTemplate;
        private PopupStoreAdmissionHost _review;
        internal PopupStoreStartController Controller => Root.GetComponent<PopupStoreStartController>();
        internal PopupStoreStartPanel Panel => Root.GetComponent<PopupStoreStartPanel>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal GameObject LegacyOverlay => Read<GameObject>(Panel, "_overlay");
        internal PopupStorePhasePreviewView Tutorial => Read<PopupStorePhasePreviewView>(Controller, "_unavailableTutorial");
        internal Button PlayButton => Read<Button>(Tutorial, "_playButton");
        internal TMP_Text Ticket => Read<TMP_Text>(Tutorial, "_ticketStatusText");

        internal Harness(bool open = true, string unavailableText = null)
        {
            Scope = new GameObject("latest-popup-entry-isolated-memory"); Scope.SetActive(false);
            try
            {
                Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Scope.transform, false);
                View = Root.GetComponent<UIPopupStore>();
                Root.GetComponent<CanvasScaler>().enabled = false; Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                var templateOwner = new GameObject("inactive-font-safe-template");
                templateOwner.transform.SetParent(Scope.transform, false); templateOwner.SetActive(false);
                _tutorialTemplate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TutorialPath), templateOwner.transform, false);
                _tutorialTemplate.SetActive(true);
                _fonts.BindBeforeActivation(Root, _tutorialTemplate);
                Write(Controller, "_unavailableTutorialPrefab", _tutorialTemplate);
                if (unavailableText != null) Write(Controller, "_unavailableTicketText", unavailableText);
                var navObject = new GameObject("latest-entry-navigation", typeof(RectTransform)); navObject.transform.SetParent(Scope.transform, false);
                Navigation = navObject.AddComponent<MobileUINavigation>();
                var mapObject = new GameObject("latest-entry-map", typeof(RectTransform), typeof(Canvas)); mapObject.transform.SetParent(Scope.transform, false);
                Map = mapObject.AddComponent<PopupStoreAdmissionMapView>();
                Write(Navigation, "_uiViews", new[] { new MobileViewDicStruct { Name = "AdmissionMap", UIView = Map },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = View } });
                Invoke(Navigation, "Init"); Scope.SetActive(true); Navigation.PushNoAnime("AdmissionMap");
                if (open) Open();
            }
            catch { Dispose(); throw; }
        }
        internal void Open()
        {
            Navigation.Push(UIPopupStore.ViewName);
            Assert.That(Navigation.FirstView, Is.SameAs(View));
            Assert.That(Root.GetComponentInChildren<PopupStoreSalesCounter>(true).Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreOrderDisplay>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStorePacking>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreCustomerEntrance>().BeginVisibleSession(), Is.True);
            Assert.That(Root.GetComponent<PopupStorePackingPresentation>().BeginVisibleSession(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreCustomerPatience>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreScore>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreResultPanel>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreExitConfirmation>().Refresh(), Is.True);
            Controller.OnViewShown();
        }
        internal PopupStoreAdmissionHost BindReview() => _review ?? (_review = PopupStoreApprovedTestSession.Attach(Root, 1));
        internal PopupStorePhasePreviewView CreatePhaseView()
            => Object.Instantiate(_tutorialTemplate, Root.transform, false).GetComponent<PopupStorePhasePreviewView>();
        internal PopupStorePhaseRewardPresentation CreateRewardView()
        {
            var holder = new GameObject("inactive-reward-font-binding"); holder.transform.SetParent(Scope.transform, false); holder.SetActive(false);
            var prefab = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource).RewardPrefab;
            var root = Object.Instantiate(prefab, holder.transform, false); root.SetActive(false);
            _fonts.BindBeforeActivation(root);
            root.transform.SetParent(Root.transform, false); Object.DestroyImmediate(holder); root.SetActive(true);
            return root.GetComponent<PopupStorePhaseRewardPresentation>();
        }
        internal void AssertLatestUnavailable()
        {
            Assert.That(Tutorial, Is.Not.Null); Assert.That(Tutorial.IsTutorialVisible, Is.True);
            Assert.That(Panel.IsVisible, Is.False); Assert.That(LegacyOverlay.activeSelf, Is.False);
            Assert.That(PlayButton.interactable, Is.False);
            Assert.That(Read<Button>(Panel, "_startButton").interactable, Is.False);
        }
        internal void AssertUntouched()
        {
            Assert.That(Controller.Host, Is.Null);
            Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted)); Assert.That(Clock.ElapsedSeconds, Is.Zero);
            Assert.That(Root.GetComponentInChildren<PopupStoreSalesCounter>(true).Count, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreCustomerEntrance>().StartedEntranceCount, Is.Zero);
            Assert.That(Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            Assert.That(Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
        public void Dispose()
        {
            _review?.Dispose(); if (Scope != null) Object.DestroyImmediate(Scope); _fonts.Dispose();
        }
    }
    private sealed class RecoveryBridge : IPopupStoreAccountPhaseBridge, IPopupStoreAccountRewardRecovery, IDisposable
    {
        public PopupStoreAdmissionHost AdmissionHost { get; }
        public bool IsReady => true;
        public string Status => "isolated recovery ownership contract";
        public PopupStoreAccountRecoverySnapshot Recovery { get; } = new PopupStoreAccountRecoverySnapshot(
            "original-recovery-session", 200, PopupStoreGradeTier.C, true, 3, false);
        internal readonly PopupStoreBusinessStartFakeRepository Repository;
        internal int Prepares, Claims;
        internal RecoveryBridge()
        {
            var store = new PopupStoreBusinessStartFakeStore(() => PopupStoreApprovedTestSession.AuthorityTime,
                new PopupStoreBusinessAccessPolicy("latest-recovery-contract", 1));
            store.RegisterAccount("latest-recovery-contract-only");
            var session = store.OpenSession("latest-recovery-contract-only");
            Repository = new PopupStoreBusinessStartFakeRepository(store);
            AdmissionHost = new PopupStoreAdmissionHost(Repository, Repository, () => session, editorReview: false);
        }
        public void PrepareResult(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed) => Prepares++;
        public void FinalizeAndClaim(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed) => Claims++;
        public void ClaimRecovery(Action<PopupStoreAccountClaimResult> completed) => Claims++;
        public void Dispose() => AdmissionHost.Dispose();
    }
    private static T Read<T>(object target, string field) => (T)Field(target.GetType(), field).GetValue(target);
    private static void Write(object target, string field, object value) => Field(target.GetType(), field).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    private static object Invoke(object owner, string method)
        => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, null);
}
#endif
