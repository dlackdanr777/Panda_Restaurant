using System;
using UnityEngine;

public enum FairyFurnitureKind { Cushion, RockingHorse, Box }

internal static class FairyFurnitureVisualSafety
{
    internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    internal static float Value(float value, float fallback, float minimum, float maximum)
        => Mathf.Clamp(IsFinite(value) ? value : fallback, minimum, maximum);
    internal static Vector2 Range(Vector2 value, Vector2 fallback, float minimum, float maximum)
    {
        float first = Value(value.x, fallback.x, minimum, maximum);
        return new Vector2(first, Value(value.y, fallback.y, first, maximum));
    }
}

[Serializable]
public sealed class FairyFurniturePoint
{
    public string Name;
    public bool Enabled = true;
    public FairyFurnitureKind Kind;

    [Header("Existing spacing anchors (no furniture activity)")]
    [Tooltip("Retained only by the existing house dispersal obstacle checks. No fairy is sent to this anchor.")]
    public Transform Approach;
    public Transform Activity;
    public Transform Exit;

    [Header("Shared visual and fixed input")]
    [Tooltip("Shared visual-only pivot for touch and idle hints. The collider must remain outside this transform.")]
    public Transform HintVisual;
    public SpriteRenderer HintSprite;
    public Collider2D TouchCollider;

    [Header("Touch feedback")]
    public bool TouchEnabled = true;
    [Tooltip("Cushion width/height multiplier, measured from the authored bottom pivot.")]
    public Vector2 TouchScale = new Vector2(1.04f, .92f);
    [Min(.01f)] public float PressDuration = .12f;
    [Min(.01f)] public float ReleaseDuration = .20f;
    [Range(0f, 15f)] public float TouchRotationDegrees = 7f;
    [Range(0f, 12f), Tooltip("Horizontal screen pixels at the current camera zoom. Use zero for the horse.")]
    public float TouchTranslationPixels;
    [Range(1f, 4f)] public float TouchCycles = 2.5f;
    [Range(.1f, 5f), Tooltip("Horse/box duration. Cushion duration is Press Duration plus Release Duration.")]
    public float TouchDuration = 1f;

    [Header("Idle hint (visual only)")]
    public bool EnableIdleHint;
    public Vector2 FirstHintDelay = new Vector2(6f, 10f);
    public Vector2 HintInterval = new Vector2(15f, 25f);
    [Min(.1f)] public float HintDuration = .65f;
    [Range(0f, 3f)] public float HintRotationDegrees = 2f;
    [Range(0f, .05f)] public float HintVerticalCompression = .03f;

    [NonSerialized] internal Transform RestVisual;
    [NonSerialized] internal Vector3 RestPosition, RestScale, TouchTranslation;
    [NonSerialized] internal Quaternion RestRotation;
    [NonSerialized] internal bool HintEligible, HintActive, TouchActive;
    [NonSerialized] internal float HintRemaining, HintElapsed, TouchElapsed;

    // These passive anchors remain available to the existing house spacing policy.
    // They do not enable reservations, fairy commands, sleeping, riding or jumping.
    public bool HasValidReferences => Enabled && Active(Approach) && Active(Activity) && Active(Exit)
        && (Kind != FairyFurnitureKind.RockingHorse || HasVisibleSprite);

    internal bool HasVisibleSprite => Active(HintVisual) && HintSprite != null && HintSprite.enabled
        && HintSprite.gameObject.activeInHierarchy && HintSprite.sprite != null;

    private static bool Active(Transform value)
    {
        if (value == null || !value.gameObject.activeInHierarchy
            || !FairyFurnitureVisualSafety.IsFinite(value.position)
            || !FairyFurnitureVisualSafety.IsFinite(value.lossyScale)) return false;
        var rotation = value.rotation;
        return FairyFurnitureVisualSafety.IsFinite(rotation.x) && FairyFurnitureVisualSafety.IsFinite(rotation.y)
            && FairyFurnitureVisualSafety.IsFinite(rotation.z) && FairyFurnitureVisualSafety.IsFinite(rotation.w);
    }

