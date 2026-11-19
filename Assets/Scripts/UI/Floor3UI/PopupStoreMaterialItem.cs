using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>A catalog-bound click target. Scrolling never adds a material.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreMaterialItem : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
    IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    public PopupStoreMaterialList Owner { get; private set; }
    public PopupStoreMaterialEntry Material { get; private set; }

    private bool _pressed, _pointerHeld;
    private int _pointerId;
    private Vector2 _pressPosition;
    private uint _inputVersion;
    private PopupStoreMaterialVisual _visual;
    private float _feedbackScale = 1f, _feedbackFrom = 1f, _feedbackTarget = 1f;
    private float _feedbackElapsed, _feedbackDuration;
    private bool _animatingFeedback;

    public void Bind(PopupStoreMaterialList owner, PopupStoreMaterialEntry material)
    {
        ResetPressFeedback();
        Owner = owner;
        Material = material;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ResetPressFeedback();
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left
            || !isActiveAndEnabled || Owner == null || !Owner.IsReady || !Owner.isActiveAndEnabled
            || Material == null || eventData.scrollDelta.sqrMagnitude != 0
            || !Owner.CanShowPressFeedback(this)) return;
        _pressed = true;
        _pointerHeld = true;
        _pointerId = eventData.pointerId;
        _pressPosition = eventData.position;
        _inputVersion = Owner.InputVersion;
        AnimateFeedback(Owner.PressedScale, Owner.PressDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool pressed = _pressed;
        _pressed = false;
        _pointerHeld = false;
        ReleaseFeedback();
        if (!pressed || eventData == null || !eventData.eligibleForClick || eventData.dragging
            || eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != _pointerId
            || eventData.scrollDelta.sqrMagnitude != 0 || !isActiveAndEnabled || Owner == null) return;

        // The native input module handles drags. This also rejects a release-position
        // jump in the same frame, before the input module gets to ProcessDrag.
        float threshold = EventSystem.current != null ? Mathf.Max(1, EventSystem.current.pixelDragThreshold) : 10;
        if ((eventData.position - _pressPosition).sqrMagnitude >= threshold * threshold) return;
        Owner.TrySelect(this, _inputVersion);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!MatchesPress(eventData)) return;
        _pointerHeld = false;
        ReleaseFeedback();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Leaving restores only the visual. The existing click/drag rules still decide selection.
        if (MatchesPress(eventData)) ReleaseFeedback();
    }

    public void OnCancel(BaseEventData eventData) => CancelPressFeedback();

    private bool MatchesPress(PointerEventData eventData)
        => _pressed && eventData != null && eventData.pointerId == _pointerId
            && eventData.button == PointerEventData.InputButton.Left;

    internal void CancelPressFeedback()
    {
        _pressed = false;
        _pointerHeld = false;
        ReleaseFeedback();
    }

    private void ReleaseFeedback() => AnimateFeedback(1f, Owner != null ? Owner.ReleaseDuration : .12f);

    private void AnimateFeedback(float target, float duration)
    {
        if (_visual == null) _visual = GetComponent<PopupStoreMaterialVisual>();
        if (_visual == null || (_animatingFeedback && _feedbackTarget == target)) return;
        _feedbackFrom = _feedbackScale;
        _feedbackTarget = target;
        _feedbackElapsed = 0f;
        _feedbackDuration = duration;
        _animatingFeedback = _feedbackFrom != target;
        if (duration <= 0f) AdvancePressFeedback(0f);
    }

    private void Update()
    {
        if (_pointerHeld && (Owner == null || _inputVersion != Owner.InputVersion || !Owner.CanShowPressFeedback(this)))
            CancelPressFeedback();
        AdvancePressFeedback(Time.unscaledDeltaTime);
    }

    private void AdvancePressFeedback(float deltaTime)
    {
        if (!_animatingFeedback || _visual == null) return;
        _feedbackElapsed += Mathf.Max(0f, deltaTime);
        float progress = _feedbackDuration <= 0f ? 1f : Mathf.Clamp01(_feedbackElapsed / _feedbackDuration);
        _feedbackScale = Mathf.Lerp(_feedbackFrom, _feedbackTarget, Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f) { _feedbackScale = _feedbackTarget; _animatingFeedback = false; }
        _visual.SetPressScale(_feedbackScale);
    }

    private void ResetPressFeedback()
    {
        _pressed = false;
        _pointerHeld = false;
        _animatingFeedback = false;
        _feedbackScale = _feedbackFrom = _feedbackTarget = 1f;
        if (_visual == null) _visual = GetComponent<PopupStoreMaterialVisual>();
        if (_visual != null) _visual.SetPressScale(1f);
    }

    private void OnDisable() => ResetPressFeedback();
}
