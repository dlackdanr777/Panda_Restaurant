#if UNITY_EDITOR
using System;
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

/// <summary>
/// Explicit request-only bridge for the fifteen synchronous, memory-only popup store fixtures.
/// Never launches Play, creates a scene, saves assets, restores a workspace or starts another Editor.
/// </summary>
[InitializeOnLoad]
public static class PopupStoreVerificationRunner
{
    private const string DirectoryPath = "Temp/PopupStoreVerification";
    private const string RequestPath = DirectoryPath + "/request.txt";
    private static readonly string[] Fixtures =
    {
        "PopupStoreNavigationTests", "PopupStoreMaterialTests", "PopupStoreSalesCounterTests",
        "PopupStoreOrderTests", "PopupStoreOrderMatchTests", "PopupStoreExactOrderMatchTests",
        "PopupStorePackingTests", "PopupStorePackingPresentationTests", "PopupStoreDeliveryTests", "PopupStoreCustomerEntranceTests",
        "PopupStoreCustomerDepartureTests", "PopupStoreOrderSupplyTests", "PopupStoreBusinessClockTests", "PopupStoreCustomerPatienceTests", "PopupStoreComplaintTests"
    };
    private static bool _running, _abort;
    private static double _nextPoll;
    private static int _editorThread;
    private static Report _report;
    private static readonly List<CaseResult> Cases = new List<CaseResult>();
    private static readonly List<LogRecord> Logs = new List<LogRecord>();

