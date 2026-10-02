using System;
using System.Collections;
using System.Collections.Generic;
using Muks.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pooled third-floor presentation with read-only item inspection.</summary>
[DisallowMultipleComponent]
public sealed class EnhancementFairyHabitat : MonoBehaviour
{
    private sealed class View
    {
        public Transform Root;
        public SpriteRenderer Body;
        public SpriteRenderer Puff;
        public SpriteRenderer[] Stars;
        public Transform BodyTransform;
        public Transform PuffTransform;
        public Transform[] StarTransforms;
        public Vector2 SpriteCenter;
        public Vector2[] SpriteVertices;
        public float PuffScalePerUnit;
        public float StarScalePerUnit;
        public bool ArrivalEffectsVisible;
        public int SortingOrder = int.MinValue;
        public float RenderedAlpha = -1f;
        public EnhancementFairyBrain Brain;
        public float Scale;
        public float ArrivalRemaining;
        public float PriorityUntil;
        public float Fade = 1f;
        public bool Replacing;
    }

    private readonly Dictionary<string, GachaItemData> _catalog = new Dictionary<string, GachaItemData>(StringComparer.Ordinal);
    private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> _ordered = new List<string>();
    private readonly List<View> _views = new List<View>();
    private readonly List<View> _pool = new List<View>();
    private EnhancementFairyArrivalQueue _arrivals;
    private EnhancementFairySettings _settings;
    private CameraController _cameraController;
    private Floor3Controller _floorController;
    private FloorLockGroup[] _floorLocks = Array.Empty<FloorLockGroup>();
    private UINavigationCoordinator _navigation;
    private UIView[] _sceneViews = Array.Empty<UIView>();
    private Coroutine _visibilityWait;
    private bool _waitingForUiHide;
    private bool _runtime;
    private bool _visible;
    private bool _settingsOwned;
    private float _clock;
    private float _rotationClock;
    private int _cursor;
    private int _rotationSlot;
    private bool _approachingFloor3;
    private Camera _worldCamera;
    private UIGachaCard _cardTemplate;
    private GameObject _cardCanvas;
    private GachaResultCardPopup _itemPopup;

    public int OwnedTypeCount => _owned.Count;
    public int ActiveCount => _visible && gameObject.activeInHierarchy ? _views.Count : 0;
    public int PooledObjectCount => _pool.Count + _views.Count;
    public int ArrivalPresentationCount { get; private set; }
    public bool IsVisible => _visible && gameObject.activeInHierarchy;
    public EnhancementFairySettings Settings => _settings;
    public bool IsItemCardOpen => _itemPopup != null && _itemPopup.IsOpen;

    public static EnhancementFairyHabitat AttachTo(Floor3Controller floor)
    {
        if (floor == null) return null;
        var habitat = floor.GetComponent<EnhancementFairyHabitat>();
        if (habitat == null) habitat = floor.gameObject.AddComponent<EnhancementFairyHabitat>();
        habitat.InitializeRuntime();
        return habitat;
    }

    public void InitializeRuntime()
    {
        if (_runtime) return;
        _runtime = true;
        LoadSettings(null);
        SetCatalog(ItemManager.Instance.GetGachaItemDataList());
        _arrivals = EnhancementFairyAcquisitionEvents.Queue;
        // Bind to this floor's scene, including an isolated host loading the real Stage1.
        // An additive scene or a preview must never borrow another scene's camera/UI.
        _floorController = GetComponent<Floor3Controller>();
        _floorLocks = GetComponentsInChildren<FloorLockGroup>(true);
        _cameraController = SceneComponents<CameraController>().Find(value => value.isActiveAndEnabled);
        _navigation = SceneComponents<UINavigationCoordinator>().Find(value => value.isActiveAndEnabled);
        _sceneViews = SceneComponents<UIView>().ToArray();
        var itemMachine = SceneComponents<UIItemGacha>().Find(value => value != null);
        _cardTemplate = itemMachine != null ? itemMachine.GetComponentInChildren<UIGachaCard>(true) : null;
        if (_cameraController != null)
        {
            _worldCamera = _cameraController.GetComponent<Camera>();
            _cameraController.OnPreviewFloorHandler += OnPreviewFloor;
            _cameraController.OnWorldTapHandler += TryOpenItemCard;
            _cameraController.OnStartMoveCameraHandler += OnCameraMoving;
            _cameraController.OnEndMoveCameraHandler += OnCameraStopped;
        }
        if (_navigation != null)
        {
            _navigation.OnShowUIHandler += OnUiOpened;
            _navigation.OnHideUIHandler += OnUiClosed;
        }
        UserInfo.OnGiveGachaItemHandler += RefreshRuntimeOwnership;
        UserInfo.OnChangeFloorHandler += RefreshRuntimeVisibility;
        EnhancementFairyAcquisitionEvents.Changed += OnConfirmedAcquisition;
        RefreshRuntimeOwnership();
        RefreshRuntimeVisibility();
    }

