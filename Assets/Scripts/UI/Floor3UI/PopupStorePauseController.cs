using System;
using Muks.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One authored pause/resume button for this popup's existing clock. No global time or account state.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePauseController : MonoBehaviour
{
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private string _pauseLabel = "일시정지";
    [SerializeField] private string _resumeLabel = "계속하기";

    private PopupStoreBusinessClock _boundClock;
    private Button _boundButton;
    private Func<int> _frameSource;
    private int _lastInputFrame = -1;
    private bool _changing;
    public bool IsReady { get; private set; }
    public int ToggleCount { get; private set; }
    public string LastError { get; private set; }

    public bool Refresh()
    {
        if (!Validate()) { Unbind(); IsReady = false; LastError = "Pause controls must belong to this popup and its authored HUD button."; return false; }
        if (_boundClock != _clock || _boundButton != _pauseButton)
        {
            Unbind(); _boundClock = _clock; _boundButton = _pauseButton;
            _boundClock.StateChanged += UpdateControls;
            _boundClock.ContextChanged += UpdateControls;
            _boundButton.onClick.AddListener(OnClicked);
        }
        IsReady = true; LastError = null;
        UpdateControls();
        return true;
    }

    public bool TryTogglePause()
    {
        if (_changing || !Refresh() || !CanUseButton()) return false;
        int frame;
        try { frame = _frameSource != null ? _frameSource() : Time.frameCount; }
        catch { return false; }
        if (frame < 0 || frame == _lastInputFrame) return false;
        _lastInputFrame = frame;
        _changing = true;
        try
        {
            bool before = _clock.IsManualPaused;
            _clock.SetManualPaused(!before);
            if (_clock.IsManualPaused == before) { UpdateControls(); return false; }
            ToggleCount++;
            UpdateControls();
#if UNITY_EDITOR
            if (Application.isPlaying)
                Debug.Log("POPUP_STORE_MANUAL_PAUSE manual=" + _clock.IsManualPaused + "; state=" + _clock.State
                    + "; remaining=" + _clock.RemainingSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + "; elapsed=" + _clock.ElapsedSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + "; toggles=" + ToggleCount, this);
#endif
            return true;
        }
        finally { _changing = false; }
    }

    private bool Validate()
    {
        var view = GetComponent<UIPopupStore>();
        return view != null && _clock != null && _clock.gameObject == gameObject
            && _pauseButton != null && _pauseButton.GetComponentInParent<UIPopupStore>(true) == view
            && _label != null && _label.transform.IsChildOf(_pauseButton.transform)
            && _pauseButton.onClick.GetPersistentEventCount() == 0
            && !string.IsNullOrEmpty(_pauseLabel) && !string.IsNullOrEmpty(_resumeLabel);
    }
    private bool CanUseButton()
    {
        var view = GetComponent<UIPopupStore>();
        return isActiveAndEnabled && IsReady && _clock != null && _clock.isActiveAndEnabled
            && view != null && view.VisibleState == VisibleState.Appeared
            && (_clock.State == PopupStoreBusinessState.Running || _clock.State == PopupStoreBusinessState.Paused);
    }
    private void UpdateControls()
    {
        if (!Validate()) return;
        _pauseButton.interactable = CanUseButton();
        _label.text = _clock.IsManualPaused ? _resumeLabel : _pauseLabel;
    }
    private void OnClicked() => TryTogglePause();
    private void Unbind()
    {
        if (_boundClock != null) { _boundClock.StateChanged -= UpdateControls; _boundClock.ContextChanged -= UpdateControls; }
        if (_boundButton != null) { _boundButton.onClick.RemoveListener(OnClicked); _boundButton.interactable = false; }
        _boundClock = null; _boundButton = null;
    }
    private void OnEnable() => Refresh();
    private void OnDisable() { Unbind(); IsReady = false; }
    private void OnDestroy() => Unbind();
}
