#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real authored views/gameplay with explicitly injected account contracts. No SDK/account/Scene writes.</summary>
public sealed class PopupStoreAccountPhaseConnectionTests
{
    private const string ResourceRoot = "Assets/Resources/UI/PopupStore/Account/";
    private Func<UIPopupStore, IPopupStoreAccountPhaseBridge> _factory;
    [SetUp] public void Before() { _factory = PopupStoreAccountPhaseBootstrap.BridgeFactory; }
    [TearDown] public void After() { PopupStoreAccountPhaseBootstrap.BridgeFactory = _factory; }

    [Test] public void DisabledAccountConfigurationCannotCallFactory()
    {
        var authored = Configuration();
        Assert.That(authored, Is.Not.Null);
        bool authoredMode = authored.EnableAccountMode;
        var disabled = Object.Instantiate(authored);
        try
        {
            // Test the disabled guard on a detached configuration. Do not require
            // the project's intentionally enabled QA asset to be turned off.
            Write(disabled, "_enableAccountMode", false);
            PopupStoreAccountPhaseBootstrap.BridgeFactory = null;
            using (var h = new Harness(false))
            {
                int calls = 0;
                PopupStoreAccountPhaseBootstrap.BridgeFactory = _ => { calls++; return null; };
                Assert.That(disabled.EnableAccountMode, Is.False);
                Assert.That(PopupStoreAccountPhaseBootstrap.TryPrepare(h.View, disabled, out _), Is.False);
                Assert.That(calls, Is.Zero); Assert.That(h.Controller.Host, Is.Null);
                Assert.That(Read<double>(h.Clock, "_initialSeconds"), Is.EqualTo(90));
            }
            Assert.That(authored.EnableAccountMode, Is.EqualTo(authoredMode), "The authored account setting must be preserved.");
        }
        finally { Object.DestroyImmediate(disabled); }
    }

    [TestCase(1, 22, 200, 5, 22)] [TestCase(2, 27, 650, 8, 20)]
    [TestCase(3, 30, 1200, 12, 18)] [TestCase(4, 34, 1900, 16, 16)]
    [TestCase(5, 38, 2800, 24, 14)] [TestCase(6, 42, 3900, 32, 12)]
    public void AccountConfigurationRetainsApprovedPhaseValues(int number, double seconds, int score, int plushes, double patience)
    {
        Assert.That(Configuration().TryFreeze(Catalog(), out var profile, out var scoring, out var error), Is.True, error);
        var phase = profile.Phases[number - 1];
        Assert.That(phase.TimeLimitSeconds, Is.EqualTo(seconds)); Assert.That(phase.TargetScore, Is.EqualTo(score));
        Assert.That(phase.AllowedPlushIds.Count, Is.EqualTo(plushes)); Assert.That(phase.CustomerPatienceSeconds, Is.EqualTo(patience));
        Assert.That(scoring.ThreeMaterialPoints, Is.EqualTo(100)); Assert.That(scoring.FourMaterialPoints, Is.EqualTo(150));
        Assert.That(scoring.FiveMaterialPoints, Is.EqualTo(220));
    }

    [TestCase("PopupStorePhasePreviewView.prefab")]
    [TestCase("PopupStorePhaseRewardView.prefab")]
    public void RuntimeAssetsPreserveExactApprovedLayoutAndHaveNoEditorAssetDependency(string file)
    {
        var oldPath = "Assets/Editor/PandaRestaurant/StaffDataValidation/UI/" + file;
        string oldText = File.ReadAllText(oldPath), current = File.ReadAllText(ResourceRoot + file);
        string oldImage = AssetDatabase.AssetPathToGUID("Assets/Editor/PandaRestaurant/StaffDataValidation/UI/PopupStoreTutorialGameplayV4.png");
        string newImage = AssetDatabase.AssetPathToGUID(ResourceRoot + "PopupStoreTutorialGameplayV4.png");
        Assert.That(current.Replace(newImage, oldImage), Is.EqualTo(oldText));
        foreach (string dependency in AssetDatabase.GetDependencies(ResourceRoot + file, true))
            Assert.That(dependency.Replace('\\', '/').Contains("/Editor/"), Is.False, dependency);
    }