    private List<T> SceneComponents<T>() where T : Component
    {
        var result = new List<T>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    /// <summary>Detached offline host: no UserInfo, SDK, singleton, save file or PlayerPrefs access.</summary>
    public void ConfigureOffline(IEnumerable<GachaItemData> catalog, IEnumerable<string> owned,
        EnhancementFairySettings settings = null, EnhancementFairyArrivalQueue arrivals = null)
    {
        if (_runtime) throw new InvalidOperationException("Use a detached habitat for an offline preview.");
        LoadSettings(settings);
        _arrivals = arrivals ?? new EnhancementFairyArrivalQueue();
        SetCatalog(catalog);
        RestoreOwnership(owned);
        SetFloorVisible(true, true);
    }

    private void LoadSettings(EnhancementFairySettings settings)
    {
        if (_settings != null) return;
        _settings = settings != null ? settings : Resources.Load<EnhancementFairySettings>(EnhancementFairySettings.ResourcePath);
        if (_settings != null) return;
        _settings = ScriptableObject.CreateInstance<EnhancementFairySettings>();
        _settingsOwned = true;
    }

    private void SetCatalog(IEnumerable<GachaItemData> catalog)
    {
        _catalog.Clear();
        if (catalog == null) return;
        foreach (var item in catalog)
            if (EnhancementFairyCatalog.IsEligible(item)) _catalog[item.Id] = item;
    }

    /// <summary>Snapshot restore is deliberately silent. Zero remaining material still means owned.</summary>
    public void RestoreOwnership(IEnumerable<string> itemIds)
    {
        _owned.Clear();
        _ordered.Clear();
        if (itemIds != null)
        {
            foreach (string id in itemIds)
                if (id != null && _catalog.ContainsKey(id) && _owned.Add(id)) _ordered.Add(id);
        }
        _ordered.Sort(StringComparer.Ordinal);
        for (int i = _views.Count - 1; i >= 0; i--)
            if (!_owned.Contains(_views[i].Brain.ItemId)) Release(i);
        Reconcile();
    }

    /// <summary>Offline committed-result adapter; production uses PublishConfirmed after commit.</summary>
    public bool ConfirmAcquisition(string transactionId, GachaItemData item)
    {
        if (!EnhancementFairyCatalog.IsEligible(item) || !_catalog.ContainsKey(item.Id)) return false;
        bool isNew = _arrivals.Confirm(transactionId, item.Id);
        if (isNew && _owned.Add(item.Id))
        {
            _ordered.Add(item.Id);
            _ordered.Sort(StringComparer.Ordinal);
        }
        Reconcile();
        return isNew;
    }

    /// <summary>Reset an isolated demo account without retaining its former first-acquisition dedup state.</summary>
    public void ResetOfflineSession(IEnumerable<string> owned)
    {
        if (_runtime) throw new InvalidOperationException("Runtime arrivals follow the account session lifecycle.");
        // A new demo account must not inherit an in-flight puff or protected slot from the former one.
        while (_views.Count > 0) Release(_views.Count - 1);
        _clock = _rotationClock = 0f;
        _cursor = _rotationSlot = 0;
        _arrivals.Clear();
        ArrivalPresentationCount = 0;
        RestoreOwnership(owned);
    }

    public void SetFloorVisible(bool unlocked, bool visible)
    {
        bool next = unlocked && visible;
        if (_visible == next)
        {
            enabled = next;
            // An already-visible flag can outlive an inactive parent in an offline host.
            // Explicitly showing that parent again must restore its pending arrivals too.
            if (next) Reconcile();
            return;
        }
        _visible = next;
        enabled = next; // events wake the component; no Update calls for a hidden third floor
        for (int i = 0; i < _views.Count; i++) _views[i].Root.gameObject.SetActive(next);
        if (next) Reconcile();
    }

    private void RefreshRuntimeOwnership() => RestoreOwnership(UserInfo.GetGiveGachaItemCountDic().Keys);
    private void OnConfirmedAcquisition() { RefreshRuntimeOwnership(); Reconcile(); }
    private void OnCameraMoving()
    {
        CloseItemCard();
        // Leave third-floor objects in world space during a pan; the end event applies
        // the destination floor. Horizontal Hall/Kitchen movement needs no hide/rebuild.
        if (!_approachingFloor3 && (_cameraController == null || _cameraController.CurrentFloor != ERestaurantFloorType.Floor3))
            SetFloorVisible(false, false);
    }
    private void OnPreviewFloor(ERestaurantFloorType floor)
    {
        bool wasVisible = _visible;
        _approachingFloor3 = floor == ERestaurantFloorType.Floor3;
        RefreshRuntimeVisibility();
        if (!wasVisible && _visible)
        {
            // Revealed artwork is fully ready on this frame, with no arrival fade delay.
            foreach (var view in _views)
            {
                if (!view.Replacing && !_arrivals.IsPending(view.Brain.ItemId)) view.Fade = 1f;
                Render(view);
            }
        }
    }
    private void OnCameraStopped(ERestaurantFloorType floor, RestaurantType restaurant)
    {
        _approachingFloor3 = false;
        RefreshRuntimeVisibility();
    }

    private void OnUiOpened()
    {
        CloseItemCard();
        if (_visibilityWait != null) StopCoroutine(_visibilityWait);
        _visibilityWait = null;
        _waitingForUiHide = false;
        SetFloorVisible(false, false);
    }

    private void OnUiClosed()
    {
        if (_visibilityWait != null) StopCoroutine(_visibilityWait);
        _visibilityWait = null;
        _waitingForUiHide = true;
        SetFloorVisible(false, false);
        if (gameObject.activeInHierarchy) _visibilityWait = StartCoroutine(ResumeAfterClosingViews());
    }

    private IEnumerator ResumeAfterClosingViews()
    {
        // Navigation removes a view immediately, before its Hide tween has completed.
        yield return null;
        while (HasClosingViews()) yield return null;
        _waitingForUiHide = false;
        _visibilityWait = null;
        RefreshRuntimeVisibility();
    }

    private bool HasClosingViews()
    {
        for (int i = 0; i < _sceneViews.Length; i++)
        {
            var view = _sceneViews[i];
            if (view != null && view.gameObject.scene == gameObject.scene && view.gameObject.activeInHierarchy
                && view.VisibleState == VisibleState.Disappearing) return true;
        }
        return false;
    }

    private void RefreshRuntimeVisibility()
    {
        if (!_runtime) return;
        bool closing = HasClosingViews();
        if (_waitingForUiHide && !closing)
        {
            // A parent can suspend a wait coroutine; a later camera/floor event can safely resume it.
            if (_visibilityWait != null) StopCoroutine(_visibilityWait);
            _visibilityWait = null;
            _waitingForUiHide = false;
        }
        // Stage1's decorative third floor is accessible with the normal Floor2 progression
        // value. Follow its actual active floor/lock objects; do not invent a second unlock
        // requirement or mutate the account's business-floor progression.
        bool unlocked = _floorController != null && _floorController.isActiveAndEnabled;
        for (int i = 0; i < _floorLocks.Length; i++)
            if (_floorLocks[i] != null && _floorLocks[i].isActiveAndEnabled) unlocked = false;
        bool visible = _cameraController != null && _cameraController.isActiveAndEnabled
            && (_cameraController.CurrentFloor == ERestaurantFloorType.Floor3 || _approachingFloor3)
            && !_waitingForUiHide && !closing
            && (_navigation == null || _navigation.GetOpenViewCount() == 0);
        SetFloorVisible(unlocked, visible);
    }

    private void Reconcile()
    {
        if (!gameObject.activeInHierarchy || _settings == null || _arrivals == null) return;
        // A confirmed purchase on Floor3 prepares its object immediately even while the
        // gacha UI covers it. The puff remains pending until that UI has actually closed.
        if (!_visible && !(_runtime && _cameraController != null
            && _cameraController.CurrentFloor == ERestaurantFloorType.Floor3 && _arrivals.PendingCount > 0)) return;
        int cap = Mathf.Clamp(_settings.MaxActive, 1, 64);
        while (_views.Count > cap) Release(_views.Count - 1);
        for (int i = 0; i < _views.Count; i++)
            if (_arrivals.IsPending(_views[i].Brain.ItemId)) BeginArrival(_views[i]);

        // Prioritize pending arrivals even when the ordinary roster is already at its limit.
        for (int i = 0; i < _ordered.Count; i++)
        {
            string id = _ordered[i];
            if (!_arrivals.IsPending(id) || HasView(id) || _catalog[id].Sprite == null) continue;
            if (_views.Count >= cap)
            {
                int replace = FindReplaceable();
                if (replace < 0) break;
                Release(replace);
            }
            Spawn(id);
        }
        while (_views.Count < cap)
        {
            string id = NextUnshown();
            if (id == null) break;
            Spawn(id);
        }
    }

    private bool HasView(string id)
    {
        for (int i = 0; i < _views.Count; i++) if (_views[i].Brain.ItemId == id) return true;
        return false;
    }

    private string NextUnshown()
    {
        for (int n = 0; n < _ordered.Count; n++)
        {
            if (_cursor >= _ordered.Count) _cursor = 0;
            string id = _ordered[_cursor++];
            if (!HasView(id) && _catalog[id].Sprite != null) return id;
        }
        return null;
    }

    private int FindReplaceable()
    {
        for (int n = 0; n < _views.Count; n++)
        {
            int i = (_rotationSlot + n) % _views.Count;
            if (_views[i].PriorityUntil > _clock || _views[i].ArrivalRemaining > 0f) continue;
            _rotationSlot = (i + 1) % _views.Count;
            return i;
        }
        return -1;
    }

    private void Spawn(string id)
    {
        View view;
        if (_pool.Count > 0) { view = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); }
        else view = CreateView();
        var sprite = _catalog[id].Sprite;
        view.Brain = new EnhancementFairyBrain(id, _settings);
        view.Root.name = "Fairy " + id;
        view.Body.sprite = sprite;
        Bounds bounds = sprite.bounds;
        view.SpriteCenter = bounds.center;
        view.SpriteVertices = sprite.vertices;
        view.Scale = _settings.SpriteHeight / Mathf.Max(0.01f, bounds.size.y);
        view.ArrivalRemaining = 0f;
        view.PriorityUntil = 0f;
        view.Fade = 0f;
        view.Replacing = false;
        view.Root.gameObject.SetActive(_visible);
        _views.Add(view);
        if (_arrivals.IsPending(id)) BeginArrival(view);
        Render(view);
    }

