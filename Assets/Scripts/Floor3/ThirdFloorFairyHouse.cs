using System;
using UnityEngine;

/// <summary>One stationary world-tap target and a visual-only shake, backed by the existing fairy habitat.</summary>
[DisallowMultipleComponent]
public sealed class ThirdFloorFairyHouse : MonoBehaviour
{
    [SerializeField, Tooltip("Only this child is animated. Keep the touch collider and entrance outside this child.")]
    private Transform _visual;
    [SerializeField, Tooltip("Stationary child collider on the camera's ignored Furniture layer.")]
    private Collider2D _touchCollider;
    [SerializeField] private Transform _entrance;
    [SerializeField, Tooltip("Optional scene reference. Otherwise resolved only inside this object's scene.")]
    private CameraController _cameraController;
    [SerializeField] private EnhancementFairyHouseSettings _settings = new EnhancementFairyHouseSettings();

    private EnhancementFairyHabitat _habitat;
    private Camera _worldCamera;
    private bool _offline;
    private bool _registered;
    private bool _subscribed;
    private bool _runtimeBound;
    private bool _hasRestPose;
    private Vector3 _restPosition, _restScale;
    private Quaternion _restRotation;
    private float _clock;
    private float _lastTapAt = float.NegativeInfinity;
    private float _shakeElapsed;
    private float _shakeAngle;
    private bool _shaking;
    private int _tapStage;
    private SpriteRenderer _visualSprite;
    private ThirdFloorApprovedArt _approvedArt;
    private readonly System.Random _hintRandom = new System.Random();
    private bool _hintEligible, _hintPlaying, _hintShown;
    private float _hintRemaining, _hintElapsed;

    public EnhancementFairyHouseSettings Settings => _settings;
    public Transform Entrance => _entrance;
    public Transform Visual => _visual;
    public Collider2D TouchCollider => _touchCollider;
    public EnhancementFairySettings BaseSettings => _habitat != null && _habitat.Settings != null
        ? _habitat.Settings : Resources.Load<EnhancementFairySettings>(EnhancementFairySettings.ResourcePath);
    public int BaseActiveLimit => _habitat != null ? _habitat.BaseActiveLimit
        : BaseSettings != null ? Mathf.Clamp(BaseSettings.MaxActive, 1, 64) : 12;
    public int CurrentShakeStage => _tapStage;
    public bool IsShaking => _shaking;
    public bool IsIdleHintPlaying => _hintPlaying;
    public int LastRequestedSpawnCount { get; private set; }
    public int LastAcceptedSpawnCount { get; private set; }

    private void OnEnable()
    {
        CaptureRestPose();
        _approvedArt = GetComponentInParent<ThirdFloorApprovedArt>();
        ResetSequence();
        if (_offline) RegisterAndSubscribe();
        else if (Application.isPlaying) TryBindRuntime();
    }

    private void Start()
    {
        if (!_offline && Application.isPlaying) TryBindRuntime();
    }

    private void Update()
    {
        if (!_offline && !_registered) TryBindRuntime();
        Advance(Time.unscaledDeltaTime);
    }

