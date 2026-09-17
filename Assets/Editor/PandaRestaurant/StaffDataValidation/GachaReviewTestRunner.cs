#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Execution;
using NUnit.Framework.Internal.Filters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Explicit local request bridge for only the collection review tests. Unity's usual EditMode
/// launcher saves/reloads user scenes, so it is intentionally not used here. NUnit executes
/// synchronous cases on the editor thread; the one audited null-yield UnityTest is advanced
/// on editor updates. New test GameObjects are redirected to an owned PreviewScene only
/// while test code is executing, then the previous creation target is restored immediately.
/// </summary>
[InitializeOnLoad]
public static class GachaReviewTestRunner
{
    private const string OutputDirectory = "Logs/GachaReview20260917-2213";
    private const string RequestPath = "Temp/GachaReview20260917-2213/tests-request.txt";
    private static readonly string[] FixtureNames =
    {
        "GachaEconomyTests", "EnhancementFairyTests", "GachaCollectionUiTests",
        "GachaCollectionPreviewSafetyTests", "GachaExchangeCatalogTests", "StaffStageMigrationCollectionTests"
    };
    private static readonly List<CaseResult> Cases = new List<CaseResult>();
    private static GachaCollectionPreviewSceneWitness _witness;
    private static Scene _testScene;
    private static IEnumerator _coroutine;
    private static string _started, _error, _group = "all";
    private static double _nextPoll, _coroutineStarted;
    private static bool _running;
    private static NUnitTestAssemblyRunner _runner;
    private static int _coroutineSteps;
    private static TestExecutionContext _coroutineContext;
    private static int _editorThread;
    public static bool IsRunning => _running;

    [Serializable] public sealed class CaseResult
    {
        public string name, status, message, stackTrace, source;
        public double seconds;
    }
    [Serializable] private sealed class Report
    {
        public bool success;
        public string group, startedUtc, finishedUtc, witnessBefore, witnessAfter, error;
        public int passed, failed, skipped, inconclusive, total;
        public string scope = "Selected review NUnit tests on main editor thread, NumberOfTestWorkers=0. New test GameObjects target a disposable PreviewScene. The audited PreviewSafety UnityTest runs its actual IEnumerator on editor updates; only null yields are permitted. No scene save, scene replacement, Play transition or production backend call.";
        public CaseResult[] tests;
    }

