using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagement : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIManagementLayout _managementLayout;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Camera _kitchenCamera;
    [SerializeField] private Camera _restaurantCamera;

    [SerializeField] private TextMeshProUGUI _floorText;
    [SerializeField] private Button _leftFloorButton;
    [SerializeField] private Button _rightFloorButton;

    [Space]
    [Header("Set Effect Components")]
    [SerializeField] private UIManagementSetEffect _setEffectGroup;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private ERestaurantFloorType _currentFloor;


    public override void Init()
    {
        _setEffectGroup.Init();
        _managementLayout.Init();

        _leftFloorButton.onClick.AddListener(() => OnChangeFloorType(-1));
        _rightFloorButton.onClick.AddListener(() => OnChangeFloorType(1));

        _kitchenCamera.gameObject.SetActive(false);
        _restaurantCamera.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.gameObject.SetActive(true);
        _kitchenCamera.gameObject.SetActive(true);
        _restaurantCamera.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _currentFloor = _mainScene.CurrentFloor;

        OnChangeFloorType(_currentFloor);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _canvasGroup.blocksRaycasts = false;
        _animeUI.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            _kitchenCamera.gameObject.SetActive(false);
            _restaurantCamera.gameObject.SetActive(false);
            gameObject.SetActive(false);
        });
    }


    private void OnChangeFloorType(ERestaurantFloorType floorType)
    {
        _currentFloor = floorType;
        _managementLayout.UpdateLayout(floorType);
        _setEffectGroup.UpdateUI(floorType);

        _floorText.SetText(Utility.GetFloorStrKrByType(_currentFloor));
    }
    

    private void OnChangeFloorType(int dir)
    {
        ERestaurantFloorType totalFloor = UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        int totalFloorCount = (int)totalFloor + 1;
        _currentFloor = (ERestaurantFloorType)(((int)_currentFloor + dir + totalFloorCount) % totalFloorCount);
        OnChangeFloorType(_currentFloor);
    }

}