    [Test] public void RuntimeTutorialScreenshotIsTheExactApprovedImage()
        => CollectionAssert.AreEqual(File.ReadAllBytes("Assets/Editor/PandaRestaurant/StaffDataValidation/UI/PopupStoreTutorialGameplayV4.png"),
            File.ReadAllBytes(ResourceRoot + "PopupStoreTutorialGameplayV4.png"));

    [Test] public void ExplicitEditorAdmissionNeverGetsAnAccountFactoryOrPaidPresentation()
    {
        using (var h = new PopupStorePhasePreviewNavigationTests.Harness(requestStart: false))
        {
            int calls = 0; PopupStoreAccountPhaseBootstrap.BridgeFactory = _ => { calls++; return null; };
            var config = Object.Instantiate(Configuration());
            try
            {
                Write(config, "_enableAccountMode", true);
                Assert.That(PopupStoreAccountPhaseBootstrap.TryPrepare(h.View, config, out _), Is.False);
                Assert.That(calls, Is.Zero); Assert.That(h.Preview.IsAccountMode, Is.False);
                Assert.That(h.Admission.IsEditorReview, Is.True);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }

    [Test] public void AccountTutorialKeepsFivePagesAndNeverConsumesBeforePlay()
    {
        using (var h = new Harness())
        {
            Assert.That(h.Phase.IsAccountMode, Is.True); Assert.That(h.Phase.RewardRoll, Is.Null);
            Assert.That(h.Tutorial.PageCount, Is.EqualTo(5)); Assert.That(h.Tutorial.IsTutorialVisible, Is.True);
            while (h.Tutorial.TryNext()) { }
            Assert.That(h.Controller.Host.LocalStartCount, Is.Zero); Assert.That(h.Controller.Host.Access.Remaining, Is.EqualTo(1));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Tutorial.TryPrevious(), Is.True); Assert.That(h.Tutorial.TryNext(), Is.True);
            Assert.That(h.Bridge.Prepares + h.Bridge.Claims, Is.Zero);
        }
    }

    [Test] public void ConfirmedAccountAdmissionStartsSharedStagesAndKeepsOrdinaryResultUnfinalized()
    {
        using (var h = new Harness())
        {
            h.Start();
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22)); Assert.That(h.Controller.Host.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Controller.Host.Access.Remaining, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(h.Tutorial.TryPlay(), Is.False);
        }
    }

    [Test] public void AsyncAccountAdmissionCanRetryAfterARejectedRequestWithoutChangingLayout()
    {
        using (var h = new Harness())
        {
            var view = h.Tutorial;
            var position = Read<Button>(view, "_playButton").transform.localPosition;
            view.RenderAccountAdmission(true, false, "pending");
            Assert.That(view.TryPlay(), Is.False);
            view.RenderAccountAdmission(false, true, null);
            while (view.TryNext()) { }
            Assert.That(view.TryPlay(), Is.True);
            Assert.That(Read<Button>(view, "_playButton").transform.localPosition, Is.EqualTo(position));
        }
    }

