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
    [Tooltip("Presentation-only controls. Functional pause, packing and clearing controls are bound separately.")]
    [SerializeField] private Button[] _displayOnlyButtons;

    internal bool IsBoundTo(UINavigation navigation) => _uiNav == navigation;

    public override void Init()
    {
        // The base Canvas cache is not serialized; also bind it in a player build.
        base.OnValidate();
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
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
        if (_closeButton == null)
            throw new InvalidOperationException("UIPopupStore requires its authored close button.");
        gameObject.SetActive(true);
        VisibleState = VisibleState.Appeared;
        GetComponent<PopupStoreBusinessClock>()?.SetViewVisible(true);
        GetComponent<PopupStorePacking>()?.Refresh();
        GetComponent<PopupStorePauseController>()?.Refresh();
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        GetComponent<PopupStoreBusinessClock>()?.SetViewVisible(false);
        gameObject.SetActive(false);
    }

    /// <summary>Shared guarded close path for the authored close button and the local result panel.</summary>
    public bool TryClose()
    {
        if (_uiNav == null || _uiNav.IsViewsInactive || _uiNav.FirstView != this
            || !PopEnabled || VisibleState != VisibleState.Appeared || !_uiNav.ViewsVisibleStateCheck()) return false;
        _uiNav.Pop(ViewName);
        return VisibleState == VisibleState.Disappeared && !gameObject.activeSelf;
    }

    private void Close() => TryClose();

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
    }
}
