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
    [SerializeField] private GameObject _recipeTab;
    [SerializeField] private GameObject _kitchenTab;

    [Space]
    [Header("Buttons")]
    [SerializeField] private Button _furnitureButton;
    [SerializeField] private Button _staffButton;
    [SerializeField] private Button _recipeButton;
    [SerializeField] private Button _kitchenButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;


    public override void Init()
    {
        _staffButton.onClick.AddListener(() => _staffTab.transform.SetAsLastSibling());
        _furnitureButton.onClick.AddListener(() => _furnitureTab.transform.SetAsLastSibling());
        _recipeButton.onClick.AddListener(() => _recipeTab.transform.SetAsLastSibling());
        _kitchenButton.onClick.AddListener(() => _kitchenTab.transform.SetAsLastSibling());

        _staffTab.Init();

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        gameObject.SetActive(true);
        _staffUI.gameObject.SetActive(false);
        _mainUI.SetActive(false);
        _furnitureTab.transform.SetAsLastSibling();
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
        _staffUI.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 1;
        TweenData tween = _mainUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            _mainUI.SetActive(false);
            TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
            tween2.OnComplete(() => gameObject.SetActive(false));
        });
    }

}
