using Muks.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Retired HUD control. Retains the authored reference solely to keep it hidden, including Player builds.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePauseController : MonoBehaviour
{
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private Button _pauseButton;
    public bool IsReady { get; private set; }
    public int ToggleCount => 0;
    public string LastError { get; private set; }

    public bool Refresh()
    {
        var view = GetComponent<UIPopupStore>();
        IsReady = view != null && _clock != null && _clock.gameObject == gameObject
            && _pauseButton != null && _pauseButton.GetComponentInParent<UIPopupStore>(true) == view;
        LastError = IsReady ? null : "The retired pause control must belong to this popup.";
        if (IsReady)
        {
            _pauseButton.interactable = false;
            _pauseButton.gameObject.SetActive(false);
        }
        return IsReady;
    }
    // Kept inert for stale callers. Internal clock context pause remains available without this button.
    public bool TryTogglePause() { Refresh(); return false; }
    private void OnEnable() => Refresh();
    private void LateUpdate() { if (_pauseButton != null && (_pauseButton.gameObject.activeSelf || _pauseButton.interactable)) Refresh(); }
}