using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class PopupStoreFinishedBox
{
    [SerializeField] private string _boxId;
    [SerializeField] private string _ribbonId;
    [SerializeField] private Sprite _sprite;

    public string BoxId => _boxId;
    public string RibbonId => _ribbonId;
    public Sprite Sprite => _sprite;
}

/// <summary>One local receipt consumer. Presentation cancellation never changes a committed order.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePackingPresentation : MonoBehaviour
{
    public enum Phase { Idle, Boom, Grow, Settle, Move, Arrive, Fade }

    [SerializeField] private PopupStorePacking _packing;
    [SerializeField] private PopupStoreOrderDisplay _orders;
    [SerializeField] private Animator _boomAnimator;
    [SerializeField] private Image _resultImage;
    [SerializeField] private PopupStoreFinishedBox[] _finishedBoxes = Array.Empty<PopupStoreFinishedBox>();
    [SerializeField, Min(0)] private float _boomDuration = .53333336f;
    [SerializeField, Min(0)] private float _growDuration = .12f;
    [SerializeField, Min(0)] private float _settleDuration = .12f;
    [SerializeField, Min(0)] private float _moveDuration = .75f;
    [SerializeField, Min(0)] private float _arrivalDuration = .12f;
    [SerializeField, Min(0)] private float _fadeDuration = .18f;
    [SerializeField, Range(.01f, 2)] private float _arrivalScale = .65f;
    [Tooltip("Offset from the customer's rectangle centre, in that rectangle's local UI units.")]
    [SerializeField] private Vector2 _targetOffset;
    [Tooltip("Draw order within each visual's own parent; clamped to its child count.")]
    [SerializeField, Min(0)] private int _boomSiblingIndex = 99;
    [SerializeField, Min(0)] private int _resultSiblingIndex = 100;
    [SerializeField, Range(.01f, 1)] private float _startScale = .35f;
    [SerializeField, Min(1)] private float _maxScale = 1.12f;
    [SerializeField] private AnimationCurve _growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _settleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private static readonly string[] BoxIds = { "BOX_PINK", "BOX_BLUE", "BOX_MINT" };
    private static readonly string[] RibbonIds = { "RIBBON_PINK", "RIBBON_YELLOW", "RIBBON_MINT" };
    private readonly List<PopupStorePackingReceipt> _receivedReceipts = new List<PopupStorePackingReceipt>();
    private readonly HashSet<string> _receivedIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _deliveredIds = new HashSet<string>(StringComparer.Ordinal);
    private ReadOnlyCollection<PopupStorePackingReceipt> _readOnlyReceipts;
    private PopupStorePacking _sessionPacking;
    private PopupStoreOrderDisplay _sessionOrders;
    private Animator _sessionBoom;
    private Image _sessionBoomImage;
    private Image _sessionResult;
    private Vector3 _baseScale;
    private Vector3 _basePosition;
    private Transform _resultParent;
    private Color _resultColor;
    private int _originalBoomSibling, _originalResultSibling;
    private Color _boomColor;
    private float _elapsed;
    private bool _sessionActive;
    private string _lastError;

    public bool IsReady { get; private set; }
    public Phase CurrentPhase { get; private set; }
    public PopupStorePackingReceipt CurrentReceipt { get; private set; }
    public int StartedPresentationCount { get; private set; }
    public int CompletedDeliveryCount => _deliveredIds.Count;
    public PopupStorePackingReceipt LastDeliveredReceipt { get; private set; }
    public RectTransform CurrentTarget { get; private set; }
    public string LastCancellationReason { get; private set; }
    public bool IsDeliveryCompleted(string receiptId) => receiptId != null && _deliveredIds.Contains(receiptId);
    // Kept after completion/cancellation for the future delivery stage; entries are immutable receipt objects.
    public IReadOnlyList<PopupStorePackingReceipt> ReceivedReceipts
        => _readOnlyReceipts ?? (_readOnlyReceipts = _receivedReceipts.AsReadOnly());

    public bool BeginVisibleSession()
    {
        if (_sessionActive) return IsReady;
        EndVisibleSession();
        if (!isActiveAndEnabled) return false;
        if (!Validate(out string error))
        {
            Report(error);
            return false;
        }

        _sessionPacking = _packing;
        _sessionOrders = _orders;
        _sessionBoom = _boomAnimator;
        _sessionBoomImage = _boomAnimator.GetComponent<Image>();
        _sessionResult = _resultImage;
        _baseScale = _resultImage.rectTransform.localScale;
        _basePosition = _resultImage.rectTransform.anchoredPosition3D;
        _resultParent = _resultImage.transform.parent;
        _resultColor = _resultImage.color;
        _originalBoomSibling = _boomAnimator.transform.GetSiblingIndex();
        _originalResultSibling = _resultImage.transform.GetSiblingIndex();
        _boomAnimator.transform.SetSiblingIndex(_boomSiblingIndex);
        _resultImage.transform.SetSiblingIndex(_resultSiblingIndex);
        _boomColor = _sessionBoomImage.color;
        ArchiveOutstanding(_sessionPacking);
        HideVisuals();
        _sessionActive = true;
        IsReady = true;
        _lastError = null;
        LastCancellationReason = null;
        return true;
    }

    public void EndVisibleSession()
    {
        // Only drain our own popup's queue, including receipts committed just before a close.
        var source = Owned(_sessionPacking) ? _sessionPacking : Owned(_packing) ? _packing : null;
        ArchiveOutstanding(source);
        if (CurrentReceipt != null) RecordCancellation("Popup presentation closed or disabled.");
        HideVisuals();
        if (Owned(_sessionBoom)) _sessionBoom.transform.SetSiblingIndex(_originalBoomSibling);
        if (Owned(_sessionResult) && _sessionResult.transform.parent == _resultParent)
            _sessionResult.transform.SetSiblingIndex(_originalResultSibling);
        CurrentReceipt = null;
        CurrentTarget = null;
        CurrentPhase = Phase.Idle;
        _elapsed = 0;
        IsReady = false;
        _sessionActive = false;
        _sessionPacking = null;
        _sessionOrders = null;
        _sessionBoom = null;
        _sessionBoomImage = null;
        _sessionResult = null;
        _resultParent = null;
    }

    public void TickPresentation(float unscaledDelta)
    {
        if (!_sessionActive) return;
        if (!isActiveAndEnabled || !Owned(_sessionPacking) || !_sessionPacking.isActiveAndEnabled
            || !Owned(_sessionBoom) || !Owned(_sessionResult) || !Owned(_sessionOrders))
        { EndVisibleSession(); return; }
        if (!NonNegative(unscaledDelta))
        { Fail("Presentation delta must be finite and non-negative."); return; }
        if (CurrentPhase == Phase.Idle && (!_sessionPacking.IsReady || !TryStartNext())) return;
        if (!ValidateCurrentTarget(out string targetError))
        { CancelCurrent(targetError); return; }

        float remaining = unscaledDelta;
        // At most this receipt's six phases; another receipt always starts on a later tick.
        for (int transitions = 0; transitions < 6 && CurrentPhase != Phase.Idle; transitions++)
        {
            float duration = PhaseDuration();
            if (!NonNegative(duration)) { Fail("Presentation duration must be finite and non-negative."); return; }
            float step = Mathf.Min(remaining, Mathf.Max(0, duration - _elapsed));
            _elapsed += step;
            remaining -= step;
            float progress = duration > 0 ? Mathf.Clamp01(_elapsed / duration) : 1;
            if (!ApplyScale(progress) || !ApplyDelivery(progress)) return;
            if (_elapsed < duration) return;
            AdvancePhase();
        }
    }

    public bool TryResolveSprite(string boxId, string ribbonId, out Sprite sprite)
    {
        sprite = null;
        if (!ValidateMappings(out _)) return false;
        foreach (var entry in _finishedBoxes)
            if (string.Equals(entry.BoxId, boxId, StringComparison.Ordinal)
                && string.Equals(entry.RibbonId, ribbonId, StringComparison.Ordinal))
            { sprite = entry.Sprite; return true; }
        return false;
    }

    private bool TryStartNext()
    {
        while (_sessionPacking.TryTakeReceipt(out var receipt))
        {
            if (!Archive(receipt)) continue;
            if (!receipt.Success || !TryResolveSprite(receipt.BoxId, receipt.RibbonId, out var sprite))
            { Fail("A successful receipt requires a registered finished-box combination."); return false; }
            CurrentReceipt = receipt;
            if (!_sessionOrders.TryGetPackedDeliveryTarget(receipt, out var target, out string reason))
            { CancelCurrent(reason); return false; }
            CurrentTarget = target;
            if (!ValidateCurrentTarget(out reason)) { CancelCurrent(reason); return false; }
            StartedPresentationCount++;
            _sessionResult.rectTransform.anchoredPosition3D = _basePosition;
            _sessionResult.color = _resultColor;
            _sessionResult.sprite = sprite;
            _sessionResult.preserveAspect = true;
            _sessionResult.raycastTarget = false;
            _sessionResult.rectTransform.localScale = _baseScale * _startScale;
            _sessionResult.gameObject.SetActive(false);
            _sessionBoomImage.raycastTarget = false;
            _sessionBoomImage.color = _boomColor;
            _sessionBoom.gameObject.SetActive(true);
            _sessionBoom.enabled = Application.isPlaying;
            if (Application.isPlaying)
            {
                _sessionBoom.updateMode = AnimatorUpdateMode.UnscaledTime;
                _sessionBoom.Rebind();
                _sessionBoom.Play("Boom_Play", 0, 0);
                _sessionBoom.Update(0);
            }
            CurrentPhase = Phase.Boom;
            _elapsed = 0;
            return true;
        }
        return false;
    }

    private float PhaseDuration()
    {
        switch (CurrentPhase)
        {
            case Phase.Boom: return _boomDuration;
            case Phase.Grow: return _growDuration;
            case Phase.Settle: return _settleDuration;
            case Phase.Move: return _moveDuration;
            case Phase.Arrive: return _arrivalDuration;
            case Phase.Fade: return _fadeDuration;
            default: return 0;
        }
    }

    private bool ApplyScale(float progress)
    {
        if (CurrentPhase != Phase.Grow && CurrentPhase != Phase.Settle) return true;
        var curve = CurrentPhase == Phase.Grow ? _growCurve : _settleCurve;
        if (curve == null || curve.length == 0) { Fail("Presentation curves are required."); return false; }
        float value = curve.Evaluate(progress);
        if (!Finite(value)) { Fail("Presentation curves must produce finite values."); return false; }
        float scale = CurrentPhase == Phase.Grow
            ? Mathf.Lerp(_startScale, _maxScale, Mathf.Clamp01(value))
            : Mathf.Lerp(_maxScale, 1, Mathf.Clamp01(value));
        _sessionResult.rectTransform.localScale = _baseScale * scale;
        return true;
    }

    private void AdvancePhase()
    {
        _elapsed = 0;
        switch (CurrentPhase)
        {
            case Phase.Boom:
                HideBoom();
                _sessionResult.gameObject.SetActive(true);
                CurrentPhase = Phase.Grow;
                break;
            case Phase.Grow:
                _sessionResult.rectTransform.localScale = _baseScale * _maxScale;
                CurrentPhase = Phase.Settle;
                break;
            case Phase.Settle:
                _sessionResult.rectTransform.localScale = _baseScale;
                CurrentPhase = Phase.Move;
                break;
            case Phase.Move:
                // Visual arrival only. The packing transaction has already committed.
                if (_deliveredIds.Add(CurrentReceipt.ReceiptId))
                {
                    LastDeliveredReceipt = CurrentReceipt;
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Debug.Log("POPUP_STORE_DELIVERED receipt=" + CurrentReceipt.ReceiptId
                            + "; slot=" + CurrentReceipt.SlotIndex + "; customer=" + CurrentReceipt.CustomerId, this);
#endif
                }
                CurrentPhase = Phase.Arrive;
                break;
            case Phase.Arrive:
                CurrentPhase = Phase.Fade;
                break;
            case Phase.Fade:
                HideVisuals();
                CurrentReceipt = null;
                CurrentTarget = null;
                CurrentPhase = Phase.Idle;
                break;
        }
    }

    private bool Validate(out string error)
    {
        error = null;
        if (!Owned(_packing) || !Owned(_orders) || !Owned(_boomAnimator) || !Owned(_resultImage)
            || !(_resultImage.transform.parent is RectTransform)
            || _boomAnimator.gameObject == gameObject || _resultImage.gameObject == gameObject
            || _boomAnimator.GetComponent<Image>() == null || _boomAnimator.runtimeAnimatorController == null
            || _boomAnimator.transform.IsChildOf(_resultImage.transform)
            || _resultImage.transform.IsChildOf(_boomAnimator.transform))
            error = "Packing, orders, an independent boom Image/Animator and result Image must belong to this popup.";
        else if (!NonNegative(_boomDuration) || !NonNegative(_growDuration)
            || !NonNegative(_settleDuration) || !NonNegative(_moveDuration)
            || !NonNegative(_arrivalDuration) || !NonNegative(_fadeDuration)
            || !Finite(_arrivalScale) || _arrivalScale <= 0 || !Finite(_targetOffset.x) || !Finite(_targetOffset.y)
            || !Finite(_startScale) || _startScale <= 0 || _startScale > 1
            || !Finite(_maxScale) || _maxScale < 1 || !ValidCurve(_growCurve) || !ValidCurve(_settleCurve)
            || !ValidCurve(_moveCurve)
            || !PositiveScale(_resultImage.rectTransform.localScale))
            error = "Presentation durations, scales and curves must be finite and valid.";
        else if (!ValidateMappings(out error)) return false;
        return error == null;
    }

    private bool ValidateMappings(out string error)
    {
        error = null;
        if (_finishedBoxes == null || _finishedBoxes.Length != 9)
        { error = "Exactly nine finished-box combinations are required."; return false; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var sprites = new HashSet<Sprite>();
        foreach (var entry in _finishedBoxes)
        {
            if (entry == null || Array.IndexOf(BoxIds, entry.BoxId) < 0 || Array.IndexOf(RibbonIds, entry.RibbonId) < 0
                || entry.Sprite == null || !keys.Add(entry.BoxId + "/" + entry.RibbonId) || !sprites.Add(entry.Sprite))
            { error = "Finished-box combinations require unique supported pairs and nine distinct sprites."; return false; }
        }
        return true;
    }

    private void ArchiveOutstanding(PopupStorePacking source)
    {
        if (source == null) return;
        while (source.TryTakeReceipt(out var receipt)) Archive(receipt);
    }

    private bool Archive(PopupStorePackingReceipt receipt)
    {
        if (receipt == null || string.IsNullOrEmpty(receipt.ReceiptId))
        { Report("A receipt identity is required for presentation."); return false; }
        if (!_receivedIds.Add(receipt.ReceiptId)) return false;
        _receivedReceipts.Add(receipt);
        return true;
    }

    private void HideBoom()
    {
        if (!Owned(_sessionBoom)) return;
        _sessionBoom.enabled = false;
        _sessionBoom.gameObject.SetActive(false);
        if (Owned(_sessionBoomImage)) _sessionBoomImage.color = _boomColor;
    }

    private void HideVisuals()
    {
        HideBoom();
        if (!Owned(_sessionResult)) return;
        _sessionResult.gameObject.SetActive(false);
        _sessionResult.rectTransform.localScale = _baseScale;
        if (_sessionResult.transform.parent == _resultParent)
            _sessionResult.rectTransform.anchoredPosition3D = _basePosition;
        _sessionResult.color = _resultColor;
        _sessionResult.sprite = null;
    }

    private bool ValidateCurrentTarget(out string reason)
    {
        reason = null;
        if (_sessionResult.transform.parent != _resultParent)
        { reason = "Result image parent changed during presentation."; return false; }
        if (!_sessionOrders.TryGetPackedDeliveryTarget(CurrentReceipt, out var current, out reason)) return false;
        if (current != CurrentTarget)
        { reason = "Customer image changed during presentation."; return false; }
        return TryGetTargetLocalPosition(_sessionResult.rectTransform, current, _targetOffset, out _, out reason);
    }

    private bool ApplyDelivery(float progress)
    {
        if (CurrentPhase != Phase.Move && CurrentPhase != Phase.Arrive && CurrentPhase != Phase.Fade) return true;
        var rect = _sessionResult.rectTransform;
        if (CurrentPhase == Phase.Arrive)
            rect.localScale = _baseScale * Mathf.Lerp(1, _arrivalScale, progress);
        else if (CurrentPhase == Phase.Fade)
        {
            rect.localScale = _baseScale * _arrivalScale;
            var color = _resultColor;
            color.a *= 1 - progress;
            _sessionResult.color = color;
        }
        if (!TryGetTargetLocalPosition(rect, CurrentTarget, _targetOffset, out var destination, out string reason))
        { CancelCurrent(reason); return false; }
        // Anchored home position is resolved again against the current layout/CanvasScaler each frame.
        rect.anchoredPosition3D = _basePosition;
        Vector3 origin = rect.localPosition;
        if (_moveCurve == null || _moveCurve.length == 0)
        { CancelCurrent("Movement curve is missing."); return false; }
        float travel = CurrentPhase == Phase.Move && progress < 1 ? _moveCurve.Evaluate(progress) : 1;
        if (!Finite(travel)) { CancelCurrent("Movement curve produced a non-finite value."); return false; }
        rect.localPosition = Vector3.Lerp(origin, destination, Mathf.Clamp01(travel));
        return true;
    }

    /// <summary>Position the result rectangle's centre at a customer-local centre/offset, including pivot differences.</summary>
    public static bool TryGetTargetLocalPosition(RectTransform result, RectTransform target, Vector2 offset,
        out Vector3 localPosition, out string reason)
    {
        localPosition = default;
        reason = null;
        if (result == null || target == null || !(result.parent is RectTransform parent)
            || !Finite(offset.x) || !Finite(offset.y))
        { reason = "Result, target, parent and a finite offset are required."; return false; }
        var sourceCanvas = target.GetComponentInParent<Canvas>();
        var resultCanvas = result.GetComponentInParent<Canvas>();
        if (sourceCanvas == null || resultCanvas == null)
        { reason = "Delivery images require Canvas coordinate spaces."; return false; }
        sourceCanvas = sourceCanvas.rootCanvas;
        resultCanvas = resultCanvas.rootCanvas;
        Vector3 targetWorld = target.TransformPoint(target.rect.center + offset);
        if (sourceCanvas == resultCanvas)
            localPosition = parent.InverseTransformPoint(targetWorld);
        else
        {
            if (sourceCanvas.targetDisplay != resultCanvas.targetDisplay)
            { reason = "Delivery cannot cross different target displays."; return false; }
            if (!TryCanvasCamera(sourceCanvas, out var sourceCamera)
                || !TryCanvasCamera(resultCanvas, out var resultCamera))
            { reason = "Camera/world-space Canvas requires an explicit world camera."; return false; }
            if (sourceCamera != null && sourceCamera.WorldToScreenPoint(targetWorld).z <= 0)
            { reason = "Delivery target is behind its Canvas camera."; return false; }
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, targetWorld);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(result, screen, resultCamera, out var point))
            { reason = "Cannot project the delivery target onto the result Canvas."; return false; }
            localPosition = parent.InverseTransformPoint(point);
        }
        localPosition -= parent.InverseTransformVector(result.TransformVector(result.rect.center));
        if (!Finite(localPosition.x) || !Finite(localPosition.y) || !Finite(localPosition.z))
        { reason = "Delivery coordinates must be finite."; return false; }
        return true;
    }

    private static bool TryCanvasCamera(Canvas canvas, out Camera camera)
    {
        camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay || camera != null;
    }

    private void CancelCurrent(string reason)
    {
        RecordCancellation(reason);
        HideVisuals();
        CurrentReceipt = null;
        CurrentTarget = null;
        CurrentPhase = Phase.Idle;
        _elapsed = 0;
    }

    private void RecordCancellation(string reason)
    {
        LastCancellationReason = reason;
        if (CurrentReceipt != null)
            Debug.LogWarning("POPUP_STORE_DELIVERY_CANCELLED receipt=" + CurrentReceipt.ReceiptId
                + "; slot=" + CurrentReceipt.SlotIndex + "; reason=" + reason, this);
    }

    private bool Owned(Component component)
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        return popup != null && component != null && component.GetComponentInParent<UIPopupStore>(true) == popup;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool NonNegative(float value) => Finite(value) && value >= 0;
    private static bool PositiveScale(Vector3 value)
        => Finite(value.x) && value.x > 0 && Finite(value.y) && value.y > 0 && Finite(value.z) && value.z > 0;
    private static bool ValidCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) return false;
        foreach (var key in curve.keys) if (!Finite(key.time) || !Finite(key.value)) return false;
        return true;
    }

    private void Fail(string error) { Report(error); EndVisibleSession(); }
    private void Report(string error)
    {
        if (_lastError == error) return;
        _lastError = error;
        Debug.LogError("PopupStorePackingPresentation: " + error, this);
    }

    private void OnEnable() { if (Application.isPlaying) BeginVisibleSession(); }
    private void Update() { if (Application.isPlaying) TickPresentation(Time.unscaledDeltaTime); }
    private void OnDisable() => EndVisibleSession();
    private void OnDestroy() => EndVisibleSession();
}