    static GachaReviewTestRunner()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += AbortForReload;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (_running)
        {
            if (_coroutine != null) AdvanceCoroutine();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextPoll) return;
        _nextPoll = now + .25;
        if (!File.Exists(RequestPath)) return;
        string command = File.ReadAllText(RequestPath).Trim(); File.Delete(RequestPath);
        try
        {
            if (command == "inspect") InspectCreationTarget();
            else if (command == "run") Begin("all");
            else if (command == "economy" || command == "fairy" || command == "ui") Begin(command);
            else throw new InvalidOperationException("Use inspect, economy, fairy, ui, or run for the related review tests.");
        }
        catch (Exception error) { Fail(error); }
    }

    private static void InspectCreationTarget()
    {
        Scene target = CreationTarget.Get();
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "tests-creation-target.txt"),
            "targetHandle=" + target.handle + "; valid=" + target.IsValid() + "; activeHandle=" +
            SceneManager.GetActiveScene().handle + "; " + GachaCollectionPreviewSceneWitness.Capture().Summary);
    }

    private static void Begin(string group)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("The related test bridge requires SDK-free Edit Mode.");
        if (GachaCollectionFairyReviewCapture.IsRunning)
            throw new InvalidOperationException("Finish fairy measurement before running tests.");
        var preview = GachaCollectionPreviewWindow.ActiveWindow;
        if (preview != null)
        {
            if (preview.IsRecordingVideo) throw new InvalidOperationException("Finish the preview recording before running tests.");
            preview.Close();
        }
        _running = true; _group = group; _started = DateTime.UtcNow.ToString("O"); _error = null; Cases.Clear();
        _editorThread = Thread.CurrentThread.ManagedThreadId;
        _witness = GachaCollectionPreviewSceneWitness.Capture();
        _testScene = EditorSceneManager.NewPreviewScene();
        Directory.CreateDirectory(OutputDirectory);
        using (new CreationTarget(_testScene))
        {
            var probe = new GameObject("Owned test scene routing probe");
            try
            {
                if (probe.scene.handle != _testScene.handle)
                    throw new InvalidOperationException("Unity did not redirect new test objects to the owned PreviewScene.");
            }
            finally { Object.DestroyImmediate(probe); }
            _witness.AssertUnchanged("owned test scene routing probe");
            RunSynchronousTests();
        }
        _witness.AssertUnchanged("synchronous related tests completed");
        if (_group != "all" && _group != "ui") { Complete(); return; }
        var fixture = new GachaCollectionPreviewSafetyTests();
        var method = new TestMethod(new MethodWrapper(typeof(GachaCollectionPreviewSafetyTests),
            nameof(GachaCollectionPreviewSafetyTests.IntegratedPreviewOpenClosePreservesCurrentSceneWorkspace)));
        _coroutineContext = new TestExecutionContext
        { CurrentTest = method, CurrentResult = method.MakeTestResult(), TestObject = fixture };
        _coroutine = fixture.IntegratedPreviewOpenClosePreservesCurrentSceneWorkspace();
        _coroutineStarted = EditorApplication.timeSinceStartup; _coroutineSteps = 0;
    }

    private static void RunSynchronousTests()
    {
        Assembly assembly = typeof(GachaReviewTestRunner).Assembly;
        var names = new List<string>();
        foreach (string name in FixtureNames)
        {
            if (_group == "fairy" && name != "EnhancementFairyTests") continue;
            if (_group == "economy" && name != "GachaEconomyTests" && name != "GachaExchangeCatalogTests" && name != "StaffStageMigrationCollectionTests") continue;
            if (_group == "ui" && name != "GachaCollectionUiTests" && name != "GachaCollectionPreviewSafetyTests") continue;
            Type fixture = assembly.GetType(name, true);
            foreach (MethodInfo method in fixture.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (name == "StaffStageMigrationCollectionTests" && !method.Name.StartsWith("EconomyBackend_", StringComparison.Ordinal)) continue;
                if (method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "UnityTestAttribute")) continue;
                if (method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "TestAttribute" ||
                    attribute.GetType().Name == "TestCaseAttribute" || attribute.GetType().Name == "TestCaseSourceAttribute"))
                    names.Add(fixture.FullName + "." + method.Name);
            }
        }
        if (names.Count == 0) throw new InvalidOperationException("The selected review fixtures contain no tests.");
        _runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        ITest tree = _runner.Load(assembly, new Dictionary<string, object>
        {
            { "NumberOfTestWorkers", 0 }, { "TestNames", names }, { "RunOnMainThread", true }
        });
        var methods = new HashSet<string>(names, StringComparer.Ordinal);
        string[] leaves = Flatten(tree).Where(test => !test.IsSuite &&
            methods.Contains(test.ClassName + "." + test.MethodName)).Select(test => test.FullName).ToArray();
        if (leaves.Length == 0) throw new InvalidOperationException("NUnit did not discover the selected review test cases.");
        var filter = new OrFilter(leaves.Select(name => (ITestFilter)new FullNameFilter(name)).ToArray());
        // In Unity's bundled NUnit, even zero workers dispatches NUnitTestAssemblyRunner.Run
        // through SimpleWorkItemDispatcher.RunnerThreadProc. Never call Run/RunAsync/Wait here.
        // Build the real NUnit work tree and execute it with an explicitly inline dispatcher.
        MethodInfo createContext = typeof(NUnitTestAssemblyRunner).GetMethod("CreateTestExecutionContext",
            BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo contextProperty = typeof(NUnitTestAssemblyRunner).GetProperty("Context",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (createContext == null || contextProperty == null)
            throw new NotSupportedException("The bundled NUnit execution context API was not found.");
        createContext.Invoke(_runner, new object[] { new Listener() });
        var context = (TestExecutionContext)contextProperty.GetValue(_runner);
        context.Dispatcher = new EditorThreadDispatcher();
        context.IsSingleThreaded = true;
        context.ParallelScope = NUnit.Framework.ParallelScope.None;
        context.TestCaseTimeout = 0;
        WorkItem work = WorkItem.CreateWorkItem(tree, filter);
        work.InitializeContext(context);
        TestExecutionContext previousContext = TestExecutionContext.CurrentContext;
        try { RequireEditorThread(); work.Execute(); }
        finally { SetExecutionContext(previousContext); }
        if (work.State.ToString() != "Complete")
            throw new InvalidOperationException("The inline NUnit work tree did not finish synchronously; no blocking wait will be used.");
        ITestResult result = work.Result;
        File.WriteAllText(Path.Combine(OutputDirectory, OutputPrefix + "-nunit.xml"), result.ToXml(true).OuterXml);
        _runner = null;
    }

    private static IEnumerable<ITest> Flatten(ITest test)
    {
        yield return test;
        foreach (ITest child in test.Tests ?? new List<ITest>())
            foreach (ITest descendant in Flatten(child)) yield return descendant;
    }

    private sealed class Listener : ITestListener
    {
        public void TestStarted(ITest test) { RequireEditorThread(); }
        public void TestOutput(TestOutput output) { }
        public void TestFinished(ITestResult result)
        {
            RequireEditorThread();
            if (result.Test.IsSuite) return;
            Cases.Add(new CaseResult { name = result.FullName, status = result.ResultState.Status.ToString(),
                message = result.Message, stackTrace = result.StackTrace, seconds = result.Duration, source = "NUnit" });
            _witness.AssertUnchanged(result.FullName);
        }
    }

    private sealed class EditorThreadDispatcher : IWorkItemDispatcher
    {
        private bool _cancelled;
        public void Dispatch(WorkItem work)
        {
            RequireEditorThread();
            if (_cancelled) { work.Cancel(false); return; }
            work.Execute();
        }
        public void CancelRun(bool force) { RequireEditorThread(); _cancelled = true; }
    }

    private static void RequireEditorThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != _editorThread)
            throw new InvalidOperationException("Review tests must execute on the original Unity editor thread.");
    }

    private static void AdvanceCoroutine()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("An unexpected Play transition occurred during an Edit-mode-only test.");
            bool next;
            TestExecutionContext previousContext = TestExecutionContext.CurrentContext;
            try
            {
                SetExecutionContext(_coroutineContext);
                using (new CreationTarget(_testScene)) next = _coroutine.MoveNext();
            }
            finally { SetExecutionContext(previousContext); }
            _coroutineSteps++;
            _witness.AssertUnchanged("preview UnityTest step " + _coroutineSteps);
            if (next)
            {
                if (_coroutine.Current != null)
                    throw new InvalidOperationException("The audited preview test yielded an unsupported instruction. No Play or scene instruction will be executed.");
                if (EditorApplication.timeSinceStartup - _coroutineStarted > 40)
                    throw new TimeoutException("The preview preservation UnityTest timed out.");
                return;
            }
            Cases.Add(new CaseResult
            {
                name = "GachaCollectionPreviewSafetyTests.IntegratedPreviewOpenClosePreservesCurrentSceneWorkspace",
                status = "Passed", seconds = EditorApplication.timeSinceStartup - _coroutineStarted,
                source = "Actual UnityTest IEnumerator, null yields on EditorApplication.update"
            });
            (_coroutine as IDisposable)?.Dispose(); _coroutine = null;
            Complete();
        }
        catch (Exception error)
        {
            Cases.Add(new CaseResult
            {
                name = "GachaCollectionPreviewSafetyTests.IntegratedPreviewOpenClosePreservesCurrentSceneWorkspace",
                status = "Failed", message = error.Message, stackTrace = error.ToString(),
                seconds = EditorApplication.timeSinceStartup - _coroutineStarted, source = "Actual UnityTest IEnumerator"
            });
            Fail(error);
        }
    }

    private static void Complete()
    {
        Cleanup(); _witness.AssertUnchanged("all related tests and test preview scene closed");
        WriteReport();
        Debug.Log("GACHA_REVIEW_TESTS_COMPLETED: " + Cases.Count + " cases; failed=" + Cases.Count(test => test.status == "Failed"));
    }

    private static void Fail(Exception error)
    {
        _error = error.ToString();
        try { Cleanup(); _witness?.AssertUnchanged("related test failure cleanup"); }
        catch (Exception cleanupError) { _error += "\nCleanup: " + cleanupError; }
        WriteReport(); Debug.LogError("GACHA_REVIEW_TESTS_FAILED: " + _error);
    }

    private static void Cleanup()
    {
        _running = false; _runner = null;
        try
        {
            if (_coroutine != null)
                using (new CreationTarget(_testScene)) (_coroutine as IDisposable)?.Dispose();
        }
        finally
        {
            _coroutine = null; _coroutineContext = null;
            if (_testScene.IsValid()) EditorSceneManager.ClosePreviewScene(_testScene);
            _testScene = default;
        }
    }

    private static void WriteReport()
    {
        Directory.CreateDirectory(OutputDirectory);
        var report = new Report
        {
            group = _group, startedUtc = _started, finishedUtc = DateTime.UtcNow.ToString("O"), error = _error,
            witnessBefore = _witness?.Summary, witnessAfter = GachaCollectionPreviewSceneWitness.Capture().Summary,
            tests = Cases.ToArray(), total = Cases.Count, passed = Cases.Count(test => test.status == "Passed"),
            failed = Cases.Count(test => test.status == "Failed"), skipped = Cases.Count(test => test.status == "Skipped"),
            inconclusive = Cases.Count(test => test.status == "Inconclusive")
        };
        report.success = string.IsNullOrEmpty(_error) && report.total > 0 && report.failed == 0 && report.skipped == 0 && report.inconclusive == 0;
        File.WriteAllText(Path.Combine(OutputDirectory, OutputPrefix + "-tests.json"), JsonUtility.ToJson(report, true));
    }

    private static string OutputPrefix => _group == "all" ? "related" : "related-" + _group;

    private static void SetExecutionContext(TestExecutionContext context)
    {
        // Unity's bundled NUnit exposes a public getter and an internal setter; preserve
        // its exact prior thread context instead of leaving a global fake context behind.
        MethodInfo setter = typeof(TestExecutionContext).GetProperty("CurrentContext",
            BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true);
        if (setter == null) throw new NotSupportedException("The bundled NUnit context setter is unavailable.");
        setter.Invoke(null, new object[] { context });
    }

    private static void AbortForReload()
    { if (_running) Fail(new InvalidOperationException("Compilation interrupted the explicit related test run.")); }

    private sealed class CreationTarget : IDisposable
    {
        private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo Getter = typeof(EditorSceneManager).GetMethod("GetTargetSceneForNewGameObjects", Flags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo Setter = typeof(EditorSceneManager).GetMethod("SetTargetSceneForNewGameObjects", Flags, null, new[] { typeof(Scene) }, null);
        private static readonly MethodInfo Clear = typeof(EditorSceneManager).GetMethod("ClearTargetSceneForNewGameObjects", Flags, null, Type.EmptyTypes, null);
        private readonly Scene _previous;
        public static Scene Get()
        {
            if (Getter == null || Setter == null || Clear == null)
                throw new NotSupportedException("This Unity version does not expose the safe new-object scene routing API.");
            return (Scene)Getter.Invoke(null, null);
        }
        public CreationTarget(Scene target)
        {
            _previous = Get();
            if (!target.IsValid() || !EditorSceneManager.IsPreviewScene(target))
                throw new InvalidOperationException("Only an owned PreviewScene may receive temporary test objects.");
            Setter.Invoke(null, new object[] { target });
        }
        public void Dispose()
        {
            if (_previous.IsValid()) Setter.Invoke(null, new object[] { _previous });
            else Clear.Invoke(null, null);
        }
    }
}
#endif
