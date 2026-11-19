using System;
using System.Collections;
using System.Collections.Generic;
using Muks.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pooled third-floor presentation with read-only item inspection.</summary>
[DisallowMultipleComponent]
public sealed partial class EnhancementFairyHabitat : MonoBehaviour
{
    private enum HouseMotion { None, Emerging, Returning, Fading }
    private sealed partial class View
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
        public ushort[] SpriteTriangles;
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
        public HouseMotion HouseMotion;
        public bool HouseSpawned;
        public float HouseElapsed;
        public Vector2 HouseStart, HouseEnd;
        public float HouseScale = 1f, HouseLift;
        public float DisplayedAt;
        public float HouseReturnSpeed, HouseReturnTimeout;
        public long DisplaySequence;
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
    private readonly System.Random _birthSoundRandom = new System.Random();
    private readonly object _popSoundOwner = new object(), _voiceSoundOwner = new object();
    private SoundManager _soundManager;
    private int _lastVoiceVariant = -1;
    private float _nextBirthAt;
    private object _houseOwner;
    private Transform _houseEntrance;
    private EnhancementFairyHouseSettings _houseSettings;
    private readonly Queue<string> _houseRequests = new Queue<string>();
    private readonly HashSet<string> _houseQueued = new HashSet<string>(StringComparer.Ordinal);
    private bool _houseSession;
    private float _nextHouseSpawnAt, _houseReturnDueAt = float.PositiveInfinity, _nextHouseReturnAt;
    private long _displaySequence;
    private int _houseExitSequence;
    // Independent presentation variation, never UnityEngine.Random or gameplay RNG.
    private System.Random _houseReturnRandom = new System.Random();
    private ThirdFloorFairyPlayArea _playArea;
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
    public int BaseActiveLimit => _settings == null ? 12 : Mathf.Clamp(_settings.MaxActive, 1, 64);
    public int HouseHardCap => Mathf.Min(24, BaseActiveLimit + (_houseSettings?.SafeExtraLimit ?? 0));
    public int PendingHouseSpawnCount => _houseRequests.Count;
    public bool HasHouseDoorwayActivity
    {
        get
        {
            if (_houseRequests.Count > 0) return true;
            foreach (var view in _views) if (view.HouseMotion != HouseMotion.None) return true;
            return false;
        }
    }
    public int HouseEmergenceCount { get; private set; }
    public float HouseActivitySeconds => _clock;
    public float HouseReturnDueAt => _houseReturnDueAt;
    public float NextHouseReturnAt => _nextHouseReturnAt;
    public int ReturningHouseCount
    {
        get { int count = 0; foreach (var view in _views) if (IsReturning(view)) count++; return count; }
    }
    public IEnumerable<string> ReturningHouseItemIds
    {
        get { foreach (var view in _views) if (IsReturning(view)) yield return view.Brain.ItemId; }
    }
    public bool CanUseHouse => HasActiveHouse && IsVisible && isActiveAndEnabled && CanPresentBirth()
        && (!_runtime || (_cameraController != null && !_cameraController.IsWorldInputBlocked
            && !UserInfo.IsTutorialStart && IsHouseEntranceOnScreen()));

    private bool HasActiveHouse => _houseOwner != null && _houseSettings != null
        && _houseEntrance != null && _houseEntrance.gameObject.activeInHierarchy;
    private static bool IsReturning(View view) => view.HouseMotion == HouseMotion.Returning || view.HouseMotion == HouseMotion.Fading;
    public IEnumerable<string> ActiveBirthItemIds
    {
        get { foreach (var view in _birthViews) yield return view.Brain.ItemId; }
    }
    public Vector2 GetBirthPosition(string itemId)
    {
        foreach (var view in _views) if (view.Brain.ItemId == itemId) return view.BirthPosition;
        return Vector2.zero;
    }

    public void RegisterHouse(object owner, Transform entrance, EnhancementFairyHouseSettings settings)
    {
        if (owner == null || entrance == null || settings == null) return;
        if (!ReferenceEquals(_houseOwner, owner)) CancelHouseSession();
        _houseOwner = owner;
        _houseEntrance = entrance;
        _houseSettings = settings;
    }

    public void UnregisterHouse(object owner)
    {
        if (!ReferenceEquals(_houseOwner, owner)) return;
        CancelHouseSession();
        _houseOwner = null;
        _houseEntrance = null;
    }