    public void Validate()
    {
        TouchScale = new Vector2(
            FairyFurnitureVisualSafety.Value(TouchScale.x, 1.04f, .5f, 1.5f),
            FairyFurnitureVisualSafety.Value(TouchScale.y, .92f, .5f, 1.5f));
        PressDuration = FairyFurnitureVisualSafety.Value(PressDuration, .12f, .01f, 2f);
        ReleaseDuration = FairyFurnitureVisualSafety.Value(ReleaseDuration, .20f, .01f, 2f);
        TouchRotationDegrees = FairyFurnitureVisualSafety.Value(TouchRotationDegrees, 7f, 0f, 15f);
        TouchTranslationPixels = FairyFurnitureVisualSafety.Value(TouchTranslationPixels, 0f, 0f, 12f);
        TouchCycles = FairyFurnitureVisualSafety.Value(TouchCycles, 2.5f, 1f, 4f);
        TouchDuration = FairyFurnitureVisualSafety.Value(TouchDuration, 1f, .1f, 5f);
        FirstHintDelay = FairyFurnitureVisualSafety.Range(FirstHintDelay, new Vector2(6f, 10f), 0f, 300f);
        HintInterval = FairyFurnitureVisualSafety.Range(HintInterval, new Vector2(15f, 25f), .1f, 300f);
        HintDuration = FairyFurnitureVisualSafety.Value(HintDuration, .65f, .1f, 3f);
        HintRotationDegrees = FairyFurnitureVisualSafety.Value(HintRotationDegrees, 2f, 0f, 3f);
        HintVerticalCompression = FairyFurnitureVisualSafety.Value(HintVerticalCompression, .03f, 0f, .05f);
    }
}

/// <summary>Three independent visual props on the existing camera's completed-world-tap route.</summary>
[DisallowMultipleComponent]
public sealed class ThirdFloorFairyPlayArea : MonoBehaviour
{
    [SerializeField] private FairyFurniturePoint[] _points = Array.Empty<FairyFurniturePoint>();
    [SerializeField, Tooltip("Optional input camera. Runtime resolves the existing habitat camera when unassigned.")]
    private CameraController _cameraController;

    private EnhancementFairyHabitat _habitat;
    private bool _offline, _registered, _runtimeBound;
    private Camera _worldCamera;
    private CameraController _subscribedCamera;
    private ThirdFloorApprovedArt _approvedArt;
    private readonly System.Random _hintRandom = new System.Random();
    private float _hintClock;

    public FairyFurniturePoint[] Points => _points;
    public bool IsRegistered => _registered && _habitat != null;
    public bool IsRuntimeBound => _runtimeBound;

#if UNITY_EDITOR
    public string EditorLastFurnitureTapDiagnostic { get; private set; } = "No world tap delivered";
#endif

    private void OnEnable()
    {
        ValidateSettings();
        CaptureRestPoses();
        _approvedArt = GetComponentInParent<ThirdFloorApprovedArt>();
        if (_offline) Register();
        else if (Application.isPlaying) TryBindRuntime();
    }

    private void Start() { if (!_offline && Application.isPlaying) TryBindRuntime(); }

    private void Update()
    {
        if (!_offline && !_registered && Application.isPlaying) TryBindRuntime();
        if (!_offline) AdvanceHints(Time.unscaledDeltaTime, Time.unscaledTime);
    }

    private void TryBindRuntime()
    {
        if (_registered || !isActiveAndEnabled || !Application.isPlaying) return;
        BindToInitializedHabitat();
    }

    // The regular Floor3.Start initializes the habitat. A late camera must not
    // permanently register a prop without its normal world-tap listener.
    private bool BindToInitializedHabitat()
    {
        if (_offline || !isActiveAndEnabled) return false;
        if (_registered) return _habitat != null;
        var floor = GetComponentInParent<Floor3Controller>();
        if (floor == null) return false;
        var habitat = floor.GetComponent<EnhancementFairyHabitat>();
        if (habitat == null || habitat.Settings == null || habitat.FurnitureWorldCamera == null
            || (_cameraController == null && habitat.FurnitureInputCamera == null)) return false;
        _habitat = habitat;
        if (_cameraController == null) _cameraController = habitat.FurnitureInputCamera;
        _worldCamera = habitat.FurnitureWorldCamera;
        _runtimeBound = true;
        Register();
        return _registered;
    }

