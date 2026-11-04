#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Explicit screenshots of an already running original memory Play. Never controls gameplay or focus.</summary>
public static class PopupStoreGameViewCapture
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Folder = "Temp/PopupStoreVerification/UIEvidence";
    private static readonly Vector2Int[] Resolutions = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2340, 1080) };
    private static Capture _capture;
    public static bool IsActive => _capture != null;
    public static string LastReportPath { get; private set; }

    [Serializable]
    public sealed class ViewState
    {
        public int instanceId, selectedIndex, renderWidth, renderHeight;
        public string group, selectedSize;
        public string[] sizes;
        public Vector2 scale, translation;
        public float defaultScale;
        public Rect position;
        public bool maximized, lowResolution;
    }
    [Serializable]
    public sealed class Screenshot
    {
        public string path, capturedUtc, sessionId, businessState, currentGrade;
        public int width, height, frame, score, successfulOrders, finalizations, draws;
        public double remainingSeconds;
    }
    [Serializable]
    public sealed class Report
    {
        public string scope = "Actual original Stage1 memory Play GameView screenshots; no injected scores, orders, time, input, or rewards.";
        public string startedUtc, finishedUtc, label, error;
        public bool success, selectionUnchanged, gameViewRestored, userGameViewChangeDetected;
        public ViewState before, after;
        public List<Screenshot> screenshots = new List<Screenshot>();
        public List<string> retainedTemporarySizes = new List<string>();
    }

    /// <summary>Called explicitly by the Editor verification request bridge while the existing Game tab is focused.</summary>
    public static void Begin(string label)
    {
        if (_capture != null) throw new InvalidOperationException("A GameView capture is already running.");
        if (label != "start" && label != "play" && label != "result")
            throw new ArgumentException("Capture label must be start, play, or result.", nameof(label));
        if (!SafeOwner()) throw new InvalidOperationException("Capture requires the existing original memory Play with no live SDK/account.");
        var capture = new Capture(label);
        _capture = capture;
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeQuit;
        try { capture.ObserveInput(); capture.SelectNext(); }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static bool SafeOwner()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorUtility.scriptCompilationFailed || !CustomerPartyManualPlayBootstrap.Active
            || !SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin
            || !SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).isLoaded) return false;
        var memory = typeof(CustomerPartyStage1Preview).GetField("memory", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as CustomerPartyOwnedMemory;
        var owner = Muks.BackEnd.BackendManager.ExistingInstance;
        return memory != null && memory.LoggedIn && owner != null && owner.IsEditorOfflineOwner
            && ReferenceEquals(owner, memory.Owner);
    }

    private static void Update()
    {
        if (_capture == null) return;
        try
        {
            if (!SafeOwner()) throw new InvalidOperationException("The isolated Play owner is no longer available.");
            if (_capture.Step()) Finish(null);
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            Finish("Play ended before all requested screenshots completed.");
    }
    private static void BeforeReload() => Finish("Assembly reload interrupted capture.");
    private static void BeforeQuit() => Finish("Editor shutdown interrupted capture.");

    private static void Finish(string error)
    {
        var capture = _capture;
        if (capture == null) return;
        _capture = null;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        EditorApplication.quitting -= BeforeQuit;
        capture.StopObservingInput();
        try { capture.Restore(); }
        catch (Exception exception) { error = Join(error, "GameView restoration: " + exception); }
        capture.Result.error = error;
        capture.Result.finishedUtc = DateTime.UtcNow.ToString("O");
        capture.Result.success = error == null && capture.Result.gameViewRestored
            && capture.Result.selectionUnchanged && capture.Result.screenshots.Count == Resolutions.Length;
        Directory.CreateDirectory(Folder);
        LastReportPath = Folder + "/gameview-" + capture.Token + "-report.json";
        File.WriteAllText(LastReportPath, JsonUtility.ToJson(capture.Result, true));
        Debug.Log("POPUP_STORE_GAMEVIEW_CAPTURE success=" + capture.Result.success
            + "; screenshots=" + capture.Result.screenshots.Count + "; restored=" + capture.Result.gameViewRestored
            + "; selectionUnchanged=" + capture.Result.selectionUnchanged + "; report=" + LastReportPath);
    }

    private sealed class Capture
    {
        internal readonly Report Result;
        internal readonly string Token;
        private readonly EditorWindow _game;
        private readonly UIPopupStore _popup;
        private readonly VisualElement _visualRoot;
        private readonly Type _gameType;
        private readonly object _sizes, _group, _groupType;
        private readonly MethodInfo _getSize, _getCount, _getBuiltinCount, _addSize, _removeSize, _selectSize;
        private readonly PropertyInfo _selected, _targetSize, _lowResolution;
        private readonly FieldInfo _zoomField, _defaultScale, _selectedSizes;
        private readonly int[] _selection;
        private readonly int _activeSelection;
        private readonly Object _selectionContext;
        private readonly object _originalSize;
        private readonly List<object> _added = new List<object>();
        private readonly List<string> _addedDescriptions = new List<string>();
        private readonly string[] _originalDescriptions;
        private ViewState _expected;
        private ViewState _stableSample;
        private bool _settling, _userInputObserved;
        private int _stableSamples;
        private int _index = -1, _selectedFrame;
        private double _deadline;
        private string _pendingPath;

        internal Capture(string label)
        {
            Token = label + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            _game = Resources.FindObjectsOfTypeAll(_gameType).OfType<EditorWindow>()
                .FirstOrDefault(window => window == EditorWindow.focusedWindow);
            if (_game == null) throw new InvalidOperationException("Keep the existing Game tab focused; capture will not create or focus a window.");
            if (GUIUtility.hotControl != 0) throw new InvalidOperationException("Complete the current mouse gesture before capturing.");
            var popups = Resources.FindObjectsOfTypeAll<UIPopupStore>()
                .Where(p => p != null && p.gameObject.activeInHierarchy && p.gameObject.scene.IsValid()).ToArray();
            if (popups.Length != 1) throw new InvalidOperationException("Capture requires exactly one active existing popup in original Stage1 Play.");
            _popup = popups[0];
            _visualRoot = _game.rootVisualElement;
            if (_visualRoot == null) throw new InvalidOperationException("Cannot observe user GameView gestures safely.");
            var sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            _sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            _groupType = sizesType.GetProperty("currentGroupType", Instance).GetValue(_sizes);
            _group = sizesType.GetMethod("GetGroup", Instance).Invoke(_sizes, new[] { _groupType });
            var groupClass = _group.GetType();
            _getSize = NeedMethod(groupClass, "GetGameViewSize", typeof(int));
            _getCount = NeedMethod(groupClass, "GetTotalCount");
            _getBuiltinCount = NeedMethod(groupClass, "GetBuiltinCount");
            _addSize = NeedMethod(groupClass, "AddCustomSize", typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize", true));
            _removeSize = NeedMethod(groupClass, "RemoveCustomSize", typeof(int));
            _selectSize = NeedMethod(_gameType, "SizeSelectionCallback", typeof(int), typeof(object));
            _selected = NeedProperty(_gameType, "selectedSizeIndex");
            _targetSize = NeedProperty(_gameType, "targetRenderSize");
            _lowResolution = NeedProperty(_gameType, "lowResolutionForAspectRatios");
            _zoomField = NeedField(_gameType, "m_ZoomArea");
            _defaultScale = NeedField(_gameType, "m_defaultScale");
            _selectedSizes = NeedField(_gameType, "m_SelectedSizes");
            _selection = (int[])Selection.instanceIDs.Clone();
            _activeSelection = Selection.activeInstanceID;
            _selectionContext = Selection.activeContext;
            var before = ReadState();
            _originalSize = SizeAt(before.selectedIndex);
            _originalDescriptions = before.sizes;
            Result = new Report { label = label, startedUtc = DateTime.UtcNow.ToString("O"), before = before };
        }

        private int Count => (int)_getCount.Invoke(_group, null);
        private bool SameGroup => Equals(_sizes.GetType().GetProperty("currentGroupType", Instance).GetValue(_sizes), _groupType);
        private object SizeAt(int index) => _getSize.Invoke(_group, new object[] { index });
        private object Zoom => _zoomField.GetValue(_game);
        private static string Describe(object size)
        {
            var type = size.GetType();
            return type.GetProperty("sizeType", Instance).GetValue(size) + "|"
                + type.GetProperty("width", Instance).GetValue(size) + "|"
                + type.GetProperty("height", Instance).GetValue(size) + "|"
                + type.GetProperty("baseText", Instance).GetValue(size);
        }
        private ViewState ReadState()
        {
            if (!SameGroup) throw new InvalidOperationException("GameView platform group changed; its new settings will not be overwritten.");
            var target = (Vector2)_targetSize.GetValue(_game);
            int selected = (int)_selected.GetValue(_game);
            object zoom = Zoom;
            return new ViewState
            {
                instanceId = _game.GetInstanceID(), group = _groupType.ToString(), selectedIndex = selected,
                selectedSize = Describe(SizeAt(selected)), renderWidth = Mathf.RoundToInt(target.x), renderHeight = Mathf.RoundToInt(target.y),
                sizes = Enumerable.Range(0, Count).Select(i => Describe(SizeAt(i))).ToArray(),
                scale = (Vector2)NeedProperty(zoom.GetType(), "scale").GetValue(zoom),
                translation = (Vector2)NeedProperty(zoom.GetType(), "translation").GetValue(zoom),
                defaultScale = (float)_defaultScale.GetValue(_game), lowResolution = (bool)_lowResolution.GetValue(_game),
                position = _game.position, maximized = _game.maximized
            };
        }

        internal void SelectNext()
        {
            if (!SameGroup) throw new InvalidOperationException("GameView platform group changed.");
            _index++;
            if (_index >= Resolutions.Length) return;
            var resolution = Resolutions[_index];
            int selected = -1;
            for (int i = 0; i < Count; i++)
            {
                var size = SizeAt(i); var type = size.GetType();
                if (Convert.ToInt32(type.GetProperty("sizeType", Instance).GetValue(size)) == 1
                    && (int)type.GetProperty("width", Instance).GetValue(size) == resolution.x
                    && (int)type.GetProperty("height", Instance).GetValue(size) == resolution.y) { selected = i; break; }
            }
            if (selected < 0)
            {
                var sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize", true);
                var fixedType = Enum.ToObject(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType", true), 1);
                object size = Activator.CreateInstance(sizeType, fixedType, resolution.x, resolution.y, "DEVUI01 " + Token + " " + resolution);
                int beforeCount = Count;
                _addSize.Invoke(_group, new[] { size });
                _added.Add(size); _addedDescriptions.Add(Describe(size));
                if (Count != beforeCount + 1 || !ReferenceEquals(SizeAt(Count - 1), size))
                    throw new InvalidOperationException("GameView custom-size insertion did not match the expected object.");
                selected = Count - 1;
            }
            _selectSize.Invoke(_game, new object[] { selected, null });
            _game.Repaint();
            _expected = ReadState();
            _stableSample = _expected;
            _stableSamples = 0;
            _settling = true;
            _selectedFrame = Time.frameCount;
            _deadline = EditorApplication.timeSinceStartup + 8;
            _pendingPath = null;
        }

        internal bool Step()
        {
            if (_game == null) throw new InvalidOperationException("The original Game tab was closed.");
            if (_popup == null || !_popup.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The captured popup was closed or replaced.");
            if (!SameGroup)
            {
                Result.userGameViewChangeDetected = true;
                throw new InvalidOperationException("The GameView platform group changed.");
            }
            if (!SameSelection()) throw new InvalidOperationException("Selection changed; no selection state will be overwritten.");
            if (EditorWindow.focusedWindow != _game) throw new InvalidOperationException("The original Game tab lost focus; capture stopped without changing focus.");
            var current = ReadState();
            if (!SameStructure(current, _expected) || (!_settling && !SameSettings(current, _expected)))
            {
                Result.userGameViewChangeDetected = true;
                throw new InvalidOperationException("GameView settings changed outside the capture; the user's new view settings are preserved.");
            }
            if (_userInputObserved)
            {
                if (!SameSettings(current, _expected)) Result.userGameViewChangeDetected = true;
                throw new InvalidOperationException("User input occurred during capture; no input was consumed or blocked.");
            }
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("GameView did not render/save the requested dimensions in time.");
            var wanted = Resolutions[_index];
            if (_settling)
            {
                // Unity fits/clamps the zoom again during OnGUI after SizeSelectionCallback. Observe
                // those tool-owned transition frames before using zoom as an external-change guard.
                _stableSamples = SameSettings(current, _stableSample) ? _stableSamples + 1 : 0;
                _stableSample = current;
                if (Time.frameCount < _selectedFrame + 2 || _stableSamples < 2) return false;
                _expected = current;
                _settling = false;
            }
            if (_pendingPath == null)
            {
                if (Time.frameCount < _selectedFrame + 2 || Screen.width != wanted.x || Screen.height != wanted.y
                    || current.renderWidth != wanted.x || current.renderHeight != wanted.y) return false;
                Directory.CreateDirectory(Folder);
                _pendingPath = Folder + "/gameview-" + Token + "-" + wanted.x + "x" + wanted.y + ".png";
                ScreenCapture.CaptureScreenshot(_pendingPath);
                return false;
            }
            if (!File.Exists(_pendingPath)) return false;
            var info = new FileInfo(_pendingPath);
            if (info.Length < 24) return false;
            byte[] png;
            try { png = File.ReadAllBytes(_pendingPath); }
            catch (IOException) { return false; }
            // Do not select the next resolution while the native screenshot writer is still finishing.
            if (png.Length < 12 || png[png.Length - 8] != 73 || png[png.Length - 7] != 69
                || png[png.Length - 6] != 78 || png[png.Length - 5] != 68) return false;
            if (png.Length < 24 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71
                || BigEndianInt(png, 16) != wanted.x || BigEndianInt(png, 20) != wanted.y)
                throw new InvalidOperationException("The captured PNG does not match the requested actual GameView dimensions.");
            var popup = _popup;
            var clock = popup != null ? popup.GetComponent<PopupStoreBusinessClock>() : null;
            var grade = popup != null ? popup.GetComponent<PopupStoreGradeSession>() : null;
            var reward = popup != null ? popup.GetComponent<PopupStoreRewardSession>() : null;
            Result.screenshots.Add(new Screenshot
            {
                path = Path.GetFullPath(_pendingPath), capturedUtc = DateTime.UtcNow.ToString("O"), width = wanted.x, height = wanted.y,
                frame = Time.frameCount, sessionId = grade != null ? grade.SessionId : null,
                businessState = clock != null ? clock.State.ToString() : null, remainingSeconds = clock != null ? clock.RemainingSeconds : 0,
                currentGrade = grade != null ? grade.Current.Grade.ToString() : null,
                score = grade != null ? grade.Current.Score : 0, successfulOrders = grade != null ? grade.Current.SuccessfulOrders : 0,
                finalizations = grade != null ? grade.FinalizationCount : 0, draws = reward != null ? reward.RandomDrawCount : 0
            });
            if (_index + 1 == Resolutions.Length) return true;
            SelectNext();
            return false;
        }

        internal void Restore()
        {
            Result.selectionUnchanged = SameSelection();
            if (_game == null || !SameGroup)
            {
                Result.userGameViewChangeDetected = true;
                RemoveOwnedSizes(null, false);
                Result.gameViewRestored = false;
                return;
            }
            var current = ReadState();
            if (_expected != null && (!SameStructure(current, _expected)
                || ((!_settling || _userInputObserved) && !SameSettings(current, _expected))))
                Result.userGameViewChangeDetected = true;
            object keepSelected = Result.userGameViewChangeDetected ? SizeAt(current.selectedIndex) : _originalSize;
            int restoreIndex = IndexOf(keepSelected);
            if (restoreIndex < 0) { Result.userGameViewChangeDetected = true; keepSelected = SizeAt(current.selectedIndex); }
            else if (!Result.userGameViewChangeDetected) _selectSize.Invoke(_game, new object[] { restoreIndex, null });

            RemoveOwnedSizes(keepSelected, true);
            int keepIndex = IndexOf(keepSelected);
            if (keepIndex >= 0)
            {
                // A removal can shift the user's total index; preserve the SAME selected size object.
                _selected.SetValue(_game, keepIndex);
                if (!Result.userGameViewChangeDetected)
                {
                    var zoom = Zoom;
                    NeedMethod(zoom.GetType(), "SetTransform", typeof(Vector2), typeof(Vector2))
                        .Invoke(zoom, new object[] { Result.before.translation, Result.before.scale });
                    _defaultScale.SetValue(_game, Result.before.defaultScale);
                }
            }
            _game.Repaint();
            Result.after = ReadState();
            Result.gameViewRestored = !Result.userGameViewChangeDetected && Result.retainedTemporarySizes.Count == 0
                && SameSettings(Result.before, Result.after) && Result.after.sizes.SequenceEqual(_originalDescriptions);
        }
        private void RemoveOwnedSizes(object keepSelected, bool ownsCurrentView)
        {
            // Unity 6000.0 RemoveCustomSize accepts the TOTAL index and subtracts built-in count itself.
            // Other existing GameViews are never shifted, and only our unchanged exact objects qualify.
            for (int item = _added.Count - 1; item >= 0; item--)
            {
                object added = _added[item];
                int index = IndexOf(added);
                if (index < 0) continue;
                if (ReferenceEquals(keepSelected, added) || index < (int)_getBuiltinCount.Invoke(_group, null)
                    || Describe(added) != _addedDescriptions[item] || OtherViewAffected(index, ownsCurrentView))
                { Result.retainedTemporarySizes.Add(Describe(added)); continue; }
                _removeSize.Invoke(_group, new object[] { index });
                if (IndexOf(added) >= 0) throw new InvalidOperationException("An owned temporary GameView size was not removed.");
            }
        }
        private bool OtherViewAffected(int removedIndex, bool ownsCurrentView)
        {
            foreach (var view in Resources.FindObjectsOfTypeAll(_gameType).OfType<EditorWindow>())
            {
                if (ownsCurrentView && view == _game) continue;
                var selected = (int[])_selectedSizes.GetValue(view);
                int groupIndex = Convert.ToInt32(_groupType);
                if (selected != null && groupIndex < selected.Length && selected[groupIndex] >= removedIndex) return true;
            }
            return false;
        }
        internal void ObserveInput()
        {
            _visualRoot.RegisterCallback<MouseDownEvent>(OnMouseInput, TrickleDown.TrickleDown);
            _visualRoot.RegisterCallback<PointerDownEvent>(OnPointerInput, TrickleDown.TrickleDown);
            _visualRoot.RegisterCallback<WheelEvent>(OnWheelInput, TrickleDown.TrickleDown);
            _visualRoot.RegisterCallback<KeyDownEvent>(OnKeyInput, TrickleDown.TrickleDown);
        }
        internal void StopObservingInput()
        {
            _visualRoot.UnregisterCallback<MouseDownEvent>(OnMouseInput, TrickleDown.TrickleDown);
            _visualRoot.UnregisterCallback<PointerDownEvent>(OnPointerInput, TrickleDown.TrickleDown);
            _visualRoot.UnregisterCallback<WheelEvent>(OnWheelInput, TrickleDown.TrickleDown);
            _visualRoot.UnregisterCallback<KeyDownEvent>(OnKeyInput, TrickleDown.TrickleDown);
        }
        private void OnMouseInput(MouseDownEvent _) => _userInputObserved = true;
        private void OnPointerInput(PointerDownEvent _) => _userInputObserved = true;
        private void OnWheelInput(WheelEvent _) => _userInputObserved = true;
        private void OnKeyInput(KeyDownEvent _) => _userInputObserved = true;
        private int IndexOf(object size)
        {
            for (int i = 0; i < Count; i++) if (ReferenceEquals(SizeAt(i), size)) return i;
            return -1;
        }
        private bool SameSelection() => Selection.activeInstanceID == _activeSelection
            && Selection.activeContext == _selectionContext && Selection.instanceIDs.SequenceEqual(_selection);
    }

    private static bool SameStructure(ViewState a, ViewState b) => a != null && b != null
        && a.instanceId == b.instanceId && a.group == b.group && a.selectedIndex == b.selectedIndex && a.selectedSize == b.selectedSize
        && a.position == b.position && a.maximized == b.maximized && a.lowResolution == b.lowResolution
        && a.sizes.SequenceEqual(b.sizes);
    private static bool SameSettings(ViewState a, ViewState b) => SameStructure(a, b)
        && (a.scale - b.scale).sqrMagnitude < .000001f && (a.translation - b.translation).sqrMagnitude < .000001f
        && Mathf.Abs(a.defaultScale - b.defaultScale) < .0001f && a.sizes.SequenceEqual(b.sizes);
    private static int BigEndianInt(byte[] bytes, int offset) => (bytes[offset] << 24) | (bytes[offset + 1] << 16)
        | (bytes[offset + 2] << 8) | bytes[offset + 3];
    private static string Join(string left, string right) => string.IsNullOrEmpty(left) ? right : left + "\n" + right;
    private static MethodInfo NeedMethod(Type type, string name, params Type[] arguments)
        => type.GetMethod(name, Instance, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);
    private static PropertyInfo NeedProperty(Type type, string name)
        => type.GetProperty(name, Instance) ?? throw new MissingMemberException(type.FullName, name);
    private static FieldInfo NeedField(Type type, string name)
        => type.GetField(name, Instance) ?? throw new MissingFieldException(type.FullName, name);
}
#endif