    /// <summary>Only already-owned, unshown, non-pending items enter this bounded presentation queue.</summary>
    public int RequestHouseEmergence(object owner, int requested)
    {
        if (!ReferenceEquals(_houseOwner, owner) || !CanUseHouse || requested <= 0) return 0;
        int slots = HouseHardCap - _views.Count - _houseRequests.Count;
        // A full house never accepts a delayed request against a future return.
        // Only a successful emergence below the cap can restart the stay timer.
        bool wasEmpty = _houseRequests.Count == 0;
        int room = Mathf.Min(Mathf.Max(0, slots), Mathf.Max(0, _houseSettings.SafeExtraLimit - _houseRequests.Count));
        int accepted = 0;
        foreach (string id in _ordered)
        {
            if (accepted >= Mathf.Min(requested, room)) break;
            if (HasView(id) || _houseQueued.Contains(id) || _arrivals.IsPending(id) || _catalog[id].Sprite == null) continue;
            _houseQueued.Add(id);
            _houseRequests.Enqueue(id);
            _houseRequestDeadlines[id] = _clock + _houseSettings.SafeExitClearanceTimeoutSeconds;
            accepted++;
        }
        if (accepted > 0)
        {
            _houseSession = true;
            // A rotation fade selected just before this tap must not silently
            // remove a guest during the new temporary-stay session.
            foreach (var view in _views) view.Replacing = false;
            if (wasEmpty) _nextHouseSpawnAt = _clock + _houseSettings.SafeTapSpawnFirstDelay;
            // The first visual response belongs to this tap; it does not wait for
            // an earlier guest to finish its doorway animation or outward walk.
            AdvanceHouseQueue();
        }
        return accepted;
    }

    private bool IsHouseEntranceOnScreen()
    {
        if (_worldCamera == null || _houseEntrance == null) return false;
        Vector3 point = _worldCamera.WorldToViewportPoint(_houseEntrance.position);
        return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
    }

    private bool IsViewOnScreen(View view)
    {
        if (!_runtime) return true;
        if (_worldCamera == null) return false;
        var bounds = view.Body.bounds;
        Vector3 min = _worldCamera.WorldToViewportPoint(bounds.min), max = _worldCamera.WorldToViewportPoint(bounds.max);
        return max.z > 0f && max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f;
    }

    private void ClearHouseRequests()
    {
        _houseRequests.Clear();
        _houseQueued.Clear();
        _houseRequestDeadlines.Clear();
    }

    private int FindOldestHouseReturnCandidate(bool allowEmerging = false)
    {
        int selected = -1;
        for (int i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            if (IsReturning(view) || (!allowEmerging && view.HouseMotion != HouseMotion.None)
                || view.ArrivalRemaining > 0f || _arrivals.IsPending(view.Brain.ItemId)
                || view.PriorityUntil > _clock
                || (!allowEmerging && (float.IsPositiveInfinity(view.DisplayedAt)
                    || _clock - view.DisplayedAt < _houseSettings.SafeMinimumIndividualStaySeconds))) continue;
            // Current presentation order, never acquisition date or screen-pan dwell time.
            // Simultaneous first-visible frames use the stable instance order.
            if (selected < 0 || view.DisplayedAt < _views[selected].DisplayedAt
                || (view.DisplayedAt == _views[selected].DisplayedAt
                    && view.DisplaySequence < _views[selected].DisplaySequence)) selected = i;
        }
        return selected;
    }

