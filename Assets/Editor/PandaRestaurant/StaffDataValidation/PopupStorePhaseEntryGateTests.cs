#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>First activation before the delayed Editor bridge binds; original admission stays authoritative.</summary>
public sealed class PopupStorePhaseEntryGateTests
{
    private const string StateKey = "Panda.PopupStore.PhasePreview.State";
    private Func<bool> _previous;
    private GachaCollectionPreviewSceneWitness _workspace;
    [SetUp] public void PreserveOwner()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PopupStoreVerificationRunner).TypeHandle);
        _previous = PopupStoreStartController.EditorPhasePreviewEntryPending;
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => false;
    }
    [TearDown] public void RestoreOwner()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = _previous;
        _workspace.AssertUnchanged("phase preview first-frame admission guard");
    }

    [TestCase(true)] [TestCase(false)]
    public void FirstActivationHidesTheOrdinaryExplanationRegardlessOfEnableOrder(bool panelFirst)
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
        using (var h = new OrdinaryHost(show: false))
        {
            h.Root.SetActive(true);
            Invoke(panelFirst ? (object)h.Panel : h.Controller, "OnEnable");
            Invoke(panelFirst ? (object)h.Controller : h.Panel, "OnEnable");
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.StartButton.interactable, Is.False);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
        }
    }

    [Test]
    public void PendingWithoutHostNeverShowsServiceUnavailableOrConsumesAnAdmission()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
        using (var h = new OrdinaryHost())
        {
            Assert.That(h.Controller.Host, Is.Null);
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Clock.AdvanceTime(100), Is.False);
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
        }
    }

    [Test]
    public void AnAlreadyBoundIsolatedHostCannotBeStartedThroughTheOldPanelWhilePreviewIsPending()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
        using (var h = new OrdinaryHost())
        {
            var host = h.Bind();
            Assert.That(h.Panel.IsVisible, Is.False);
            h.StartButton.onClick.Invoke();
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(host.Access.Remaining, Is.EqualTo(1));
            Assert.That(host.LocalStartCount, Is.Zero);
            Assert.That(host.Transaction, Is.Null);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
        }
    }

    [Test]
    public void InitializedPhaseModelWithoutItsAuthoredTutorialStillCannotConsumeTheTicket()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
        using (var h = new PopupStorePhasePreviewIntegrationTests.Host(false, false))
        {
            var start = h.Root.GetComponent<PopupStoreStartController>();
            start.OnViewShown();
            Assert.That(h.Preview.IsInitialized, Is.True); Assert.That(h.Preview.TutorialView, Is.Null);
            Assert.That(h.Root.GetComponent<PopupStoreStartPanel>().IsVisible, Is.False);
            Assert.That(start.RequestStart(), Is.False);
            Assert.That(h.Admission.Access.Remaining, Is.EqualTo(1));
            Assert.That(h.Admission.LocalStartCount, Is.Zero);
        }
    }

    [Test]
    public void FullyAttachedTutorialCanStartOnlyThroughItsFinalPageWhileTheBridgeStillFinishesBinding()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => true;
        using (var h = new PopupStorePhasePreviewIntegrationTests.Host(false, false))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath), h.Root.transform, false);
            root.SetActive(false); fonts.BindBeforeActivation(root); root.SetActive(true);
            var view = root.GetComponent<PopupStorePhasePreviewView>();
            var close = new SerializedObject(h.Root.GetComponent<UIPopupStore>()).FindProperty("_closeButton").objectReferenceValue as Button;
            Assert.That(h.Preview.AttachView(view, close), Is.True);
            Assert.That(h.Root.GetComponent<PopupStoreStartPanel>().IsVisible, Is.False);
            Assert.That(view.IsTutorialVisible, Is.True);
            Assert.That(h.Root.GetComponent<PopupStoreStartController>().RequestStart(), Is.False);
            Assert.That(view.TryPlay(), Is.False);
            while (view.TryNext()) { }
            Assert.That(view.TryPlay(), Is.True); Assert.That(view.TryPlay(), Is.False);
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Admission.Access.Remaining, Is.Zero);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Stage"));
        }
    }

    [Test]
    public void OrdinaryReviewKeepsItsExistingStartPanelAndNinetySecondApproval()
    {
        using (var h = new OrdinaryHost())
        {
            var host = h.Bind();
            Assert.That(h.Root.GetComponent<PopupStorePhasePreview>(), Is.Null);
            Assert.That(h.Panel.IsVisible, Is.True); Assert.That(h.Panel.Remaining, Is.EqualTo(1));
            Assert.That(h.StartButton.interactable, Is.True);
            Assert.That(h.Controller.RequestStart(), Is.True, h.Controller.LastError);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90));
            Assert.That(host.LocalStartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ClearingThePendingOwnerRestoresOrdinaryUnstartedPresentationWithoutAutoStarting()
    {
        bool pending = true;
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => pending;
        using (var h = new OrdinaryHost())
        {
            var host = h.Bind(); Assert.That(h.Panel.IsVisible, Is.False);
            pending = false; h.Controller.OnViewShown();
            Assert.That(h.Panel.IsVisible, Is.True); Assert.That(h.StartButton.interactable, Is.True);
            Assert.That(host.Access.Remaining, Is.EqualTo(1)); Assert.That(host.LocalStartCount, Is.Zero);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
        }
    }

    [Test]
    public void BrokenPendingOwnerFailsClosedInsteadOfStartingTheOrdinaryBusiness()
    {
        PopupStoreStartController.EditorPhasePreviewEntryPending = () => throw new InvalidOperationException("owner unavailable");
        using (var h = new OrdinaryHost())
        {
            var host = h.Bind(); Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(host.Access.Remaining, Is.EqualTo(1)); Assert.That(host.Transaction, Is.Null);
        }
    }

    [TestCase("starting", true)] [TestCase("ready", false)] [TestCase("stopped", false)] [TestCase("", false)]
    public void RunnerUsesOnlyTheExistingExplicitStartingSessionFlag(string state, bool expected)
    {
        string previous = SessionState.GetString(StateKey, string.Empty);
        try
        {
            Assert.That(_previous, Is.Not.Null);
            SessionState.SetString(StateKey, state);
            Assert.That(_previous(), Is.EqualTo(expected));
        }
        finally { if (previous.Length == 0) SessionState.EraseString(StateKey); else SessionState.SetString(StateKey, previous); }
    }

    private sealed class OrdinaryHost : IDisposable
    {
        internal readonly GameObject Root;
        internal PopupStoreStartController Controller => Root.GetComponent<PopupStoreStartController>();
        internal PopupStoreStartPanel Panel => Root.GetComponent<PopupStoreStartPanel>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal Button StartButton => new SerializedObject(Panel).FindProperty("_startButton").objectReferenceValue as Button;
        private PopupStoreAdmissionHost _host;
        internal OrdinaryHost(bool show = true)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewIntegrationTests.Host.Prefab));
            Root.GetComponent<CanvasScaler>().enabled = false;
            Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            var view = Root.GetComponent<UIPopupStore>(); view.Init();
            if (show) view.Show();
        }
        internal PopupStoreAdmissionHost Bind() => _host ?? (_host = PopupStoreApprovedTestSession.Attach(Root, 1));
        public void Dispose() { _host?.Dispose(); if (Root != null) Object.DestroyImmediate(Root); }
    }
    private static void Invoke(object owner, string method)
        => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}
#endif
