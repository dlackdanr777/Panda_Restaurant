using Muks.MobileUI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Animates only the approved map frame and forwards one completed tap to the existing map event.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ThirdFloorApprovedMapButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler, IPointerExitHandler, ICancelHandler, IBeginDragHandler, IDragHandler
{
    [SerializeField, Tooltip("Existing map SpriteTouchEvent wired to Floor3Controller. Its old collider is disabled by ApprovedArt.")]
    private SpriteTouchEvent _legacyMapTouch;
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private MobileUINavigation _navigation;
    [SerializeField, Min(1f), Tooltip("Maximum screen-space travel for a tap; native EventSystem drag cancellation also applies.")]
    private float _dragThresholdPixels = 12f;

    [Header("Frame visual (keep the collider outside these children)")]
    [SerializeField] private Transform _idlePivot;
    [SerializeField] private Transform _pressVisual;
    [SerializeField] private SpriteRenderer _frameSprite;

    [Header("Idle hint")]
    [SerializeField] private bool _enableIdleHint = true;
    [SerializeField, Min(0f)] private float _firstVisibleHintDelay = .5f;
    [SerializeField, Min(.1f)] private float _hintIntervalMin = 9f;
    [SerializeField, Min(.1f)] private float _hintIntervalMax = 15f;
    [SerializeField, Range(0f, 5f)] private float _hintRotationDegrees = 2f;
    [SerializeField, Min(.1f)] private float _hintDuration = .8f;

    [Header("Completed tap feedback")]
    [SerializeField] private bool _enablePressAnimation = true;
    [SerializeField, Range(.8f, 1f)] private float _pressScale = .95f;
    [SerializeField, Min(.01f)] private float _pressDuration = .08f;
    [SerializeField, Min(.01f)] private float _releaseDuration = .12f;
    [SerializeField, Min(0f), Tooltip("Minimum delay after a completed tap. The frame always finishes restoring before opening the map.")]
    private float _mapOpenDelay = .2f;

    [Header("Accepted map click sound")]
    [SerializeField] private bool _enableClickSound = true;
    [SerializeField, Range(0f, 1f)] private float _clickSoundVolume = 1f;
    [SerializeField, Range(.5f, 2f)] private float _clickSoundPitch = 1f;

    private const int NoPointer = int.MinValue;
    private enum PressPhase { None, Pressing, Held, Releasing }
    private int _pointerId = NoPointer;
    private Vector2 _pressPosition;
    private bool _pressed, _released;
    private bool _openingMap, _dispatchingMap;
    private float _openElapsed, _pressElapsed;
    private PointerEventData _pendingClick;
    private PressPhase _pressPhase;
    private bool _hintEligible, _hintWaiting, _hintSwinging;
    private float _hintRemaining, _hintElapsed;
    private Transform _capturedIdlePivot, _capturedPressVisual;
    private Quaternion _restIdleRotation;
    private Vector3 _restPressScale;
    private Camera _worldCamera;
    private CameraController _observedCamera;
    private ThirdFloorApprovedArt _approvedArt;
    private bool _firstHintConsumed;
    private readonly System.Random _hintRandom = new System.Random();

    private void OnEnable()
    {
        ValidateSettings();
        _approvedArt = GetComponentInParent<ThirdFloorApprovedArt>();
        ObserveCamera();
        CaptureVisualPose();
        CancelInteraction();
    }

    private void OnValidate() => ValidateSettings();
    private void OnDisable() => CancelInteraction();
    private void Update() => Tick(Time.unscaledDeltaTime);

    private void ObserveCamera()
    {
        if (_observedCamera != _cameraController)
        {
            if (_observedCamera != null) _observedCamera.OnEndMoveCameraHandler -= OnFloorChanged;
            _observedCamera = _cameraController;
            if (_observedCamera != null) _observedCamera.OnEndMoveCameraHandler += OnFloorChanged;
        }
        if (_cameraController != null && _cameraController.CurrentFloor != ERestaurantFloorType.Floor3)
            _firstHintConsumed = false;
    }

    // Keep this small floor observer while art is disabled, so a real floor exit resets the visit.
    private void OnFloorChanged(ERestaurantFloorType floor, RestaurantType restaurant)
    {
        if (floor != ERestaurantFloorType.Floor3) _firstHintConsumed = false;
    }

    private void OnDestroy()
    {
        if (_observedCamera != null) _observedCamera.OnEndMoveCameraHandler -= OnFloorChanged;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left || _openingMap || _dispatchingMap) return;
        if (_pressed)
        {
            // A second pointer invalidates the original gesture; neither may open the map.
            CancelInteraction();
            return;
        }

        CancelInteraction();
        if (!CanOpenMap() || Input.touchCount > 1) return;
        CaptureVisualPose();
        _pointerId = eventData.pointerId;
        _pressPosition = eventData.position;
        _pressed = true;
        if (_enablePressAnimation && HasIndependentVisuals())
        {
            _pressPhase = PressPhase.Pressing;
            _pressElapsed = 0f;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Matches(eventData) || !_pressed) return;
        _pressed = false;
        _released = IsTap(eventData) && CanOpenMap();
        if (!_released) CancelInteraction();
        else if (_pressPhase == PressPhase.Held) BeginRelease();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Matches(eventData) || !_released || _openingMap || _dispatchingMap) return;
        bool open = IsTap(eventData) && CanOpenMap();
        // Consume the gesture before any delayed or reentrant callback.
        ResetGesture();
        if (!open) { CancelInteraction(); return; }

        _openingMap = true;
        _openElapsed = 0f;
        // EventSystem reuses its event objects. Retain the completed tap, not that mutable instance.
        _pendingClick = new PointerEventData(EventSystem.current)
        {
            pointerId = eventData.pointerId,
            button = eventData.button,
            position = eventData.position,
            pressPosition = eventData.pressPosition,
            pointerPressRaycast = eventData.pointerPressRaycast,
            pointerCurrentRaycast = eventData.pointerCurrentRaycast,
            clickCount = eventData.clickCount,
            clickTime = eventData.clickTime,
            eligibleForClick = true
        };
        if (_pressPhase == PressPhase.Held) BeginRelease();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Matches(eventData)) CancelInteraction();
    }

    public void OnBeginDrag(PointerEventData eventData) => CancelDrag(eventData);
    public void OnDrag(PointerEventData eventData) => CancelDrag(eventData);
    public void OnCancel(BaseEventData eventData) => CancelInteraction();

    // The actual runtime step is also used by detached Editor verification, without accounts or Play Mode.
    private void Tick(float deltaSeconds)
    {
        if (!isActiveAndEnabled || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f) return;
        ObserveCamera();
        CaptureVisualPose();
        if (!CanOpenMap() || ((_pressed || _openingMap) && Input.touchCount > 1))
        {
            CancelInteraction();
            return;
        }

        AdvancePress(deltaSeconds);
        if (_openingMap)
        {
            _openElapsed += deltaSeconds;
            if (_openElapsed >= _mapOpenDelay && _pressPhase == PressPhase.None) OpenExistingMap();
            return;
        }
        if (_released && _pressPhase == PressPhase.None) ResetGesture();

        bool eligible = _enableIdleHint && HasIndependentVisuals() && IsFrameInView()
            && !_pressed && !_released && _pressPhase == PressPhase.None;
        if (!eligible)
        {
            StopHint();
            return;
        }
        if (!_hintEligible)
        {
            _hintEligible = true;
            ScheduleHint(!_firstHintConsumed);
        }
        float hintStep = deltaSeconds;
        if (_hintWaiting)
        {
            _hintRemaining -= deltaSeconds;
            if (_hintRemaining > 0f) return;
            // The first visible .5-second cue takes precedence over the optional quiet gap
            // left by a cancelled hint during the preceding camera transition.
            if (_approvedArt != null && !_approvedArt.TryBeginIdleHint(Time.unscaledTime, _hintDuration, !_firstHintConsumed))
            {
                _hintRemaining = 0f;
                return;
            }
            hintStep = Mathf.Max(0f, -_hintRemaining);
            _hintWaiting = false;
            _hintSwinging = true;
            _hintElapsed = 0f;
            _firstHintConsumed = true;
        }
        if (!_hintSwinging) return;
        _hintElapsed += hintStep;
        if (_hintElapsed >= _hintDuration)
        {
            RestoreIdleRotation();
            _hintSwinging = false;
            ScheduleHint(false);
            return;
        }
        float t = _hintElapsed / _hintDuration;
        // Two small oscillations, with zero velocity at either endpoint.
        float envelope = Mathf.Sin(t * Mathf.PI);
        float angle = Mathf.Sin(t * Mathf.PI * 4f) * envelope * envelope * _hintRotationDegrees;
        _idlePivot.localRotation = _restIdleRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void AdvancePress(float deltaSeconds)
    {
        if (!_enablePressAnimation || !HasIndependentVisuals())
        {
            RestorePressScale();
            _pressPhase = PressPhase.None;
            return;
        }
        // Carry the remaining frame time through the press/release boundary even for very short taps.
        float remaining = deltaSeconds;
        for (int phase = 0; phase < 2; phase++)
        {
            if (_pressPhase == PressPhase.None || _pressPhase == PressPhase.Held) return;
            bool pressing = _pressPhase == PressPhase.Pressing;
            float duration = pressing ? _pressDuration : _releaseDuration;
            float step = Mathf.Min(remaining, Mathf.Max(0f, duration - _pressElapsed));
            _pressElapsed += step;
            remaining -= step;
            float progress = Mathf.Clamp01(_pressElapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            _pressVisual.localScale = _restPressScale * (pressing
                ? Mathf.Lerp(1f, _pressScale, eased) : Mathf.Lerp(_pressScale, 1f, eased));
            if (progress < 1f) return;
            if (!pressing)
            {
                RestorePressScale();
                _pressPhase = PressPhase.None;
                return;
            }
            _pressPhase = PressPhase.Held;
            if (_pressed) return;
            BeginRelease();
        }
    }

    private void BeginRelease()
    {
        _pressPhase = PressPhase.Releasing;
        _pressElapsed = 0f;
    }

    private void OpenExistingMap()
    {
        if (!CanOpenMap()) { CancelInteraction(); return; }
        var click = _pendingClick;
        CancelInteraction();
        _dispatchingMap = true;
        try
        {
            // The legacy SpriteTouchEvent / Floor3Controller / UIFloor3 path has no click audio.
            // Dispatch only after the delayed input recheck; hints and cancelled taps stay silent.
            var sound = _enableClickSound ? SoundManager.TryGetExistingInstance() : null;
            if (sound != null)
                sound.PlayPresentationEffect(EffectType.None, sound.GetEffectClip(SoundEffectType.ButtonClickSound),
                    _clickSoundVolume, _clickSoundPitch, this, 1);
            // Retain Floor3Controller's existing subscriptions and ShowFloor3UI DataBind route.
            // Its original press tween affects only the hidden legacy frame.
            _legacyMapTouch.OnPointerDown(click);
            _legacyMapTouch.OnPointerUp(click);
        }
        finally { _dispatchingMap = false; }
    }

    private void ScheduleHint(bool first)
    {
        _hintWaiting = true;
        _hintRemaining = first ? _firstVisibleHintDelay
            : Mathf.Lerp(_hintIntervalMin, _hintIntervalMax, (float)_hintRandom.NextDouble());
    }

    private bool HasIndependentVisuals() => _idlePivot != null && _pressVisual != null && _frameSprite != null
        && _idlePivot != transform && _pressVisual != transform && _pressVisual != _idlePivot
        && _idlePivot.IsChildOf(transform) && _pressVisual.IsChildOf(_idlePivot)
        && _frameSprite.transform.IsChildOf(_pressVisual);

    private bool IsFrameInView()
    {
        if (_frameSprite == null || !_frameSprite.enabled || !_frameSprite.gameObject.activeInHierarchy || _frameSprite.sprite == null) return false;
        if (_worldCamera == null && _cameraController != null) _worldCamera = _cameraController.GetComponent<Camera>();
        if (_worldCamera == null) return false;
        Vector3 point = _worldCamera.WorldToViewportPoint(_frameSprite.bounds.center);
        return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
    }

    private bool CanOpenMap() => isActiveAndEnabled && _legacyMapTouch != null
        && _legacyMapTouch.isActiveAndEnabled && _cameraController != null && _navigation != null
        && !_cameraController.IsCameraMoving && !_cameraController.IsWorldInputBlocked
        && _cameraController.CurrentFloor == ERestaurantFloorType.Floor3
        && !UserInfo.IsTutorialStart && !PopupManager.IsCustomerEncounterPresentationBlocked
        && !_navigation.IsViewsInactive && _navigation.Count == 0 && _navigation.ViewsVisibleStateCheck()
        && (!HasIndependentVisuals() || IsFrameInView());

    private void CaptureVisualPose()
    {
        if (_capturedIdlePivot == _idlePivot && _capturedPressVisual == _pressVisual) return;
        RestoreIdleRotation();
        RestorePressScale();
        _capturedIdlePivot = _idlePivot;
        _capturedPressVisual = _pressVisual;
        if (_idlePivot != null) _restIdleRotation = _idlePivot.localRotation;
        if (_pressVisual != null) _restPressScale = _pressVisual.localScale;
    }

    private void RestoreIdleRotation()
    {
        if (_capturedIdlePivot != null) _capturedIdlePivot.localRotation = _restIdleRotation;
    }

    private void RestorePressScale()
    {
        if (_capturedPressVisual != null) _capturedPressVisual.localScale = _restPressScale;
    }

    private void StopHint()
    {
        _hintEligible = _hintWaiting = _hintSwinging = false;
        _hintRemaining = _hintElapsed = 0f;
        RestoreIdleRotation();
    }

    private void CancelInteraction()
    {
        ResetGesture();
        _openingMap = false;
        _pendingClick = null;
        _openElapsed = _pressElapsed = 0f;
        _pressPhase = PressPhase.None;
        StopHint();
        RestorePressScale();
    }

    private void CancelDrag(PointerEventData eventData)
    {
        if (Matches(eventData)) CancelInteraction();
    }

    private bool Matches(PointerEventData eventData) => eventData != null
        && eventData.button == PointerEventData.InputButton.Left && eventData.pointerId == _pointerId;

    private bool IsTap(PointerEventData eventData) => !eventData.dragging && Input.touchCount <= 1
        && (eventData.position - _pressPosition).sqrMagnitude <= _dragThresholdPixels * _dragThresholdPixels;

    private void ResetGesture()
    {
        _pointerId = NoPointer;
        _pressed = _released = false;
    }

    private void ValidateSettings()
    {
        _dragThresholdPixels = Safe(_dragThresholdPixels, 12f, 1f, 100f);
        _firstVisibleHintDelay = Safe(_firstVisibleHintDelay, .5f, 0f, 300f);
        _hintIntervalMin = Safe(_hintIntervalMin, 9f, .1f, 300f);
        _hintIntervalMax = Safe(_hintIntervalMax, 15f, _hintIntervalMin, 300f);
        _hintRotationDegrees = Safe(_hintRotationDegrees, 2f, 0f, 5f);
        _hintDuration = Safe(_hintDuration, .8f, .1f, 5f);
        _pressScale = Safe(_pressScale, .95f, .8f, 1f);
        _pressDuration = Safe(_pressDuration, .08f, .01f, 1f);
        _releaseDuration = Safe(_releaseDuration, .12f, .01f, 1f);
        _mapOpenDelay = Safe(_mapOpenDelay, .2f, 0f, 2f);
        _clickSoundVolume = Safe(_clickSoundVolume, 1f, 0f, 1f);
        _clickSoundPitch = Safe(_clickSoundPitch, 1f, .5f, 2f);
    }

    private static float Safe(float value, float fallback, float minimum, float maximum)
        => Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);
}
