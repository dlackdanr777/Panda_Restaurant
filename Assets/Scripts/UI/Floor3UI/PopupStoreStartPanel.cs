using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored start UI only. The external start controller owns usage and approval.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreStartPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup _inputGroup;
    [SerializeField] private GameObject _overlay;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _ticketsText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private GameObject _reviewNotice;
    [SerializeField] private TMP_Text _usageHudText;
    [SerializeField] private string _ticketsFormat = "오늘의 영업권 {0}/{1}";
    [SerializeField] private string _usageHudFormat = "영업권 {0}/{1}";

    private Button _boundStart, _boundClose;
    private bool _hasRender, _show = true, _canStart, _canClose = true;
    private bool _ownsInput, _oldInteractable, _oldRaycasts, _dispatching;
    private GameObject _previousSelection;

    public event Action StartRequested;
    public event Action CloseRequested;
    public bool IsVisible => _show && _overlay != null && _overlay.activeInHierarchy;
    public int Remaining { get; private set; } = -1;
    public int Limit { get; private set; } = -1;
    public string LastError { get; private set; }

    public bool Refresh()
    {
        if (!Validate())
        {
            LastError = "Start panel requires local authored overlay, input group, text and unbound controls.";
            if (_startButton != null) _startButton.interactable = false;
            return false;
        }
        if (_boundStart != _startButton || _boundClose != _closeButton)
        {
            Unbind();
            _boundStart = _startButton;
            _boundClose = _closeButton;
            _boundStart.onClick.AddListener(OnStartClicked);
            _boundClose.onClick.AddListener(OnCloseClicked);
        }
        LastError = null;
        return true;
    }

    public void Render(bool show, bool canStart, bool canClose, int remaining, int limit, string status, bool editorReview)
    {
        _hasRender = true;
        _show = show;
        _canStart = canStart;
        _canClose = canClose;
        Remaining = remaining;
        Limit = limit;
        if (!Refresh()) return;
        string remainingLabel = remaining < 0 ? "—" : remaining.ToString(CultureInfo.InvariantCulture);
        string limitLabel = limit < 0 ? "—" : limit.ToString(CultureInfo.InvariantCulture);
        _ticketsText.text = Format(_ticketsFormat, remainingLabel, limitLabel);
        _usageHudText.text = Format(_usageHudFormat, remainingLabel, limitLabel);
        _statusText.text = status ?? string.Empty;
#if UNITY_EDITOR
        _reviewNotice.SetActive(editorReview);
#else
        _reviewNotice.SetActive(false);
#endif
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_overlay == null) return;
        if (_show)
        {
            if (isActiveAndEnabled && !_ownsInput)
            {
                _oldInteractable = _inputGroup.interactable;
                _oldRaycasts = _inputGroup.blocksRaycasts;
                _ownsInput = true;
                _inputGroup.interactable = false;
                _inputGroup.blocksRaycasts = false;
                var events = EventSystem.current;
                _previousSelection = events != null ? events.currentSelectedGameObject : null;
                _overlay.transform.SetAsLastSibling();
            }
            _overlay.SetActive(true);
        }
        else
        {
            ReleaseInput();
            _overlay.SetActive(false);
        }
        _startButton.interactable = _show && _canStart;
        _closeButton.interactable = _show && _canClose;
    }

    private void ReleaseInput()
    {
        if (!_ownsInput) return;
        _ownsInput = false;
        if (_inputGroup != null)
        {
            _inputGroup.interactable = _oldInteractable;
            _inputGroup.blocksRaycasts = _oldRaycasts;
        }
        var events = EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && _overlay != null && selected.transform.IsChildOf(_overlay.transform))
            events.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        _previousSelection = null;
    }

    private bool Validate()
    {
        if (_inputGroup == null || _inputGroup.gameObject != gameObject
            || _overlay == null || _overlay == gameObject || !_overlay.transform.IsChildOf(transform)
            || _startButton == null || _closeButton == null || _startButton == _closeButton
            || !_startButton.transform.IsChildOf(_overlay.transform) || !_closeButton.transform.IsChildOf(_overlay.transform)
            || _startButton.onClick.GetPersistentEventCount() != 0 || _closeButton.onClick.GetPersistentEventCount() != 0
            || _ticketsText == null || !_ticketsText.transform.IsChildOf(_overlay.transform)
            || _statusText == null || !_statusText.transform.IsChildOf(_overlay.transform)
            || _reviewNotice == null || !_reviewNotice.transform.IsChildOf(_overlay.transform)
            || _usageHudText == null || !_usageHudText.transform.IsChildOf(transform)) return false;
        var group = _overlay.GetComponent<CanvasGroup>();
        var blocker = _overlay.GetComponent<Image>();
        return group != null && group.ignoreParentGroups && group.interactable && group.blocksRaycasts
            && group.alpha == 1f && blocker != null && blocker.raycastTarget;
    }

    private static string Format(string template, string remaining, string limit)
    {
        return (template ?? string.Empty).Replace("{0}", remaining).Replace("{1}", limit);
    }

    private void OnStartClicked()
    {
        if (_dispatching || !IsVisible || !_canStart || !_startButton.interactable) return;
        _dispatching = true;
        _canStart = false;
        _startButton.interactable = false;
        try { StartRequested?.Invoke(); }
        finally { _dispatching = false; }
    }

    private void OnCloseClicked()
    {
        if (_dispatching || !IsVisible || !_canClose || !_closeButton.interactable) return;
        _dispatching = true;
        try { CloseRequested?.Invoke(); }
        finally { _dispatching = false; }
    }

    private void Unbind()
    {
        if (_boundStart != null) _boundStart.onClick.RemoveListener(OnStartClicked);
        if (_boundClose != null) _boundClose.onClick.RemoveListener(OnCloseClicked);
        _boundStart = null;
        _boundClose = null;
    }

    private void OnEnable()
    {
        if (!_hasRender) Render(true, false, true, -1, -1, "조회 중", false);
        else if (Refresh()) ApplyVisibility();
    }
    private void OnDisable() { ReleaseInput(); Unbind(); }
    private void OnDestroy() { ReleaseInput(); Unbind(); }
}
