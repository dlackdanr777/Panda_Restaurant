#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Harness = PopupStorePhasePreviewNavigationTests.Harness;
using Object = UnityEngine.Object;

/// <summary>Actual authored tutorial, admission and MobileUINavigation; no replacement navigation or receipt.</summary>
public sealed class PopupStorePhaseTutorialNavigationTests
{
    private GachaCollectionPreviewSceneWitness _workspace;
    [SetUp] public void RecordWorkspace() => _workspace = GachaCollectionPreviewSceneWitness.Capture();
    [TearDown] public void PreserveWorkspace() => _workspace.AssertUnchanged("phase tutorial real-navigation memory test");

    [Test]
    public void ActualMenuPolishBindingImmediatelyRefreshesNotStartedTimeWithoutStartingOrAdvancingBusiness()
    {
        using (var t = new TutorialHarness())
        {
            var h = t.Game;
            var time = Field<TMP_Text>(h.Clock, "_timeText");
            var clockSettings = new SerializedObject(h.Clock);
            Assert.That(clockSettings.FindProperty("_notStartedText").stringValue, Is.EqualTo("—"));
            Assert.That(time.text, Is.EqualTo("—"), "The latest authored value no longer duplicates the separate time heading.");
            var headings = Array.FindAll(h.Root.GetComponentsInChildren<TMP_Text>(true), text => text.name == "Time Label");
            Assert.That(headings.Length, Is.EqualTo(1));
            Assert.That(headings[0].text, Is.EqualTo("남은 시간"));
            // Exercise a real Inspector override and a stale label on this memory instance only.
            clockSettings.FindProperty("_notStartedText").stringValue = "대기 중";
            clockSettings.ApplyModifiedPropertiesWithoutUndo();
            time.text = "남은 시간 —";
            double remaining = h.Clock.RemainingSeconds;
            double elapsed = h.Clock.ElapsedSeconds;
            t.AssertUnused();
            var bind = typeof(PopupStoreVerificationRunner).GetMethod("BindPhasePreviewPolish",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bind, Is.Not.Null);

            // Exercise the actual menu binder synchronously: no Update, focus, start or elapsed-time tick.
            bind.Invoke(null, new object[] { h.View, t.View, h.Preview });

            Assert.That(time.text, Is.EqualTo("대기 중"));
            clockSettings.Update();
            Assert.That(clockSettings.FindProperty("_notStartedText").stringValue, Is.EqualTo("대기 중"));
            Assert.That(headings[0].text, Is.EqualTo("남은 시간"));
            Assert.That(time.text, Does.Not.Contain("남은 시간"));
            Assert.That(t.View.IsTutorialVisible, Is.True);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(h.Score.TotalScore, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Preview.Session.PhaseGeneration, Is.Zero);
            t.AssertUnused();
        }
    }