    private void CancelHouseSession()
    {
        CancelAllSpacing();
        ClearHouseRequests();
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            if (IsReturning(view)) { Release(i); continue; }
            if (view.HouseMotion == HouseMotion.Emerging) view.Brain.PlaceForBirth(view.HouseEnd);
            view.HouseMotion = HouseMotion.None;
            view.HouseScale = 1f;
            view.HouseLift = 0f;
            if (view.Root != null) Render(view);
        }
        // Hidden/disabled visuals must not leave a coroutine waiting for a doorway.
        // Births and their priority window remain protected even during cleanup.
        while (_views.Count > BaseActiveLimit)
        {
            int oldest = FindOldestHouseReturnCandidate(true);
            if (oldest < 0) break;
            Release(oldest);
        }
        _houseSession = _views.Count > BaseActiveLimit;
        _houseReturnDueAt = _houseSession ? _clock : float.PositiveInfinity;
        _nextHouseSpawnAt = _nextHouseReturnAt = _clock;
    }

    private void AdvanceHouseQueue()
    {
        PruneHouseRequests(); // Expire/invalid cleanup also runs while the cap or camera blocks spawning.
        if (_houseRequests.Count == 0 || _clock < _nextHouseSpawnAt || !CanUseHouse || _views.Count >= HouseHardCap) return;
        while (_houseRequests.Count > 0)
        {
            string id = _houseRequests.Peek();
            if (!TryPrepareHouseExit(id, out Vector2 landing, out Vector2 destination, out int side)) return;
            _houseRequests.Dequeue();
            _houseQueued.Remove(id);
            _houseRequestDeadlines.Remove(id);
            Spawn(id);
            var view = _views[_views.Count - 1];
            Vector2 start = transform.InverseTransformPoint(_houseEntrance.position);
            _houseExitSequence++;
            view.HouseStart = start;
            // The distant landing reserves room for the later walk. The doorway
            // reveal itself stays close, with SmoothStep's peak speed no faster
            // than the ordinary calm walk (its maximum derivative is 1.5).
            float calmSpeed = Mathf.Max(.1f, _settings.WalkSpeed) * Mathf.Max(.1f, _settings.CalmEnergy);
            view.HouseEnd = Vector2.MoveTowards(start, landing,
                calmSpeed * _houseSettings.SafeEntranceDuration / 1.5f);
            view.HouseExitSide = side;
            view.HasPlannedExit = true;
            view.PlannedExitTarget = destination;
            view.Brain.PlaceForHouse(start);
            view.HouseMotion = HouseMotion.Emerging;
            view.HouseSpawned = true;
            view.HouseElapsed = 0f;
            view.HouseScale = .65f;
            view.Fade = .15f;
            HouseEmergenceCount++;
            LastHouseExitDecision = "started: reserved landing and same-side outward target";
            // Keep the scheduled cadence instead of adding a frame's rounding on
            // every guest. A slow frame still creates at most one guest per call.
            _nextHouseSpawnAt = Mathf.Max(_nextHouseSpawnAt + _houseSettings.SafeSpawnInterval, _clock);
            if (_views.Count > BaseActiveLimit)
                _houseReturnDueAt = _clock + _houseSettings.SafeTemporaryPlaySeconds;
            Render(view);
            break; // One emergence per cadence, even after a slow frame.
        }
    }

    private void BeginHouseReturns()
    {
        if (!_houseSession) return;
        if (!HasActiveHouse) { CancelHouseSession(); return; }
        if (_views.Count <= BaseActiveLimit && ReturningHouseCount == 0)
        {
            _houseReturnDueAt = float.PositiveInfinity;
            if (_houseRequests.Count == 0) _houseSession = false;
            return;
        }
        // A confirmed birth can claim a reserved house slot before its queued
        // emergence. That priority path still owes a bounded overflow lifetime.
        if (_views.Count > BaseActiveLimit && float.IsPositiveInfinity(_houseReturnDueAt))
            _houseReturnDueAt = _clock + _houseSettings.SafeTemporaryPlaySeconds;
        int returningCount = ReturningHouseCount;
        if (_clock < _houseReturnDueAt || _clock < _nextHouseReturnAt
            || returningCount >= _houseSettings.SafeMaximumConcurrentReturns
            || _views.Count - returningCount <= BaseActiveLimit) return;
        int index = FindOldestHouseReturnCandidate();
        if (index < 0) return;
        var view = _views[index];
        CancelSpacing(view);
        view.Replacing = false;
        view.Fade = 1f;
        view.HouseMotion = HouseMotion.Returning;
        view.HouseElapsed = 0f;
        view.HouseEnd = transform.InverseTransformPoint(_houseEntrance.position);
        float distance = Vector2.Distance(view.Brain.GroundPosition, view.HouseEnd);
        float calmSpeed = Mathf.Max(.1f, _settings.WalkSpeed) * Mathf.Max(.1f, _settings.CalmEnergy);
        view.HouseReturnSpeed = Mathf.Max(.001f, Mathf.Min(calmSpeed, Mathf.Min(_houseSettings.SafeReturnMoveSpeed,
            distance / _houseSettings.SafeReturnTravelSeconds)));
        // Four seconds is a preferred minimum, not an order to rush across the
        // entire third floor. A distant fairy walks longer and keeps a finite guard.
        view.HouseReturnTimeout = Mathf.Max(_houseSettings.SafeReturnTimeoutSeconds,
            distance / view.HouseReturnSpeed + 1f);
    }

    private bool AdvanceHouseMotion(View view, float deltaTime)
    {
        if (view.HouseMotion == HouseMotion.None) return false;
        view.HouseElapsed += deltaTime;
        if (view.HouseMotion == HouseMotion.Emerging)
        {
            float progress = Mathf.Clamp01(view.HouseElapsed / _houseSettings.SafeEntranceDuration);
            view.Brain.MoveForHouse(Vector2.Lerp(view.HouseStart, view.HouseEnd, Mathf.SmoothStep(0f, 1f, progress)), deltaTime);
            view.HouseScale = Mathf.Lerp(.65f, 1f, progress);
            view.HouseLift = Mathf.Sin(progress * Mathf.PI) * Mathf.Min(.35f, _settings.HopHeight);
            view.Fade = Mathf.Lerp(.15f, 1f, progress);
            if (progress >= 1f)
            {
                view.Brain.PlaceForBirth(view.HouseEnd);
                view.HouseMotion = HouseMotion.None;
                view.HouseScale = 1f;
                view.HouseLift = 0f;
                BeginHouseDispersal(view);
            }
        }
        else if (view.HouseMotion == HouseMotion.Returning)
        {
            Vector2 next = Vector2.MoveTowards(view.Brain.GroundPosition, view.HouseEnd,
                view.HouseReturnSpeed * deltaTime);
            view.Brain.MoveForHouse(next, deltaTime);
            if (Vector2.SqrMagnitude(next - view.HouseEnd) <= .0025f
                || view.HouseElapsed >= view.HouseReturnTimeout)
            {
                // Fade only at the doorway, including the bounded safety fallback.
                view.Brain.PlaceForHouse(view.HouseEnd);
                view.HouseMotion = HouseMotion.Fading;
                view.HouseElapsed = 0f;
            }
        }
        else
        {
            float progress = Mathf.Clamp01(view.HouseElapsed / _houseSettings.SafeReturnFadeSeconds);
            view.HouseScale = Mathf.Lerp(1f, .65f, progress);
            view.Fade = 1f - progress;
            if (progress >= 1f)
            {
                // The idle gap starts after arrival AND fade, never when travel starts.
                _nextHouseReturnAt = _clock + Mathf.Lerp(_houseSettings.SafeReturnStaggerMinSeconds,
                    _houseSettings.SafeReturnStaggerMaxSeconds, (float)_houseReturnRandom.NextDouble());
                return true;
            }
        }
        return false;
    }

    public void RegisterPlayArea(ThirdFloorFairyPlayArea area)
    {
        if (area == null || ReferenceEquals(_playArea, area)) return;
        _playArea?.CancelAll();
        _playArea = area;
    }

    public void UnregisterPlayArea(ThirdFloorFairyPlayArea area)
    {
        if (!ReferenceEquals(_playArea, area)) return;
        _playArea.CancelAll();
        _playArea = null;
    }

    // Furniture is now visual-only. These anchors/speed still belong to the
    // existing house dispersal clearance, not an activity or reservation system.
    private float FurnitureWalkSpeed => Mathf.Max(.1f, _settings.WalkSpeed) * Mathf.Max(.1f, _settings.CalmEnergy);
    private Vector2 FurnitureLocal(Transform target) => transform.InverseTransformPoint(target.position);
    internal CameraController FurnitureInputCamera => _cameraController;
    internal Camera FurnitureWorldCamera => _worldCamera;

    public bool CanPresentFurnitureHints(ThirdFloorFairyPlayArea area)
        => ReferenceEquals(_playArea, area) && area != null && area.isActiveAndEnabled
            && IsVisible && isActiveAndEnabled && !IsItemCardOpen && CanPresentBirth()
            && (!_runtime || (_cameraController != null && !_cameraController.IsWorldInputBlocked
                && !UserInfo.IsTutorialStart));

