using System;
using System.Globalization;
using Muks.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Reads one immutable local result after existing visuals settle. Never owns transactions or time.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreResultPanel : MonoBehaviour
{
    [SerializeField] private PopupStoreGradeSession _grade;
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private PopupStorePacking _packing;
    [SerializeField] private PopupStorePackingPresentation _presentation;
    [SerializeField] private PopupStoreCustomerEntrance _entrance;
    [SerializeField] private CanvasGroup _inputGroup;
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Image _frame;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _gradeText;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _confirmText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private PopupStoreRewardBox _rewardBox;
    [SerializeField, Min(0)] private float _showDuration = .2f;
    [SerializeField, Range(.01f, 1f)] private float _startScale = .92f;
    [SerializeField] private AnimationCurve _showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private string _title = "영업 종료";
    [SerializeField] private string _scoreFormat = "최종 점수 {0}";
    [SerializeField] private string _gradeFormat = "최종 등급 {0}";
    [SerializeField] private string _noneGradeText = "최종 등급 없음";
    [SerializeField] private string _countFormat = "완료 주문 {0}건";
    [SerializeField] private string _confirmLabel = "확인";

    private UIPopupStore _view;
    private PopupStoreGradeSession _ownerGrade;
    private PopupStoreBusinessClock _ownerClock;
    private PopupStorePacking _ownerPacking;
    private PopupStorePackingPresentation _ownerPresentation;
    private PopupStoreCustomerEntrance _ownerEntrance;
    private Button _boundButton;
    private CanvasGroup _shownOverlay, _lockedGroup;
    private RectTransform _sizedPanel;
    private Vector3 _baseScale;
    private GameObject _previousSelection;
    private bool _oldInteractable, _oldRaycasts, _ready, _shown, _appearing, _hasAppeared, _confirming;
    private float _elapsed;
    private string _lastError;

    public PopupStoreGradeResult ShownResult { get; private set; }
    public bool IsVisible => _shown && _shownOverlay != null && _shownOverlay.gameObject.activeInHierarchy;
    public bool IsAppearing => IsVisible && _appearing;
    public int PresentationCount { get; private set; }
    public int AppearanceCount { get; private set; }
    public int ConfirmationCount { get; private set; }

    /// <summary>Binds only local UI references. Does not refresh grade/score, consume receipts or open a panel.</summary>
    public bool Refresh()
    {
        if (!Validate(out string error)) { Fail(error); return false; }
        _view = GetComponent<UIPopupStore>();
        if (_ownerGrade == null)
        {
            _ownerGrade = _grade; _ownerClock = _clock; _ownerPacking = _packing;
            _ownerPresentation = _presentation; _ownerEntrance = _entrance;
        }
        if (_boundButton != _confirmButton)
        {
            Unbind();
            _boundButton = _confirmButton;
            _boundButton.onClick.AddListener(OnConfirmClicked);
        }
        if (_sizedPanel != _panel)
        {
            _sizedPanel = _panel;
            _baseScale = _panel.localScale;
        }
        _ready = true;
        _lastError = null;
        return true;
    }

    /// <summary>Called after the existing Update-driven delivery and customer motions, never from their events.</summary>
    public void TickResult(float unscaledDelta)
    {
        if (!isActiveAndEnabled) return;
        if (!Finite(unscaledDelta) || unscaledDelta < 0) { Fail("Result delta must be finite and non-negative."); return; }
        if (!_ready && !Refresh()) return;
        if (!Validate(out string error)) { Fail(error); return; }
        if (_view.VisibleState != VisibleState.Appeared) return;
        if (_clock.State != PopupStoreBusinessState.Ended || !_grade.IsFinalized) return;
        var result = _grade.FinalResult;
        if (result == null || !result.IsFinalized || string.IsNullOrEmpty(result.SessionId)
            || !string.Equals(result.SessionId, _grade.SessionId, StringComparison.Ordinal)
            || result.Score < 0 || result.SuccessfulOrders < 0 || !Enum.IsDefined(typeof(PopupStoreGradeTier), result.Grade)
            || (ShownResult != null && !ReferenceEquals(ShownResult, result)))
        { Fail("Final result must be the original finalized result of this local session."); return; }
        if (!_shown)
        {
            // Includes queued successful receipts. Waiting/Entering/Packed alone are not outstanding exits.
            if (_presentation.HasPendingVisualWork || HasActiveDeparture()) return;
            if (!Open(result)) return;
        }
        if (!_appearing)
        {
            if (_rewardBox != null && _rewardBox.gameObject == gameObject) _rewardBox.TickPresentation(unscaledDelta);
            return;
        }
        _elapsed = Mathf.Min(_showDuration, _elapsed + unscaledDelta);
        float t = _showDuration > 0 ? Mathf.Clamp01(_elapsed / _showDuration) : 1f;
        float eased = _showCurve.Evaluate(t);
        if (!Finite(eased)) { Fail("Result animation curve returned an invalid value."); return; }
        // Same local unscaled tick/curve pattern as the existing popup delivery and entrance visuals.
        _overlay.alpha = Mathf.Clamp01(eased);
        _panel.localScale = _baseScale * Mathf.LerpUnclamped(_startScale, 1f, Mathf.Clamp01(eased));
        if (t < 1f) return;
        CompleteAppearance();
    }

    private bool HasActiveDeparture()
    {
        if (!_entrance.isActiveAndEnabled || !_entrance.IsReady || !_entrance.IsRunning) return false;
        for (int i = 0; i < 3; i++)
        {
            var state = _entrance.GetState(i);
            if (state == PopupStoreCustomerState.Leaving || state == PopupStoreCustomerState.Expired) return true;
        }
        return false;
    }

    private bool Open(PopupStoreGradeResult result)
    {
        // Format every string before showing/locking anything. A broken Inspector format cannot partially open UI.
        try
        {
            string score = string.Format(CultureInfo.InvariantCulture, _scoreFormat, result.Score);
            string grade = result.Grade == PopupStoreGradeTier.None ? _noneGradeText
                : string.Format(CultureInfo.InvariantCulture, _gradeFormat, result.Grade);
            string count = string.Format(CultureInfo.InvariantCulture, _countFormat, result.SuccessfulOrders);
            _titleText.text = _title; _scoreText.text = score; _gradeText.text = grade;
            _countText.text = count; _confirmText.text = _confirmLabel;
        }
        catch (Exception exception) { Fail("Result text could not be formatted: " + exception.Message); return false; }

        ShownResult = result;
        _confirming = false;
        _elapsed = 0;
        _lockedGroup = _inputGroup;
        _oldInteractable = _lockedGroup.interactable;
        _oldRaycasts = _lockedGroup.blocksRaycasts;
        _lockedGroup.interactable = false;
        _lockedGroup.blocksRaycasts = false;
        // This authored child ignores the parent's input group, leaving only its confirm button interactive.
        _shownOverlay = _overlay;
        _overlay.transform.SetAsLastSibling();
        _overlay.gameObject.SetActive(true);
        _overlay.blocksRaycasts = true;
        _overlay.interactable = true;
        _shown = true;
        PresentationCount++;
        _appearing = !_hasAppeared && _showDuration > 0f;
        if (!_hasAppeared) { _hasAppeared = true; AppearanceCount++; }
        _confirmButton.interactable = false;
        var eventSystem = EventSystem.current;
        _previousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (_previousSelection != null && _previousSelection.transform.IsChildOf(transform))
            eventSystem.SetSelectedGameObject(null);
        if (_appearing)
        {
            _overlay.alpha = 0;
            _panel.localScale = _baseScale * _startScale;
        }
        else CompleteAppearance();
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_RESULT_SHOWN session=" + result.SessionId + " score=" + result.Score
                + " grade=" + result.Grade + " successful=" + result.SuccessfulOrders
                + " shown=" + PresentationCount + " appearance=" + AppearanceCount, this);
#endif
        return true;
    }

    private void CompleteAppearance()
    {
        _appearing = false;
        _overlay.alpha = 1;
        _panel.localScale = _baseScale;
        _confirmButton.interactable = true;
        if (_rewardBox != null && _rewardBox.gameObject == gameObject) _rewardBox.Present(ShownResult);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_confirmButton.gameObject);
    }

    public bool TryConfirm()
    {
        if (!_ready || !isActiveAndEnabled || !IsVisible || _appearing || _confirming
            || _view == null || _clock == null || _clock.State != PopupStoreBusinessState.Ended
            || _grade == null || !ReferenceEquals(ShownResult, _grade.FinalResult)
            || ShownResult == null || ShownResult.SessionId != _grade.SessionId) return false;
        _confirming = true;
        if (!_view.TryClose()) { _confirming = false; return false; }
        ConfirmationCount++;
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_RESULT_CONFIRMED session=" + ShownResult.SessionId
                + " confirmations=" + ConfirmationCount, this);
