#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TutorialAttendanceAndStaffEffectTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<Object> _created = new List<Object>();
    private bool _tutorialClear;
    private bool _tutorialActive;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void SetUp()
    {
        _tutorialClear = UserInfo.IsFirstTutorialClear;
        _tutorialActive = UserInfo.IsTutorialStart;
        _random = UnityEngine.Random.state;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
        UserInfo.IsFirstTutorialClear = _tutorialClear;
        UserInfo.IsTutorialStart = _tutorialActive;
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void AttendancePreparation_DoesNotEnterQueueUntilTutorialAndViewsAreFinished()
    {
        MainScene scene = CreateAttendanceScene(out var main, out var tutorial);
        IEnumerator wait = (IEnumerator)Invoke(scene, "WaitToScheduleAttendance");

        UserInfo.IsFirstTutorialClear = false;
        UserInfo.IsTutorialStart = false;
        Assert.That(wait.MoveNext(), Is.True, "Async preparation must wait even before IsTutorialStart is set");
        Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.False);
        UserInfo.IsTutorialStart = true;
        Assert.That(wait.MoveNext(), Is.True);
        UserInfo.IsFirstTutorialClear = true;
        Assert.That(wait.MoveNext(), Is.True, "Server completion alone does not mean tutorial presentation has closed");
        Assert.That(main.StateReads + tutorial.StateReads, Is.Zero,
            "Preparation/completion flags must short-circuit before reading any navigation state");
        UserInfo.IsTutorialStart = false;
        tutorial.ActiveViews.Add("UITutorial");
        Assert.That(wait.MoveNext(), Is.True);
        tutorial.ActiveViews.Clear();
        tutorial.Stable = false;
        Assert.That(wait.MoveNext(), Is.True, "An empty tutorial stack can still have a disappearing view");
        tutorial.Stable = true;
        main.ActiveViews.Add("UISetting");
        Assert.That(wait.MoveNext(), Is.True, "Do not interrupt an already open main view");
        main.ActiveViews.Clear();
        main.Stable = false;
        Assert.That(wait.MoveNext(), Is.True, "Wait for main view teardown animation too");
        main.Stable = true;
        Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(true));
        // The same scene cannot reserve another command after its first reservation.
        Set(scene, "_attendanceScheduled", true);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(((IEnumerator)Invoke(scene, "WaitToScheduleAttendance")).MoveNext(), Is.False);
        Assert.That(main.Pushes + tutorial.Pushes, Is.Zero, "Preparation and repeated checks do not show/grant attendance");
        Assert.That(main.NamedLookups + tutorial.NamedLookups, Is.Zero,
            "Waiting must not ask the main navigation for views registered only on tutorial navigation");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkipCleanup_RealViewsRemoveTextAndTransitionThenQueueExactlyOneAttendance(bool hidingConfirmation)
    {
        var backendField = typeof(BackendManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object backendBefore = backendField.GetValue(null);
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        try
        {
            var roots = preview.GetRootGameObjects();
            var npc = roots.SelectMany(root => root.GetComponentsInChildren<UITutorialDescriptionNPC>(true)).Single();
            var guide = roots.SelectMany(root => root.GetComponentsInChildren<UITutorial>(true)).Single();
            var skip = Get<UITutorialSkip>(npc, "_tutorialSkip");
            npc.transform.SetParent(null, false);
            guide.transform.SetParent(null, false);
            skip.transform.SetParent(null, false);
            var fixture = NewObject("detached native tutorial cleanup");
            fixture.SetActive(false);
            var nav = fixture.AddComponent<MobileUINavigation>();
            var first = fixture.AddComponent<FirstTutorial>();
            Set(nav, "_viewDic", new Dictionary<string, MobileUIView>
            {
                ["UITutorial"] = guide, ["UITutorialDescription"] = npc, ["UITutorialSkip"] = skip
            });
            Set(skip, "_uiNav", nav);
            Set(first, "_uiNav", nav);
            Set(first, "_uiTutorial", guide);
            Set(first, "_uiDescriptionNPC", npc);
            Set(first, "_viewsOpened", true);
            guide.Show();
            npc.Show();
            npc.ShowGuidanceText("last tutorial message");
            skip.gameObject.SetActive(true);
            var active = Get<List<MobileUIView>>(nav, "_activeViewList");
            active.AddRange(new MobileUIView[] { guide, npc, skip });
            Action lateAnimation = null;
            if (hidingConfirmation)
            {
                skip.Hide();
                lateAnimation = Get<Dictionary<int, Action>>(Get<RectTransform>(skip, "_animeUI").GetComponent<Muks.Tween.TweenData>(), "_onCompletedDic").Values.Single();
            }
            else skip.VisibleState = VisibleState.Appeared;
            Assert.That(nav.Count, Is.EqualTo(3));

            MainScene mainScene = CreateAttendanceScene(out var main, out _);
            Set(mainScene, "_uiTutorialNav", nav);
            WithHeldAttendanceQueue((scheduler, queue) =>
            {
                UserInfo.IsFirstTutorialClear = true; // confirmed state; receipt behavior tested separately
                UserInfo.IsTutorialStart = true;
                var waiter = (IEnumerator)Invoke(mainScene, "WaitToScheduleAttendance");
                Assert.That(waiter.MoveNext(), Is.True);
                Invoke(first, "CloseTutorialViews");
                Invoke(first, "CloseTutorialViews"); // idempotent completion/disable cleanup
                lateAnimation?.Invoke();
                Assert.That(nav.Count, Is.Zero);
                Assert.That(nav.ViewsVisibleStateCheck(), Is.True);
                foreach (var view in new MobileUIView[] { guide, npc, skip })
                {
                    Assert.That(view.gameObject.activeSelf, Is.False);
                    Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Disappeared));
                }
                foreach (string field in new[] { "_descriptionText1", "_descriptionText2", "_descriptionText3", "_descriptionText4" })
                    Assert.That(Get<UIImageAndText>(npc, field).gameObject.activeSelf, Is.False);
                Assert.That(Get<Button>(npc, "_screenButton").gameObject.activeSelf, Is.False);
                Assert.That(Get<Button>(npc, "_skipButton").gameObject.activeSelf, Is.False);
                Assert.That(((IEnumerable)queue).Cast<object>(), Is.Empty, "Completion UI must finish before attendance enters the queue");
                UserInfo.IsTutorialStart = false;
                Assert.That(waiter.MoveNext(), Is.True);
                object command = ((IEnumerable)queue).Cast<object>().Single();
                Assert.That(Get<Func<bool>>(command, "CanExecute")(), Is.True);
                Get<Action>(command, "Execute")();
                Get<Action>(command, "Execute")();
                Assert.That(main.Pushes, Is.EqualTo(1));
                Assert.That(main.ActiveViews, Is.EquivalentTo(new[] { "UIAttendance" }));
            });
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            Assert.That(backendField.GetValue(null), Is.SameAs(backendBefore), "No SDK initialization during UI regression");
        }
    }

    [Test]
    public void SkipConfirmation_RemovesTransitionBeforeInvokingCompletionAndConsumesCallbackOnce()
    {
        var host = NewObject("native skip callback ordering");
        host.SetActive(false);
        var nav = host.AddComponent<MobileUINavigation>();
        var skip = NewObject("confirmation", host.transform).AddComponent<UITutorialSkip>();
        Set(skip, "_animeUI", (RectTransform)NewObject("panel", skip.transform).transform);
        Set(skip, "_canvasGroup", skip.gameObject.AddComponent<CanvasGroup>());
        Set(skip, "_uiNav", nav);
        Set(nav, "_viewDic", new Dictionary<string, MobileUIView> { ["UITutorialSkip"] = skip });
        Get<List<MobileUIView>>(nav, "_activeViewList").Add(skip);
        skip.VisibleState = VisibleState.Appeared;
        int calls = 0;
        Set(skip, "_onOkButtonClicked", (Action)(() =>
        {
            calls++;
            Assert.That(nav.Count, Is.Zero);
            Assert.That(nav.ViewsVisibleStateCheck(), Is.True);
            Assert.That(skip.gameObject.activeSelf, Is.False);
        }));
        Invoke(skip, "OnOkButtonClicked");
        Invoke(skip, "OnOkButtonClicked");
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void AttendanceQueue_SceneResetReleasesExecutionLeaseForNextScene()
    {
        WithHeldAttendanceQueue((scheduler, queue) =>
        {
            Assert.That(Get<bool>(scheduler, "_isExecuting"), Is.True);
            Invoke(scheduler, "ResetCommand");
            Assert.That(Get<bool>(scheduler, "_isExecuting"), Is.False);
            Assert.That(((IEnumerable)queue).Cast<object>(), Is.Empty);
            Assert.That(Get<Canvas>(scheduler, "_dontTouchCanvas").gameObject.activeSelf, Is.False);
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void AttendanceMissingNavigation_FailsClosedWithoutReservation(bool missingMain, bool missingTutorial)
    {
        MainScene scene = CreateAttendanceScene(out var main, out var tutorial);
        if (missingMain) Set(scene, "_uiMainNav", null);
        if (missingTutorial) Set(scene, "_uiTutorialNav", null);
        UserInfo.IsFirstTutorialClear = true;
        UserInfo.IsTutorialStart = false;
        Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(false));
        Assert.That(((IEnumerator)Invoke(scene, "WaitToScheduleAttendance")).MoveNext(), Is.True);
        Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.False);
        Assert.That(main.Pushes + tutorial.Pushes + main.NamedLookups + tutorial.NamedLookups, Is.Zero);
    }

    [Test]
    public void AttendanceQueue_ReservesOnceAndRechecksViewsAndCompletionWithoutGrantingRewards()
    {
        MainScene scene = CreateAttendanceScene(out var main, out var tutorial);
        WithHeldAttendanceQueue((scheduler, queue) =>
        {
            UserInfo.IsFirstTutorialClear = true;
            UserInfo.IsTutorialStart = false;
            IEnumerator wait = (IEnumerator)Invoke(scene, "WaitToScheduleAttendance");
            Assert.That(wait.MoveNext(), Is.True, "Wait for the accepted command to be handled");
            Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.True);
            Assert.That(((IEnumerator)Invoke(scene, "WaitToScheduleAttendance")).MoveNext(), Is.False);
            object command = ((IEnumerable)queue).Cast<object>().Single();
            Assert.That(Get<int>(scheduler, "_commandCounter"), Is.EqualTo(1), "Repeated entry must not enqueue another command");
            Action execute = Get<Action>(command, "Execute");
            Func<bool> canExecute = Get<Func<bool>>(command, "CanExecute");
            Func<bool> finished = Get<Func<bool>>(command, "IsFinished");

            tutorial.ActiveViews.Add("UITutorialDescription");
            Assert.That(canExecute(), Is.False);
            tutorial.ActiveViews.Clear();
            tutorial.Stable = false;
            Assert.That(canExecute(), Is.False);
            tutorial.Stable = true;
            main.ActiveViews.Add("UISetting");
            Assert.That(canExecute(), Is.False);
            main.ActiveViews.Clear();
            main.Stable = false;
            Assert.That(canExecute(), Is.False);
            main.Stable = true;
            Assert.That(main.Pushes, Is.Zero, "The queue does not execute while a view is open or transitioning");

            Assert.That(canExecute(), Is.True);
            RemoveHeldCommand(queue, command);
            execute();
            Assert.That(main.Pushes, Is.EqualTo(1));
            Assert.That(main.ActiveViews, Is.EquivalentTo(new[] { "UIAttendance" }));
            Assert.That(finished(), Is.False);
            execute();
            Assert.That(main.Pushes, Is.EqualTo(1), "Repeated callback cannot push a second attendance window");
            main.ActiveViews.Clear();
            Assert.That(finished(), Is.True);
            Assert.That(wait.MoveNext(), Is.False, "The successful reservation completes rather than scheduling again");
            Assert.That(((IEnumerable)queue).Cast<object>(), Is.Empty);
            Assert.That(tutorial.NamedLookups, Is.Zero);
        });
    }

    [TestCase("not-completed")]
    [TestCase("tutorial-running")]
    [TestCase("main-reference")]
    [TestCase("tutorial-reference")]
    public void AttendanceQueue_ChangedPreparationReleasesOldCommandAndWaitsOutsideQueue(string changedState)
    {
        MainScene scene = CreateAttendanceScene(out var main, out var tutorial);
        WithHeldAttendanceQueue((scheduler, queue) =>
        {
            UserInfo.IsFirstTutorialClear = true;
            UserInfo.IsTutorialStart = false;
            IEnumerator wait = (IEnumerator)Invoke(scene, "WaitToScheduleAttendance");
            Assert.That(wait.MoveNext(), Is.True);
            object oldCommand = ((IEnumerable)queue).Cast<object>().Single();
            Action oldExecute = Get<Action>(oldCommand, "Execute");
            Func<bool> oldCanExecute = Get<Func<bool>>(oldCommand, "CanExecute");
            Func<bool> oldFinished = Get<Func<bool>>(oldCommand, "IsFinished");
            switch (changedState)
            {
                case "not-completed": UserInfo.IsFirstTutorialClear = false; break;
                case "tutorial-running": UserInfo.IsTutorialStart = true; break;
                case "main-reference": Set(scene, "_uiMainNav", null); break;
                case "tutorial-reference": Set(scene, "_uiTutorialNav", null); break;
                default: Assert.Fail("Unexpected test state"); break;
            }
            Assert.That(oldCanExecute(), Is.True,
                "A queued attendance command must release its slot instead of blocking tutorial preparation behind it");
            RemoveHeldCommand(queue, oldCommand);
            oldExecute();
            Assert.That(main.Pushes, Is.Zero);
            Assert.That(oldFinished(), Is.True);
            Assert.That(wait.MoveNext(), Is.True, "Preparation now waits in the scene coroutine, outside the command queue");
            Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.False);
            Assert.That(((IEnumerable)queue).Cast<object>(), Is.Empty);
            Assert.That(wait.MoveNext(), Is.True);
            Assert.That(Get<int>(scheduler, "_commandCounter"), Is.EqualTo(1));

            UserInfo.IsFirstTutorialClear = true;
            UserInfo.IsTutorialStart = false;
            Set(scene, "_uiMainNav", main);
            Set(scene, "_uiTutorialNav", tutorial);
            Assert.That(wait.MoveNext(), Is.True);
            object newCommand = ((IEnumerable)queue).Cast<object>().Single();
            Assert.That(newCommand, Is.Not.SameAs(oldCommand));
            Assert.That(Get<int>(scheduler, "_commandCounter"), Is.EqualTo(2));
            oldExecute();
            Assert.That(main.Pushes, Is.Zero, "A late duplicate of the abandoned command must remain a no-op");
            Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.True);
            Assert.That(((IEnumerable)queue).Cast<object>().Count(), Is.EqualTo(1));
            Assert.That(Get<Func<bool>>(newCommand, "CanExecute")(), Is.True);
            RemoveHeldCommand(queue, newCommand);
            Get<Action>(newCommand, "Execute")();
            Assert.That(main.Pushes, Is.EqualTo(1));
            oldExecute();
            Assert.That(main.Pushes, Is.EqualTo(1));
            main.ActiveViews.Clear();
            Assert.That(Get<Func<bool>>(newCommand, "IsFinished")(), Is.True);
            Assert.That(wait.MoveNext(), Is.False);
            Assert.That(((IEnumerable)queue).Cast<object>(), Is.Empty);
        });
    }

    private void WithHeldAttendanceQueue(Action<SequentialCommandManager, object> test)
    {
        var instanceField = typeof(SequentialCommandManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var attendanceTimeField = typeof(UserInfo).GetField("_lastAttendanceTime", BindingFlags.Static | BindingFlags.NonPublic);
        object originalScheduler = instanceField.GetValue(null);
        object originalAttendanceTime = attendanceTimeField.GetValue(null);
        long money = UserInfo.Money;
        int diamonds = UserInfo.Dia, attendanceDays = UserInfo.TotalAttendanceDays;
        try
        {
            // Hold the real queue without Awake, coroutines, a singleton lookup, or SDK time.
            var host = NewObject("held attendance queue");
            host.SetActive(false);
            var scheduler = host.AddComponent<SequentialCommandManager>();
            Type commandType = typeof(SequentialCommandManager).GetNestedType("Command", BindingFlags.NonPublic);
            Type comparerType = typeof(SequentialCommandManager).GetNestedType("CommandComparer", BindingFlags.NonPublic);
            object comparer = Activator.CreateInstance(comparerType, true);
            object queue = Activator.CreateInstance(typeof(SortedSet<>).MakeGenericType(commandType), comparer);
            Set(scheduler, "_commandQueue", queue);
            Set(scheduler, "_dontTouchCanvas", NewObject("queue input shield", host.transform).AddComponent<Canvas>());
            Set(scheduler, "_isExecuting", true);
            instanceField.SetValue(null, scheduler);
            attendanceTimeField.SetValue(null, string.Empty); // Due without consulting BackendManager.ServerTime.
            test(scheduler, queue);
            Assert.That(UserInfo.Money, Is.EqualTo(money));
            Assert.That(UserInfo.Dia, Is.EqualTo(diamonds));
            Assert.That(UserInfo.TotalAttendanceDays, Is.EqualTo(attendanceDays));
            Assert.That(UserInfo.LastAttendanceTime, Is.Empty, "Showing attendance does not receive a reward");
        }
        finally
        {
            instanceField.SetValue(null, originalScheduler);
            attendanceTimeField.SetValue(null, originalAttendanceTime);
        }
    }

    private static void RemoveHeldCommand(object queue, object command)
    {
        // SequentialCommandManager removes its command when execution begins. Tests
        // preserve that order without starting an EditMode coroutine or UI input.
        Assert.That(queue.GetType().GetMethod("Remove").Invoke(queue, new[] { command }), Is.EqualTo(true));
    }

    [Test]
    public void AttendanceSceneRegistration_UsesRealSeparateStage1NavigationsAndWaitsForTheirViews()
    {
        var backendField = typeof(BackendManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object backendBefore = backendField.GetValue(null);
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        try
        {
            MainScene scene = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainScene>(true)).Single();
            var main = Get<UINavigation>(scene, "_uiMainNav") as MobileUINavigation;
            var tutorial = Get<UINavigation>(scene, "_uiTutorialNav") as MobileUINavigation;
            Assert.That(main, Is.Not.Null);
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(tutorial, Is.Not.SameAs(main));
            FirstTutorial first = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FirstTutorial>(true)).Single();
            Assert.That(Get<MobileUINavigation>(first, "_uiNav"), Is.SameAs(tutorial));
            var mainRegistered = Get<MobileViewDicStruct[]>(main, "_uiViews").ToDictionary(item => item.Name, item => item.UIView);
            var tutorialRegistered = Get<MobileViewDicStruct[]>(tutorial, "_uiViews").ToDictionary(item => item.Name, item => item.UIView);
            Assert.That(mainRegistered.ContainsKey("UIAttendance"), Is.True);
            Assert.That(mainRegistered.ContainsKey("UITutorial") || mainRegistered.ContainsKey("UITutorialDescription"), Is.False);
            Assert.That(tutorialRegistered.ContainsKey("UITutorial") && tutorialRegistered.ContainsKey("UITutorialDescription"), Is.True);
            // Use the actual serialized registrations and actual navigation methods, but do
            // not call Init/ViewInit/Awake: those initialize product UI/backend dependencies.
            Set(main, "_viewDic", mainRegistered);
            Set(tutorial, "_viewDic", tutorialRegistered);
            var mainActive = Get<List<MobileUIView>>(main, "_activeViewList");
            var tutorialActive = Get<List<MobileUIView>>(tutorial, "_activeViewList");
            mainActive.Clear(); tutorialActive.Clear();
            foreach (MobileUIView view in mainRegistered.Values.Concat(tutorialRegistered.Values))
                view.VisibleState = VisibleState.Disappeared;

            UserInfo.IsFirstTutorialClear = false;
            UserInfo.IsTutorialStart = false;
            Assert.That(((IEnumerator)Invoke(scene, "WaitToScheduleAttendance")).MoveNext(), Is.True,
                "Real main navigation must never be queried for unregistered tutorial names");
            UserInfo.IsFirstTutorialClear = true;
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(true));
            MobileUIView guide = tutorialRegistered["UITutorialDescription"];
            guide.VisibleState = VisibleState.Appeared;
            tutorialActive.Add(guide);
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(false));
            tutorialActive.Clear();
            guide.VisibleState = VisibleState.Disappearing;
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(false));
            guide.VisibleState = VisibleState.Disappeared;
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(true));
            MobileUIView attendance = mainRegistered["UIAttendance"];
            mainActive.Add(attendance);
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(false));
            mainActive.Clear();
            attendance.VisibleState = VisibleState.Appearing;
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(false));
            attendance.VisibleState = VisibleState.Disappeared;
            Assert.That(Invoke(scene, "IsAttendanceReady"), Is.EqualTo(true));
            Assert.That(Get<bool>(scene, "_attendanceScheduled"), Is.False);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            Assert.That(backendField.GetValue(null), Is.SameAs(backendBefore), "Scene inspection must not initialize an SDK owner");
        }
    }

    private MainScene CreateAttendanceScene(out AttendanceTestNavigation main, out AttendanceTestNavigation tutorial)
    {
        var host = NewObject("attendance scheduling");
        host.SetActive(false);
        var scene = host.AddComponent<MainScene>();
        main = host.AddComponent<AttendanceTestNavigation>();
        main.RegisteredNames.UnionWith(new[] { "UIAttendance", "UISetting" });
        tutorial = host.AddComponent<AttendanceTestNavigation>();
        tutorial.RegisteredNames.UnionWith(new[] { "UITutorial", "UITutorialDescription" });
        Set(scene, "_uiMainNav", main);
        Set(scene, "_uiTutorialNav", tutorial);
        return scene;
    }

    [Test]
    public void MarketerEffect_BindsBeforeFirstEquipAndOnlyVisibleFloorOwnsTheFlame()
    {
        var viewObject = NewObject("marketer view");
        viewObject.SetActive(false);
        var view = viewObject.AddComponent<UIMarketerImage>();
        var flame = NewObject("authored Skill Effect", viewObject.transform).AddComponent<Image>();
        Set(view, "_marketerSkillEffect", flame);
        Invoke(view, "Awake");
        Assert.That(flame.gameObject.activeSelf, Is.False);

        var first = NewObject("Floor1 marketer").AddComponent<StaffMarketer>();
        var second = NewObject("Floor2 marketer").AddComponent<StaffMarketer>();
        first.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
        second.SetSkillEffect(flame, ERestaurantFloorType.Floor2);
        Assert.That(flame.gameObject.activeSelf, Is.False, "Binding without equipped data must not light the authored flame");
        StaffData toruru = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/STAFF11.asset");
        Assert.That(toruru.Id, Is.EqualTo("STAFF11"));
        Assert.That(toruru.Name, Is.EqualTo("토루루"));
        Set(first, "_staffData", toruru);
        Set(second, "_staffData", toruru);
        Set(view, "_currentFloor", ERestaurantFloorType.Floor1);

        Invoke(second, "SkillEffectSetActive", true);
        Assert.That(flame.gameObject.activeSelf, Is.False, "Another floor cannot light the current floor");
        Invoke(first, "OnStartFeverEvent");
        Assert.That(flame.gameObject.activeSelf, Is.True);
        Invoke(second, "SkillEffectSetActive", false);
        Assert.That(flame.gameObject.activeSelf, Is.True, "Another floor cannot extinguish the visible active effect");
        Invoke(first, "OnEndFeverEvent");
        Assert.That(flame.gameObject.activeSelf, Is.False);
        Invoke(first, "SkillEffectSetActive", true);
        Invoke(view, "OnDisable");
        Assert.That(flame.gameObject.activeSelf, Is.False);
        view.RefreshSkillEffect();
        Assert.That(flame.gameObject.activeSelf, Is.True, "Reopened UI reads its existing source, not an authored default");
        Invoke(first, "CancelActiveSkill", StaffSkillCancellationReason.ObjectPoolDespawned, true);
        Assert.That(flame.gameObject.activeSelf, Is.False);
        first.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
        Assert.That(flame.gameObject.activeSelf, Is.False, "Pool reuse must not revive the old visual request");
        Invoke(first, "SkillEffectSetActive", true);
        view.UnregisterSkillEffect(first);
        Assert.That(flame.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void RegisteredStaff_CommonSkillAndFeverEffectsEndOnCancellationAndDoNotChangeAssetValues()
    {
        StaffData[] staffAssets = AssetDatabase.FindAssets("t:StaffData", new[] { "Assets/Resources/StaffData" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<StaffData>)
            .Where(data => data != null).ToArray();
        Assert.That(staffAssets.Length, Is.GreaterThanOrEqualTo(32));
        foreach (StaffData data in staffAssets)
        {
            string original = EditorJsonUtility.ToJson(data);
            var obj = NewObject(data.Id + " effect probe");
            Staff staff = data is MarketerData ? obj.AddComponent<StaffMarketer>() : obj.AddComponent<Staff>();
            Set(staff, "_staffData", data);
            GameObject effect = NewObject("effect", obj.transform);
            if (staff is StaffMarketer marketer)
                marketer.SetSkillEffect(effect.AddComponent<Image>(), ERestaurantFloorType.Floor1);
            else Set(staff, "_skillEffect", effect.AddComponent<SpriteRenderer>());
            Invoke(staff, "SkillEffectSetActive", false);
            Assert.That(effect.activeSelf, Is.False, data.Id + " idle");
            Invoke(staff, "OnStartFeverEvent");
            Assert.That(effect.activeSelf, Is.True, data.Id + " fever");
            Set(staff, "_usingSkill", true);
            Invoke(staff, "OnEndFeverEvent");
            Assert.That(effect.activeSelf, Is.True, data.Id + " an active skill survives fever end");
            Invoke(staff, "CancelActiveSkill", StaffSkillCancellationReason.NormalDurationCompleted, false);
            Assert.That(effect.activeSelf, Is.False, data.Id + " skill duration complete");
            Invoke(staff, "SkillEffectSetActive", true);
            Invoke(staff, "CancelActiveSkill", StaffSkillCancellationReason.GameObjectDisabled, true);
            Assert.That(effect.activeSelf, Is.False, data.Id + " disabled/reused");
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(original), data.Id + " data unchanged");
        }
    }

    [Test]
    public void StaffPool_RepeatedSpawnSubscribesOnceAndDespawnRemovesItsHandlers()
    {
        var managerObject = NewObject("detached skill manager");
        managerObject.SetActive(false);
        GameManager manager = managerObject.AddComponent<GameManager>();
        Staff staff = NewObject("pooled staff").AddComponent<Staff>();
        Set(staff, "_gameManager", manager);
        staff.ObjectPoolSpawnEvent();
        staff.ObjectPoolSpawnEvent();
        var upgradeField = typeof(UserInfo).GetField("OnUpgradeStaffHandler", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(((Delegate)upgradeField.GetValue(null)).GetInvocationList().Count(item => ReferenceEquals(item.Target, staff)), Is.EqualTo(1));
        Assert.That(Get<Delegate>(manager, "OnChangeStaffSkillValueHandler").GetInvocationList().Count(item => ReferenceEquals(item.Target, staff)), Is.EqualTo(1));
        staff.ObjectPoolDespawnEvent();
        var upgrade = (Delegate)upgradeField.GetValue(null);
        Assert.That(upgrade == null || upgrade.GetInvocationList().All(item => !ReferenceEquals(item.Target, staff)), Is.True);
        var skill = Get<Delegate>(manager, "OnChangeStaffSkillValueHandler");
        Assert.That(skill == null || skill.GetInvocationList().All(item => !ReferenceEquals(item.Target, staff)), Is.True);
    }

    [Test]
    public void StaffEffectLifecycle_GameObjectDisableAndReenableDoNotReviveMarketerFlame()
    {
        AssertEffectProbeIsIsolated(() =>
        {
            UIMarketerImage view = CreateEffectOwner(out Image flame);
            var first = NewObject("actual Floor1 marketer lifetime").AddComponent<StaffMarketer>();
            var second = NewObject("independent Floor2 marketer lifetime").AddComponent<StaffMarketer>();
            StaffData data = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/STAFF11.asset");
            Set(first, "_staffData", data);
            Set(second, "_staffData", data);
            // Staff has no Awake/Update. Only this detached component receives real
            // Unity edit-mode lifetime messages; no scene/controller is activated.
            first.runInEditMode = true;
            first.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
            second.SetSkillEffect(flame, ERestaurantFloorType.Floor2);
            Invoke(first, "SkillEffectSetActive", true);
            Assert.That(flame.gameObject.activeSelf, Is.True);

            first.gameObject.SetActive(false);
            Assert.That(Get<bool>(first, "_skillEffectRequested"), Is.False,
                "SetActive must deliver OnDisable and clear the source, not merely hide its parent");
            Assert.That(flame.gameObject.activeSelf, Is.False);
            first.gameObject.SetActive(true);
            first.SetStaffData(data, ERestaurantFloorType.Floor1);
            first.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
            Assert.That(flame.gameObject.activeSelf, Is.False, "Re-enable/rebind cannot revive a stale skill request");

            Invoke(second, "SkillEffectSetActive", true);
            Assert.That(flame.gameObject.activeSelf, Is.False, "Another floor remains independent");
            Invoke(first, "SkillEffectSetActive", true);
            Object.DestroyImmediate(first.gameObject);
            Assert.That(flame.gameObject.activeSelf, Is.False, "Actual destruction removes the visible source");
            Assert.That(Get<Dictionary<ERestaurantFloorType, StaffMarketer>>(view, "_skillEffectSources").Keys,
                Is.EquivalentTo(new[] { ERestaurantFloorType.Floor2 }));
        });
    }

    [Test]
    public void StaffEffectLifecycle_SameDataReassignmentAndSkillEndPreserveOnlyCurrentFever()
    {
        AssertEffectProbeIsIsolated(() =>
        {
            var feverObject = NewObject("inactive visual-only fever dependency");
            feverObject.SetActive(false);
            var fever = feverObject.AddComponent<FeverSystem>();
            var context = new FeverRuntimeContext();
            Set(fever, "_feverRuntimeContext", context);
            foreach (string id in new[] { "STAFF06", "STAFF11" })
            {
                StaffData data = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/" + id + ".asset");
                string assetBefore = EditorJsonUtility.ToJson(data);
                var host = NewObject(id + " same-data reassignment");
                Staff staff = data is MarketerData ? host.AddComponent<StaffMarketer>() : host.AddComponent<Staff>();
                Set(staff, "_staffData", data);
                Set(staff, "_feverSystem", fever);
                GameObject effect;
                if (staff is StaffMarketer marketer)
                {
                    CreateEffectOwner(out Image flame);
                    marketer.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
                    effect = flame.gameObject;
                }
                else
                {
                    effect = NewObject("common staff flame", host.transform);
                    Set(staff, "_skillEffect", effect.AddComponent<SpriteRenderer>());
                }

                Assert.That(context.TryActivate(15f, out FeverRuntimeToken token), Is.True);
                Set(fever, "_activeFeverToken", token);
                try
                {
                    // No FeverStart/skill coroutine: those call customers, audio, and
                    // gameplay systems. Exercise the actual same-data public entry.
                    staff.SetStaffData(data, ERestaurantFloorType.Floor1);
                    Assert.That(effect.activeSelf, Is.True, id + " reassigned during active fever");
                    Set(staff, "_usingSkill", true);
                    Invoke(staff, "CancelActiveSkill", StaffSkillCancellationReason.NormalDurationCompleted, false);
                    Assert.That(Get<bool>(staff, "_usingSkill"), Is.False);
                    Assert.That(effect.activeSelf, Is.True, id + " skill completion must preserve continuing fever");
                    Assert.That(context.Deactivate(token), Is.True);
                    Invoke(staff, "OnEndFeverEvent");
                    Assert.That(effect.activeSelf, Is.False, id + " actual fever state ended");
                    staff.SetStaffData(data, ERestaurantFloorType.Floor1);
                    Assert.That(effect.activeSelf, Is.False, id + " reassign after fever must remain idle");
                    Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(assetBefore));
                }
                finally
                {
                    context.Deactivate(token);
                    Set(fever, "_activeFeverToken", default(FeverRuntimeToken));
                }
            }
        });
    }

    [Test]
    public void StaffEffectLifecycle_PoolDespawnAndReuseClearFlameAndKeepSubscriptionsSingular()
    {
        AssertEffectProbeIsIsolated(() =>
        {
            var managerObject = NewObject("inactive pooled effect manager");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            UIMarketerImage view = CreateEffectOwner(out Image flame);
            var staff = NewObject("pooled marketer lifecycle").AddComponent<StaffMarketer>();
            StaffData data = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/STAFF11.asset");
            Set(staff, "_staffData", data);
            Set(staff, "_gameManager", manager);
            staff.runInEditMode = true;
            try
            {
                staff.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
                staff.ObjectPoolSpawnEvent();
                staff.ObjectPoolSpawnEvent();
                AssertStaffPoolSubscriptions(staff, manager, 1);
                Invoke(staff, "SkillEffectSetActive", true);
                Assert.That(flame.gameObject.activeSelf, Is.True);
                staff.ObjectPoolDespawnEvent();
                Assert.That(flame.gameObject.activeSelf, Is.False);
                Assert.That(Get<bool>(staff, "_skillEffectRequested"), Is.False);
                AssertStaffPoolSubscriptions(staff, manager, 0);

                staff.gameObject.SetActive(false);
                staff.gameObject.SetActive(true);
                staff.ObjectPoolSpawnEvent();
                staff.ObjectPoolSpawnEvent();
                staff.SetSkillEffect(flame, ERestaurantFloorType.Floor1);
                staff.SetStaffData(data, ERestaurantFloorType.Floor1);
                AssertStaffPoolSubscriptions(staff, manager, 1);
                Assert.That(flame.gameObject.activeSelf, Is.False, "The reused instance starts without its previous effect");
                Invoke(staff, "OnStartFeverEvent");
                Assert.That(flame.gameObject.activeSelf, Is.True);
                Invoke(staff, "OnEndFeverEvent");
                Assert.That(flame.gameObject.activeSelf, Is.False);
            }
            finally
            {
                staff.ObjectPoolDespawnEvent();
                AssertStaffPoolSubscriptions(staff, manager, 0);
                Object.DestroyImmediate(staff.gameObject);
                Assert.That(Get<Dictionary<ERestaurantFloorType, StaffMarketer>>(view, "_skillEffectSources"), Is.Empty);
            }
        });
    }

    private UIMarketerImage CreateEffectOwner(out Image flame)
    {
        var host = NewObject("inactive detached marketer effect owner");
        host.SetActive(false);
        var owner = host.AddComponent<UIMarketerImage>();
        flame = NewObject("detached flame", host.transform).AddComponent<Image>();
        Set(owner, "_marketerSkillEffect", flame);
        Set(owner, "_currentFloor", ERestaurantFloorType.Floor1);
        Invoke(owner, "Awake");
        return owner;
    }

    private static void AssertStaffPoolSubscriptions(Staff staff, GameManager manager, int expected)
    {
        var upgrade = (Delegate)typeof(UserInfo).GetField("OnUpgradeStaffHandler", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var scene = (Delegate)typeof(LoadingSceneManager).GetField("OnLoadSceneHandler", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var skill = Get<Delegate>(manager, "OnChangeStaffSkillValueHandler");
        foreach (Delegate handlers in new[] { upgrade, scene, skill })
            Assert.That(handlers == null ? 0 : handlers.GetInvocationList().Count(item => ReferenceEquals(item.Target, staff)), Is.EqualTo(expected));
    }

    private static void AssertEffectProbeIsIsolated(Action probe)
    {
        var backend = typeof(BackendManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var game = typeof(GameManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var upgrade = typeof(UserInfo).GetField("OnUpgradeStaffHandler", BindingFlags.Static | BindingFlags.NonPublic);
        var scene = typeof(LoadingSceneManager).GetField("OnLoadSceneHandler", BindingFlags.Static | BindingFlags.NonPublic);
        object backendBefore = backend.GetValue(null), gameBefore = game.GetValue(null);
        object upgradeBefore = upgrade.GetValue(null), sceneBefore = scene.GetValue(null);
        long money = UserInfo.Money;
        int diamonds = UserInfo.Dia, skinToken = UserInfo.SkinToken, gachaCount = UserInfo.TotalUseGachaMachineCount;
        bool clear = UserInfo.IsFirstTutorialClear, active = UserInfo.IsTutorialStart;
        try { probe(); }
        finally
        {
            Assert.That(backend.GetValue(null), Is.SameAs(backendBefore), "Effect fixtures must not initialize SDK ownership");
            Assert.That(game.GetValue(null), Is.SameAs(gameBefore), "Detached managers must not replace the live singleton");
            Assert.That(upgrade.GetValue(null), Is.EqualTo(upgradeBefore));
            Assert.That(scene.GetValue(null), Is.EqualTo(sceneBefore));
            Assert.That(UserInfo.Money, Is.EqualTo(money));
            Assert.That(UserInfo.Dia, Is.EqualTo(diamonds));
            Assert.That(UserInfo.SkinToken, Is.EqualTo(skinToken));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(gachaCount));
            Assert.That(UserInfo.IsFirstTutorialClear, Is.EqualTo(clear));
            Assert.That(UserInfo.IsTutorialStart, Is.EqualTo(active));
        }
    }

    [Test]
    public void PointerLifecycle_HiddenTutorialRejectsDelayedOrderCursorAndReopensClean()
    {
        WithTutorialPointer((tutorial, waitParent) =>
        {
            tutorial.OrderHoleSetActive(true);
            HoleClickHandler hole = Get<HoleClickHandler>(tutorial, "_orderHole");
            GameObject cursor = Get<GameObject>(tutorial, "_orderHoleCursor");
            Assert.That(cursor.activeSelf, Is.False);
            Action delayed = CapturePointerDelay(tutorial, waitParent, "OnOrderHoleAnimeCompleted");

            tutorial.Hide();
            delayed(); // Real queued product callback, delivered after the owner closed.
            Assert.That(cursor.activeSelf, Is.False, "A delayed completed-hole callback must not revive a hidden cursor");
            Assert.That(hole.Interactable, Is.False);
            tutorial.Show();
            tutorial.PunchHoleSetActive(true);
            Assert.That(cursor.activeInHierarchy, Is.False, "A new guide starts without the previous order pointer");
            Assert.That(Get<RectTransform>(hole, "_parent").gameObject.activeSelf, Is.False);
            delayed(); // An old callback must also be rejected after reopening the same view.
            Assert.That(cursor.activeSelf, Is.False);

            tutorial.OrderHoleSetActive(true);
            delayed(); // Reusing even the same hole cannot make the old callback current.
            Assert.That(cursor.activeSelf, Is.False);
            CapturePointerDelay(tutorial, waitParent, "OnOrderHoleAnimeCompleted")();
            Assert.That(cursor.activeInHierarchy, Is.True);
            Assert.That(hole.Interactable, Is.True);
        });
    }

    [TestCase("ShopMask", "shopHole", "shopMaskCursor")]
    [TestCase("AddCustomerHole", "addCustomerHole", "addCustomerHoleCursor")]
    [TestCase("TableHole", "tableHole", "tableHoleCursor")]
    [TestCase("KitchenHole", "kitchenHole", "kitchenHoleCursor")]
    [TestCase("RecipeHole", "recipeHole", "recipeHoleCursor")]
    [TestCase("BuyHole", "buyHole", "buyHoleCursor")]
    [TestCase("ExitHole", "exitHole", "exitHoleCursor")]
    [TestCase("BackHole", "backHole", "backHoleCursor")]
    [TestCase("CustomerGuideHole", "customerGuideHole", "customerGuideHoleCursor")]
    [TestCase("OrderHole", "orderHole", "orderHoleCursor")]
    [TestCase("ServingHole", "servingHole", "servingHoleCursor")]
    [TestCase("Table1Hole", "table1Hole", "table1HoleCursor")]
    public void PointerLifecycle_ReactivatedHoleRejectsPreviousDelayAndAcceptsCurrentAnimation(
        string api, string holeField, string cursorField)
    {
        WithTutorialPointer((tutorial, waitParent) =>
        {
            HoleClickHandler hole = Get<HoleClickHandler>(tutorial, "_" + holeField);
            GameObject cursor = Get<GameObject>(tutorial, "_" + cursorField);
            Invoke(tutorial, api + "SetActive", true);
            Action previous = CapturePointerDelay(tutorial, waitParent, "On" + api + "AnimeCompleted");
            Invoke(tutorial, api + "SetActive", false);
            Invoke(tutorial, api + "SetActive", true);
            previous();
            Assert.That(cursor.activeSelf, Is.False, "Off/on in the same view invalidates the prior animation's delay");
            Assert.That(hole.Interactable, Is.False);
            if (api == "Table1Hole") Assert.That(Get<Button>(tutorial, "_table1Button").gameObject.activeSelf, Is.False);
            if (api == "ShopMask") Assert.That(Get<Button>(tutorial, "_shopButton").interactable, Is.False);

            CapturePointerDelay(tutorial, waitParent, "On" + api + "AnimeCompleted")();
            Assert.That(cursor.activeInHierarchy, Is.True, "The current completed animation still reveals its authored cursor");
            Assert.That(hole.Interactable, Is.True);
            if (api == "Table1Hole") Assert.That(Get<Button>(tutorial, "_table1Button").gameObject.activeSelf, Is.True);
            if (api == "ShopMask") Assert.That(Get<Button>(tutorial, "_shopButton").interactable, Is.True);
        });
    }

    [Test]
    public void PointerLifecycle_CustomHoleRetargetRejectsOldDelayAndPreservesCurrentPositionAndDirection()
    {
        WithTutorialPointer((tutorial, waitParent) =>
        {
            var firstTarget = (RectTransform)NewObject("first tutorial UI target").transform;
            firstTarget.position = new Vector3(150, 230, 0);
            var nextTarget = (RectTransform)NewObject("next tutorial UI target").transform;
            nextTarget.position = new Vector3(420, 360, 0);
            tutorial.CustomHoleSetActive(true, 180, firstTarget.name, firstTarget, true);
            Action previous = CapturePointerDelay(tutorial, waitParent, "OnCustomHoleAnimeCompleted");
            tutorial.CustomHoleHide();
            tutorial.CustomHoleSetActive(true, 240, nextTarget.name, nextTarget, false);
            RectTransform cursor = Get<RectTransform>(tutorial, "_customHoleCursorParent");
            HoleClickHandler hole = Get<HoleClickHandler>(tutorial, "_customHole");
            previous();
            Assert.That(cursor.gameObject.activeSelf, Is.False, "The old target must not reveal the next target prematurely");
            Assert.That(cursor.position, Is.EqualTo(nextTarget.position));
            Assert.That(hole.HoleRect.position, Is.EqualTo(nextTarget.position));
            Assert.That(cursor.sizeDelta, Is.EqualTo(Vector2.one * 240));

            CapturePointerDelay(tutorial, waitParent, "OnCustomHoleAnimeCompleted")();
            Assert.That(cursor.gameObject.activeInHierarchy, Is.True);
            Assert.That(Get<GameObject>(tutorial, "_customHoleCursorUp").activeSelf, Is.False);
            Assert.That(Get<GameObject>(tutorial, "_customHoleCursorDown").activeSelf, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PointerLifecycle_OverlayOrObjectDisableClearsRevealedCursorAndInvalidatesPendingDelay(bool disableObject)
    {
        WithTutorialPointer((tutorial, waitParent) =>
        {
            tutorial.OrderHoleSetActive(true);
            Action previous = CapturePointerDelay(tutorial, waitParent, "OnOrderHoleAnimeCompleted");
            previous();
            GameObject cursor = Get<GameObject>(tutorial, "_orderHoleCursor");
            Assert.That(cursor.activeInHierarchy, Is.True);
            if (disableObject)
            {
                // Only the detached UI component receives real edit-mode lifetime
                // messages; no manager, tutorial controller, or account is started.
                tutorial.runInEditMode = true;
                tutorial.gameObject.SetActive(false);
                tutorial.gameObject.SetActive(true);
            }
            else
            {
                tutorial.PunchHoleSetActive(false);
                tutorial.PunchHoleSetActive(true);
            }
            previous();
            Assert.That(cursor.activeSelf, Is.False);
            Assert.That(Get<HoleClickHandler>(tutorial, "_orderHole").Interactable, Is.False);
            if (disableObject)
            {
                Assert.That(Get<GameObject>(tutorial, "_uiPunchHole").activeSelf, Is.False,
                    "Re-enabling the component must not restore the previous screen's highlight");
                Assert.That(Get<Button>(tutorial, "_screenButton").gameObject.activeSelf, Is.False);
                // A new tutorial step explicitly opts into a new highlight;
                // simply enabling the old view may not revive one over a mini-game.
                tutorial.PunchHoleSetActive(true);
            }
            tutorial.OrderHoleSetActive(true);
            previous();
            Assert.That(cursor.activeSelf, Is.False);
            CapturePointerDelay(tutorial, waitParent, "OnOrderHoleAnimeCompleted")();
            Assert.That(cursor.activeInHierarchy, Is.True);
        });
    }

    private void WithTutorialPointer(Action<UITutorial, GameObject> test)
    {
        var backendField = typeof(BackendManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object backendBefore = backendField.GetValue(null);
        var waitParentField = typeof(Muks.Tween.Tween).GetField("_waitQueueParent", BindingFlags.Static | BindingFlags.NonPublic);
        var waitQueueField = typeof(Muks.Tween.Tween).GetField("_tweenWaitQueue", BindingFlags.Static | BindingFlags.NonPublic);
        object originalWaitParent = waitParentField.GetValue(null), originalWaitQueue = waitQueueField.GetValue(null);
        long money = UserInfo.Money;
        int diamonds = UserInfo.Dia, gachaCount = UserInfo.TotalUseGachaMachineCount;
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        try
        {
            // Reuse actual UI references detached from navigation/managers. Do not
            // call Init, FirstTutorial, SDK, or a reward/purchase entrypoint.
            UITutorial tutorial = preview.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UITutorial>(true)).Single();
            tutorial.transform.SetParent(null, false);
            tutorial.transform.localScale = Vector3.one;
            var waitParent = NewObject("isolated existing Tween.Wait queue");
            waitParentField.SetValue(null, waitParent);
            waitQueueField.SetValue(null, new Queue<Muks.Tween.TweenWait>());
            tutorial.Show();
            tutorial.PunchHoleSetActive(true);
            test(tutorial, waitParent);
        }
        finally
        {
            waitParentField.SetValue(null, originalWaitParent);
            waitQueueField.SetValue(null, originalWaitQueue);
            EditorSceneManager.ClosePreviewScene(preview);
            Assert.That(backendField.GetValue(null), Is.SameAs(backendBefore));
            Assert.That(UserInfo.Money, Is.EqualTo(money));
            Assert.That(UserInfo.Dia, Is.EqualTo(diamonds));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(gachaCount));
        }
    }

    [Test]
    public void PointerLifecycle_DestroyedViewRejectsPendingDelayWithoutAccessingDestroyedReferences()
    {
        WithTutorialPointer((tutorial, waitParent) =>
        {
            tutorial.OrderHoleSetActive(true);
            Action delayed = CapturePointerDelay(tutorial, waitParent, "OnOrderHoleAnimeCompleted");
            tutorial.runInEditMode = true;
            Object.DestroyImmediate(tutorial.gameObject);
            Assert.DoesNotThrow(() => delayed());
        });
    }

    private static Action CapturePointerDelay(UITutorial tutorial, GameObject waitParent, string completedMethod)
    {
        int previousCount = waitParent.GetComponentsInChildren<Muks.Tween.TweenWait>(true).Length;
        Invoke(tutorial, completedMethod);
        var waits = waitParent.GetComponentsInChildren<Muks.Tween.TweenWait>(true);
        Assert.That(waits.Length, Is.EqualTo(previousCount + 1));
        Action delayed = Get<Action>(waits.Last(), "_onCompleted");
        Assert.That(delayed, Is.Not.Null);
        return delayed;
    }

    [Test]
    public void ExistingPointerAssets_KeepOriginalFramesLoopAndPrefabGeometry()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Tutorial/Cursor/Cursor_Idle.anim");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animation/Tutorial/Cursor/CursorAnimator.controller");
        Assert.That(controller.layers[0].stateMachine.defaultState.motion, Is.SameAs(clip));
        Assert.That(controller.layers[0].stateMachine.defaultState.speed, Is.EqualTo(1));
        Assert.That(clip.frameRate, Is.EqualTo(2));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
        var curve = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
        Assert.That(curve.Select(frame => frame.time), Is.EqualTo(new[] { 0f, 1f, 2f }));
        Assert.That(curve[0].value, Is.SameAs(curve[2].value));
        Assert.That(curve[1].value, Is.Not.SameAs(curve[0].value));
        var cursor = NewObject("original cursor sample").AddComponent<Image>();
        var animator = cursor.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        foreach (var frame in curve)
        {
            // Exercise the real non-legacy controller/binding. SampleAnimation alone
            // does not bind this UI PPtr curve on a fresh Image in EditMode. Sample
            // inside each held frame instead of exactly on a frame transition.
            animator.Play("Base Layer.Cursor_Idle", 0, (frame.time + 0.1f) / clip.length);
            animator.Update(0f);
            Assert.That(cursor.sprite, Is.SameAs(frame.value));
        }
        var touch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Manager/TouchManager/UITouchImage.prefab");
        var rect = touch.GetComponent<RectTransform>();
        Assert.That(rect.localScale, Is.EqualTo(Vector3.one * 0.5f));
        Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.one * 250));
        Assert.That(rect.pivot, Is.EqualTo(Vector2.one * 0.5f));
        Assert.That(touch.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
    }

    private GameObject NewObject(string name, Transform parent = null)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        if (parent != null) obj.transform.SetParent(parent, false);
        _created.Add(obj);
        return obj;
    }

    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, InstanceFields | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static object Invoke(object target, string name, params object[] args)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(name, InstanceFields | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().Name, name);
    }
}

public sealed class AttendanceTestNavigation : UINavigation
{
    public bool Stable = true;
    public int Pushes;
    public int StateReads;
    public int NamedLookups;
    public readonly HashSet<string> RegisteredNames = new HashSet<string>();
    public readonly HashSet<string> ActiveViews = new HashSet<string>();
    public override int Count { get { StateReads++; return ActiveViews.Count; } }
    public override bool IsViewsInactive => Count == 0;
    public override UIView FirstView => null;
    public override void Push(string name) { RequireRegistered(name); Pushes++; ActiveViews.Add(name); }
    public override void PushNoAnime(string name) => Push(name);
    public override void Pop() { }
    public override void Pop(string name) { }
    public override void AllPop() { }
    public override void PopNoAnime(string name) { }
    public override void AllShow() { }
    public override void AllHide() { }
    public override bool CheckActiveView(string name) { RequireRegistered(name); return ActiveViews.Contains(name); }
    public override VisibleState GetVisibleState(string name) { RequireRegistered(name); return ActiveViews.Contains(name) ? VisibleState.Appeared : VisibleState.Disappeared; }
    public override bool ViewsVisibleStateCheck() { StateReads++; return Stable; }
    private void RequireRegistered(string name)
    {
        NamedLookups++;
        if (!RegisteredNames.Contains(name)) throw new InvalidOperationException("Unregistered view queried: " + name);
    }
}
#endif
