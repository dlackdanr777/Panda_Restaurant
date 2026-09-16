#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in unsaved scene. SessionState holds only editor scene setup, never account or request data.</summary>
[InitializeOnLoad]
public static class StaffGachaOfflineHost
{
    private const string ActiveKey = "Panda.StaffPurchaseOffline.Active";
    private const string ScenesKey = "Panda.StaffPurchaseOffline.SceneSetup";
    private const string NavigationKey = "Panda.StaffPurchaseOffline.Navigation";
    private const string SceneName = "Staff Purchase Offline (Unsaved)";
    private const string TemplateName = "Staff Purchase Offline Inactive Template";
    private static GameObject _template;
    private static StaffGachaOfflineFonts _fonts;
    private static string _lastNavigationObservation;
    private static double _nextObservation;
    public static StaffGachaOfflineSession Session { get; private set; }
    public static UIGacha View { get; private set; }
    public static UIStaffGacha Staff { get; private set; }
    public static string Message { get; private set; } = "편집 모드에서 독립 검증 환경을 시작하세요.";
    public static bool IsActive => Application.isPlaying && SessionState.GetBool(ActiveKey, false) &&
        ((View != null && View.gameObject.scene == SceneManager.GetActiveScene()) ||
         (_template != null && _template.scene == SceneManager.GetActiveScene()));
    public static bool IsNavigationCase => SessionState.GetBool(NavigationKey, false);

    [Serializable] private sealed class SavedScenes { public SavedScene[] Scenes; }
    [Serializable] private sealed class SavedScene { public string Path; public bool Loaded; public bool Active; }

