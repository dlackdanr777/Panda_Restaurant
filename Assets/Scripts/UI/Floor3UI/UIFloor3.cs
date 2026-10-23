using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIFloor3 : MobileUIView
{
    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button[] _buttons;

    [Header("Popup store")]
    [SerializeField] private Button _popupStoreButton;
    [SerializeField] private UIPopupStore _popupStoreView;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    public override void Init()
    {
        gameObject.SetActive(false);

        if (_buttons == null) return;
        for(int i = 0; i < _buttons.Length; i++)
        {
            var button = _buttons[i];
            if (button == null) continue;
            button.onClick.RemoveListener(OnButtonClicked);
            button.onClick.RemoveListener(OnPopupStoreClicked);
            if (button == _popupStoreButton && _popupStoreView != null)
                button.onClick.AddListener(OnPopupStoreClicked);
            else
                button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnPopupStoreClicked()
    {
        if (_popupStoreButton == null || _popupStoreView == null || _uiNav == null
            || !_popupStoreView.IsBoundTo(_uiNav) || _uiNav.IsViewsInactive
            || _uiNav.FirstView != this || VisibleState != VisibleState.Appeared
            || !_uiNav.ViewsVisibleStateCheck() || PopupManager.IsCustomerEncounterPresentationBlocked
            || _uiNav.CheckActiveView(UIPopupStore.ViewName)) return;

        _uiNav.Push(UIPopupStore.ViewName);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;

        gameObject.SetActive(true);
        _animeUI.SetActive(true);
        _dontTouchArea.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        TweenData tween2 = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween2.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            gameObject.SetActive(true);
            _dontTouchArea.gameObject.SetActive(false);
        });
        
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;

        gameObject.SetActive(true);
        _animeUI.SetActive(true);
        _dontTouchArea.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);
        TweenData tween2 = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween2.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
            _dontTouchArea.gameObject.SetActive(false);
        });
    }

    private void OnButtonClicked()
    {
        PopupManager.Instance.ShowDisplayText("업데이트 예정");
    }
}
