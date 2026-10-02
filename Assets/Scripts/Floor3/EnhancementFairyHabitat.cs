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
        public Vector2 PuffCenter;
        public Vector2 StarCenter;
        public bool ArrivalEffectsVisible;
        public int SortingOrder = int.MinValue;
        public float RenderedAlpha = -1f;
        public EnhancementFairyBrain Brain;
        public float Scale;
        public float ArrivalRemaining;
        public bool ArrivalStarted;
        public float BirthElapsed;
        public Vector2 BirthPosition, TrailStart, TrailControl;
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
    private bool _waitForCameraFrame;
    private Camera _worldCamera;
    private UIGachaCard _cardTemplate;
    private GameObject _cardCanvas;
    private GachaResultCardPopup _itemPopup;
    private readonly List<View> _birthViews = new List<View>(5);
    // Presentation randomness must never consume gameplay/gacha RNG state.
    private readonly System.Random _birthRandom = new System.Random();
    private float _nextBirthAt;

    public int OwnedTypeCount => _owned.Count;
    public int ActiveCount => _visible && gameObject.activeInHierarchy ? _views.Count : 0;
    public int PooledObjectCount => _pool.Count + _views.Count;
    public int ArrivalPresentationCount { get; private set; }
    public bool IsVisible => _visible && gameObject.activeInHierarchy;
    public EnhancementFairySettings Settings => _settings;
    public bool IsItemCardOpen => _itemPopup != null && _itemPopup.IsOpen;
    public string ActiveBirthItemId => _birthViews.Count > 0 ? _birthViews[0].Brain.ItemId : null;
    public float BirthElapsed => _birthViews.Count > 0 ? _birthViews[0].BirthElapsed : 0f;
    public int ActiveBirthCount => _birthViews.Count;
    public IEnumerable<string> ActiveBirthItemIds
    {
        get { foreach (var view in _birthViews) yield return view.Brain.ItemId; }
    }
    public Vector2 GetBirthPosition(string itemId)
    {
        foreach (var view in _views) if (view.Brain.ItemId == itemId) return view.BirthPosition;
        return Vector2.zero;
    }

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
        _nextBirthAt = 0f;
        _cursor = _rotationSlot = 0;
        _arrivals.Clear();
        ArrivalPresentationCount = 0;
        RestoreOwnership(owned);
    }

    public void SetFloorVisible(bool unlocked, bool visible)
    {
        bool next = unlocked && visible;
        if (!next) PauseBirth();
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
    private void OnConfirmedAcquisition()
    {
        for (int i = _birthViews.Count - 1; i >= 0; i--)
            if (!_arrivals.IsPending(_birthViews[i].Brain.ItemId)) PauseBirth(_birthViews[i]);
        RefreshRuntimeOwnership(); Reconcile();
    }
    private void OnCameraMoving()
    {
        _waitForCameraFrame = true;
        PauseBirth();
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
        // The existing camera Tween invokes completion before writing its final
        // Transform. Choose a visible birth position on the following normal update.
        _waitForCameraFrame = true;
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
        // Prioritize pending arrivals even when the ordinary roster is already at its limit.
        foreach (string id in _arrivals.PendingItems)
        {
            if (!_owned.Contains(id) || !_catalog.ContainsKey(id) || HasView(id) || _catalog[id].Sprite == null) continue;
            if (_views.Count >= cap)
            {
                int replace = FindReplaceable(true);
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
        for (int i = 0; i < _views.Count; i++)
            if (_arrivals.IsPending(_views[i].Brain.ItemId)) BeginArrival(_views[i]);
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

    private int FindReplaceable(bool forArrival = false)
    {
        for (int n = 0; n < _views.Count; n++)
        {
            int i = (_rotationSlot + n) % _views.Count;
            if ((!forArrival && _views[i].PriorityUntil > _clock) || _views[i].ArrivalRemaining > 0f
                || _arrivals.IsPending(_views[i].Brain.ItemId)) continue;
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
        view.ArrivalStarted = false;
        view.BirthElapsed = 0f;
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
        if (_birthViews.Contains(view) || _birthViews.Count >= Mathf.Clamp(_settings.MaxConcurrentBirths, 1, 5)
            || _clock < _nextBirthAt || !CanPresentBirth() || !IsNextWaitingBirth(view)) return;
        if (!TryFindBirthPosition(view, out Vector2 position, out Rect area)) return;
        view.BirthPosition = position;
        view.BirthElapsed = 0f;
        view.Brain.PlaceForBirth(position); // The ordinary brain stays on its existing ground plane.
        float angle = BirthRange(0f, Mathf.PI * 2f);
        Vector2 endpoint = position + Vector2.up * _settings.SpriteHeight * .5f;
        view.TrailStart = ClampPoint(area, endpoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BirthRange(4f, 7f));
        Vector2 travel = endpoint - view.TrailStart;
        view.TrailControl = ClampPoint(area, (endpoint + view.TrailStart) * .5f
            + new Vector2(-travel.y, travel.x).normalized * BirthRange(-3f, 3f));
        _birthViews.Add(view);
        view.ArrivalRemaining = _settings.BirthDuration;
        view.PriorityUntil = _clock + _settings.BirthDuration + Mathf.Max(1f, _settings.NewArrivalPrioritySeconds);
        view.Fade = 1f;
        view.Replacing = false;
        _nextBirthAt = _clock + BirthRange(Mathf.Max(.02f, _settings.BirthStartDelay.x),
            Mathf.Max(.02f, _settings.BirthStartDelay.y));
        if (!view.ArrivalStarted) { ArrivalPresentationCount++; view.ArrivalStarted = true; }
        Render(view);
    }

    private bool IsNextWaitingBirth(View candidate)
    {
        foreach (string id in _arrivals.PendingItems)
        {
            bool started = false;
            foreach (var active in _birthViews) if (active.Brain.ItemId == id) { started = true; break; }
            if (!started) return id == candidate.Brain.ItemId;
        }
        return false;
    }

    private float BirthRange(float min, float max) => Mathf.Lerp(min, Mathf.Max(min, max), (float)_birthRandom.NextDouble());
    private static Vector2 ClampPoint(Rect area, Vector2 point) => new Vector2(
        Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));

    private bool TryGetVisibleBirthArea(out Rect area)
    {
        area = _settings.BirthArea;
        // Keep each airborne x inside the existing landing bounds, away from the
        // room's outer frame. Clamping must never merge two landing destinations.
        area = Rect.MinMaxRect(Mathf.Max(area.xMin, _settings.SafeGroundArea.xMin), area.yMin,
            Mathf.Min(area.xMax, _settings.SafeGroundArea.xMax), area.yMax);
        if (_runtime)
        {
            if (_worldCamera == null) return false;
            float depth = _worldCamera.WorldToViewportPoint(transform.position).z;
            if (depth <= 0f) return false;
            Vector2 min = transform.InverseTransformPoint(_worldCamera.ViewportToWorldPoint(new Vector3(.08f, .06f, depth)));
            Vector2 max = transform.InverseTransformPoint(_worldCamera.ViewportToWorldPoint(new Vector3(.92f, .88f, depth)));
            float radius = Mathf.Max(_settings.ArrivalPuffSize * .55f, _settings.SpriteHeight * .85f);
            area = Rect.MinMaxRect(Mathf.Max(area.xMin, min.x + radius), Mathf.Max(area.yMin, min.y + radius),
                Mathf.Min(area.xMax, max.x - radius), Mathf.Min(area.yMax, max.y - Mathf.Max(radius, _settings.SpriteHeight + _settings.HopHeight)));
        }
        return area.width > .5f && area.height > .5f;
    }

    private bool TryFindBirthPosition(View candidate, out Vector2 position, out Rect area)
    {
        position = Vector2.zero;
        if (!TryGetVisibleBirthArea(out area)) return false;
        float separation = Mathf.Max(_settings.BirthMinSeparation, _settings.ArrivalPuffSize * 1.1f + .4f);
        // Bounded sampling, then a staggered grid fallback. If space is unavailable,
        // the confirmed item waits; it is never forced onto another birth or consumed.
        int offset = _birthRandom.Next(24);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            if (attempt < 40) position = new Vector2(BirthRange(area.xMin, area.xMax), BirthRange(area.yMin, area.yMax));
            else
            {
                int cell = (attempt - 40 + offset) % 24;
                position = new Vector2(Mathf.Lerp(area.xMin, area.xMax, (cell % 8 + .5f) / 8f),
                    Mathf.Lerp(area.yMin, area.yMax, (cell / 8 + .5f) / 3f));
            }
            bool free = true;
            foreach (var active in _birthViews)
                if (Vector2.Distance(active.BirthPosition, position) < separation
                    || Mathf.Abs(active.Brain.GroundPosition.x - position.x) < LandingHalfWidth(active) + LandingHalfWidth(candidate))
                { free = false; break; }
            if (free) return true;
        }
        return false;
    }

    private float LandingHalfWidth(View view) => Mathf.Max(_settings.SpriteHeight * .7f,
        view.Body.sprite.bounds.size.x * view.Scale * .56f) + .15f;

    private bool CanPresentBirth()
    {
        if (!_visible || _approachingFloor3 || _waitForCameraFrame || !gameObject.activeInHierarchy || IsItemCardOpen) return false;
        if (!_runtime) return true;
        if (_cameraController == null || _cameraController.IsCameraMoving || _worldCamera == null
            || _cameraController.CurrentFloor != ERestaurantFloorType.Floor3
            || _waitingForUiHide || HasClosingViews()
            || (_navigation != null && _navigation.GetOpenViewCount() > 0)) return false;
        foreach (var view in _sceneViews)
            if (view != null && view.gameObject.activeInHierarchy
                && view.VisibleState != VisibleState.Disappeared) return false;
        return TryGetVisibleBirthArea(out _);
    }

    private void PauseBirth(View view)
    {
        if (!_birthViews.Remove(view)) return;
        view.BirthElapsed = view.ArrivalRemaining = 0f;
        if (view.Root != null) Render(view);
    }

    private void PauseBirth()
    {
        while (_birthViews.Count > 0) PauseBirth(_birthViews[_birthViews.Count - 1]);
        _nextBirthAt = _clock;
    }

    private void AdvanceBirth(float deltaTime)
    {
        if (_birthViews.Count == 0) return;
        if (!CanPresentBirth()) { PauseBirth(); return; }
        TryGetVisibleBirthArea(out Rect area);
        for (int i = _birthViews.Count - 1; i >= 0; i--)
        {
            var view = _birthViews[i];
            if (!_arrivals.IsPending(view.Brain.ItemId) || !area.Contains(view.BirthPosition)) { PauseBirth(view); continue; }
            view.BirthElapsed += deltaTime;
            view.ArrivalRemaining = Mathf.Max(0f, _settings.BirthDuration - view.BirthElapsed);
            if (view.ArrivalRemaining > 0f) continue;
            _arrivals.Consume(view.Brain.ItemId);
            _birthViews.RemoveAt(i);
            Render(view);
        }
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
        EffectGeometry(_settings.ArrivalSprite, out view.PuffCenter, out view.PuffScalePerUnit);
        Rect visible = _settings.ArrivalVisibleRect;
        if (_settings.ArrivalSprite != null && visible.width > 0f && visible.height > 0f
            && (visible.width < 1f || visible.height < 1f))
        {
            var sprite = _settings.ArrivalSprite;
            Vector2 size = Vector2.Scale(sprite.rect.size, visible.size) / sprite.pixelsPerUnit;
            view.PuffCenter = (Vector2.Scale(sprite.rect.size, visible.center) - sprite.pivot) / sprite.pixelsPerUnit;
            view.PuffScalePerUnit = 1f / Mathf.Max(.01f, Mathf.Max(size.x, size.y));
        }
        EffectGeometry(_settings.SparkleSprite, out view.StarCenter, out view.StarScalePerUnit);
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
        PauseBirth(view);
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
        _waitForCameraFrame = false;
        float dt = Mathf.Min(deltaTime, 0.1f);
        _clock += dt;
        _rotationClock += dt;
        AdvanceBirth(dt);
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            var neighbour = _views.Count > 1 ? _views[(i + 1) % _views.Count].Brain.GroundPosition : Vector2.zero;
            // Keep each birth at its own ground position; resume its ordinary brain on landing.
            if (!_arrivals.IsPending(view.Brain.ItemId)) view.Brain.Step(dt, neighbour, _views.Count > 1);
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
            || (_arrivals.PendingCount > 0 && _birthViews.Count < Mathf.Clamp(_settings.MaxConcurrentBirths, 1, 5) && _clock >= _nextBirthAt))
            Reconcile();
    }

    private void Render(View view)
    {
        var brain = view.Brain;
        float duration = Mathf.Max(0.1f, _settings.ArrivalSeconds);
        bool arriving = view.ArrivalRemaining > 0f;
        float revealAt = Mathf.Max(.1f, _settings.TrailSeconds) + Mathf.Max(.05f, _settings.GatherSeconds);
        bool revealed = arriving && view.BirthElapsed >= revealAt;
        float progress = revealed ? Mathf.Clamp01((view.BirthElapsed - revealAt) / duration) : 1f;
        float settleAt = revealAt + duration;
        float settle = arriving ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((view.BirthElapsed - settleAt)
            / Mathf.Max(.1f, _settings.BirthSettleSeconds))) : 1f;
        view.Root.localPosition = arriving ? (Vector3)Vector2.Lerp(view.BirthPosition, brain.GroundPosition, settle)
            : (Vector3)brain.GroundPosition;
        float pop = progress < 1f ? Mathf.Clamp01(progress / Mathf.Max(0.1f, _settings.ArrivalScaleInFraction))
            + Mathf.Sin(progress * Mathf.PI) * _settings.ArrivalScaleOvershoot : 1f;
        float scale = view.Scale * pop;
        float jumpWave = progress * Mathf.PI * Mathf.Clamp(_settings.BirthJumpCount, 1, 2);
        float squash = revealed ? Mathf.Sin(jumpWave * 2f) * .055f : brain.Squash;
        var bodyScale = new Vector3(scale * (1f + squash), scale * (1f - squash), 1f);
        var rotation = Quaternion.Euler(0f, 0f, revealed ? Mathf.Sin(jumpWave * 2f) * 7f : brain.Tilt);
        // A sprite's pivot, tilt, flip and squash must not lift its lowest mesh point.
        float minY = float.PositiveInfinity;
        foreach (var vertex in view.SpriteVertices)
        {
            var point = new Vector3((brain.FacingLeft ? -vertex.x : vertex.x) * bodyScale.x,
                vertex.y * bodyScale.y, 0f);
            minY = Mathf.Min(minY, (rotation * point).y);
        }
        if (float.IsInfinity(minY)) minY = 0f;
        float lift = revealed
            ? Mathf.Abs(Mathf.Sin(jumpWave)) * Mathf.Max(0f, _settings.HopHeight) * Mathf.Lerp(1f, .7f, progress) : brain.VisualHeight;
        view.BodyTransform.SetLocalPositionAndRotation(
            new Vector3(-view.SpriteCenter.x * scale, -minY + lift, 0f), rotation);
        view.BodyTransform.localScale = bodyScale;
        view.Body.flipX = brain.FacingLeft;
        int sortingOrder = 500 - Mathf.RoundToInt(brain.GroundPosition.y * 10f);
        if (view.SortingOrder != sortingOrder)
        { view.SortingOrder = sortingOrder; view.Body.sortingOrder = sortingOrder; }
        float alpha = _arrivals.IsPending(brain.ItemId) && !revealed ? 0f : view.Fade;
        if (view.RenderedAlpha != alpha)
        { view.RenderedAlpha = alpha; view.Body.color = new Color(1f, 1f, 1f, alpha); }
        bool puff = revealed && progress < 1f;
        if (view.ArrivalEffectsVisible != puff)
        {
            view.ArrivalEffectsVisible = puff;
            view.Puff.enabled = puff;
            for (int i = 0; i < view.Stars.Length; i++) view.Stars[i].enabled = puff;
        }
        if (arriving && !revealed) { RenderBirthTrail(view, sortingOrder); return; }
        // Most frames have no arrival. Keep their six disabled renderers untouched.
        if (!puff)
        {
            for (int i = 0; i < view.Stars.Length; i++) view.Stars[i].enabled = false;
            return;
        }
        view.Puff.sortingOrder = sortingOrder - 1;
        PlaceEffect(view.PuffTransform, new Vector2(0f, _settings.SpriteHeight * 0.5f), view.PuffCenter,
            view.PuffScalePerUnit * Mathf.Max(0f, _settings.ArrivalPuffSize * Mathf.Lerp(0.35f, 1f, progress)), 0f);
        view.Puff.color = new Color(1f, 0.93f, 0.78f, (1f - progress) * _settings.ArrivalPuffOpacity);
        for (int i = 0; i < view.Stars.Length; i++)
        {
            var star = view.Stars[i];
            float angle = i * Mathf.PI * 2f / view.Stars.Length;
            PlaceEffect(view.StarTransforms[i], new Vector2(Mathf.Cos(angle) * progress * _settings.ArrivalStarRadius.x,
                _settings.SpriteHeight * 0.45f + Mathf.Sin(angle) * progress * _settings.ArrivalStarRadius.y), view.StarCenter,
                view.StarScalePerUnit * Mathf.Max(0f, _settings.ArrivalStarSize * (1f - progress)), i * 30f);
            star.sortingOrder = sortingOrder + 1;
            star.color = new Color(1f, 0.88f, 0.47f, 1f - progress);
        }
    }

    private void RenderBirthTrail(View view, int sortingOrder)
    {
        float travel = Mathf.Max(.1f, _settings.TrailSeconds);
        bool gathering = view.BirthElapsed >= travel;
        float gather = Mathf.Clamp01((view.BirthElapsed - travel) / Mathf.Max(.05f, _settings.GatherSeconds));
        for (int i = 0; i < view.Stars.Length; i++)
        {
            var star = view.Stars[i];
            star.enabled = i < 3;
            if (i >= 3) continue;
            float progress = Mathf.Clamp01((view.BirthElapsed - i * .065f) / travel);
            Vector2 endpoint = view.BirthPosition + Vector2.up * _settings.SpriteHeight * .5f;
            float remaining = 1f - progress;
            Vector2 point = remaining * remaining * view.TrailStart + 2f * remaining * progress * view.TrailControl + progress * progress * endpoint;
            if (gathering) point = Vector2.Lerp(point, endpoint, gather);
            float size = (gathering ? Mathf.Lerp(_settings.TrailStarSize, _settings.GatherStarSize, gather) : _settings.TrailStarSize) * (i == 0 ? 1f : .45f);
            PlaceEffect(view.StarTransforms[i], point - view.BirthPosition, view.StarCenter,
                view.StarScalePerUnit * size, view.BirthElapsed * 100f + i * 30f);
            star.color = new Color(1f, .91f, .5f, i == 0 ? 1f : .5f);
            star.sortingOrder = sortingOrder + 2;
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
        PauseBirth();
        CloseItemCard();
        OnItemCardClosed();
    }

    private static void EffectGeometry(Sprite sprite, out Vector2 center, out float scalePerUnit)
    {
        center = Vector2.zero;
        scalePerUnit = 1f;
        if (sprite == null) return;
        // Existing effects have large transparent margins. Their tight mesh, not the
        // original texture rectangle/pivot, defines the visible size and path center.
        var vertices = sprite.vertices;
        Bounds bounds = sprite.bounds;
        if (vertices.Length > 0)
        {
            bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]);
        }
        center = bounds.center;
        scalePerUnit = 1f / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.y));
    }

    private static void PlaceEffect(Transform target, Vector2 position, Vector2 meshCenter, float scale, float degrees)
    {
        var rotation = Quaternion.Euler(0f, 0f, degrees);
        target.SetLocalPositionAndRotation((Vector3)position - rotation * (Vector3)(meshCenter * scale), rotation);
        target.localScale = Vector3.one * scale;
    }

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
