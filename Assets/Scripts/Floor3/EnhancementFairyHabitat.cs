using System;
using System.Collections;
using System.Collections.Generic;
using Muks.UI;
using UnityEngine;

/// <summary>A passive, pooled presentation attached only to the actual third-floor root.</summary>
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

    public int OwnedTypeCount => _owned.Count;
    public int ActiveCount => _visible && gameObject.activeInHierarchy ? _views.Count : 0;
    public int PooledObjectCount => _pool.Count + _views.Count;
    public int ArrivalPresentationCount { get; private set; }
    public bool IsVisible => _visible && gameObject.activeInHierarchy;
    public EnhancementFairySettings Settings => _settings;

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
        // Once at attachment, never a scene search in Update.
        _cameraController = FindFirstObjectByType<CameraController>();
        _navigation = FindFirstObjectByType<UINavigationCoordinator>();
        _sceneViews = FindObjectsByType<UIView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (_cameraController != null)
        {
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
    private void OnCameraMoving() => SetFloorVisible(false, false);
    private void OnCameraStopped(ERestaurantFloorType floor, RestaurantType restaurant) => RefreshRuntimeVisibility();

    private void OnUiOpened()
    {
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
        bool unlocked = UserInfo.GetUnlockFloor(UserInfo.CurrentStage) >= ERestaurantFloorType.Floor3;
        bool visible = _cameraController != null && _cameraController.isActiveAndEnabled
            && _cameraController.CurrentFloor == ERestaurantFloorType.Floor3
            && _cameraController.CurrentRestaurant == RestaurantType.Hall
            && !_waitingForUiHide && !closing
            && (_navigation == null || _navigation.GetOpenViewCount() == 0);
        SetFloorVisible(unlocked, visible);
    }

    private void Reconcile()
    {
        if (!_visible || !gameObject.activeInHierarchy || _settings == null || _arrivals == null) return;
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
        view.Scale = _settings.SpriteHeight / Mathf.Max(0.01f, bounds.size.y);
        view.ArrivalRemaining = 0f;
        view.PriorityUntil = 0f;
        view.Fade = 0f;
        view.Replacing = false;
        view.Root.gameObject.SetActive(true);
        _views.Add(view);
        if (_arrivals.IsPending(id)) BeginArrival(view);
        Render(view);
    }

    private void BeginArrival(View view)
    {
        if (!_arrivals.Consume(view.Brain.ItemId)) return;
        view.ArrivalRemaining = Mathf.Max(0.1f, _settings.ArrivalSeconds);
        view.PriorityUntil = _clock + Mathf.Max(1f, _settings.NewArrivalPrioritySeconds);
        view.Fade = 1f;
        view.Replacing = false;
        ArrivalPresentationCount++;
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
        float dt = Mathf.Min(deltaTime, 0.1f);
        _clock += dt;
        _rotationClock += dt;
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            var neighbour = _views.Count > 1 ? _views[(i + 1) % _views.Count].Brain.GroundPosition : Vector2.zero;
            view.Brain.Step(dt, neighbour, _views.Count > 1);
            view.ArrivalRemaining = Mathf.Max(0f, view.ArrivalRemaining - dt);
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
        float baseHeight = _settings.SpriteHeight * 0.5f - view.SpriteCenter.y * scale;
        view.BodyTransform.SetLocalPositionAndRotation(
            new Vector3(-view.SpriteCenter.x * scale, baseHeight + brain.VisualHeight, 0f),
            Quaternion.Euler(0f, 0f, brain.Tilt));
        view.BodyTransform.localScale = new Vector3(scale * (1f + brain.Squash), scale * (1f - brain.Squash), 1f);
        view.Body.flipX = brain.FacingLeft;
        int sortingOrder = 500 - Mathf.RoundToInt(brain.GroundPosition.y * 10f);
        if (view.SortingOrder != sortingOrder)
        { view.SortingOrder = sortingOrder; view.Body.sortingOrder = sortingOrder; }
        if (view.RenderedAlpha != view.Fade)
        { view.RenderedAlpha = view.Fade; view.Body.color = new Color(1f, 1f, 1f, view.Fade); }
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

    private static float SpriteScale(Sprite sprite, float worldSize) => sprite == null ? 1f
        : Mathf.Max(0f, worldSize) / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));

    private void OnDestroy()
    {
        if (_runtime)
        {
            UserInfo.OnGiveGachaItemHandler -= RefreshRuntimeOwnership;
            UserInfo.OnChangeFloorHandler -= RefreshRuntimeVisibility;
            EnhancementFairyAcquisitionEvents.Changed -= OnConfirmedAcquisition;
            if (_cameraController != null)
            {
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