    private void BeginArrival(View view)
    {
        if (!_visible || _approachingFloor3) return;
        if (!_arrivals.Consume(view.Brain.ItemId)) return;
        view.ArrivalRemaining = Mathf.Max(0.1f, _settings.ArrivalSeconds);
        view.PriorityUntil = _clock + Mathf.Max(1f, _settings.NewArrivalPrioritySeconds);
        view.Fade = 1f;
        view.Replacing = false;
        ArrivalPresentationCount++;
        Render(view);
    }

    private View CreateView()
    {
        var root = new GameObject("Fairy").transform;
        root.SetParent(transform, false);
        root.gameObject.layer = Mathf.Clamp(_settings.VisualLayer, 0, 31); // Default: Ignore Raycast.
        var view = new View { Root = root, Stars = new SpriteRenderer[5], StarTransforms = new Transform[5] };
        view.Body = CreateRenderer(root, "Item sprite", null);
        view.BodyTransform = view.Body.transform;
        view.Puff = CreateRenderer(root, "Arrival puff", _settings.ArrivalSprite);
        view.PuffTransform = view.Puff.transform;
        view.PuffScalePerUnit = SpriteScale(_settings.ArrivalSprite, 1f);
        view.StarScalePerUnit = SpriteScale(_settings.SparkleSprite, 1f);
        view.Puff.enabled = false;
        for (int i = 0; i < view.Stars.Length; i++)
        {
            view.Stars[i] = CreateRenderer(root, "Arrival sparkle", _settings.SparkleSprite);
            view.StarTransforms[i] = view.Stars[i].transform;
            view.Stars[i].enabled = false;
        }
        return view;
    }