    [Test] public void ConfirmedExhaustedAccountTicketsRemainVisibleWithPlayDisabled()
    {
        using (var h = new Harness(ticketsExhausted: true))
        {
            while (h.Tutorial.TryNext()) { }
            h.Controller.OnViewShown();
            Assert.That(h.Controller.Host.Access.Remaining, Is.Zero);
            Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("이용권 0/1"));
            Assert.That(Read<Button>(h.Tutorial, "_playButton").interactable, Is.False);
            Assert.That(h.Tutorial.TryPlay(), Is.False);
            Assert.That(h.Controller.Host.LocalStartCount, Is.Zero);
        }
    }

    [Test] public void UnknownAccountTicketsKeepConfirmationWarningAndCannotStart()
    {
        using (var h = new Harness())
        {
            while (h.Tutorial.TryNext()) { }
            h.Phase.RenderStart(true, false, -1, -1, "영업권을 다시 조회해 주세요.");
            Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("시작 확인 필요"));
            Assert.That(Read<Button>(h.Tutorial, "_playButton").interactable, Is.False);
            Assert.That(h.Tutorial.TryPlay(), Is.False);
            Assert.That(h.Controller.Host.LocalStartCount, Is.Zero);
        }
    }

    [Test] public void PendingAccountStartOverridesOldCountThenRestoresConfirmedExhaustion()
    {
        using (var h = new Harness(ticketsExhausted: true))
        {
            while (h.Tutorial.TryNext()) { }
            h.Phase.RenderStart(true, false, 1, 1, "시작 승인을 확인하고 있습니다.", true);
            Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("시작 확인 중"));
            Assert.That(Read<Button>(h.Tutorial, "_playButton").interactable, Is.False);
            Assert.That(h.Tutorial.TryPlay(), Is.False);
            h.Controller.OnViewShown();
            Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("이용권 0/1"));
            Assert.That(h.Tutorial.TryPlay(), Is.False);
            Assert.That(h.Controller.Host.LocalStartCount, Is.Zero);
        }
    }

    [Test] public void BlockedAccountAdmissionWithKnownCountRetainsConfirmationWarning()
    {
        using (var h = new Harness())
        {
            while (h.Tutorial.TryNext()) { }
            foreach (int remaining in new[] { 1, 0 })
            {
                h.Phase.RenderStart(true, false, remaining, 1, "기존 시작 요청의 복구가 필요합니다.");
                Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("시작 확인 필요"));
                Assert.That(h.Tutorial.TryPlay(), Is.False);
            }
            h.Bridge.IsReady = false;
            h.Phase.RenderStart(true, false, 0, 1, "계정 상태를 확인해 주세요.", false, true);
            Assert.That(Read<TMPro.TMP_Text>(h.Tutorial, "_ticketStatusText").text, Is.EqualTo("시작 확인 필요"));
            Assert.That(h.Tutorial.TryPlay(), Is.False);
            Assert.That(h.Controller.Host.LocalStartCount, Is.Zero);
        }
    }

    [Test] public void AccountResultIsPreparedOnceBeforeReceiveAndDoesNotUseReviewRandom()
    {
        using (var h = new Harness())
        {
            h.CResult();
            Assert.That(h.Bridge.Prepares, Is.EqualTo(1)); Assert.That(h.Bridge.Claims, Is.Zero);
            Assert.That(h.Phase.RewardRoll, Is.Null); Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero);
            h.Panel.TickResult(1); h.Panel.TickResult(1);
            Assert.That(h.Bridge.Prepares, Is.EqualTo(1)); Assert.That(h.Phase.Session.TotalScore, Is.EqualTo(200));
        }
    }

    [Test] public void SavedClaimBeforeVisualsUsesOriginalResultAndNeverAwardsAgainOnDoubleTap()
    {
        using (var h = new Harness())
        {
            h.CResult(); Assert.That(h.Panel.TryConfirm(), Is.True); Assert.That(h.Panel.TryConfirm(), Is.False);
            Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero); Assert.That(h.Bridge.Claims, Is.EqualTo(1));
            h.Bridge.Complete(3);
            var reward = h.Phase.RewardPresentation;
            Assert.That(reward.BeginCount, Is.EqualTo(1)); Assert.That(reward.Amount, Is.EqualTo(3));
            reward.Tick(reward.RiseDuration, true); Assert.That(reward.TryOpen(), Is.True); Assert.That(reward.TryOpen(), Is.False);
            reward.Tick(reward.OpeningDuration, true);
            Assert.That(Read<TMPro.TMP_Text>(reward, "_disclaimerText").text, Is.EqualTo("보상 수령 완료"));
            Assert.That(reward.TryConfirm(), Is.True); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            Assert.That(h.Root.GetComponent<PopupStoreSessionLifetime>().IsRetired, Is.True);
            h.Bridge.Complete(3); Assert.That(reward.BeginCount, Is.EqualTo(1)); Assert.That(h.Bridge.Claims, Is.EqualTo(1));
        }
    }

    [TestCase(PopupStoreAccountClaimState.Pending)] [TestCase(PopupStoreAccountClaimState.Retryable)]
    [TestCase(PopupStoreAccountClaimState.Rejected)]
    public void UnconfirmedClaimNeverShowsPaidDiamonds(PopupStoreAccountClaimState state)
    {
        using (var h = new Harness())
        {
            h.CResult(); h.Panel.TryConfirm(); h.Bridge.Complete(3, state);
            Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero); Assert.That(h.Phase.AccountClaimConfirmed, Is.False);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View)); Assert.That(h.Phase.Session.FinalizationCount, Is.EqualTo(1));
        }
    }

    [TestCase(0)] [TestCase(2)] [TestCase(300)]
    public void SavedAmountOutsideTheExactGradeR1RowCannotAppearAsConfirmed(int amount)
    {
        using (var h = new Harness())
        { h.CResult(); h.Panel.TryConfirm(); h.Bridge.Complete(amount); Assert.That(h.Phase.AccountClaimConfirmed, Is.False); Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero); }
    }

    [Test] public void WrongBusinessOrSwitchedAccountCallbackCannotRevealReward()
    {
        using (var h = new Harness())
        {
            h.CResult(); h.Panel.TryConfirm(); h.Bridge.Complete(3, session: "another-business");
            Assert.That(h.Phase.AccountClaimConfirmed, Is.False); Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero);
            h.Panel.TryConfirm(); h.Bridge.IsReady = false; h.Bridge.Complete(3);
            Assert.That(h.Phase.AccountClaimConfirmed, Is.False); Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero);
        }
    }

    [Test] public void AbandonedAccountGamePersistsNoRewardAndClosesWithoutBox()
    {
        using (var h = new Harness())
        {
            h.Start(); Assert.That(h.Phase.RequestAbandon(), Is.True); Assert.That(h.Phase.TickCleanup(), Is.True);
            h.Panel.TickResult(1); h.Panel.TickResult(1); Assert.That(h.Panel.TryConfirm(), Is.True);
            h.Bridge.Complete(0);
            Assert.That(h.Bridge.Prepares, Is.EqualTo(1)); Assert.That(h.Phase.RewardPresentation.BeginCount, Is.Zero);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void RestartRecoveryUsesStoredFactsWithoutCreatingPhaseOrNewReceipt(bool interrupted)
    {
        var snapshot = new PopupStoreAccountRecoverySnapshot("restored-business", interrupted ? 100 : 200,
            interrupted ? PopupStoreGradeTier.None : PopupStoreGradeTier.C, !interrupted, interrupted ? 0 : 3, interrupted);
        using (var h = new Harness(recovery: snapshot))
        {
            Assert.That(h.Phase, Is.Null); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            h.Panel.TickResult(1); h.Panel.TickResult(1);
            Assert.That(h.Panel.TryConfirm(), Is.True); Assert.That(h.Bridge.Claims, Is.EqualTo(1));
            h.Bridge.Complete(snapshot.Diamonds);
            var recovery = h.Root.GetComponent<PopupStoreAccountRecoveryPresentation>();
            if (interrupted) Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            else
            {
                Assert.That(recovery.Reward.Amount, Is.EqualTo(3)); Assert.That(recovery.Reward.BeginCount, Is.EqualTo(1));
                recovery.Reward.Tick(recovery.Reward.RiseDuration, true); Assert.That(recovery.Reward.TryOpen(), Is.True);
                recovery.Reward.Tick(recovery.Reward.OpeningDuration, true); Assert.That(recovery.Reward.TryConfirm(), Is.True);
                Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            }
            Assert.That(h.Bridge.Prepares, Is.Zero);
        }
    }

    private static PopupStoreAccountPhaseConfiguration Configuration()
        => Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
    private static PopupStoreMaterialCatalog Catalog() => Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
    private static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static void Write(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f != null) { f.SetValue(obj, value); return; } }
        throw new MissingFieldException(name);
    }
    private static void Invoke(object obj, string name) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);

    private sealed class Bridge : IPopupStoreAccountPhaseBridge, IPopupStoreAccountRewardRecovery
    {
        public PopupStoreAdmissionHost AdmissionHost { get; }
        public bool IsReady { get; set; } = true;
        public string Status => "isolated UI contract test";
        public PopupStoreAccountRecoverySnapshot Recovery { get; }
        public int Prepares, Claims;
        private Action<PopupStoreAccountClaimResult> _pending;
        private string _session;
        internal Bridge(PopupStoreAccountRecoverySnapshot recovery, bool ticketsExhausted = false)
        {
            Recovery = recovery;
            var store = new PopupStoreBusinessStartFakeStore(() => PopupStoreApprovedTestSession.AuthorityTime,
                new PopupStoreBusinessAccessPolicy("DEV01E-UI-test", 1));
            store.RegisterAccount("ui-account"); var session = store.OpenSession("ui-account");
            if (ticketsExhausted)
                Assert.That(store.Submit(session, new PopupStoreBusinessStartRequest("ui-account", "earlier-start",
                    PopupStoreBusinessMode.Rewarded, "DEV01E-UI-test")).Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.Started));
            var repository = new PopupStoreBusinessStartFakeRepository(store);
            AdmissionHost = new PopupStoreAdmissionHost(repository, repository, () => session, editorReview: false);
        }
        public void PrepareResult(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed)
        { Prepares++; completed(new PopupStoreAccountClaimResult(PopupStoreAccountClaimState.Confirmed, result.BusinessSessionId, 0, null)); }
        public void FinalizeAndClaim(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed)
        { Claims++; _session = result.BusinessSessionId; _pending = completed; }
        public void ClaimRecovery(Action<PopupStoreAccountClaimResult> completed)
        { Claims++; _session = Recovery.SessionId; _pending = completed; }
        internal void Complete(int amount, PopupStoreAccountClaimState state = PopupStoreAccountClaimState.Confirmed, string session = null)
            => _pending?.Invoke(new PopupStoreAccountClaimResult(state, session ?? _session, amount, "test response"));
    }

    private sealed class Harness : IDisposable
    {
        internal readonly GameObject Scope;
        internal readonly UIPopupStore View;
        internal readonly MobileUINavigation Navigation;
        internal readonly PopupStoreAdmissionMapView Map;
        internal readonly Bridge Bridge;
        private PopupStoreAccountPhaseConfiguration _configuration;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal GameObject Root => View.gameObject;
        internal PopupStorePhasePreview Phase => Root.GetComponent<PopupStorePhasePreview>();
        internal PopupStorePhasePreviewView Tutorial => Phase.TutorialView;
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal PopupStoreStartController Controller => Root.GetComponent<PopupStoreStartController>();
        internal PopupStoreResultPanel Panel => Root.GetComponent<PopupStoreResultPanel>();
        internal Harness(bool bind = true, PopupStoreAccountRecoverySnapshot recovery = null, bool ticketsExhausted = false)
        {
            Scope = new GameObject("account-phase-isolated-contract-test"); Scope.SetActive(false);
            try
            {
                var root = Object.Instantiate(Resources.Load<GameObject>("UI/PopupStore/UIPopupStore"), Scope.transform, false);
                View = root.GetComponent<UIPopupStore>(); _fonts.BindBeforeActivation(root);
                Root.GetComponent<CanvasScaler>().enabled = false; Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                var navObject = new GameObject("account-phase-navigation", typeof(RectTransform)); navObject.transform.SetParent(Scope.transform, false);
                Navigation = navObject.AddComponent<MobileUINavigation>();
                var mapObject = new GameObject("account-phase-map", typeof(RectTransform), typeof(Canvas)); mapObject.transform.SetParent(Scope.transform, false);
                Map = mapObject.AddComponent<PopupStoreAdmissionMapView>();
                Write(Navigation, "_uiViews", new[] { new MobileViewDicStruct { Name = "AdmissionMap", UIView = Map },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = View } });
                Invoke(Navigation, "Init"); Scope.SetActive(true); Navigation.PushNoAnime("AdmissionMap"); Navigation.Push(UIPopupStore.ViewName);
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
                Assert.That(Panel.Refresh(), Is.True); Assert.That(Root.GetComponent<PopupStoreExitConfirmation>().Refresh(), Is.True);
                if (!bind) return;
                Bridge = new Bridge(recovery, ticketsExhausted);
                PopupStoreAccountPhaseBootstrap.BridgeFactory = _ => Bridge;
                _configuration = Object.Instantiate(Configuration()); Write(_configuration, "_enableAccountMode", true);
                Assert.That(PopupStoreAccountPhaseBootstrap.TryPrepare(View, _configuration, out var error), Is.True, error);
                foreach (var child in Root.GetComponentsInChildren<PopupStorePhasePreviewView>(true))
                { child.gameObject.SetActive(false); _fonts.BindBeforeActivation(child.gameObject); child.gameObject.SetActive(true); }
                foreach (var child in Root.GetComponentsInChildren<PopupStorePhaseRewardPresentation>(true))
                { child.gameObject.SetActive(false); _fonts.BindBeforeActivation(child.gameObject); child.gameObject.SetActive(true); }
                Controller.OnViewShown();
            }
            catch { Dispose(); throw; }
        }
        internal void Start()
        { while (Tutorial.TryNext()) { } Assert.That(Tutorial.TryPlay(), Is.True); PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(Phase, Clock); }
        internal void CResult()
        {
            Start(); var entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
            entrance.TickEntrance(0); entrance.TickEntrance(Phase.EntryInterval); entrance.TickEntrance(Phase.EntryInterval); entrance.TickEntrance(1.3f);
            for (int n = 0; n < 2; n++)
            {
                int slot = Enumerable.Range(0, 3).First(entrance.IsWaiting);
                Assert.That(Root.GetComponent<PopupStoreOrderDisplay>().TryGetEntryBinding(slot, out var order, out _), Is.True);
                Assert.That(order.TryResolve(Catalog(), out var entries, out _), Is.True);
                foreach (var entry in entries.Reverse()) Assert.That(Root.GetComponentInChildren<PopupStoreSalesCounter>(true).TryAdd(entry), Is.True);
                Assert.That(Root.GetComponent<PopupStorePacking>().TryPack().Success, Is.True);
                var visual = Root.GetComponent<PopupStorePackingPresentation>(); visual.TickPresentation(0);
                for (int i = 0; i < 500 && visual.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++) visual.TickPresentation(.02f);
                entrance.TickEntrance(2);
            }
            Clock.AdvanceTime(Clock.RemainingSeconds); Assert.That(Phase.TickCleanup(), Is.True);
            PopupStorePhasePreviewIntegrationTests.CompleteNextPresentation(Phase, Clock);
            Clock.AdvanceTime(Clock.RemainingSeconds); Assert.That(Phase.TickCleanup(), Is.True);
            PopupStorePhasePreviewIntegrationTests.CompleteFailurePresentation(Phase, Clock);
            Panel.TickResult(1); Panel.TickResult(1);
            Assert.That(Phase.Session.TotalScore, Is.EqualTo(200)); Assert.That(Phase.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.C));
        }
        public void Dispose()
        {
            Bridge?.AdmissionHost.Dispose(); if (Scope != null) Object.DestroyImmediate(Scope);
            if (_configuration != null) Object.DestroyImmediate(_configuration); _fonts.Dispose();
        }
    }
}
#endif