#if UNITY_EDITOR
    [ContextMenu("Editor/요정 AI 상태 기록")]
    private void LogAiDiagnosticSnapshot() => Debug.Log(GetAiDiagnosticSnapshot(), this);

    // Explicit read-only diagnostic. Never initializes account services or logs each frame.
    public string GetAiDiagnosticSnapshot()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("clock=" + _clock + "; runtime=" + _runtime + "; visible=" + IsVisible
            + "; area=" + (_playArea != null) + "; presentationAllowed=" + CanPresentBirth()
            + "; returning=" + ReturningHouseCount + "; houseReturnDue=" + _houseReturnDueAt);
        text.AppendLine("houseCrowd=" + HouseCrowdCount + "; houseResidents=" + HouseResidentCount
            + "; residentLimit=" + (_houseSettings != null ? _houseSettings.SafeHouseResidentLimit : 0)
            + "; dispersing=" + DispersingCount + "; pendingExit=" + _houseRequests.Count
            + "; exitWait=" + HouseExitBlockedSeconds + "; exitDecision=" + LastHouseExitDecision);
        if (_runtime)
        {
            text.AppendLine("camera=" + (_cameraController != null) + "; cameraMoving="
                + (_cameraController != null && _cameraController.IsCameraMoving)
                + "; worldInputBlocked=" + (_cameraController != null && _cameraController.IsWorldInputBlocked)
                + "; tutorial=" + UserInfo.IsTutorialStart + "; approachingFloor=" + _approachingFloor3
                + "; waitingCameraFrame=" + _waitForCameraFrame + "; waitingUi=" + _waitingForUiHide
                + "; closingViews=" + HasClosingViews()
                + "; openViews=" + (_navigation != null ? _navigation.GetOpenViewCount() : 0));
            foreach (var uiView in _sceneViews)
                if (uiView != null && uiView.gameObject.activeInHierarchy && uiView.VisibleState != VisibleState.Disappeared)
                    text.AppendLine("blockingView=" + uiView.name + "; state=" + uiView.VisibleState);
        }
        foreach (var view in _views)
        {
            if (view.Brain == null) continue;
            text.AppendLine("item=" + view.Brain.ItemId + "; action=" + view.Brain.Action
                + "; ground=" + view.Brain.GroundPosition + "; house=" + view.HouseMotion
                + "; priorityUntil=" + view.PriorityUntil + "; arrivalRemaining=" + view.ArrivalRemaining
                + "; owned=" + _owned.Contains(view.Brain.ItemId) + "; onScreen=" + IsViewOnScreen(view)
                + "; birthPending=" + _arrivals.IsPending(view.Brain.ItemId) + "; replacing=" + view.Replacing
                + "; dispersing=" + view.Dispersing + "; dispersalTarget=" + view.DispersalTarget
                + "; movementTarget=" + view.Brain.MovementTarget + "; exitSide=" + view.HouseExitSide
                + "; halfWidth=" + view.SpacingHalfWidth + "; houseDwell=" + view.HouseResidentSeconds
                + "; bodyOverlapSeconds=" + view.HouseOverlapSeconds + "; yielding=" + view.SpacingYielding
                + "; plannedLanding=" + view.HouseEnd + "; plannedExitTarget=" + view.PlannedExitTarget);
        }
        return text.ToString();
    }
