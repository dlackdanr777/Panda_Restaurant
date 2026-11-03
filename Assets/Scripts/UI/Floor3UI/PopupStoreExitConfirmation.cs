using Muks.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Local user-exit interception only. No abandonment, new session, reward or account operation.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreExitConfirmation : MonoBehaviour
{
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private CanvasGroup _inputGroup;
    [SerializeField] private GameObject _overlay;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _abandonButton;
    [SerializeField] private GameObject _reviewHint;

    private UIPopupStore _view;
    private Button _boundContinue;
    private GameObject _previousSelection;
    private bool _shown, _changing, _oldPopEnabled, _oldInteractable, _oldRaycasts;
    public bool IsVisible => _shown && _overlay != null && _overlay.activeInHierarchy;
    public int ShowCount { get; private set; }
    public int ContinueCount { get; private set; }
    public bool CanConfirmAbandon => false;
    public string LastError { get; private set; }

    public bool Refresh()
    {
        _view = GetComponent<UIPopupStore>();
        if (!Validate())
        { LastError = "Exit confirmation requires its own clock, input group and authored modal controls."; return false; }
        if (_boundContinue != _continueButton)
        {
            Unbind();
            _boundContinue = _continueButton;
            _boundContinue.onClick.AddListener(OnContinueClicked);
        }
        _abandonButton.interactable = false;
        _continueButton.interactable = true;
#if UNITY_EDITOR
        if (_reviewHint != null) _reviewHint.SetActive(true);
#else
        if (_reviewHint != null) _reviewHint.SetActive(false);
#endif
        if (!_shown) _overlay.SetActive(false);
        LastError = null;
        return true;
    }

    /// <summary>Returns true when handled, including an already visible confirmation. Never pops a running view.</summary>
    public bool TryRequestExit()
    {
        if (_changing || !isActiveAndEnabled || !Refresh() || !_view.CanRequestExit) return false;
        _clock.SynchronizeNow();
        if (_clock.State == PopupStoreBusinessState.NotStarted || _clock.State == PopupStoreBusinessState.Ended)
        {
            if (_shown) DismissForForcedHide();
            return _view.CloseWithoutConfirmation();
        }
        if (_shown) return true;
        if (!_view.PopEnabled) return false;
        _changing = true;
        try
        {
            // This setter settles the last running sample again. A deadline always wins over confirmation.
            _clock.SetExitConfirmationPaused(true);
            if (_clock.State == PopupStoreBusinessState.Ended)
            {
                _clock.SetExitConfirmationPaused(false);
                return _view.CloseWithoutConfirmation();
            }
            // Retire any legacy manual-button pause without clearing external app/focus conditions.
            _clock.SetManualPaused(false);
            _oldPopEnabled = _view.PopEnabled;
            _oldInteractable = _inputGroup.interactable;
            _oldRaycasts = _inputGroup.blocksRaycasts;
            _view.PopEnabled = false;
            _inputGroup.interactable = false;
            _inputGroup.blocksRaycasts = false;
            var events = EventSystem.current;
            _previousSelection = events != null ? events.currentSelectedGameObject : null;
            _shown = true;
            _overlay.transform.SetAsLastSibling();
            _overlay.SetActive(true);
            ShowCount++;
            if (events != null) events.SetSelectedGameObject(_continueButton.gameObject);
            Log("SHOWN");
            return true;
        }
        finally { _changing = false; }
    }

    public bool TryContinue()
    {
        if (_changing || !isActiveAndEnabled || !IsVisible || _view == null || !_view.CanRequestExit) return false;
        _changing = true;
        try
        {
            DismissForForcedHide();
            ContinueCount++;
            Log("CONTINUED");
            return true;
        }
        finally { _changing = false; }
    }

    /// <summary>Reserved action boundary. DEV-05D-3C must implement a terminal transaction before enabling this.</summary>
    public bool TryConfirmAbandon() => false;

    internal void DismissForForcedHide()
    {
        if (_shown)
        {
            _shown = false;
            if (_view != null) _view.PopEnabled = _oldPopEnabled;
            if (_inputGroup != null)
            { _inputGroup.interactable = _oldInteractable; _inputGroup.blocksRaycasts = _oldRaycasts; }
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != null && _overlay != null && selected.transform.IsChildOf(_overlay.transform))
                events.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _previousSelection = null;
        }
        if (_overlay != null) _overlay.SetActive(false);
        // The caller hides the business first for forced cleanup. Never clears app/focus pause reasons.
        if (_clock != null) _clock.SetExitConfirmationPaused(false);
    }

    private bool Validate()
    {
        if (_view == null || _clock == null || _clock.gameObject != gameObject
            || _inputGroup == null || _inputGroup.gameObject != gameObject
            || _overlay == null || _overlay == gameObject || !_overlay.transform.IsChildOf(transform)
            || _continueButton == null || _abandonButton == null || _continueButton == _abandonButton
            || !_continueButton.transform.IsChildOf(_overlay.transform) || !_abandonButton.transform.IsChildOf(_overlay.transform)
            || _continueButton.onClick.GetPersistentEventCount() != 0 || _abandonButton.onClick.GetPersistentEventCount() != 0)
            return false;
        var group = _overlay.GetComponent<CanvasGroup>();
        var blocker = _overlay.GetComponent<Image>();
        return group != null && group.ignoreParentGroups && group.interactable && group.blocksRaycasts
            && group.alpha == 1f && blocker != null && blocker.raycastTarget
            && (_reviewHint == null || _reviewHint.transform.IsChildOf(_overlay.transform));
    }
    private void Log(string action)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_EXIT_CONFIRMATION " + action + "; state=" + _clock.State
                + "; remaining=" + _clock.RemainingSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "; shown=" + ShowCount + "; continued=" + ContinueCount + "; abandonEnabled=false", this);
#endif
    }
    private void OnContinueClicked() => TryContinue();
    private void Unbind()
    { if (_boundContinue != null) _boundContinue.onClick.RemoveListener(OnContinueClicked); _boundContinue = null; }
    private void OnEnable() => Refresh();
    private void OnDisable() { DismissForForcedHide(); Unbind(); }
    private void OnDestroy() { DismissForForcedHide(); Unbind(); }
}