    static StaffGachaOfflineHost()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.update += ObserveNavigation;
        EditorApplication.delayCall += () => { if (EditorApplication.isPlaying && SessionState.GetBool(ActiveKey, false)) AttachView(); };
    }

    public static void PrepareAndEnter() => PrepareAndEnter(false);
    public static void PrepareNavigationAndEnter() => PrepareAndEnter(true);

    private static void PrepareAndEnter(bool navigation)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Message = "기존 Play Mode에서는 시작하지 않습니다. 먼저 종료해 주세요."; return; }
        if (EditorSettings.enterPlayModeOptionsEnabled &&
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
        { Message = "실제 계정의 정적 상태와 분리하기 위해 Domain Reload가 켜진 Play Mode가 필요합니다. 설정은 자동 변경하지 않습니다."; return; }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty || (string.IsNullOrEmpty(scene.path) && scene.rootCount != 0))
            { Message = "열린 씬에 미저장 내용이 있습니다. 사용자가 먼저 저장/정리한 뒤 시작해 주세요."; return; }
        }
        if (Resources.FindObjectsOfTypeAll<BackendManager>().Any(owner => owner != null && owner.gameObject.scene.IsValid()))
        { Message = "실제 Backend 객체가 있는 편집 씬에서는 시작하지 않습니다. 빈 씬을 열고 실행해 주세요."; return; }
        var saved = new SavedScenes { Scenes = setup.Select(item => new SavedScene { Path = item.path, Loaded = item.isLoaded, Active = item.isActive }).ToArray() };
        SessionState.SetString(ScenesKey, JsonUtility.ToJson(saved));
        try
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = SceneName;
            GameObject root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(scene, out var view, out var staff, navigation);
            // A sanitized, never-activated template permits display recreation without loading a gameplay scene in Play.
            _template = UnityEngine.Object.Instantiate(root);
            _template.name = TemplateName;
            View = view; Staff = staff;
            SetGameViewResolution();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(NavigationKey, navigation);
            Message = "빈 임시 씬에서 Play Mode를 시작합니다. 실제 계정·SDK는 연결하지 않습니다.";
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(NavigationKey, false);
            RestoreEditorScenes();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) AttachView();
        else if (state == PlayModeStateChange.ExitingPlayMode) ReleaseMemory();
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(NavigationKey, false);
            ReleaseMemory();
            RestoreEditorScenes();
            Message = "독립 검증 세션을 종료했습니다. 메모리 자료는 보존하지 않습니다.";
        }
    }

    private static void AttachView()
    {
        if (View != null && Staff != null) return;
        if (SceneManager.sceneCount != 1)
        { Message = "독립 검증 씬이 아니므로 시작하지 않습니다."; EditorApplication.ExitPlaymode(); return; }
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        _template = roots.SingleOrDefault(root => root.name == TemplateName && !root.activeSelf);
        var views = roots.Where(root => root.name == StaffGachaOfflineViewFactory.RootName)
            .SelectMany(root => root.GetComponentsInChildren<UIGacha>(true)).ToArray();
        // Unity reloads an unsaved scene as Untitled. Verify the exact inactive copy roots instead of its transient name.
        if (roots.Length != 2 || _template == null || views.Length != 1 || views[0].gameObject.activeInHierarchy ||
            Resources.FindObjectsOfTypeAll<BackendManager>().Any(owner => owner != null && owner.gameObject.scene.IsValid()))
        { Message = "검증 화면 또는 계정 소유자가 격리되어 있지 않습니다."; EditorApplication.ExitPlaymode(); return; }
        try
        {
            // After domain reload, but before any copied TMP text is activated. Both copies share
            // this Play-session-only font graph, including subsequent recreated displays.
            _fonts = new StaffGachaOfflineFonts();
            _fonts.BindBeforeActivation(views[0].transform.root.gameObject, _template);
            View = views[0];
            Staff = View.GetComponentInChildren<UIStaffGacha>(true);
            if (IsNavigationCase)
            {
                Session = new StaffGachaOfflineSession(Resources.LoadAll<StaffData>("StaffData"));
                StaffGachaOfflineViewFactory.ConfigureNavigation(View, Staff, Session.Owner);
                if (!Session.TryStart(StaffGachaOfflineCase.Eleven, out string error))
                    throw new InvalidOperationException(error);
                Session.ReplySuccess();
                if (!Session.Request.IsCompleted) throw new InvalidOperationException("Mock completion was not confirmed.");
                // A single authorized memory fixture; all subsequent input is display/navigation only.
                Session.Owner.EditorStaffPurchaseAdmission = _ => false;
                View.transform.root.gameObject.SetActive(true);
                Session.Owner.StartCoroutine(BeginNavigationWhenStarted());
            }
        }
        catch (Exception exception)
        {
            _fonts?.Dispose(); _fonts = null;
            Message = "오프라인 글꼴 분리 실패: " + exception.Message;
            EditorApplication.ExitPlaymode();
            return;
        }
        Message = IsNavigationCase ? "상점·머신 내비게이션 준비 중입니다." : "오프라인 환경 준비 완료. 아래 독립 시험을 선택하세요.";
    }

    private static IEnumerator BeginNavigationWhenStarted()
    {
        yield return null; // Native MobileUINavigation.Start initializes the copied three-view stack.
        StaffGachaOfflineViewFactory.BeginNavigation(View);
        Message = "상점의 가챠 진입 버튼을 누르세요. 준비된 모의 결과 1건만 사용하며 추가 구매는 차단됩니다.";
        Debug.Log("STAFF_OFFLINE_NAVIGATION_READY: mock completion=1; real SDK/login/server=0; user input only.");
    }

    private static void ObserveNavigation()
    {
        if (!IsActive || !IsNavigationCase || Session == null || Staff == null ||
            EditorApplication.timeSinceStartup < _nextObservation) return;
        _nextObservation = EditorApplication.timeSinceStartup + 0.25;
        // Observation is outside product callbacks; file/QA errors never block completion or navigation.
        try
        {
            var display = Staff.EditorOfflinePurchaseDisplay;
            var nav = View.transform.root.GetComponent<Muks.MobileUI.MobileUINavigation>();
            var state = new JObject
            {
                ["requestId"] = Session.Request?.Identity.RequestId,
                ["completionCount"] = Session.Request?.CompletionCount,
                ["draws"] = Session.DrawCount, ["mockPurchaseWrites"] = Session.PurchaseWrites,
                ["mockFollowupWrites"] = Session.FollowupWrites, ["notifications"] = Session.RecordNotifications,
                ["diamonds"] = Session.Diamonds, ["pandaTokens"] = Session.Account.PandaTokens,
                ["navigationTop"] = nav?.FirstView?.GetType().Name, ["navigationCount"] = nav?.Count,
                ["viewVisible"] = View.VisibleState.ToString(),
                ["resultVisible"] = display?.EditorIsResultVisible, ["resultIndex"] = display?.EditorResultIndex,
                ["animating"] = display?.EditorIsAnimating, ["animationStarts"] = display?.EditorAnimationStartCount,
                ["sdkInitialized"] = BackEnd.Backend.IsInitialized, ["sdkLoggedIn"] = BackEnd.Backend.IsLogin
            };
            string json = state.ToString(Newtonsoft.Json.Formatting.None);
            if (json == _lastNavigationObservation) return;
            string path = Path.GetFullPath("Logs/StaffOfflineNavigation_Manual.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            state["utc"] = DateTime.UtcNow.ToString("O");
            File.AppendAllText(path, state.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
            _lastNavigationObservation = json;
        }
        catch (Exception exception) { Debug.LogWarning("Offline navigation observation only: " + exception.Message); }
    }

    public static void StartCase(StaffGachaOfflineCase scenario)
    {
        if (IsNavigationCase) { Message = "이 세션은 보관된 결과의 표시·이동만 검증합니다."; return; }
        if (!IsActive || Staff == null) { Message = "먼저 독립 검증 환경을 시작하세요."; return; }
        if (Session != null && Session.HasUnresolvedRequest)
        { Message = "처리 중·미확정 요청은 새 시험으로 지울 수 없습니다. 응답을 처리하거나 독립 Play 세션을 종료하세요."; return; }
        try
        {
            if (Session != null) View.SetEditorOfflineVisible(false);
            Session?.Dispose();
            Session = new StaffGachaOfflineSession(Resources.LoadAll<StaffData>("StaffData"));
            // Owner is configured before enabling the cloned view. The only display Tick is UIStaffGacha.Update.
            Staff.ConfigureEditorOffline(Session.Owner, View);
            View.ConfigureEditorOfflineView(Staff);
            View.transform.root.gameObject.SetActive(true);
            View.SetEditorOfflineVisible(true);
            if (!Session.TryStart(scenario, out string error)) Message = error;
            else Message = "요청 대기 중입니다. 모의 성공 응답 전에는 구매를 확정하지 않습니다.";
        }
        catch (Exception exception) { Message = exception.ToString(); }
    }

    public static void SetScreenVisible(bool visible)
    {
        if (IsNavigationCase) return; // Only the actual product buttons alter the navigation stack.
        if (!IsActive || Session == null) return;
        if (visible && View == null) RecreateScreen();
        if (View == null) return;
        View.SetEditorOfflineVisible(visible);
        Message = visible ? "화면을 다시 열었습니다. 요청·계산은 다시 실행하지 않습니다." : "화면만 닫았습니다. 요청·결과는 메모리에 유지됩니다.";
    }

    public static void ShowResult()
    {
        if (IsNavigationCase) return;
        if (Session == null || !IsActive) return;
        SetScreenVisible(true);
        if (Staff == null) return;
        if (!Staff.EditorOfflinePurchaseDisplay.TryShowCompleted(false, out string error)) Message = error;
        else Message = "이미 완료된 결과를 다시 표시했습니다.";
    }

    public static void RecreateScreen()
    {
        if (IsNavigationCase) return;
        if (!IsActive || Session == null || _template == null)
        { Message = "독립 검증 화면의 비활성 원본이 없습니다. 요청은 그대로 보존합니다."; return; }
        if (View != null)
        {
            View.SetEditorOfflineVisible(false);
            UnityEngine.Object.Destroy(View.transform.root.gameObject);
        }
        GameObject root = UnityEngine.Object.Instantiate(_template);
        root.name = StaffGachaOfflineViewFactory.RootName;
        View = root.GetComponentInChildren<UIGacha>(true);
        Staff = root.GetComponentInChildren<UIStaffGacha>(true);
        Staff.ConfigureEditorOffline(Session.Owner, View);
        View.ConfigureEditorOfflineView(Staff);
        root.SetActive(true);
        View.SetEditorOfflineVisible(true);
        Message = "표시 객체만 재생성했습니다. 요청·결과·가상 잔액은 유지됩니다.";
    }

    public static void EndSession()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        // This closes the isolated Play session, never a production request Reset.
        EditorApplication.ExitPlaymode();
    }

    private static void BeforeReload()
    {
        if (EditorApplication.isPlaying && SessionState.GetBool(ActiveKey, false)) ReleaseMemory();
    }

    private static void ReleaseMemory()
    {
        if (View != null && Staff != null && Session != null && !IsNavigationCase) View.SetEditorOfflineVisible(false);
        if (View != null) View.transform.root.gameObject.SetActive(false);
        if (_template != null) _template.SetActive(false);
        Session?.Dispose(); Session = null; View = null; Staff = null; _template = null;
        _fonts?.Dispose(); _fonts = null;
    }

    private static void RestoreEditorScenes()
    {
        string json = SessionState.GetString(ScenesKey, "");
        SessionState.EraseString(ScenesKey);
        if (string.IsNullOrEmpty(json)) return;
        var saved = JsonUtility.FromJson<SavedScenes>(json);
        var setup = saved.Scenes.Where(item => !string.IsNullOrEmpty(item.Path)).Select(item => new SceneSetup
            { path = item.Path, isLoaded = item.Loaded, isActive = item.Active }).ToArray();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void SetGameViewResolution()
    {
        // Select a fixed Game View resolution; no scene, prefab or build setting is written.
        Assembly editor = typeof(EditorWindow).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sizes);
        if (groupType == null)
            groupType = Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), EditorUserBuildSettings.selectedBuildTargetGroup.ToString());
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type groupClass = group.GetType();
        var count = groupClass.GetMethod("GetTotalCount");
        var get = groupClass.GetMethod("GetGameViewSize");
        int selected = -1;
        for (int index = 0; index < (int)count.Invoke(group, null); index++)
        {
            object size = get.Invoke(group, new object[] { index });
            if ((int)size.GetType().GetProperty("width").GetValue(size) == 3840 && (int)size.GetType().GetProperty("height").GetValue(size) == 2160)
            { selected = index; break; }
        }
        if (selected < 0)
        {
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            object fixedType = Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1);
            object size = Activator.CreateInstance(sizeType, fixedType, 3840, 2160, "Staff Offline 3840x2160");
            groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            selected = (int)count.Invoke(group, null) - 1;
        }
        Type gameView = editor.GetType("UnityEditor.GameView");
        EditorWindow game = EditorWindow.GetWindow(gameView);
        gameView.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, selected);
        game.Show();
    }

    public static void CaptureScreen()
    {
        if (!IsActive) return;
        string folder = Path.GetFullPath("Logs");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "StaffPurchaseOffline_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Message = "현재 Player 화면 캡처 예약: " + path;
    }
}