#endif

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
        PrepareBirthSounds();
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
        PrepareBirthSounds();
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

    private void PrepareBirthSounds()
    {
        // No live singleton creation in an isolated Editor/preview host.
        _soundManager = Application.isPlaying ? SoundManager.Instance : SoundManager.TryGetExistingInstance();
        if (_soundManager == null) return;
        _soundManager.PreloadAudioClip(_settings.BirthPopClip);
        foreach (var variant in _settings.BirthVoiceVariants ?? Array.Empty<EnhancementFairySettings.VoiceVariant>())
            if (variant != null) _soundManager.PreloadAudioClip(variant.Clip);
    }

    private void AdvanceBirthSounds(View view)
    {
        float revealAt = Mathf.Max(.1f, _settings.TrailSeconds) + Mathf.Max(.05f, _settings.GatherSeconds);
        if (view.BirthElapsed < revealAt) return;
        string id = view.Brain.ItemId;
        // Claim the presentation even while muted/voice-limited: returning to a
        // floor or unmuting must not replay an old birth sound.
        if (_arrivals.TryPresentPop(id) && _soundManager != null)
            _soundManager.PlayPresentationEffect(EffectType.None, _settings.BirthPopClip,
                _settings.BirthPopVolume, 1f, _popSoundOwner, Mathf.Clamp(_settings.MaxConcurrentPops, 1, 5));
        if (view.BirthElapsed < revealAt + Mathf.Clamp(_settings.VoiceDelay, .05f, .18f)
            || !_arrivals.TryPresentVoice(id)) return;
        var variants = _settings.BirthVoiceVariants;
        if (_soundManager == null || variants == null || variants.Length == 0) return;
        int index = _birthSoundRandom.Next(variants.Length > 1 && _lastVoiceVariant >= 0 ? variants.Length - 1 : variants.Length);
        if (variants.Length > 1 && _lastVoiceVariant >= 0 && index >= _lastVoiceVariant) index++;
        var voice = variants[index];
        if (voice == null || voice.Clip == null) return;
        if (_soundManager.PlayPresentationEffect(EffectType.None, voice.Clip,
            _settings.VoiceVolume * voice.Volume, voice.Pitch, _voiceSoundOwner,
            Mathf.Clamp(_settings.MaxConcurrentVoices, 1, 5), _settings.VoiceStartSeconds) != null) _lastVoiceVariant = index;
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
        StopBirthSounds();
        ClearHouseRequests();
        _houseSession = false;
        _houseReturnDueAt = float.PositiveInfinity;
        _nextHouseSpawnAt = _nextHouseReturnAt = 0f;
        _houseExitSequence = HouseEmergenceCount = 0;
        _displaySequence = 0;
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
        if (!next) { _playArea?.CancelAll(); PauseBirth(); CancelHouseSession(); }
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
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            if (_views[i].Root == null || _views[i].Body == null) { Release(i); continue; }
            _views[i].Root.gameObject.SetActive(next);
        }
        if (next) Reconcile();
    }

    private void RefreshRuntimeOwnership() => RestoreOwnership(UserInfo.GetGiveGachaItemCountDic().Keys);
    private void OnConfirmedAcquisition()
    {
        if (_arrivals.PendingCount == 0) StopBirthSounds();
        for (int i = _views.Count - 1; i >= 0; i--)
            if (_views[i].ArrivalRemaining > 0f && !_arrivals.IsPending(_views[i].Brain.ItemId)) CancelBirth(_views[i]);
        RefreshRuntimeOwnership(); Reconcile();
    }
    private void OnCameraMoving()
    {
        _waitForCameraFrame = true;
        ClearHouseRequests();
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
        _approachingFloor3 = floor == ERestaurantFloorType.Floor3
            && _cameraController != null && _cameraController.CurrentFloor != ERestaurantFloorType.Floor3;
        _waitForCameraFrame = true;
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
        int cap = _houseSession ? HouseHardCap : BaseActiveLimit;
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
        while (_views.Count < BaseActiveLimit)
        {
            string id = NextUnshown();
            if (id == null) break;
            Spawn(id);
        }
        // Resume existing presentations before assigning slots/positions to new ones.
        for (int i = 0; i < _views.Count; i++)
            if (_views[i].ArrivalRemaining > 0f && _arrivals.IsPending(_views[i].Brain.ItemId)) BeginArrival(_views[i]);
        for (int i = 0; i < _views.Count; i++)
            if (_views[i].ArrivalRemaining <= 0f && _arrivals.IsPending(_views[i].Brain.ItemId)) BeginArrival(_views[i]);
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
            if (!HasView(id) && !_houseQueued.Contains(id) && _catalog[id].Sprite != null) return id;
        }
        return null;
    }

    private int FindReplaceable(bool forArrival = false)
    {
        for (int n = 0; n < _views.Count; n++)
        {
            int i = (_rotationSlot + n) % _views.Count;
            if (_views[i].HouseMotion != HouseMotion.None
                || (!forArrival && IsDispersing(_views[i]))
                || (!forArrival && _views[i].HouseSpawned && _houseSettings != null
                    && (float.IsPositiveInfinity(_views[i].DisplayedAt)
                        || _clock - _views[i].DisplayedAt < _houseSettings.SafeMinimumIndividualStaySeconds))
                || (!forArrival && _views[i].PriorityUntil > _clock) || _views[i].ArrivalRemaining > 0f
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
        view.SpriteTriangles = sprite.triangles;
        view.Scale = _settings.SpriteHeight / Mathf.Max(0.01f, bounds.size.y);
        view.ArrivalRemaining = 0f;
        view.ArrivalStarted = false;
        view.BirthElapsed = 0f;
        view.PriorityUntil = 0f;
        view.Fade = 0f;
        view.Replacing = false;
        view.HouseMotion = HouseMotion.None;
        view.HouseSpawned = false;
        view.HouseElapsed = view.HouseLift = 0f;
        view.HouseReturnSpeed = view.HouseReturnTimeout = 0f;
        view.DisplayedAt = float.PositiveInfinity;
        view.HouseScale = 1f;
        view.DisplaySequence = _displaySequence++;
        view.Root.gameObject.SetActive(_visible);
        _views.Add(view);
        InitializeSpacing(view);
        Render(view);
    }

    private void BeginArrival(View view)
    {
        if (_birthViews.Contains(view) || _birthViews.Count >= Mathf.Clamp(_settings.MaxConcurrentBirths, 1, 5)
            || !CanPresentBirth()) return;
        if (view.ArrivalRemaining > 0f)
        {
            // A paused birth keeps its world position, path and sound claims. If that
            // position is outside this room, wait for it to become visible again.
            if (!TryGetVisibleBirthArea(out Rect visibleArea) || !visibleArea.Contains(view.BirthPosition)) return;
            _birthViews.Add(view);
            view.PriorityUntil = Mathf.Max(view.PriorityUntil,
                _clock + view.ArrivalRemaining + Mathf.Max(1f, _settings.NewArrivalPrioritySeconds));
            Render(view);
            return;
        }
        if (_clock < _nextBirthAt || !IsNextWaitingBirth(view)) return;
        if (!TryFindBirthPosition(view, out Vector2 position, out Rect area)) return;
        CancelSpacing(view);
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
            foreach (var view in _views)
                if (view.ArrivalRemaining > 0f && view.Brain.ItemId == id) { started = true; break; }
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
            foreach (var active in _views)
                if (active != candidate && active.ArrivalRemaining > 0f
                    && (Vector2.Distance(active.BirthPosition, position) < separation
                    || Mathf.Abs(active.Brain.GroundPosition.x - position.x) < LandingHalfWidth(active) + LandingHalfWidth(candidate)))
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
        _arrivals.PauseSoundPresentation(view.Brain.ItemId);
        // Temporary input, camera motion and occlusion suspend the same presentation.
        // Only cancellation/releasing a view discards its in-memory progress.
        if (view.Root != null) Render(view);
    }

    private void CancelBirth(View view)
    {
        PauseBirth(view);
        view.BirthElapsed = view.ArrivalRemaining = view.PriorityUntil = 0f;
        view.ArrivalStarted = false;
        if (view.Root != null) Render(view);
    }

    private void StopBirthSounds()
    {
        if (_soundManager != null)
        {
            _soundManager.StopPresentationEffects(_popSoundOwner);
            _soundManager.StopPresentationEffects(_voiceSoundOwner);
        }
    }

    private void PauseBirth()
    {
        StopBirthSounds();
        while (_birthViews.Count > 0) PauseBirth(_birthViews[_birthViews.Count - 1]);
    }

    private void AdvanceBirth(float deltaTime)
    {
        if (_birthViews.Count == 0) return;
        if (!CanPresentBirth()) { PauseBirth(); return; }
        TryGetVisibleBirthArea(out Rect area);
        for (int i = _birthViews.Count - 1; i >= 0; i--)
        {
            var view = _birthViews[i];
            if (!_arrivals.IsPending(view.Brain.ItemId)) { CancelBirth(view); continue; }
            if (!area.Contains(view.BirthPosition)) { PauseBirth(view); continue; }
            view.BirthElapsed += deltaTime;
            AdvanceBirthSounds(view);
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
        CancelSpacing(view);
        CancelBirth(view);
        if (view.Root == null || view.Body == null)
        {
            if (view.Root != null)
            {
                if (Application.isPlaying) Destroy(view.Root.gameObject);
                else DestroyImmediate(view.Root.gameObject);
            }
            view.Brain = null;
            _views.RemoveAt(index);
            return;
        }
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
        BeginHouseReturns();
        UpdateHouseSpacing(dt);
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            if (view.Root == null || view.Body == null) { Release(i); continue; }
            if (view.HouseMotion != HouseMotion.None)
            {
                if (AdvanceHouseMotion(view, dt)) { Release(i); continue; }
                Render(view);
                continue;
            }
            var neighbour = _views.Count > 1 ? _views[(i + 1) % _views.Count].Brain.GroundPosition : Vector2.zero;
            // Keep each birth at its own ground position; resume its ordinary brain on landing.
            if (!_arrivals.IsPending(view.Brain.ItemId) && !ShouldYieldSpacingWalk(view))
                view.Brain.Step(dt, neighbour, _views.Count > 1);
            if (view.Dispersing && !view.Brain.IsDirectedWalking) CancelSpacing(view);
            view.Fade = Mathf.MoveTowards(view.Fade, view.Replacing ? 0f : 1f, dt / Mathf.Max(0.1f, _settings.RotationFadeSeconds));
            if (view.Replacing && view.Fade <= 0f) { Release(i); continue; }
            Render(view);
        }
        AdvanceHouseQueue();
        if (!_houseSession && _rotationClock >= Mathf.Max(1f, _settings.RotationSeconds) && _ordered.Count > _views.Count)
        {
            _rotationClock = 0f;
            int replace = FindReplaceable();
            if (replace >= 0) _views[replace].Replacing = true;
        }
        // Paused births can resume immediately; BeginArrival still applies the
        // configured start cadence when assigning a genuinely new presentation.
        if (_views.Count < Mathf.Min(_ordered.Count, Mathf.Clamp(_settings.MaxActive, 1, 64))
            || (_arrivals.PendingCount > 0 && _birthViews.Count < Mathf.Clamp(_settings.MaxConcurrentBirths, 1, 5)))
            Reconcile();
    }

    private void Render(View view)
    {
        if (view.Root == null || view.Body == null || view.Brain == null) return;
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
        float scale = view.Scale * pop * view.HouseScale;
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
            ? Mathf.Abs(Mathf.Sin(jumpWave)) * Mathf.Max(0f, _settings.HopHeight) * Mathf.Lerp(1f, .7f, progress)
            : brain.VisualHeight + view.HouseLift;
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
        if (float.IsPositiveInfinity(view.DisplayedAt) && alpha > 0f && _visible && view.Root.gameObject.activeInHierarchy
            && IsViewOnScreen(view))
        {
            // Pending births and offscreen pooled instances acquire their age only
            // when the body first becomes visible in this presentation session.
            view.DisplayedAt = _clock;
        }
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
        bool furnitureTap = _playArea != null && _playArea.IsFurnitureTapTarget(screenPosition);
        // An active prop consumes repeated taps until its visual has returned to rest.
        // Idle direct fairy mesh hits still open the existing inspection card.
        if (furnitureTap && _playArea.IsAnimatingFurnitureTapTarget(screenPosition)) return false;
        foreach (var view in _views)
        {
            if (view.Root == null || view.Body == null || view.Brain == null || !view.Body.enabled || view.Body.sprite == null
                || view.Body.color.a < .5f || !view.Root.gameObject.activeInHierarchy) continue;
            var bounds = view.Body.bounds;
            Vector3 lower = _worldCamera.WorldToScreenPoint(bounds.min);
            Vector3 upper = _worldCamera.WorldToScreenPoint(bounds.max);
            if (lower.z <= 0f) continue;
            Vector2 center = (lower + upper) * .5f;
            // Preserve direct fairy taps, but let exposed furniture receive clicks through
            // transparent corners of a tight sprite mesh and enlarged touch padding.
            if (furnitureTap && (!Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y).Contains(screenPosition)
                || !IsDirectFairyMeshHit(view, screenPosition))) continue;
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
        _playArea?.CancelAll();
        _cameraController.SetWorldInputBlocked(this, true);
        return true;
    }

    private bool IsDirectFairyMeshHit(View view, Vector2 screenPosition)
    {
        if (view.SpriteVertices == null || view.SpriteTriangles == null || view.BodyTransform == null) return false;
        Ray ray = _worldCamera.ScreenPointToRay(screenPosition);
        var plane = new Plane(view.BodyTransform.forward, view.BodyTransform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector2 point = view.BodyTransform.InverseTransformPoint(ray.GetPoint(distance));
        if (view.Body.flipX) point.x = -point.x;
        if (view.Body.flipY) point.y = -point.y;
        var vertices = view.SpriteVertices;
        var triangles = view.SpriteTriangles;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            if (Mathf.Abs(TapEdge(a, b, c)) <= Mathf.Epsilon) continue;
            float ab = TapEdge(a, b, point), bc = TapEdge(b, c, point), ca = TapEdge(c, a, point);
            if (!((ab < 0f || bc < 0f || ca < 0f) && (ab > 0f || bc > 0f || ca > 0f))) return true;
        }
        return false;
    }

    private static float TapEdge(Vector2 a, Vector2 b, Vector2 point)
        => (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);

    public void CloseItemCard() => _itemPopup?.Hide();
    private void OnItemCardClosed() => _cameraController?.SetWorldInputBlocked(this, false);

    private void OnDisable()
    {
        _playArea?.CancelAll();
        CancelHouseSession();
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
        _playArea?.CancelAll();
        ClearHouseRequests();
        PauseBirth();
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