    private SpriteRenderer CreateRenderer(Transform parent, string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName);
        go.layer = Mathf.Clamp(_settings.VisualLayer, 0, 31);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = _settings.SortingLayer;
        return renderer;
    }

    private void Release(int index)
    {
        var view = _views[index];
        view.Root.gameObject.SetActive(false);
        view.Body.sprite = null;
        view.Brain = null;
        _views.RemoveAt(index);
        _pool.Add(view);
    }

    private void OnEnable()
    {
        if (_settings == null) return;
        if (_runtime)
        {
            // A parent becoming inactive cancels its coroutines; restart the closing-view wait on return.
            if (HasClosingViews()) OnUiClosed();
            else { _waitingForUiHide = false; RefreshRuntimeVisibility(); }
        }
        else if (_visible) Reconcile();
    }

    private void Update() => AdvancePreview(Time.deltaTime);

    /// <summary>Same update as the game, exposed for a detached Editor preview and deterministic tests.</summary>
    public void AdvancePreview(float deltaTime)
    {
        if (!_visible || !gameObject.activeInHierarchy || _settings == null || deltaTime <= 0f
            || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        // A slow or cancelled reveal must not spend pending births, rotation slots or behavior time.
        if (_approachingFloor3) return;
        float dt = Mathf.Min(deltaTime, 0.1f);
        _clock += dt;
        _rotationClock += dt;
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            var neighbour = _views.Count > 1 ? _views[(i + 1) % _views.Count].Brain.GroundPosition : Vector2.zero;
            // Keep each birth at its own ground position; resume its ordinary brain on landing.
            if (!_approachingFloor3 && view.ArrivalRemaining <= 0f) view.Brain.Step(dt, neighbour, _views.Count > 1);
            if (!_approachingFloor3) view.ArrivalRemaining = Mathf.Max(0f, view.ArrivalRemaining - dt);
            view.Fade = Mathf.MoveTowards(view.Fade, view.Replacing ? 0f : 1f, dt / Mathf.Max(0.1f, _settings.RotationFadeSeconds));
            if (view.Replacing && view.Fade <= 0f) { Release(i); continue; }
            Render(view);
        }
        if (_rotationClock >= Mathf.Max(1f, _settings.RotationSeconds) && _ordered.Count > _views.Count)
        {
            _rotationClock = 0f;
            int replace = FindReplaceable();
            if (replace >= 0) _views[replace].Replacing = true;
        }
        // New arrivals waiting for a priority slot are retried at a bounded cadence.
        if (_views.Count < Mathf.Min(_ordered.Count, Mathf.Clamp(_settings.MaxActive, 1, 64))
            || (_arrivals.PendingCount > 0 && Mathf.FloorToInt(_clock * 2f) != Mathf.FloorToInt((_clock - dt) * 2f)))
            Reconcile();
    }

    private void Render(View view)
    {
        var brain = view.Brain;
        view.Root.localPosition = new Vector3(brain.GroundPosition.x, brain.GroundPosition.y, 0f);
        float duration = Mathf.Max(0.1f, _settings.ArrivalSeconds);
        float progress = view.ArrivalRemaining > 0f ? 1f - view.ArrivalRemaining / duration : 1f;
        float pop = progress < 1f ? Mathf.Clamp01(progress / Mathf.Max(0.1f, _settings.ArrivalScaleInFraction))
            + Mathf.Sin(progress * Mathf.PI) * _settings.ArrivalScaleOvershoot : 1f;
        float scale = view.Scale * pop;
        float squash = view.ArrivalRemaining > 0f ? 0f : brain.Squash;
        var bodyScale = new Vector3(scale * (1f + squash), scale * (1f - squash), 1f);
        var rotation = Quaternion.Euler(0f, 0f, brain.Tilt);
        // A sprite's pivot, tilt, flip and squash must not lift its lowest mesh point.
        float minY = float.PositiveInfinity;
        foreach (var vertex in view.SpriteVertices)
        {
            var point = new Vector3((brain.FacingLeft ? -vertex.x : vertex.x) * bodyScale.x,
                vertex.y * bodyScale.y, 0f);
            minY = Mathf.Min(minY, (rotation * point).y);
        }
        if (float.IsInfinity(minY)) minY = 0f;
        float lift = view.ArrivalRemaining > 0f
            ? Mathf.Sin(progress * Mathf.PI) * Mathf.Max(0f, _settings.HopHeight) : brain.VisualHeight;
        view.BodyTransform.SetLocalPositionAndRotation(
            new Vector3(-view.SpriteCenter.x * scale, -minY + lift, 0f), rotation);
        view.BodyTransform.localScale = bodyScale;
        view.Body.flipX = brain.FacingLeft;
        int sortingOrder = 500 - Mathf.RoundToInt(brain.GroundPosition.y * 10f);
        if (view.SortingOrder != sortingOrder)
        { view.SortingOrder = sortingOrder; view.Body.sortingOrder = sortingOrder; }
        float alpha = _arrivals.IsPending(brain.ItemId) ? 0f : view.Fade;
        if (view.RenderedAlpha != alpha)
        { view.RenderedAlpha = alpha; view.Body.color = new Color(1f, 1f, 1f, alpha); }
        bool puff = view.ArrivalRemaining > 0f;
        if (view.ArrivalEffectsVisible != puff)
        {
            view.ArrivalEffectsVisible = puff;
            view.Puff.enabled = puff;
            for (int i = 0; i < view.Stars.Length; i++) view.Stars[i].enabled = puff;
        }
        // Most frames have no arrival. Keep their six disabled renderers untouched.
        if (!puff) return;
        view.Puff.sortingOrder = sortingOrder - 1;
        view.PuffTransform.localPosition = new Vector3(0f, _settings.SpriteHeight * 0.5f, 0f);
        view.PuffTransform.localScale = Vector3.one * view.PuffScalePerUnit *
            Mathf.Max(0f, _settings.ArrivalPuffSize * Mathf.Lerp(0.35f, 1f, progress));
        view.Puff.color = new Color(1f, 0.93f, 0.78f, (1f - progress) * _settings.ArrivalPuffOpacity);
        for (int i = 0; i < view.Stars.Length; i++)
        {
            var star = view.Stars[i];
            float angle = i * Mathf.PI * 2f / view.Stars.Length;
            view.StarTransforms[i].localPosition = new Vector3(Mathf.Cos(angle) * progress * _settings.ArrivalStarRadius.x,
                _settings.SpriteHeight * 0.45f + Mathf.Sin(angle) * progress * _settings.ArrivalStarRadius.y, 0f);
            view.StarTransforms[i].localScale = Vector3.one * view.StarScalePerUnit *
                Mathf.Max(0f, _settings.ArrivalStarSize * (1f - progress));
            star.sortingOrder = sortingOrder + 1;
            star.color = new Color(1f, 0.88f, 0.47f, 1f - progress);
        }
    }

    public EnhancementFairyBrain GetActiveBrain(int index) => _views[index].Brain;

    /// <summary>Called by the camera only for a completed, UI-free tap, never a drag.</summary>
    public bool TryOpenItemCard(Vector2 screenPosition)
    {
        if (!_runtime || !IsVisible || IsItemCardOpen || _worldCamera == null || _cardTemplate == null
            || _cameraController.CurrentFloor != ERestaurantFloorType.Floor3 || _approachingFloor3
            || (_navigation != null && _navigation.GetOpenViewCount() > 0)) return false;
        View nearest = null;
        float distance = float.PositiveInfinity;
        float minimumSize = Mathf.Clamp(Screen.dpi * .7f / 2.54f, 44f, 96f);
        foreach (var view in _views)
        {
            if (!view.Body.enabled || view.Body.color.a < .5f || !view.Root.gameObject.activeInHierarchy) continue;
            var bounds = view.Body.bounds;
            Vector3 lower = _worldCamera.WorldToScreenPoint(bounds.min);
            Vector3 upper = _worldCamera.WorldToScreenPoint(bounds.max);
            if (lower.z <= 0f) continue;
            Vector2 center = (lower + upper) * .5f;
            Vector2 size = new Vector2(Mathf.Max(minimumSize, upper.x - lower.x), Mathf.Max(minimumSize, upper.y - lower.y));
            if (!new Rect(center - size * .5f, size).Contains(screenPosition)) continue;
            float candidate = (screenPosition - center).sqrMagnitude;
            if (candidate < distance) { nearest = view; distance = candidate; }
        }
        if (nearest == null) return false;
        if (_itemPopup == null)
        {
            _cardCanvas = new GameObject("Fairy Item Inspection", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _cardCanvas.transform.SetParent(transform, false);
            var canvas = _cardCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _cardCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            _itemPopup = new GachaResultCardPopup(_cardCanvas.transform, _cardTemplate);
            _itemPopup.Closed += OnItemCardClosed;
        }
        _itemPopup.ShowItem(_catalog[nearest.Brain.ItemId], false);
        _cameraController.SetWorldInputBlocked(this, true);
        return true;
    }

    public void CloseItemCard() => _itemPopup?.Hide();
    private void OnItemCardClosed() => _cameraController?.SetWorldInputBlocked(this, false);

    private void OnDisable()
    {
        CloseItemCard();
        OnItemCardClosed();
    }

    private static float SpriteScale(Sprite sprite, float worldSize) => sprite == null ? 1f
        : Mathf.Max(0f, worldSize) / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));

    private void OnDestroy()
    {
        CloseItemCard();
        OnItemCardClosed();
        _itemPopup?.Dispose();
        if (_runtime)
        {
            UserInfo.OnGiveGachaItemHandler -= RefreshRuntimeOwnership;
            UserInfo.OnChangeFloorHandler -= RefreshRuntimeVisibility;
            EnhancementFairyAcquisitionEvents.Changed -= OnConfirmedAcquisition;
            if (_cameraController != null)
            {
                _cameraController.OnPreviewFloorHandler -= OnPreviewFloor;
                _cameraController.OnWorldTapHandler -= TryOpenItemCard;
                _cameraController.OnStartMoveCameraHandler -= OnCameraMoving;
                _cameraController.OnEndMoveCameraHandler -= OnCameraStopped;
            }
            if (_navigation != null)
            {
                _navigation.OnShowUIHandler -= OnUiOpened;
                _navigation.OnHideUIHandler -= OnUiClosed;
            }
        }
        if (_settingsOwned && _settings != null)
        {
            if (Application.isPlaying) Destroy(_settings);
            else DestroyImmediate(_settings);
        }
    }
}