    public void ConfigureOffline(EnhancementFairyHabitat habitat, FairyFurniturePoint[] points,
        Camera worldCamera = null, CameraController inputCamera = null)
    {
        if (_runtimeBound) throw new InvalidOperationException("Use a detached play area for offline verification.");
        Unregister();
        CancelAll();
        _offline = true;
        _habitat = habitat;
        _points = points ?? Array.Empty<FairyFurniturePoint>();
        _cameraController = inputCamera;
        _worldCamera = worldCamera != null ? worldCamera : inputCamera != null ? inputCamera.GetComponent<Camera>() : null;
        _hintClock = 0f;
        ValidateSettings();
        CaptureRestPoses();
        _approvedArt = GetComponentInParent<ThirdFloorApprovedArt>();
        if (isActiveAndEnabled) Register();
    }

    private void Register()
    {
        if (_registered || _habitat == null) return;
        ValidateSettings();
        CaptureRestPoses();
        _habitat.RegisterPlayArea(this);
        _registered = true;
        if (_cameraController == null) return;
        _cameraController.OnWorldTapHandler += HandleCompletedWorldTap;
        _subscribedCamera = _cameraController;
    }

    private void Unregister()
    {
        if (_subscribedCamera != null) _subscribedCamera.OnWorldTapHandler -= HandleCompletedWorldTap;
        _subscribedCamera = null;
        if (_registered && _habitat != null) _habitat.UnregisterPlayArea(this);
        _registered = false;
    }

    private bool InputAvailable => isActiveAndEnabled && IsRegistered
        && _habitat.CanPresentFurnitureHints(this)
        && (_cameraController == null ? _offline : !_cameraController.IsCameraMoving
            && !_cameraController.IsWorldInputBlocked && _cameraController.CurrentFloor == ERestaurantFloorType.Floor3);

