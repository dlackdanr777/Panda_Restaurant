using Muks.DataBind;
using Muks.Tween;
using Muks.UI;
using UnityEngine;
using UnityEngine.UI;

public class UICamera : MonoBehaviour 
{
    public enum CameraTr
    {
        Restaurant,
        Kitchen,
    }

    [Header("Components")]
    [SerializeField] private UINavigation _uiNav;
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;

    [Space]
    [Header("Camera Move Options")]
    [SerializeField] private float _moveSpeed;
    [SerializeField] private Ease _moveTweenMode;
    [SerializeField] private Transform _restaurantTr;
    [SerializeField] private Transform _kitchenTr;

    private CameraTr _currentTr;


    private void Awake()
    {
        _leftArrowButton.gameObject.SetActive(true);
        _rightArrowButton.gameObject.SetActive(false);

        _leftArrowButton.onClick.AddListener(() => ArrowButtonClicked(CameraTr.Kitchen));
        _rightArrowButton.onClick.AddListener(() => ArrowButtonClicked(CameraTr.Restaurant));

        DataBind.SetUnityActionValue("ShowRestaurant", () => 
        {
            _uiNav.AllPop();
            ArrowButtonClicked(CameraTr.Restaurant);
            });

        DataBind.SetUnityActionValue("ShowKitchen", () =>
        {
            _uiNav.AllPop();
            ArrowButtonClicked(CameraTr.Kitchen);
        });
    }


    public void ArrowButtonClicked(CameraTr moveTr)
    {
        if (_currentTr == moveTr)
            return;

        _currentTr = moveTr;
        transform.TweenStop();
        TweenData tween;
        if (moveTr == CameraTr.Restaurant)
        {
            _leftArrowButton.gameObject.SetActive(false);
            _rightArrowButton.gameObject.SetActive(false);
            tween = _cameraController.transform.TweenMove(_restaurantTr.position, _moveSpeed, _moveTweenMode);
            tween.OnComplete(() =>
            {
                _leftArrowButton.gameObject.SetActive(true);
            });
        }

        else if (moveTr == CameraTr.Kitchen)
        {
            _leftArrowButton.gameObject.SetActive(false);
            _rightArrowButton.gameObject.SetActive(false);
            tween = _cameraController.transform.TweenMove(_kitchenTr.position, _moveSpeed, _moveTweenMode);
            tween.OnComplete(() =>
            {
                _rightArrowButton.gameObject.SetActive(true);
            });
        }
    }

}
