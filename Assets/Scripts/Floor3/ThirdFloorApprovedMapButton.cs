using Muks.MobileUI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Forwards one completed tap to the existing Floor3Controller map event.</summary>
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

    private const int NoPointer = int.MinValue;
    private int _pointerId = NoPointer;
    private Vector2 _pressPosition;
    private bool _pressed;
    private bool _released;
    private bool _cancelled;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (_pressed)
        {
            // A second pointer invalidates the original gesture; neither may open the map.
            _cancelled = true;
            return;
        }

        ResetGesture();
        if (!CanOpenMap() || Input.touchCount > 1) return;
        _pointerId = eventData.pointerId;
        _pressPosition = eventData.position;
        _pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Matches(eventData) || !_pressed) return;
        _pressed = false;
        _released = !_cancelled && IsTap(eventData) && CanOpenMap();
        if (!_released) ResetGesture();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Matches(eventData) || !_released) return;
        bool open = !_cancelled && IsTap(eventData) && CanOpenMap();
        // Consume before invoking external callbacks, including a possible reentrant click.
        ResetGesture();
        if (!open) return;

        // Keep the original Start subscriptions and ShowFloor3UI DataBind route intact.
        // Any existing press tween affects only the hidden legacy frame, never the room art.
        _legacyMapTouch.OnPointerDown(eventData);
        _legacyMapTouch.OnPointerUp(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Matches(eventData)) _cancelled = true;
    }

    public void OnBeginDrag(PointerEventData eventData) => CancelDrag(eventData);
    public void OnDrag(PointerEventData eventData) => CancelDrag(eventData);
    public void OnCancel(BaseEventData eventData) => ResetGesture();
    private void OnDisable() => ResetGesture();

    private void Update()
    {
        if (_pressed && (Input.touchCount > 1 || !CanOpenMap())) _cancelled = true;
    }

    private void CancelDrag(PointerEventData eventData)
    {
        if (Matches(eventData)) _cancelled = true;
    }

    private bool Matches(PointerEventData eventData) => eventData != null
        && eventData.button == PointerEventData.InputButton.Left && eventData.pointerId == _pointerId;

    private bool IsTap(PointerEventData eventData) => !eventData.dragging && Input.touchCount <= 1
        && (eventData.position - _pressPosition).sqrMagnitude <= _dragThresholdPixels * _dragThresholdPixels;

    private bool CanOpenMap() => isActiveAndEnabled && _legacyMapTouch != null
        && _legacyMapTouch.isActiveAndEnabled && _cameraController != null && _navigation != null
        && !_cameraController.IsCameraMoving && !_cameraController.IsWorldInputBlocked
        && !UserInfo.IsTutorialStart && !PopupManager.IsCustomerEncounterPresentationBlocked
        && !_navigation.IsViewsInactive && _navigation.Count == 0 && _navigation.ViewsVisibleStateCheck();

    private void ResetGesture()
    {
        _pointerId = NoPointer;
        _pressed = false;
        _released = false;
        _cancelled = false;
    }
}