    private void TryBindRuntime()
    {
        if (_registered || !Application.isPlaying || !isActiveAndEnabled) return;
        var floor = GetComponentInParent<Floor3Controller>();
        if (floor == null) return;
        _habitat = floor.GetComponent<EnhancementFairyHabitat>();
        // Floor3Controller.Start owns runtime initialization. Waiting for it avoids
        // accessing account/singleton state here and keeps item-card taps first.
        if (_habitat == null || _habitat.Settings == null) return;
        if (_cameraController == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<CameraController>(true))
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    _cameraController = candidate;
                    break;
                }
                if (_cameraController != null) break;
            }
        }
        if (_cameraController == null || _cameraController.gameObject.scene != gameObject.scene) return;
        _worldCamera = _cameraController.GetComponent<Camera>();
        if (_worldCamera == null) return;
        _runtimeBound = true;
        RegisterAndSubscribe();
    }

    /// <summary>Detached test/preview injection. Does not initialize runtime ownership, accounts or scenes.</summary>
    public void ConfigureOffline(EnhancementFairyHabitat habitat, Transform visual, Collider2D touchCollider,
        Transform entrance, Camera worldCamera = null, CameraController inputCamera = null)
    {
        if (_runtimeBound) throw new InvalidOperationException("Use a detached house for offline verification.");
        UnregisterAndUnsubscribe();
        RestoreRestPose();
        _offline = true;
        _habitat = habitat;
        _visual = visual;
        _touchCollider = touchCollider;
        _entrance = entrance;
        _worldCamera = worldCamera != null ? worldCamera : inputCamera != null ? inputCamera.GetComponent<Camera>() : null;
        _cameraController = inputCamera;
        CaptureRestPose();
        ResetSequence();
        if (isActiveAndEnabled) RegisterAndSubscribe();
    }

    private void RegisterAndSubscribe()
    {
        if (_registered || _habitat == null || !HasIndependentVisual()) return;
        if (_settings == null) _settings = new EnhancementFairyHouseSettings();
        _settings.Validate();
        _habitat.RegisterHouse(this, _entrance, _settings);
        _registered = true;
        if (_cameraController != null)
        {
            // CameraController stops at the first consuming handler. The habitat's
            // existing item-card handler was registered during Floor3.Start first.
            _cameraController.OnWorldTapHandler += HandleCompletedWorldTap;
            _subscribed = true;
        }
    }

    private bool HasIndependentVisual() => _visual != null && _visual != transform
        && _touchCollider != null && _entrance != null
        && !_touchCollider.transform.IsChildOf(_visual) && !_entrance.IsChildOf(_visual);

    /// <summary>Receives only the camera's completed, drag-filtered, UI-free world taps.</summary>
    public bool HandleCompletedWorldTap(Vector2 screenPosition)
    {
        if (_worldCamera == null || _touchCollider == null) return false;
        var ray = _worldCamera.ScreenPointToRay(screenPosition);
        var plane = new Plane(Vector3.forward, _touchCollider.transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        return TryTapAtWorldPoint(ray.GetPoint(distance));
    }

    /// <summary>The shared hit-test and presentation path, also used by detached deterministic tests.</summary>
    public bool TryTapAtWorldPoint(Vector2 worldPosition)
    {
        if (!isActiveAndEnabled || !_registered || _habitat == null || !_habitat.CanUseHouse || _touchCollider == null
            || !_touchCollider.enabled || !_touchCollider.gameObject.activeInHierarchy
            || (_cameraController != null && (_cameraController.IsCameraMoving || _cameraController.IsWorldInputBlocked))
            || !_touchCollider.OverlapPoint(worldPosition)) return false;

        int maximumStage = _settings.SafeMaximumShakeStage;
        StopIdleHint();
        bool expired = _clock - _lastTapAt > _settings.SafeTapSequenceResetSeconds;
        _tapStage = expired ? 1 : Mathf.Min(maximumStage, _tapStage + 1);
        _lastTapAt = _clock;
        LastRequestedSpawnCount = _settings.SpawnCountForStage(_tapStage);
        LastAcceptedSpawnCount = _habitat.RequestHouseEmergence(this, LastRequestedSpawnCount);
        _shakeAngle = _settings.ShakeAngleForStage(_tapStage);
        _shakeElapsed = 0f;
        _shaking = true;
        RestoreRestPose();
        return true;
    }

    /// <summary>Advances the real shake without Play Mode, input simulation, accounts or timers.</summary>
    public void AdvancePreview(float deltaTime)
    {
        if (!_offline) throw new InvalidOperationException("Configure a detached house before advancing its preview.");
        Advance(deltaTime);
    }

    private void Advance(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
        _clock += deltaTime;
        if (_visual == null || !_hasRestPose) return;
        if (_habitat == null || !_habitat.CanUseHouse
            || (_cameraController != null && (_cameraController.IsCameraMoving || _cameraController.IsWorldInputBlocked)))
        {
            _shaking = false;
            StopIdleHint();
            RestoreRestPose();
            return;
        }
        if (!_shaking)
        {
            AdvanceIdleHint(deltaTime);
            return;
        }
        _shakeElapsed += deltaTime;
        float duration = _settings.SafeShakeDuration;
        if (_shakeElapsed >= duration)
        {
            _shaking = false;
            RestoreRestPose();
            return;
        }
        float progress = _shakeElapsed / duration;
        float angle = Mathf.Sin(progress * Mathf.PI * 2f * Mathf.Clamp(_settings.ShakeCycles, 1, 5))
            * _shakeAngle * (1f - progress);
        _visual.localRotation = _restRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void AdvanceIdleHint(float deltaTime)
    {
        if (!_settings.EnableIdleHint || !_registered || _habitat.HasHouseDoorwayActivity || !IsVisualOnScreen())
        {
            StopIdleHint();
            return;
        }
        if (!_hintEligible)
        {
            _hintEligible = true;
            var delay = _hintShown ? _settings.HintInterval : _settings.FirstHintDelay;
            _hintRemaining = Mathf.Lerp(delay.x, delay.y, (float)_hintRandom.NextDouble());
        }
        if (!_hintPlaying)
        {
            _hintRemaining -= deltaTime;
            if (_hintRemaining > 0f) return;
            if (_approvedArt != null && !_approvedArt.TryBeginIdleHint(Time.unscaledTime, _settings.HintDuration))
            { _hintRemaining = 0f; return; }
            _hintElapsed = Mathf.Max(0f, -_hintRemaining);
            _hintPlaying = _hintShown = true;
        }
        else _hintElapsed += deltaTime;
        if (_hintElapsed >= _settings.HintDuration)
        {
            StopIdleHint();
            return;
        }
        float t = _hintElapsed / _settings.HintDuration;
        float envelope = Mathf.Sin(t * Mathf.PI);
        float angle = Mathf.Sin(t * Mathf.PI * 4f) * envelope * envelope * _settings.HintRotationDegrees;
        _visual.localRotation = _restRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private bool IsVisualOnScreen()
    {
        if (_worldCamera == null || _visualSprite == null || !_visualSprite.enabled
            || !_visualSprite.gameObject.activeInHierarchy || _visualSprite.sprite == null) return false;
        var point = _worldCamera.WorldToViewportPoint(_visualSprite.bounds.center);
        return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
    }

    private void StopIdleHint()
    {
        if (_hintPlaying) RestoreRestPose();
        _hintEligible = _hintPlaying = false;
        _hintRemaining = _hintElapsed = 0f;
    }

    private void CaptureRestPose()
    {
        _hasRestPose = _visual != null;
        _visualSprite = _visual != null ? _visual.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (!_hasRestPose) return;
        _restPosition = _visual.localPosition;
        _restScale = _visual.localScale;
        _restRotation = _visual.localRotation;
    }

    private void RestoreRestPose()
    {
        if (!_hasRestPose || _visual == null) return;
        _visual.localPosition = _restPosition;
        _visual.localScale = _restScale;
        _visual.localRotation = _restRotation;
    }

    private void ResetSequence()
    {
        _clock = 0f;
        _lastTapAt = float.NegativeInfinity;
        _tapStage = 0;
        _shaking = false;
        StopIdleHint();
        _hintShown = false;
        LastRequestedSpawnCount = LastAcceptedSpawnCount = 0;
    }

    private void UnregisterAndUnsubscribe()
    {
        if (_subscribed && _cameraController != null) _cameraController.OnWorldTapHandler -= HandleCompletedWorldTap;
        _subscribed = false;
        if (_registered && _habitat != null) _habitat.UnregisterHouse(this);
        _registered = false;
    }

    private void OnDisable()
    {
        RestoreRestPose();
        ResetSequence();
        UnregisterAndUnsubscribe();
    }

    private void OnDestroy() => UnregisterAndUnsubscribe();

    private void OnValidate()
    {
        if (_settings == null) _settings = new EnhancementFairyHouseSettings();
        _settings.Validate();
    }
}