    private bool IsPointInView(Vector3 worldPosition)
    {
        if (_worldCamera == null) return _offline;
        Vector3 viewport = _worldCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>The existing camera rejects drags, cancellation, multitouch and UI before dispatch.</summary>
    public bool HandleCompletedWorldTap(Vector2 screenPosition)
    {
#if UNITY_EDITOR
        EditorLastFurnitureTapDiagnostic = "world tap delivered: allowed=" + InputAvailable
            + "; camera=" + (_worldCamera != null);
#endif
        if (!TryScreenPoint(screenPosition, out Vector3 world)) return false;
        return TryTapAtWorldPoint(world);
    }

    internal bool IsFurnitureTapTarget(Vector2 screenPosition)
        => TryScreenPoint(screenPosition, out Vector3 world) && FindTappedFurniture(world) != null;

    internal bool IsAnimatingFurnitureTapTarget(Vector2 screenPosition)
    {
        if (!TryScreenPoint(screenPosition, out Vector3 world)) return false;
        var point = FindTappedFurniture(world);
        return point != null && point.TouchActive;
    }

    private bool TryScreenPoint(Vector2 screenPosition, out Vector3 world)
    {
        world = default;
        if (!InputAvailable || _worldCamera == null || !FairyFurnitureVisualSafety.IsFinite(screenPosition)) return false;
        Ray ray = _worldCamera.ScreenPointToRay(screenPosition);
        var plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        world = ray.GetPoint(distance);
        return true;
    }

    public bool TryTapAtWorldPoint(Vector2 worldPosition)
    {
        if (!InputAvailable || !FairyFurnitureVisualSafety.IsFinite(worldPosition)) return false;
        var point = FindTappedFurniture(worldPosition);
        if (point == null) return false;
        if (point.TouchActive)
        {
#if UNITY_EDITOR
            EditorLastFurnitureTapDiagnostic = point.Kind + ": touch animation already running; repeat ignored";
#endif
            return true;
        }
        point.Validate();
        StopHint(point);
        CaptureRestPose(point);
        point.TouchTranslation = ScreenTranslation(point);
        point.TouchElapsed = 0f;
        point.TouchActive = true;
#if UNITY_EDITOR
        EditorLastFurnitureTapDiagnostic = point.Kind + ": visual touch started";
#endif
        return true;
    }

    public bool IsTouchAnimating(FairyFurniturePoint point) => Contains(point) && point.TouchActive;

    private FairyFurniturePoint FindTappedFurniture(Vector2 worldPosition)
    {
        foreach (var point in _points)
        {
            if (point == null || !point.TouchEnabled || !HasSafeVisual(point) || !HasFixedCollider(point)
                || !point.TouchCollider.OverlapPoint(worldPosition)
                || !IsPointInView(point.HintSprite.bounds.center)) continue;
            return point;
        }
        return null;
    }

    private bool HasSafeVisual(FairyFurniturePoint point) => point != null && point.Enabled && point.HasVisibleSprite
        && point.HintVisual != transform && point.HintVisual.IsChildOf(transform)
        && point.HintSprite.transform.IsChildOf(point.HintVisual)
        && (point.TouchCollider == null || !point.TouchCollider.transform.IsChildOf(point.HintVisual))
        && (point.Approach == null || !point.Approach.IsChildOf(point.HintVisual))
        && (point.Exit == null || !point.Exit.IsChildOf(point.HintVisual))
        && (point.Kind == FairyFurnitureKind.RockingHorse || point.Activity == null
            || !point.Activity.IsChildOf(point.HintVisual));

    private static bool HasFixedCollider(FairyFurniturePoint point) => point.TouchCollider != null
        && point.TouchCollider.enabled && point.TouchCollider.gameObject.activeInHierarchy
        && !point.TouchCollider.transform.IsChildOf(point.HintVisual);

    private Vector3 ScreenTranslation(FairyFurniturePoint point)
    {
        if (_worldCamera == null || point.TouchTranslationPixels <= 0f) return Vector3.zero;
        Vector3 screen = _worldCamera.WorldToScreenPoint(point.HintVisual.position);
        if (screen.z <= 0f) return Vector3.zero;
        Vector3 shifted = screen + Vector3.right * point.TouchTranslationPixels;
        Vector3 worldDelta = _worldCamera.ScreenToWorldPoint(shifted) - _worldCamera.ScreenToWorldPoint(screen);
        return point.HintVisual.parent != null ? point.HintVisual.parent.InverseTransformVector(worldDelta) : worldDelta;
    }

    /// <summary>Runs the actual visual-only touch/hint logic in a detached account-free fixture.</summary>
    public void AdvanceHintPreview(float deltaTime)
    {
        if (!_offline) throw new InvalidOperationException("Configure a detached play area before advancing its hints.");
        if (!FairyFurnitureVisualSafety.IsFinite(deltaTime) || deltaTime < 0f) return;
        _hintClock += deltaTime;
        AdvanceHints(deltaTime, _hintClock);
    }

    private void AdvanceHints(float deltaTime, float now)
    {
        if (!FairyFurnitureVisualSafety.IsFinite(deltaTime) || deltaTime < 0f) return;
        bool inputAvailable = InputAvailable;
        foreach (var point in _points)
        {
            if (point == null) continue;
            bool visible = inputAvailable && HasSafeVisual(point) && IsPointInView(point.HintSprite.bounds.center);
            if (!visible || point.RestVisual != point.HintVisual)
            {
                StopTouch(point);
                StopHint(point);
                if (visible) CaptureRestPose(point);
                continue;
            }
            if (point.TouchActive)
            {
                if (!point.TouchEnabled || !HasFixedCollider(point)) StopTouch(point);
                else AdvanceTouch(point, deltaTime);
                if (point.TouchActive) continue;
            }
            if (!point.EnableIdleHint || point.Kind == FairyFurnitureKind.Box)
            { StopHint(point); continue; }
            if (!point.HintEligible)
            {
                CaptureRestPose(point);
                point.HintEligible = true;
                point.HintRemaining = HintRange(point.FirstHintDelay);
            }
            float animationStep = deltaTime;
            if (!point.HintActive)
            {
                point.HintRemaining -= deltaTime;
                if (point.HintRemaining > 0f) continue;
                if (_approvedArt != null && !_approvedArt.TryBeginIdleHint(now, point.HintDuration))
                { point.HintRemaining = 0f; continue; }
                animationStep = Mathf.Max(0f, -point.HintRemaining);
                point.HintActive = true;
                point.HintElapsed = 0f;
            }
            point.HintElapsed += animationStep;
            if (point.HintElapsed >= point.HintDuration)
            {
                RestorePose(point);
                point.HintActive = false;
                point.HintRemaining = HintRange(point.HintInterval);
                continue;
            }
            float t = Mathf.Clamp01(point.HintElapsed / point.HintDuration);
            float envelope = Mathf.Sin(t * Mathf.PI);
            if (point.Kind == FairyFurnitureKind.Cushion)
            {
                Vector3 scale = point.RestScale;
                scale.y *= 1f - envelope * envelope * point.HintVerticalCompression;
                point.HintVisual.localScale = scale;
            }
            else
            {
                float angle = Mathf.Sin(t * Mathf.PI * 4f) * envelope * envelope * point.HintRotationDegrees;
                point.HintVisual.localRotation = point.RestRotation * Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    private static void AdvanceTouch(FairyFurniturePoint point, float deltaTime)
    {
        point.TouchElapsed += deltaTime;
        float duration = point.Kind == FairyFurnitureKind.Cushion
            ? point.PressDuration + point.ReleaseDuration : point.TouchDuration;
        if (point.TouchElapsed >= duration) { StopTouch(point); return; }
        if (point.Kind == FairyFurnitureKind.Cushion)
        {
            float press = point.TouchElapsed < point.PressDuration
                ? Mathf.SmoothStep(0f, 1f, point.TouchElapsed / point.PressDuration)
                : 1f - Mathf.SmoothStep(0f, 1f, (point.TouchElapsed - point.PressDuration) / point.ReleaseDuration);
            Vector3 scale = point.RestScale;
            scale.x *= Mathf.Lerp(1f, point.TouchScale.x, press);
            scale.y *= Mathf.Lerp(1f, point.TouchScale.y, press);
            point.HintVisual.localScale = scale;
            return;
        }
        float t = Mathf.Clamp01(point.TouchElapsed / duration);
        float envelope = Mathf.Sin(t * Mathf.PI);
        float wave = Mathf.Sin(t * Mathf.PI * 2f * point.TouchCycles) * envelope * envelope;
        point.HintVisual.localRotation = point.RestRotation * Quaternion.Euler(0f, 0f, wave * point.TouchRotationDegrees);
        point.HintVisual.localPosition = point.RestPosition + point.TouchTranslation * wave;
    }

    private float HintRange(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)_hintRandom.NextDouble());

    private void CaptureRestPoses()
    {
        foreach (var point in _points) CaptureRestPose(point);
    }

    private static void CaptureRestPose(FairyFurniturePoint point)
    {
        if (point == null || point.HintVisual == null || point.TouchActive || point.HintActive) return;
        point.RestVisual = point.HintVisual;
        point.RestPosition = point.HintVisual.localPosition;
        point.RestRotation = point.HintVisual.localRotation;
        point.RestScale = point.HintVisual.localScale;
    }

    private static void RestorePose(FairyFurniturePoint point)
    {
        if (point == null || point.RestVisual == null) return;
        point.RestVisual.localPosition = point.RestPosition;
        point.RestVisual.localRotation = point.RestRotation;
        point.RestVisual.localScale = point.RestScale;
    }

    private static void StopHint(FairyFurniturePoint point)
    {
        if (point == null) return;
        if (point.HintActive) RestorePose(point);
        point.HintEligible = point.HintActive = false;
        point.HintElapsed = point.HintRemaining = 0f;
    }

    private static void StopTouch(FairyFurniturePoint point)
    {
        if (point == null) return;
        if (point.TouchActive) RestorePose(point);
        point.TouchActive = false;
        point.TouchElapsed = 0f;
        point.TouchTranslation = Vector3.zero;
    }

    public void CancelAll()
    {
        foreach (var point in _points) { StopTouch(point); StopHint(point); }
    }

    private bool Contains(FairyFurniturePoint point)
    {
        if (point == null) return false;
        foreach (var value in _points) if (value == point) return true;
        return false;
    }

    private void OnDisable() { CancelAll(); Unregister(); }
    private void OnDestroy() { CancelAll(); Unregister(); }

    private void ValidateSettings()
    {
        if (_points == null) _points = Array.Empty<FairyFurniturePoint>();
        foreach (var point in _points) point?.Validate();
    }

    private void OnValidate() => ValidateSettings();
}
