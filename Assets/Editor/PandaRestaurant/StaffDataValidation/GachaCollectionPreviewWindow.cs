#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Edit-mode host for the unchanged collection service and native UI. All mutable objects,
/// fonts, economy data and render targets belong to disposable preview scenes or memory.
/// No Play transition, scene replacement, scene save, asset save or Undo operation occurs.
/// </summary>
public sealed class GachaCollectionPreviewWindow : EditorWindow
{
    public static GachaCollectionPreviewWindow ActiveWindow { get; private set; }
    public bool IsReady => _root != null && Economy != null;
    public UIGacha View { get; private set; }
    public UIStaffGacha Staff { get; private set; }
    public UIItemGacha Item { get; private set; }
    public UIGachaCard ResultCard { get; private set; }
    public GachaEconomyService Economy { get; private set; }
    public EnhancementFairyHabitat Fairies => _floor?.Habitat;
    public IReadOnlyList<GachaData> Catalog => _catalog.Items;
    private Scene _scene;
    private GameObject _root;
    private Camera _camera;
    private RenderTexture _target;
    private EventSystem _events;
    private StaffGachaOfflineFonts _fonts;
    private GachaCollectionPreviewCatalog _catalog;
    private EnhancementFairyOfflinePreview _floor;
    private GachaEconomyMemoryStore _store;
    private readonly System.Random _random = new System.Random(917);
    private Queue<string> _fixed = new Queue<string>();
    private GachaMachineKind _machine = GachaMachineKind.Staff;
    private bool _showFloor;
    private string _message = "오프라인 · 메모리 전용";
    private const double PreviewFrameSeconds = 1.0 / 60.0;
    private double _lastTick, _hudTick, _lastRender, _nextTick;
    private float _hudElapsed;
    private readonly List<GameObject> _hiddenArt = new List<GameObject>();
    private Transform _slotFrame, _slotParent;
    private int _slotSibling;
    private GameObject _pointerDown;
    private Action _restoreMachineArt;
    private GachaCollectionVideoRecorder _recorder;
    private sealed class OfflineClock : IGachaExchangeClock
    {
        private readonly double _start = EditorApplication.timeSinceStartup;
        public bool TryGetUtcNow(out DateTime utc)
        {
            // Explicit fake date + monotonic elapsed time; no device date or backend calls.
            utc = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc)
                .AddSeconds(Math.Max(0, EditorApplication.timeSinceStartup - _start));
            return true;
        }
    }
    public bool IsRecordingVideo => _recorder != null && !_recorder.IsFinished;
    public void StartVideo(string encoderPath, string path, double seconds = 12)
    {
        if (IsRecordingVideo) throw new InvalidOperationException("A preview recording is already running.");
        _recorder?.Dispose();
        _recorder = new GachaCollectionVideoRecorder(encoderPath, path, _target.width, _target.height, seconds);
    }

    public void SetPreviewResolution(int width, int height)
    {
        if (IsRecordingVideo) throw new InvalidOperationException("Finish recording before changing resolution.");
        _target.Release(); _target.width = width; _target.height = height; _target.Create();
        Repaint();
    }

    public static GachaCollectionPreviewWindow Open()
    {
        var window = GetWindow<GachaCollectionPreviewWindow>("가챠 통합 데모");
        window.minSize = new Vector2(850, 590);
        try { window.InitializePreview(); window.Show(); return window; }
        catch { window.Close(); throw; }
    }

    public void InitializePreview()
    {
        if (IsReady) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("이 데모는 Edit Mode의 별도 미리보기 창에서 실행됩니다.");
        if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("오프라인 데모는 SDK가 초기화되지 않은 Edit Mode에서 실행됩니다.");
        ActiveWindow = this;
        _scene = EditorSceneManager.NewPreviewScene();
        _root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(_scene, out var view, out var staff, true, true);
        View = view; Staff = staff; Item = _root.GetComponentInChildren<UIItemGacha>(true);
        foreach (var shop in _root.GetComponentsInChildren<UIRestaurantAdmin>(true)) Object.DestroyImmediate(shop.gameObject);
        foreach (var list in _root.GetComponentsInChildren<UIStaff>(true)) Object.DestroyImmediate(list.gameObject);
        Object.DestroyImmediate(_root.GetComponent<UIMainCanvas>());
        Object.DestroyImmediate(_root.GetComponent<MobileUINavigation>());
        _fonts = new StaffGachaOfflineFonts(); _fonts.BindBeforeActivation(_root);
        _catalog = new GachaCollectionPreviewCatalog();
        _floor = new EnhancementFairyOfflinePreview();
        _floor.Camera.scene = _floor.Habitat.gameObject.scene;
        CreateEconomy(0, 5000);
        Staff.ConfigureCollectionOffline(View, Economy);
        Item.ConfigureCollectionOffline(View, Economy);
        View.ConfigureEditorOfflineNavigation(Staff, Item);
        View.BindCollectionEconomy(Economy);
        _fonts.BindBeforeActivation(_root);
        _camera = _root.GetComponentInChildren<Camera>(true);
        _camera.enabled = false; _camera.scene = _scene;
        _camera.orthographic = true; _camera.orthographicSize = 540;
        _camera.transform.position = new Vector3(0, 0, -100);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color32(55, 32, 27, 255);
        _camera.cullingMask = 1 << 31;
        _target = new RenderTexture(1920, 1080, 24) { hideFlags = HideFlags.HideAndDontSave };
        _target.Create(); _camera.targetTexture = _target;
        foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 10; }
        foreach (var animator in _root.GetComponentsInChildren<Animator>(true))
        { animator.fireEvents = false; animator.enabled = false; }
        foreach (var audio in _root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var listener in _root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        _events = _root.GetComponentInChildren<EventSystem>(true);
        foreach (var input in _root.GetComponentsInChildren<BaseInputModule>(true)) input.enabled = false;
        _events.enabled = false;
        WireButtons();
        _root.SetActive(true);
        View.SetEditorOfflineVisible(true);
        ShowMachine(GachaMachineKind.Staff);
        _lastTick = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += DisposePreview;
        _message = "별도 PreviewScene · 원래 씬 유지 · 서버 연결 없음";
        Debug.Log("COLLECTION_OFFLINE_READY: EditorWindow + PreviewScene; SDK=false; memory economy.");
    }

    private void CreateEconomy(int counter, long tokens)
    {
        var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
            new[] { new StaffAccountStaffRecord("STAFF01", 1) }, tokens, new GachaEconomySaveData(3, 3, counter, counter));
        _store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("offline-preview", account, 1000));
        Economy = new GachaEconomyService(_store, Catalog, Resources.Load<GachaEconomySettings>("GachaEconomySettings"),
            Select, new OfflineClock(), new System.Random(1917));
        Economy.Committed += tx =>
        {
            foreach (var result in tx.Results)
                if (result.IsNew && result.Data is GachaItemData item) Fairies.ConfirmAcquisition(tx.Id, item);
            _message = $"확정 결과 {tx.Results.Count}개 · 판다토큰 {tx.After.Account.PandaTokens:N0} · 메모리 저장";
        };
        Fairies.ResetOfflineSession(Array.Empty<string>());
    }

    private GachaData Select(GachaMachineKind machine, IReadOnlyList<GachaData> pool, bool guaranteed)
    {
        if (!guaranteed && _fixed.Count > 0)
        {
            string id = _fixed.Dequeue();
            return pool.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("추첨 풀에 없는 데모 데이터: " + id);
        }
        return GachaEconomyService.SelectWithRandom(machine, pool, guaranteed, _random);
    }

    public void Reset(int counter = 0, long tokens = 5000)
    {
        ClearResult(); _fixed.Clear();
        CreateEconomy(counter, tokens); View.BindCollectionEconomy(Economy);
        ShowMachine(_machine);
    }

    public void ShowMachine(GachaMachineKind machine)
    {
        ClearResult(); _machine = machine; _showFloor = false;
        View.gameObject.SetActive(true); View.SetEditorOfflineVisible(true);
        View.SelectCollectionOfflineMachine(machine);
        View.SetActiveUIComponents(true); WireButtons(); RefreshHud(); Repaint();
    }

    public void Draw(GachaMachineKind machine, string[] ids = null, bool eleven = false)
        => DrawWithPayment(machine, ids, eleven ? GachaPaymentKind.DiamondsEleven : GachaPaymentKind.DiamondsSingle);

    private void DrawWithPayment(GachaMachineKind machine, string[] ids, GachaPaymentKind payment)
    {
        ShowMachine(machine); _fixed = new Queue<string>(ids ?? Array.Empty<string>());
        if (!Economy.TryDraw(machine, payment, Present, out string error)) _message = error;
        Repaint();
    }

    // Edit-mode projection: the service already committed the immutable result. Reuse the
    // authored result cards/slots, avoiding runtime coroutine/Animator clocks and all grant hooks.
    private void Present(GachaEconomyTransaction tx)
    {
        var machine = tx.Machine == GachaMachineKind.Staff ? (Component)Staff : Item;
        _restoreMachineArt = (Action)typeof(UIGacha).GetMethod("IsolateMachineArt",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(View, new object[] { machine });
        ResultCard = Read<UIGachaCard>(machine, tx.Machine == GachaMachineKind.Staff ? "_gachaCard" : "_skinGachaCard");
        _slotFrame = Read<Transform>(machine, tx.Machine == GachaMachineKind.Staff ? "_getStaffSlotFrame" : "_getItemSlotFrame");
        _slotParent = _slotFrame.parent; _slotSibling = _slotFrame.GetSiblingIndex();
        _slotFrame.SetParent(machine.transform, true);
        var retained = new HashSet<Transform> { ResultCard.transform, _slotFrame };
        foreach (Transform child in machine.transform)
            if (child.gameObject.activeSelf && !retained.Contains(child))
            { _hiddenArt.Add(child.gameObject); child.gameObject.SetActive(false); }
        View.SetStartGacha(true); View.SetActiveUIComponents(false);
        var slots = _slotFrame.GetComponentsInChildren<UIGachaCardSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].gameObject.SetActive(tx.Results.Count == 11 && i < 10);
            if (tx.Results.Count != 11 || i >= 10) continue;
            var result = tx.Results[i];
            if (result.Data is GachaStaffData staff)
                slots[i].TrySetStaffAcquisitionResult(staff, Acquisition(result));
            else BindOfflineItem(slots[i], result);
            int index = i;
            var button = slots[i].GetComponent<Button>() ?? slots[i].gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => BindCard(tx.Results[index], true));
            button.interactable = true;
            slots[i].transform.localScale = Vector3.one;
        }
        _slotFrame.gameObject.SetActive(tx.Results.Count == 11);
        BindCard(tx.Results[tx.Results.Count - 1], tx.Results.Count == 11);
        ResultCard.transform.SetAsLastSibling(); _slotFrame.SetAsLastSibling();
        foreach (var result in tx.Results) View.NotifyCollectionReveal(result);
        _hudElapsed = 0; _hudTick = EditorApplication.timeSinceStartup;
        RefreshHud();
    }

    private static StaffGachaAcquisitionItem Acquisition(GachaAcquisitionResult r)
        => (StaffGachaAcquisitionItem)Activator.CreateInstance(typeof(StaffGachaAcquisitionItem),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { r.Id, r.Rank, r.IsNew, r.PandaTokenReward }, null);

    private void BindCard(GachaAcquisitionResult result, bool summary)
    {
        if (result.Data is GachaStaffData staff) ResultCard.TrySetStaffAcquisitionResult(staff, Acquisition(result));
        else BindOfflineItem(ResultCard, result);
        ResultCard.SetPosition(summary ? new Vector3(600, 0, 0) : Vector3.zero);
        ResultCard.SetScale(1); ResultCard.gameObject.SetActive(true);
        var close = Read<Button>(ResultCard, "_closeButton");
        if (close != null) Bind(close, () => ShowMachine(_machine));
    }

    private void BindOfflineItem(Component card, GachaAcquisitionResult result)
    {
        // Native SetData's effect text reads live UserInfo. Project the same catalog art,
        // rank frame and badge while deriving ownership text solely from the memory snapshot.
        foreach (string method in new[] { "SetImage", "UpdateFrame", "SetName", "SetDescription", "SetType" })
            card.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(card, new object[] { result.Data });
        Read<UIItemStar>(card, "_itemStar").SetStar(result.Rank);
        var name = Read<TextMeshProUGUI>(card, "_nameText");
        var anchor = card is UIGachaCardSlot ? Read<Image>(card, "_star1Frame").rectTransform : null;
        GachaAcquisitionBadge.Bind(card.transform, name.font, result.IsNew, anchor);
        Economy.Snapshot.ItemCounts.TryGetValue(result.Id, out int count);
        Read<TextMeshProUGUI>(card, "_effectText").text = "메모리 데모 보유 " + count + "개";
    }

    private void ClearResult()
    {
        if (View == null) return;
        if (ResultCard != null) ResultCard.gameObject.SetActive(false);
        if (_slotFrame != null)
        {
            _slotFrame.gameObject.SetActive(false);
            if (_slotParent != null) { _slotFrame.SetParent(_slotParent, true); _slotFrame.SetSiblingIndex(_slotSibling); }
        }
        foreach (var obj in _hiddenArt) if (obj != null) obj.SetActive(true);
        _restoreMachineArt?.Invoke(); _restoreMachineArt = null;
        _hiddenArt.Clear(); _slotFrame = null; _slotParent = null; ResultCard = null;
        View.SetStartGacha(false); View.CollectionExchange?.SetVisible(false);
        View.CollectionHud?.SnapToCommitted();
    }

    public void OpenExchange(TokenExchangeTab tab)
    {
        ShowMachine(_machine); View.OpenCollectionExchange(); View.CollectionExchange.SelectTab(tab); Repaint();
    }

    public void ShowFairies()
    {
        ClearResult(); _showFloor = true; View.gameObject.SetActive(false);
        _floor.SetVisibility(true, true); _floor.Tick(0.05f); Repaint();
    }

    private void WireButtons()
    {
        Bind(Staff.SingleButton, () => Draw(GachaMachineKind.Staff));
        Bind(Read<Button>(Staff, "_tenButton"), () => Draw(GachaMachineKind.Staff, null, true));
        Bind(Item.SingleButton, () => Draw(GachaMachineKind.Item));
        Bind(Read<Button>(Item, "_tenButton"), () => Draw(GachaMachineKind.Item, null, true));
        Bind(Read<Button>(View, "_leftButton"), () => ShowMachine(GachaMachineKind.Item));
        Bind(Read<Button>(View, "_rightButton"), () => ShowMachine(GachaMachineKind.Staff));
        var exit = View.transform.Find("Anime UI/UI Components/Exit Button")?.GetComponent<Button>();
        if (exit != null) Bind(exit, Close);
        if (View.CollectionHud != null)
            Bind(Read<Button>(View.CollectionHud, "_ticket"), () => DrawWithPayment(_machine, null, GachaPaymentKind.TicketSingle));
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    { if (button == null) return; button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); button.interactable = true; }

    private void RefreshHud()
    {
        Write(View, "_nextCollectionRefresh", 0f);
        typeof(UIGacha).GetMethod("UpdateCollectionUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(View, null);
        Staff.SingleButton.interactable = Economy.CanDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, out _);
        Read<Button>(Staff, "_tenButton").interactable = Economy.CanDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsEleven, out _);
        Item.SingleButton.interactable = Economy.CanDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, out _);
        Read<Button>(Item, "_tenButton").interactable = Economy.CanDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsEleven, out _);
    }

    private void Tick()
    {
        if (!IsReady) return;
        _recorder?.PollCompletion();
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextTick) return;
        // One elapsed-time simulation per display opportunity. Never catch up by running
        // multiple steps after focus/compilation stalls, and never advance from OnGUI.
        _nextTick = now + PreviewFrameSeconds;
        float delta = Mathf.Clamp((float)(now - _lastTick), 0, .1f); _lastTick = now;
        if (_showFloor)
        {
            long probeTime = System.Diagnostics.Stopwatch.GetTimestamp();
            long probeBytes = GC.GetAllocatedBytesForCurrentThread();
            _floor.Tick(delta);
            EnhancementFairyFrameProbe.RecordSimulation(now,
                (System.Diagnostics.Stopwatch.GetTimestamp() - probeTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
                GC.GetAllocatedBytesForCurrentThread() - probeBytes, Fairies.ActiveCount);
        }
        else
        {
            View.CollectionExchange?.TickPresentation(Time.realtimeSinceStartup);
            RefreshHud();
            foreach (var rotation in _root.GetComponentsInChildren<RotationGameObject>())
                if (rotation.enabled && Read<bool>(rotation, "_isStart"))
                    Read<GameObject>(rotation, "_gameObject")?.transform.Rotate(0, 0, Read<float>(rotation, "_rotatePerMinute") * delta);
            var hud = View.CollectionHud;
            if (hud != null && hud.PendingProgressCount > 0)
            {
                _hudElapsed += (float)(now - _hudTick);
                float duration = Read<bool>(hud, "_guaranteed") ? .8f : .16f;
                Write(hud, "_start", Time.unscaledTime - _hudElapsed);
                Write(hud, "_until", Time.unscaledTime - _hudElapsed + duration);
                typeof(GachaCollectionMachineHud).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                if (_hudElapsed >= duration) _hudElapsed = 0;
            }
            _hudTick = now;
        }
        Repaint();
    }

    private void OnFocus()
    {
        _lastTick = _hudTick = EditorApplication.timeSinceStartup;
        _nextTick = _lastTick;
    }

    private void OnGUI()
    {
        if (!IsReady) { EditorGUILayout.HelpBox("메뉴에서 통합 오프라인 데모를 열어 주세요.", MessageType.Info); return; }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("직원 머신", EditorStyles.toolbarButton)) ShowMachine(GachaMachineKind.Staff);
            if (GUILayout.Button("아이템 머신", EditorStyles.toolbarButton)) ShowMachine(GachaMachineKind.Item);
            if (GUILayout.Button("토큰 교환소", EditorStyles.toolbarButton)) OpenExchange(TokenExchangeTab.Tickets);
            if (GUILayout.Button("3층 요정", EditorStyles.toolbarButton)) ShowFairies();
            if (GUILayout.Button("데모 종료", EditorStyles.toolbarButton)) Close();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1회 뽑기")) Draw(_machine);
            if (GUILayout.Button("10+1회")) Draw(_machine, null, true);
            if (GUILayout.Button("Unique 확인")) { Reset(0); Draw(GachaMachineKind.Staff, new[] { Catalog.OfType<GachaStaffData>().First(x => x.Rank == Rank.Unique).Id }); }
            if (GUILayout.Button("100회 보장 확인")) { Reset(99); Draw(_machine); }
            if (GUILayout.Button("요정 획득 확인")) { Draw(GachaMachineKind.Item, new[] { Catalog.OfType<GachaItemData>().First(x => x.UpgradeType != UpgradeType.None).Id }); ShowFairies(); }
            if (GUILayout.Button("초기화")) Reset();
        }
        EditorGUILayout.LabelField(_message, EditorStyles.miniLabel);
        Rect available = GUILayoutUtility.GetRect(1, 10000, 1, 10000);
        float width = Mathf.Min(available.width, available.height * 16 / 9);
        var rect = new Rect(available.x + (available.width - width) / 2, available.y, width, width * 9 / 16);
        if (Event.current.type == EventType.Repaint)
        {
            // Layout, hover and expose events can request extra repaint passes; these only
            // present the cached texture until the next frame opportunity.
            if (EditorApplication.timeSinceStartup - _lastRender >= PreviewFrameSeconds * .8) Render();
            GUI.DrawTexture(rect, _target, ScaleMode.StretchToFill, false);
        }
        RouteInput(rect);
    }

    private void Render()
    {
        double probeNow = EditorApplication.timeSinceStartup;
        long probeTime = System.Diagnostics.Stopwatch.GetTimestamp();
        long probeBytes = GC.GetAllocatedBytesForCurrentThread();
        var camera = _showFloor ? _floor.Camera : _camera;
        camera.targetTexture = _target;
        if (!_showFloor)
        {
            foreach (var transform in _root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }
        camera.Render();
        _lastRender = probeNow;
        _recorder?.Capture(_target, probeNow);
        if (_showFloor) EnhancementFairyFrameProbe.RecordRender(probeNow,
            (System.Diagnostics.Stopwatch.GetTimestamp() - probeTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
            GC.GetAllocatedBytesForCurrentThread() - probeBytes);
    }

    public void RenderToPng(string path)
    {
        Render(); var previous = RenderTexture.active;
        var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = _target;
            image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
    }

    private void RouteInput(Rect rect)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseUp && !rect.Contains(evt.mousePosition)) _pointerDown = null;
        if (_showFloor || !rect.Contains(evt.mousePosition) || (!evt.isMouse && evt.type != EventType.ScrollWheel)) return;
        var point = new Vector2((evt.mousePosition.x - rect.x) / rect.width * _target.width,
            (1 - (evt.mousePosition.y - rect.y) / rect.height) * _target.height);
        var pointer = new PointerEventData(_events) { position = point, button = PointerEventData.InputButton.Left,
            scrollDelta = new Vector2(0, -evt.delta.y) };
        var hits = new List<RaycastResult>();
        foreach (var raycaster in _root.GetComponentsInChildren<GraphicRaycaster>()) raycaster.Raycast(pointer, hits);
        var hit = hits.OrderByDescending(x => x.sortingOrder).ThenByDescending(x => x.depth).FirstOrDefault().gameObject;
        if (evt.type == EventType.MouseDown && evt.button == 0)
        { _pointerDown = hit; evt.Use(); }
        else if (evt.type == EventType.MouseUp && evt.button == 0)
        {
            if (hit != null && hit == _pointerDown) ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
            _pointerDown = null; evt.Use();
        }
        else if (evt.type == EventType.ScrollWheel && hit != null)
        { ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler); evt.Use(); }
    }

    private void PlayStateChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingEditMode) Close(); }
    private void OnDisable() => DisposePreview();
    private void DisposePreview()
    {
        _recorder?.Dispose(); _recorder = null;
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= DisposePreview;
        if (_root != null) Object.DestroyImmediate(_root);
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        _scene = default; _root = null;
        _floor?.Dispose(); _floor = null;
        _fonts?.Dispose(); _fonts = null;
        _catalog?.Dispose(); _catalog = null;
        if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); _target = null; }
        View = null; Staff = null; Item = null; ResultCard = null; Economy = null;
        if (ActiveWindow == this) ActiveWindow = null;
    }

    private static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (field != null) return field; }
        throw new MissingFieldException(obj.GetType().FullName, name);
    }
    private static T Read<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    private static void Write(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}
#endif