    [Serializable] private sealed class LogRecord { public string type, message, stackTrace; }
    [Serializable] private sealed class CaseResult
    {
        public string id, fullName, status, message, stackTrace;
        public double seconds;
    }
    [Serializable] private sealed class Workspace
    {
        public string setup, activeScene, dirty, selection, startScene, undo, roots;
    }
    [Serializable] private sealed class Inspection
    {
        public string capturedUtc, error;
        public bool playing, compiling, updating, sdkInitialized, isLogin;
        public int[] gameManagerIds, challengeManagerIds;
        public Workspace workspace;
    }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, error;
        public string scope = "Fifteen popup-store fixtures, synchronous NUnit on the existing Editor main thread. Memory objects only; no scene creation/replacement/save, Play transition or account operation. A workspace difference stops further cases without automatic rollback.";
        public bool success, compiling, updating, scriptCompilationFailed, workspaceUnchanged;
        public int discovered, total, passed, failed, skipped, inconclusive;
        public Workspace before, after;
        public CaseResult[] tests;
        public LogRecord[] logs;
    }

    static PopupStoreVerificationRunner() => EditorApplication.update += Tick;

    private static void Tick()
    {
        if (_running || EditorApplication.isCompiling || EditorApplication.isUpdating
            || GachaReviewTestRunner.IsRunning) return;
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + .5;
        if (!File.Exists(RequestPath)) return;
        string request;
        try
        {
            request = File.ReadAllText(RequestPath).Trim();
            if (request != "inspect" && EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath); // Consume only this explicit request; never retry a failed run automatically.
        }
        catch (IOException) { return; } // A writer may still own the request file.
        if (request == "inspect") InspectWorkspace();
        else Run(request);
    }

    private static void InspectWorkspace()
    {
        var inspection = new Inspection { capturedUtc = DateTime.UtcNow.ToString("O"),
            playing = EditorApplication.isPlaying, compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating };
        try
        {
            inspection.sdkInitialized = BackEnd.Backend.IsInitialized;
            inspection.isLogin = BackEnd.Backend.IsLogin;
            inspection.gameManagerIds = Resources.FindObjectsOfTypeAll<GameManager>()
                .Where(manager => manager.gameObject.scene.IsValid()).Select(manager => manager.GetInstanceID()).OrderBy(id => id).ToArray();
            inspection.challengeManagerIds = Resources.FindObjectsOfTypeAll<ChallengeManager>()
                .Where(manager => manager.gameObject.scene.IsValid()).Select(manager => manager.GetInstanceID()).OrderBy(id => id).ToArray();
            inspection.workspace = ReadWorkspace();
        }
        catch (Exception error) { inspection.error = Unwrap(error).ToString(); }
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/inspection.json", JsonUtility.ToJson(inspection, true));
    }

    private static void Run(string request)
    {
        _running = true; _abort = false; Cases.Clear(); Logs.Clear();
        _editorThread = Thread.CurrentThread.ManagedThreadId;
        _report = new Report
        {
            startedUtc = DateTime.UtcNow.ToString("O"), compiling = EditorApplication.isCompiling,
            updating = EditorApplication.isUpdating, scriptCompilationFailed = EditorUtility.scriptCompilationFailed
        };
        Application.logMessageReceived += CaptureLog;
        try
        {
            if (request != "run") throw new InvalidOperationException("Only the explicit request 'run' is supported.");
            if (_report.compiling || _report.updating || _report.scriptCompilationFailed)
                throw new InvalidOperationException("Compilation must finish successfully before popup-store verification.");
            var activeRunMethod = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeRunMethod == null || (bool)activeRunMethod.Invoke(null, null))
                throw new InvalidOperationException("The existing Unity Test Runner must be idle; only its status is read, never its scene-changing launcher.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin
                || Muks.BackEnd.BackendManager.ExistingInstance != null)
                throw new InvalidOperationException("Verification requires idle Edit Mode without a live backend account.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab editing before this memory-only verification; no automatic closing is performed.");
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => scene.isDirty))
                throw new InvalidOperationException("A loaded, unmodified edit scene is required. User scenes will not be saved or discarded.");
            _report.before = ReadWorkspace();
            ExecuteFixtures();
        }
        catch (Exception error) { _report.error = Unwrap(error).ToString(); }
        finally
        {
            try
            {
                _report.after = ReadWorkspace();
                _report.workspaceUnchanged = _report.before != null && Same(_report.before, _report.after);
                if (_report.before != null && !_report.workspaceUnchanged)
                    _report.error = (_report.error ?? "") + "\nWorkspace changed. No automatic restoration was attempted.";
            }
            catch (Exception error) { _report.error = (_report.error ?? "") + "\nWorkspace evidence: " + Unwrap(error); }
            Application.logMessageReceived -= CaptureLog;
            _report.tests = Cases.ToArray(); _report.logs = Logs.ToArray();
            _report.total = Cases.Count;
            _report.passed = Cases.Count(test => test.status == "Passed");
            _report.failed = Cases.Count(test => test.status == "Failed");
            _report.skipped = Cases.Count(test => test.status == "Skipped");
            _report.inconclusive = Cases.Count(test => test.status == "Inconclusive");
            _report.finishedUtc = DateTime.UtcNow.ToString("O");
            _report.success = string.IsNullOrEmpty(_report.error) && _report.workspaceUnchanged
                && _report.total > 0 && _report.total == _report.discovered && _report.passed == _report.total;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(DirectoryPath + "/result.json", JsonUtility.ToJson(_report, true));
            }
            finally { _running = false; }
            Debug.Log("POPUP_STORE_VERIFICATION_COMPLETED: success=" + _report.success + "; total=" + _report.total
                + "; passed=" + _report.passed + "; failed=" + _report.failed + "; result=" + DirectoryPath + "/result.json");
        }
    }

    private static void ExecuteFixtures()
    {
        var assembly = typeof(PopupStoreVerificationRunner).Assembly;
        var names = new List<string>();
        foreach (string fixtureName in Fixtures)
        {
            Type fixture = assembly.GetType(fixtureName, true);
            int beforeCount = names.Count;
            foreach (var method in fixture.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                string[] attributes = method.GetCustomAttributes(false).Select(attribute => attribute.GetType().Name).ToArray();
                if (attributes.Contains("UnityTestAttribute"))
                    throw new NotSupportedException("Coroutine tests are outside this synchronous memory-only bridge: " + method.Name);
                if (!attributes.Any(name => name == "TestAttribute" || name == "TestCaseAttribute" || name == "TestCaseSourceAttribute")) continue;
                if (method.ReturnType != typeof(void))
                    throw new NotSupportedException("Only synchronous void tests are allowed: " + method.Name);
                names.Add(fixture.FullName + "." + method.Name);
            }
            if (names.Count == beforeCount) throw new InvalidOperationException("No cases found in " + fixtureName);
        }
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        ITest tree = runner.Load(assembly, new Dictionary<string, object>
        {
            { "NumberOfTestWorkers", 0 }, { "TestNames", names }, { "RunOnMainThread", true }
        });
        var methods = new HashSet<string>(names, StringComparer.Ordinal);
        string[] leaves = Flatten(tree).Where(test => !test.IsSuite && methods.Contains(test.ClassName + "." + test.MethodName))
            .Select(test => test.FullName).ToArray();
        _report.discovered = leaves.Length;
        if (leaves.Length == 0) throw new InvalidOperationException("NUnit discovered no popup-store cases.");
        var filter = new OrFilter(leaves.Select(name => (ITestFilter)new FullNameFilter(name)).ToArray());
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo createContext = typeof(NUnitTestAssemblyRunner).GetMethod("CreateTestExecutionContext", flags);
        PropertyInfo contextProperty = typeof(NUnitTestAssemblyRunner).GetProperty("Context", flags);
        if (createContext == null || contextProperty == null)
            throw new NotSupportedException("The installed NUnit inline execution API is unavailable.");
        using (var listener = new Listener())
        {
            createContext.Invoke(runner, new object[] { listener });
            var context = (TestExecutionContext)contextProperty.GetValue(runner);
            context.Dispatcher = new InlineDispatcher(); context.IsSingleThreaded = true;
            context.ParallelScope = NUnit.Framework.ParallelScope.None; context.TestCaseTimeout = 0;
            WorkItem work = WorkItem.CreateWorkItem(tree, filter);
            work.InitializeContext(context);
            TestExecutionContext previous = TestExecutionContext.CurrentContext;
            try { RequireEditorThread(); work.Execute(); }
            finally { SetExecutionContext(previous); }
            if (work.State.ToString() != "Complete")
                throw new InvalidOperationException("NUnit did not finish synchronously; no background or blocking execution is allowed.");
            if (work.Result.ResultState.Status != TestStatus.Passed)
                _report.error = (_report.error ?? "") + "\nNUnit assembly result: " + work.Result.ResultState
                    + "\n" + work.Result.Message;
        }
    }

    private sealed class Listener : ITestListener, IDisposable
    {
        private IDisposable _logScope;
        public void TestStarted(ITest test)
        {
            RequireEditorThread();
            if (test.IsSuite) return;
            Type type = typeof(UnityEngine.TestTools.LogAssert).Assembly.GetType("UnityEngine.TestTools.Logging.LogScope", true);
            _logScope = (IDisposable)Activator.CreateInstance(type, true);
        }
        public void TestOutput(TestOutput output) { }
        public void TestFinished(ITestResult result)
        {
            RequireEditorThread();
            if (result.Test.IsSuite) return;
            try
            {
                if (_logScope != null) _logScope.GetType().GetMethod("EvaluateLogScope", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(_logScope, new object[] { true });
            }
            catch (Exception error) { MarkFailure(result, "Unexpected or missing Unity diagnostic: " + Unwrap(error)); }
            finally { Dispose(); }
            try
            {
                if (!Same(_report.before, ReadWorkspace()))
                    throw new InvalidOperationException("Workspace changed after " + result.FullName + "; subsequent cases are cancelled without rollback.");
            }
            catch (Exception error)
            {
                _abort = true;
                MarkFailure(result, Unwrap(error).ToString());
                _report.error = (_report.error ?? "") + "\n" + Unwrap(error);
            }
            Cases.Add(new CaseResult { id = result.Test.Id, fullName = result.FullName, status = result.ResultState.Status.ToString(),
                message = result.Message, stackTrace = result.StackTrace, seconds = result.Duration });
        }
        public void Dispose() { _logScope?.Dispose(); _logScope = null; }
    }

    private sealed class InlineDispatcher : IWorkItemDispatcher
    {
        public void Dispatch(WorkItem work)
        {
            RequireEditorThread();
            if (_abort) { work.Cancel(false); return; }
            work.Execute();
        }
        public void CancelRun(bool force) { RequireEditorThread(); _abort = true; }
    }

    private static Workspace ReadWorkspace()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var method = typeof(Undo).GetMethod("GetRecords", flags, null, new[] { typeof(List<string>), typeof(int).MakeByRefType() }, null);
        if (method == null) throw new NotSupportedException("Undo history evidence is unavailable; verification cannot claim workspace preservation.");
        var records = new List<string>(); var arguments = new object[] { records, 0 };
        method.Invoke(null, arguments);
        return new Workspace
        {
            setup = string.Join("\n", EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive)),
            activeScene = SceneManager.GetActiveScene().handle + "|" + SceneManager.GetActiveScene().path,
            dirty = string.Join("\n", scenes.Select(scene => scene.handle + "|" + scene.path + "|" + scene.isDirty)),
            roots = string.Join("\n", scenes.Select(scene => scene.handle + "|" + (scene.isLoaded
                ? string.Join(",", scene.GetRootGameObjects().Select(root => root.GetInstanceID()).OrderBy(id => id)) : "unloaded"))),
            selection = string.Join(",", Selection.instanceIDs) + "|" + Selection.activeInstanceID + "|" + (Selection.activeContext == null ? 0 : Selection.activeContext.GetInstanceID()),
            startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
            undo = arguments[1] + "|" + string.Join("\n", records)
        };
    }
    private static bool Same(Workspace first, Workspace second) => JsonUtility.ToJson(first) == JsonUtility.ToJson(second);
    private static void CaptureLog(string message, string stackTrace, LogType type)
        => Logs.Add(new LogRecord { type = type.ToString(), message = message, stackTrace = stackTrace });
    private static void MarkFailure(ITestResult result, string message)
    {
        if (result is TestResult mutable) mutable.SetResult(ResultState.Failure, (result.Message ?? "") + "\n" + message, result.StackTrace);
        else throw new InvalidOperationException(message);
    }
    private static Exception Unwrap(Exception error) => error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error;
    private static IEnumerable<ITest> Flatten(ITest test)
    {
        yield return test;
        foreach (var child in test.Tests ?? new List<ITest>()) foreach (var nested in Flatten(child)) yield return nested;
    }
    private static void RequireEditorThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != _editorThread) throw new InvalidOperationException("Popup-store tests must remain on the Editor main thread.");
    }
    private static void SetExecutionContext(TestExecutionContext context)
    {
        var setter = typeof(TestExecutionContext).GetProperty("CurrentContext", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true);
        if (setter == null) throw new NotSupportedException("The installed NUnit context setter is unavailable.");
        setter.Invoke(null, new object[] { context });
    }
}
#endif
