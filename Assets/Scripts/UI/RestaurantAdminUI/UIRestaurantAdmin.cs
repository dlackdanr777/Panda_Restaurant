using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;

public enum BackgroundType
{
    Furniture,
    Staff,
    Recipe,
    Kitchen,
}


public class UIRestaurantAdmin : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIStaff _staffUI;
    [SerializeField] private UIFurniture _furnitureUI;
    [SerializeField] private UIKitchen _kitchenUI;
    [SerializeField] private GameObject _mainUI;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("BackgroundImage")]
    [SerializeField] private ScrollingImage[] _scrollImages;
    private ScrollingImage _currentScrollImage;


    [Space]
    [Header("Tabs")]
    [SerializeField] private UIFurnitureTab _furnitureTab;
    [SerializeField] private UIStaffTab _staffTab;
    [SerializeField] private UIRecipeTab _recipeTab;
    [SerializeField] private UIKitchenTab _kitchenTab;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIRestaurantAdminTabButton _furnitureButton;
    [SerializeField] private UIRestaurantAdminTabButton _staffButton;
    [SerializeField] private UIRestaurantAdminTabButton _recipeButton;
    [SerializeField] private UIRestaurantAdminTabButton _kitchenButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _shopMusic;
    [SerializeField] private AudioClip _mainMusic;


    public override void Init()
    {
        _staffButton.OnClickEvent(ShowStaffTab);
        _furnitureButton.OnClickEvent(ShowFurnitureTab);
        _recipeButton.OnClickEvent(ShowRecipeTab);
        _kitchenButton.OnClickEvent(ShowKitchenTab);
        _staffTab.Init();
        _recipeTab.Init();
        _furnitureTab.Init();
        _kitchenTab.Init();

        for(int i = 0, cnt = _scrollImages.Length; i < cnt; i++)
        {
            _scrollImages[i].Init();
        }

        SetBackgroundImage(BackgroundType.Furniture);



        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_shopMusic, 0.5f);
        gameObject.SetActive(true);
        _mainUI.SetActive(false);
        SetBackgroundImage(BackgroundType.Furniture);
        ShowFurnitureTab();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0;

        _staffTab.UpdateUI();
        _furnitureTab.UpdateUI();
        _recipeTab.UpdateUI();
        _kitchenTab.UpdateUI();

        TweenData tween = _canvasGroup.TweenAlpha(1, 0.1f);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _mainUI.SetActive(true);
            _mainUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            TweenData tween2 = _mainUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
            tween2.OnComplete(() => _canvasGroup.blocksRaycasts = true);
        });
    }

    public override void Hide()
    {
        if(_mainUI.activeSelf)
        {
            VisibleState = VisibleState.Disappearing;
            SoundManager.Instance.PlayBackgroundAudio(_mainMusic, 0.5f);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 1;
            _mainUI.transform.localScale = Vector3.one;
            TweenData tween = _mainUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
            tween.OnComplete(() =>
            {
                _mainUI.SetActive(false);
                ResetBackgroundImageOffset();
                TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
                tween2.OnComplete(() => gameObject.SetActive(false));
                VisibleState = VisibleState.Disappeared;
            });
        }

        else
        {
            SoundManager.Instance.PlayBackgroundAudio(_mainMusic, 0.5f);
            VisibleState = VisibleState.Disappeared;
            if (_uiNav.CheckActiveView("UIStaff"))
                _uiNav.Pop("UIStaff");

            if (_uiNav.CheckActiveView("UIFurniture"))
                _uiNav.Pop("UIFurniture");

            if (_uiNav.CheckActiveView("UIKitchen"))
                _uiNav.Pop("UIKitchen");

            _mainUI.SetActive(false);
            Tween.Wait(_hideDuration, () =>
            {
                TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
                tween2.OnComplete(() => gameObject.SetActive(false));
                VisibleState = VisibleState.Disappeared;
            });
        }
       
    }

    public void MainUISetActive(bool active)
    {
        if (VisibleState == VisibleState.Disappeared || VisibleState == VisibleState.Disappearing)
            _uiNav.PushNoAnime("RestaurantAdminUI");

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1;
        _mainUI.transform.localScale = new Vector3(1, 1, 1);
        _mainUI.SetActive(active);
    }


    public void ShowStaffTab()
    {
        _staffTab.transform.SetAsLastSibling();
        _staffButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _recipeButton.UnselectedButton();
        _kitchenButton.UnselectedButton();

        SetBackgroundImage(BackgroundType.Staff);
    }


    public void ShowFurnitureTab()
    {
        _furnitureTab.transform.SetAsLastSibling();
        _furnitureButton.SelectButton();
        _staffButton.UnselectedButton();
        _recipeButton.UnselectedButton();
        _kitchenButton.UnselectedButton();

        SetBackgroundImage(BackgroundType.Furniture);
    }


    public void ShowRecipeTab()
    {
        _recipeTab.transform.SetAsLastSibling();
        _recipeButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _staffButton.UnselectedButton();
        _kitchenButton.UnselectedButton();

        SetBackgroundImage(BackgroundType.Recipe);
    }


    public void ShowKitchenTab()
    {
        _kitchenTab.transform.SetAsLastSibling();
        _kitchenButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _staffButton.UnselectedButton();
        _recipeButton.UnselectedButton();

        SetBackgroundImage(BackgroundType.Kitchen);
    }

    private void SetBackgroundImage(BackgroundType type)
    {
        for(int i = 0, cnt = _scrollImages.Length; i < cnt; i++)
        {
            _scrollImages[i].gameObject.SetActive(false);
        }

        if(_currentScrollImage != null)
            _scrollImages[(int)type].SetOffset(_currentScrollImage.Offset);

        _scrollImages[(int)type].gameObject.SetActive(true);
        _currentScrollImage = _scrollImages[(int)type];
    }

    private void ResetBackgroundImageOffset()
    {
        for (int i = 0, cnt = _scrollImages.Length; i < cnt; i++)
        {
            _scrollImages[i].SetOffset(Vector2.zero);
        }
    }

}