public sealed class StaffGachaOfflineWindow : EditorWindow
{
    private Vector2 _scroll;
    [MenuItem("Tools/Panda Restaurant/Staff Gacha/Runtime Purchase Screen Validation")]
    public static void Open() => GetWindow<StaffGachaOfflineWindow>("Staff Purchase Offline").Show();
    private void OnEnable() { minSize = new Vector2(480, 620); EditorApplication.update += Repaint; }
    private void OnDisable() { EditorApplication.update -= Repaint; StaffGachaOfflineHost.SetScreenVisible(false); }
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.HelpBox("런타임 구매 화면 검증 · 테스트 전용\n실제 계정/SDK 대신 독립 메모리 소유자와 모의 전송을 사용합니다. 기존 모의 요청 미리보기와 다른 경로입니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("독립 오프라인 Play 환경 시작"))
                EditorApplication.delayCall += StaffGachaOfflineHost.PrepareAndEnter;
            if (GUILayout.Button("최종 화면 확인 · 머신 이동 / 상점 복귀"))
                EditorApplication.delayCall += StaffGachaOfflineHost.PrepareNavigationAndEnter;
        }
        using (new EditorGUI.DisabledScope(!StaffGachaOfflineHost.IsActive))
        {
            using (new EditorGUI.DisabledScope(StaffGachaOfflineHost.IsNavigationCase))
            {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("단일 신규 시험")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.SingleNew);
                if (GUILayout.Button("11회 시험")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.Eleven);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("첫 결과 중복")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.SingleDuplicate);
                if (GUILayout.Button("11개 전체 중복")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.ElevenDuplicates);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("유니크 등급")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.Unique);
                if (GUILayout.Button("스페셜 등급")) StaffGachaOfflineHost.StartCase(StaffGachaOfflineCase.Special);
            }
            var session = StaffGachaOfflineHost.Session;
            using (new EditorGUI.DisabledScope(session == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("모의 성공 응답")) session.ReplySuccess();
                    if (GUILayout.Button("동일 성공 응답 다시 전달")) session.ReplySuccess();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("결과 미확정 응답")) session.ReplyIndeterminate();
                    if (GUILayout.Button("대기 중 다이아 +5")) session.AddFiveDiamonds(out _);
                }
                EditorGUILayout.LabelField("응답을 보내지 않으면 대기 상태가 유지됩니다.");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("검증 화면 닫기")) StaffGachaOfflineHost.SetScreenVisible(false);
                    if (GUILayout.Button("검증 화면 재개방")) StaffGachaOfflineHost.SetScreenVisible(true);
                    if (GUILayout.Button("완료 결과 다시 보기")) StaffGachaOfflineHost.ShowResult();
                }
                if (GUILayout.Button("현재 Game View 캡처")) StaffGachaOfflineHost.CaptureScreen();
                if (GUILayout.Button("표시 객체만 재생성 (요청 유지)")) StaffGachaOfflineHost.RecreateScreen();
            }
            }
            if (GUILayout.Button("독립 검증 세션 종료 (Play 종료)")) StaffGachaOfflineHost.EndSession();
            DrawDiagnostics(StaffGachaOfflineHost.Session);
        }
        EditorGUILayout.HelpBox(StaffGachaOfflineHost.Message, MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    private static void DrawDiagnostics(StaffGachaOfflineSession session)
    {
        if (session == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("검증용 다이아", session.Diamonds.ToString());
        EditorGUILayout.LabelField("검증용 판다토큰", session.Account?.PandaTokens.ToString() ?? "없음");
        EditorGUILayout.LabelField("직원", session.Account == null ? "없음" : string.Join(", ", session.Account.Staff.Select(item => item.Id + " Lv." + item.Level)), EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("요청 ID", session.Request?.Identity.RequestId ?? "없음", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("요청 / 로컬 완료", (session.Request?.Status.ToString() ?? "대기") + " / " + (session.Request?.IsCompleted == true));
        EditorGUILayout.LabelField("모의 전송", $"구매 {session.PurchaseWrites} / 후속 저장 {session.FollowupWrites} / 추첨 {session.DrawCount}");
        EditorGUILayout.LabelField("확정 알림", $"지갑 {session.WalletNotifications} / 기록 수신 {session.RecordNotifications}");
        EditorGUILayout.LabelField("payload Dia", $"구매 {ReadDia(session.PurchasePayload)} / 후속 {ReadDia(session.FollowupPayload)}");
        var acquisition = session.Request?.Plan?.AccountResult.Acquisition;
        if (acquisition != null) EditorGUILayout.LabelField("고정 결과", $"신규 {acquisition.NewStaffIds.Count} / 중복 {acquisition.Items.Count - acquisition.NewStaffIds.Count} / 추가 토큰 {acquisition.TotalPandaTokens}");
        var staff = StaffGachaOfflineHost.Staff;
        var display = staff == null ? null : staff.EditorOfflinePurchaseDisplay;
        if (display != null)
        {
            EditorGUILayout.LabelField("실제 표시", $"연출 {display.EditorIsAnimating} / 카드 {display.EditorIsResultVisible} / {display.EditorResultIndex + 1}/{display.EditorResultCount}");
            EditorGUILayout.LabelField("연출 시작", display.EditorAnimationStartCount.ToString());
            if (!string.IsNullOrEmpty(display.EditorAnimationError))
                EditorGUILayout.HelpBox(display.EditorAnimationError, MessageType.Warning);
            using (var bindings = new SerializedObject(staff))
            {
                var animator = bindings.FindProperty("_gachaMacineAnimator").objectReferenceValue as Animator;
                var audio = bindings.FindProperty("_gachaSound").objectReferenceValue as AudioSource;
                EditorGUILayout.LabelField("직원 머신 상태", $"Events {animator?.fireEvents} / Audio 재생 {audio?.isPlaying} / 입력 잠금 {StaffGachaOfflineHost.View.IsStartGacha}");
            }
        }
    }
    private static string ReadDia(string json) => json == null ? "없음" : JObject.Parse(json)["Dia"]?.ToString() ?? "없음";
}
#endif