#endif
        return true;
    }

    private bool Validate(out string error)
    {
        error = null;
        var view = GetComponent<UIPopupStore>();
        if (view == null || !LocalRoot(_grade) || !LocalRoot(_clock) || !LocalRoot(_packing)
            || !LocalRoot(_presentation) || !LocalRoot(_entrance) || !LocalRoot(_inputGroup))
            error = "Result sources and input group must belong to this popup root.";
        else if ((_ownerGrade != null && _ownerGrade != _grade) || (_ownerClock != null && _ownerClock != _clock)
            || (_ownerPacking != null && _ownerPacking != _packing)
            || (_ownerPresentation != null && _ownerPresentation != _presentation)
            || (_ownerEntrance != null && _ownerEntrance != _entrance))
            error = "A result panel cannot be rebound to another session source.";
        else if (_overlay == null || _overlay.gameObject == gameObject || !_overlay.transform.IsChildOf(transform)
            || !_overlay.ignoreParentGroups || _panel == null || !_panel.IsChildOf(_overlay.transform)
            || _panel == _overlay.transform || !InPanel(_frame) || !InPanel(_titleText) || !InPanel(_scoreText)
            || !InPanel(_gradeText) || !InPanel(_countText) || !InPanel(_confirmText) || !InPanel(_confirmButton))
            error = "Result controls must be in the authored independent overlay of this popup.";
        else if (!Finite(_showDuration) || _showDuration < 0 || !Finite(_startScale) || _startScale <= 0 || _startScale > 1
            || _showCurve == null || _showCurve.length == 0)
            error = "Result appearance settings must be finite and valid.";
        else foreach (var key in _showCurve.keys)
            if (!Finite(key.time) || !Finite(key.value)) { error = "Result curve keys must be finite."; break; }
        return error == null;
    }

    private bool LocalRoot(Component component) => component != null && component.gameObject == gameObject;
    // A reward is an optional dedicated graphic, never a substitute for existing result controls.
    internal bool ContainsResultGraphic(Image image) => _confirmButton != null && InPanel(image) && image != _frame
        && !_confirmButton.transform.IsChildOf(image.transform) && !image.transform.IsChildOf(_confirmButton.transform)
        && image.GetComponent<TMP_Text>() == null;
    private bool InPanel(Component component) => component != null && _panel != null && component.transform.IsChildOf(_panel);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnConfirmClicked() => TryConfirm();
    private void Fail(string error)
    {
        HidePanel(); Unbind(); _ready = false;
        if (_lastError != error) Debug.LogError("PopupStoreResultPanel: " + error, this);
        _lastError = error;
    }
    private void HidePanel()
    {
        if (_rewardBox != null && _rewardBox.gameObject == gameObject) _rewardBox.HidePresentation();
        _appearing = false; _shown = false; _elapsed = 0;
        if (_lockedGroup != null)
        {
            _lockedGroup.interactable = _oldInteractable;
            _lockedGroup.blocksRaycasts = _oldRaycasts;
            _lockedGroup = null;
        }
        if (_shownOverlay != null)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(_shownOverlay.transform))
                EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _shownOverlay.alpha = 0; _shownOverlay.interactable = false; _shownOverlay.blocksRaycasts = false;
            _shownOverlay.gameObject.SetActive(false);
            _shownOverlay = null;
        }
        if (_sizedPanel != null) _sizedPanel.localScale = _baseScale;
        _previousSelection = null;
    }
    private void Unbind()
    {
        if (_boundButton != null) _boundButton.onClick.RemoveListener(OnConfirmClicked);
        _boundButton = null;
    }
    private void OnEnable() { HidePanel(); Refresh(); }
    private void LateUpdate() { if (Application.isPlaying) TickResult(Time.unscaledDeltaTime); }
    private void OnDisable() { HidePanel(); Unbind(); _ready = false; }
    private void OnDestroy() { HidePanel(); Unbind(); }
}
