using System;
using Muks.MobileUI;
using Muks.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Popup view. Closing leaves the existing Floor3 navigation entry intact.</summary>
public sealed class UIPopupStore : MobileUIView
{
    public const string ViewName = "UIPopupStore";

    [SerializeField] private Button _closeButton;
    [Tooltip("Presentation-only controls. Packing and clearing controls are bound separately.")]
    [SerializeField] private Button[] _displayOnlyButtons;

    private bool _closing;
    internal UINavigation Navigation => _uiNav;
    internal bool IsClosing => _closing;
    internal Button PhaseCloseButton => _closeButton;

    internal bool IsBoundTo(UINavigation navigation) => _uiNav == navigation;

    public override void Init()
    {
        // The base Canvas cache is not serialized; also bind it in a player build.
        base.OnValidate();
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
            PopupStoreSound.BindButton(_closeButton, SoundEffectType.ButtonExitSound);
        }
        if (_displayOnlyButtons != null)
            foreach (var button in _displayOnlyButtons)
                if (button != null && button != _closeButton) button.interactable = false;
        VisibleState = VisibleState.Disappeared;
        GetComponent<PopupStoreBusinessClock>()?.SetViewVisible(false);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        if (GetComponent<PopupStoreSessionLifetime>()?.IsRetired == true)
            throw new InvalidOperationException("A retired popup session must be replaced before entry.");
        if (_closeButton == null)
            throw new InvalidOperationException("UIPopupStore requires its authored close button.");
        gameObject.SetActive(true);
        VisibleState = VisibleState.Appeared;
        GetComponent<PopupStoreBusinessClock>()?.SetViewVisible(true);
        GetComponent<PopupStorePacking>()?.Refresh();
        GetComponent<PopupStorePauseController>()?.Refresh();
        GetComponent<PopupStoreExitConfirmation>()?.Refresh();
        PopupStoreAccountPhaseBootstrap.TryPrepare(this, out _);
        GetComponent<PopupStoreStartController>()?.OnViewShown();
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        GetComponent<PopupStoreBusinessClock>()?.SetViewVisible(false);
        GetComponent<PopupStoreExitConfirmation>()?.DismissForForcedHide();
        gameObject.SetActive(false);
    }

    /// <summary>User close request. A running business stays on the stack while confirmation is shown.</summary>
    public bool TryRequestExit()
    {
        if (!CanRequestExit) return false;
        if (GetComponent<PopupStoreAccountRecoveryPresentation>()?.IsBound == true) return false;
        var preview = GetComponent<PopupStorePhasePreview>();
        if (preview != null && preview.Session?.IsTerminal == true) return preview.TryConfirmResult();
        var confirmation = GetComponent<PopupStoreExitConfirmation>();
        if (confirmation != null) return confirmation.TryRequestExit();
        // A missing authored guard must never silently close an active business.
        var clock = GetComponent<PopupStoreBusinessClock>();
        clock?.SynchronizeNow();
        return (clock == null || clock.State == PopupStoreBusinessState.NotStarted
            || clock.State == PopupStoreBusinessState.Ended) && CloseWithoutConfirmation();
    }

    internal bool CanRequestExit => _uiNav != null && !_uiNav.IsViewsInactive && _uiNav.FirstView == this
        && VisibleState == VisibleState.Appeared && _uiNav.ViewsVisibleStateCheck();

    /// <summary>Compatibility for result confirmation: true only when this view actually closed.</summary>
    public bool TryClose()
    {
        if (!TryRequestExit()) return false;
        return VisibleState == VisibleState.Disappeared && !gameObject.activeSelf;
    }

    internal bool CloseWithoutConfirmation()
    {
        if (_closing || !CanRequestExit || !PopEnabled) return false;
        var clock = GetComponent<PopupStoreBusinessClock>();
        clock?.SynchronizeNow();
        if (clock != null && clock.State != PopupStoreBusinessState.NotStarted
            && clock.State != PopupStoreBusinessState.Ended) return false;
        var lifetime = GetComponent<PopupStoreSessionLifetime>();
        bool completed = clock != null && clock.State == PopupStoreBusinessState.Ended;
        if (completed && (lifetime == null || !lifetime.MarkCompletedForClose())) return false;
        _closing = true;
        try
        {
            _uiNav.Pop(ViewName);
            bool closed = VisibleState == VisibleState.Disappeared && !gameObject.activeSelf;
            if (!closed && completed) lifetime.UndoCompletedClose();
            return closed;
        }
        finally { _closing = false; }
    }

    internal bool TryAbandonAndClose()
    {
        var confirmation = GetComponent<PopupStoreExitConfirmation>();
        var clock = GetComponent<PopupStoreBusinessClock>();
        var lifetime = GetComponent<PopupStoreSessionLifetime>();
        if (_closing || !CanRequestExit || confirmation == null || !confirmation.IsVisible
            || clock == null || lifetime == null || lifetime.IsRetired) return false;
        var preview = GetComponent<PopupStorePhasePreview>();
        if (preview != null && preview.IsInitialized) return preview.RequestAbandon();
        _closing = true;
        try
        {
            if (!clock.TryAbandonSession()) return false;
            // Terminal first, then release only the modal's input lock. Focus cannot resume this session.
            confirmation.DismissForForcedHide();
            _uiNav.Pop(ViewName);
#if UNITY_EDITOR
            if (Application.isPlaying)
                Debug.Log("POPUP_STORE_SESSION_ABANDONED session=" + lifetime.SessionId
                    + "; reason=" + clock.EndReason + "; abandoned=" + clock.AbandonCount
                    + "; remaining=" + clock.RemainingSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), this);
#endif
            return VisibleState == VisibleState.Disappeared && !gameObject.activeSelf;
        }
        finally { _closing = false; }
    }

    private void Close() => TryClose();

    // Preview termination is not normal score-grade completion. Re-entry can only show the
    // same terminal local result; a new run needs a new isolated Editor owner.
    internal bool ClosePhasePreviewResult(PopupStorePhasePreview owner)
    {
        if (_closing || owner == null || owner.gameObject != gameObject || !owner.IsInitialized
            || owner.Session?.IsTerminal != true || !CanRequestExit || !PopEnabled) return false;
        _closing = true;
        try
        {
            var lifetime = GetComponent<PopupStoreSessionLifetime>();
            if (owner.IsAccountMode && (lifetime == null || !lifetime.MarkAccountPhaseClosed(owner))) return false;
            _uiNav.Pop(ViewName);
            bool closed = VisibleState == VisibleState.Disappeared && !gameObject.activeSelf;
            if (!closed && owner.IsAccountMode) lifetime.UndoCompletedClose();
            return closed;
        }
        finally { _closing = false; }
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
    }
}