    [Test]
    public void TutorialCloseReturnsToTheRealMapWithoutConsumingAdmissionAndReentryKeepsItsPage()
    {
        using (var t = new TutorialHarness())
        {
            var h = t.Game;
            Assert.That(t.View.TryNext(), Is.True);
            Assert.That(t.View.TryClose(), Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            Assert.That(h.Root.activeSelf, Is.False);
            t.AssertUnused();
            h.Reenter();
            Assert.That(t.View.IsTutorialVisible, Is.True);
            Assert.That(t.View.CurrentPage, Is.EqualTo(1));
            Assert.That(t.TicketStatus.text, Is.EqualTo("이용권 1/1"));
            Assert.That(t.View.TryClose(), Is.True, "Reopening must clear the accepted close latch.");
            Assert.That(t.View.CloseRequestCount, Is.EqualTo(2));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            t.AssertUnused();
        }
    }

    [Test]
    public void DirectAdmissionAndEarlyPagePlayAreBlockedUntilTheLastActualTutorialAction()
    {
        using (var t = new TutorialHarness())
        {
            for (int page = 0; page < t.View.PageCount - 1; page++)
            {
                Assert.That(t.Game.Controller.RequestStart(), Is.False);
                Assert.That(t.View.TryPlay(), Is.False);
                t.AssertUnused();
                Assert.That(t.View.TryNext(), Is.True);
            }
            Assert.That(t.Game.Controller.RequestStart(), Is.False, "Even the last page uses its explicit play action.");
            Assert.That(t.View.TryPlay(), Is.True);
            Assert.That(t.Game.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(t.Game.Admission.Access.Remaining, Is.Zero);
            Assert.That(t.Game.Preview.PresentationCue, Is.EqualTo("Stage"));
            Assert.That(t.Game.Preview.CanPlay, Is.False);
            Assert.That(t.View.PlayRequestCount, Is.EqualTo(1));
            Assert.That(t.View.TryPlay(), Is.False);
            Assert.That(t.Game.Controller.RequestStart(), Is.False);
            Assert.That(t.Game.Admission.LocalStartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void InitialCueCloseAndRepeatedBackRequestUseOneModalThenResumeTheSameCue()
    {
        using (var t = new TutorialHarness())
        {
            var h = t.Game; t.Play();
            Assert.That(t.OriginalClose.gameObject.activeInHierarchy, Is.True);
            string cue = h.Preview.PresentationCue;
            float progress = h.Preview.PresentationProgress;
            t.OriginalClose.onClick.Invoke();
            // TryRequestExit is also the existing coordinator's popup back/Escape handler.
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Exit.IsVisible, Is.True);
            Assert.That(h.Exit.ShowCount, Is.EqualTo(1));
            Assert.That(h.Clock.AdvanceTime(10), Is.False);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
            Assert.That(h.Preview.PresentationProgress, Is.EqualTo(progress));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.Exit.TryContinue(), Is.True);
            Assert.That(h.Exit.TryContinue(), Is.False);
            Assert.That(h.Clock.AdvanceTime(.1), Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
            Assert.That(h.Preview.PresentationProgress, Is.GreaterThan(progress));
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void RejectedPlayWhileTheViewContextIsUnavailableCanRetryWithoutConsumingTwice()
    {
        using (var t = new TutorialHarness())
        {
            t.Last();
            t.Game.Clock.SetApplicationFocused(false);
            Assert.That(t.View.TryPlay(), Is.False);
            Assert.That(t.View.PlayRequestCount, Is.Zero);
            t.AssertUnused();
            t.Game.Clock.SetApplicationFocused(true);
            Assert.That(t.Game.Controller.RequestStart(), Is.False, "A failed page action must not leave an admission bypass open.");
            Assert.That(t.View.TryPlay(), Is.True);
            Assert.That(t.View.PlayRequestCount, Is.EqualTo(1));
            Assert.That(t.Game.Admission.LocalStartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RejectedTutorialCloseKeepsItsButtonUsableForTheNextRealNavigationAttempt()
    {
        using (var t = new TutorialHarness())
        {
            t.Game.View.PopEnabled = false;
            Assert.That(t.View.TryClose(), Is.False);
            Assert.That(t.View.CloseRequestCount, Is.Zero);
            Assert.That(t.View.IsTutorialVisible, Is.True);
            t.Game.View.PopEnabled = true;
            Assert.That(t.View.TryClose(), Is.True);
            Assert.That(t.View.CloseRequestCount, Is.EqualTo(1));
            Assert.That(t.Game.Navigation.FirstView, Is.SameAs(t.Game.Map));
            t.AssertUnused();
        }
    }

    [Test]
    public void RepeatedTutorialCloseReentryThenPlayDoesNotAccumulateHandlersOrDuplicateApproval()
    {
        using (var t = new TutorialHarness())
        {
            for (int run = 1; run <= 3; run++)
            {
                Assert.That(t.View.TryClose(), Is.True);
                Assert.That(t.View.TryClose(), Is.False);
                Assert.That(t.View.CloseRequestCount, Is.EqualTo(run));
                t.AssertUnused(); t.Game.Reenter();
                Assert.That(t.OriginalClose.gameObject.activeInHierarchy, Is.False);
                Assert.That(t.TutorialClose.gameObject.activeInHierarchy, Is.True);
                Assert.That(t.Game.Root.GetComponent<PopupStoreStartPanel>().IsVisible, Is.False);
            }
            t.Play();
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(t.Game.Preview, t.Game.Clock);
            Assert.That(t.View.PlayRequestCount, Is.EqualTo(1));
            Assert.That(t.Game.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(t.Game.Admission.BindingCount, Is.EqualTo(1));
            Assert.That(t.Game.Preview.Session.PhaseGeneration, Is.EqualTo(1));
            t.Game.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void TutorialBlocksActualClockEntranceCounterAndPackingBeforeApproval()
    {
        using (var t = new TutorialHarness())
        {
            var h = t.Game;
            Assert.That(h.Clock.AdvanceTime(1000), Is.False);
            h.Entrance.TickEntrance(1000);
            Assert.That(h.Counter.TryAdd(h.Entries(0)[0]), Is.False);
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreCustomerPatience>().ExpiredCount, Is.Zero);
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(h.Preview.Session.PhaseGeneration, Is.Zero);
            t.AssertUnused();
        }
    }

    [Test]
    public void GameOverCannotCloseAsAbandonAndItsConfirmedResultReentersWithoutTheTutorial()
    {
        using (var t = new TutorialHarness())
        {
            var h = t.Game; t.Play();
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(h.Preview, h.Clock);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("GameOver"));
            Assert.That(h.View.TryRequestExit(), Is.False);
            Assert.That(h.Exit.IsVisible, Is.False);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            h.ShowResult();
            var result = h.Preview.Session.FinalResult;
            h.ConfirmToMap(); h.Reenter(); h.ShowResult();
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(result));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(t.View.IsTutorialVisible || t.View.IsCueVisible, Is.False);
            Assert.That(t.View.TryPlay(), Is.False);
            h.ConfirmToMap(); h.AssertSingleUseAndNoR1();
        }
    }

    private sealed class TutorialHarness : IDisposable
    {
        internal readonly Harness Game;
        internal readonly PopupStorePhasePreviewView View;
        internal readonly Button OriginalClose;
        internal Button TutorialClose => Field<Button>(View, "_closeButton");
        internal TMP_Text TicketStatus => Field<TMP_Text>(View, "_ticketStatusText");
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal TutorialHarness()
        {
            Game = new Harness(requestStart: false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                var root = Object.Instantiate(prefab, Game.Root.transform, false);
                root.SetActive(false); _fonts.BindBeforeActivation(root); root.SetActive(true);
                View = root.GetComponent<PopupStorePhasePreviewView>();
                OriginalClose = Field<Button>(Game.View, "_closeButton");
                Assert.That(Game.Preview.AttachView(View, OriginalClose), Is.True);
                Assert.That(View.IsTutorialVisible, Is.True);
                Assert.That(OriginalClose.gameObject.activeInHierarchy, Is.False);
                Assert.That(TutorialClose.gameObject.activeInHierarchy, Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal void Last() { while (View.TryNext()) { } Assert.That(View.CurrentPage, Is.EqualTo(View.PageCount - 1)); }
        internal void Play() { Last(); Assert.That(View.TryPlay(), Is.True); }
        internal void AssertUnused()
        {
            Assert.That(Game.Admission.LocalStartCount, Is.Zero);
            Assert.That(Game.Admission.Access.Remaining, Is.EqualTo(1));
            Assert.That(Game.Controller.IsApproved, Is.False);
            Assert.That(Game.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(Game.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Ready));
            Assert.That(Game.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
        public void Dispose() { Game?.Dispose(); _fonts.Dispose(); }
    }
    private static T Field<T>(Object obj, string field) where T : Object
        => new SerializedObject(obj).FindProperty(field).objectReferenceValue as T;
}
#endif
