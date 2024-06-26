using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIRestaurantAdmin : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIStaff _staffUI;
    [SerializeField] private GameObject _mainUI;
    [SerializeField] private CanvasGroup _canvasGroup;


    [Space]
    [Header("Tabs")]
    [SerializeField] private GameObject _furnitureTab;
    [SerializeField] private UIStaffTab _staffTab;
    [SerializeField] private UIRecipeTab _recipeTab;
    [SerializeField] private GameObject _kitchenTab;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIRestaurantAdminTabButton _furnitureButton;
    [SerializeField] private UIRestaurantAdminTabButton _staffButton;
    [SerializeField] private UIRestaurantAdminTabButton _recipeButton;
    [SerializeField] private UIRestaurantAdminTabButton _kitchenButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;


    public override void Init()
    {
        _staffButton.OnClickEvent(StaffButtonClicked);
        _furnitureButton.OnClickEvent(FurnitureButtonClicked);
        _recipeButton.OnClickEvent(RecipeButtonClicked);
        _kitchenButton.OnClickEvent(KitchenButtonClicked);
        _staffTab.Init();
        _recipeTab.Init();

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        gameObject.SetActive(true);
        _mainUI.SetActive(false);
        DataBind.GetUnityActionValue("HideNoAnimeStaffUI")?.Invoke();
        FurnitureButtonClicked();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0;

        TweenData tween = _canvasGroup.TweenAlpha(1, 0.1f);
        tween.OnComplete(() =>
        {
            _mainUI.SetActive(true);
            _mainUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            TweenData tween2 = _mainUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
            tween2.OnComplete(() => _canvasGroup.blocksRaycasts = true);
        });
    }

    public override void Hide()
    {
        _mainUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 1;

        _mainUI.transform.localScale = Vector3.one;
        TweenData tween = _mainUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            _mainUI.SetActive(false);
            TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
            tween2.OnComplete(() => gameObject.SetActive(false));
        });
    }

    public void MainUISetActive(bool active)
    {
        _mainUI.SetActive(active);
    }


    private void StaffButtonClicked()
    {
        _staffTab.transform.SetAsLastSibling();
        _staffButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _recipeButton.UnselectedButton();
        _kitchenButton.UnselectedButton();
    }


    private void FurnitureButtonClicked()
    {
        _furnitureTab.transform.SetAsLastSibling();
        _furnitureButton.SelectButton();
        _staffButton.UnselectedButton();
        _recipeButton.UnselectedButton();
        _kitchenButton.UnselectedButton();
    }


    private void RecipeButtonClicked()
    {
        _recipeTab.transform.SetAsLastSibling();
        _recipeButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _staffButton.UnselectedButton();
        _kitchenButton.UnselectedButton();
    }


    private void KitchenButtonClicked()
    {
        _kitchenTab.transform.SetAsLastSibling();
        _kitchenButton.SelectButton();
        _furnitureButton.UnselectedButton();
        _staffButton.UnselectedButton();
        _recipeButton.UnselectedButton();
    }

}
