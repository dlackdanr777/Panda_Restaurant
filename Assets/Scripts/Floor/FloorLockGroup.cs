using Muks.Tween;
using TMPro;
using UnityEngine;

public class FloorLockGroup : MonoBehaviour
{
    [SerializeField] private ERestaurantFloorType _floorType;

    [Space]
    [SerializeField] private int _score;
    [SerializeField] private MoneyType _moneyType;
    [SerializeField] private int _moneyAmount;

    [Space]
    [SerializeField] private UIFloorLock _uiFloorLock;
    [SerializeField] private GameObject _lockObject;
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private GameObject _tweenTarget;
    [SerializeField] private FloorValueGroup _scoreGroup;
    [SerializeField] private FloorValueGroup _moneyGroup;


    private void Start()
    {
        CheckFloorLock();
        UserInfo.OnChangeFloorHandler += CheckFloorLock;

        _touchEvent.AddDownEvent(OnTouchStart);
        _touchEvent.AddUpEvent(OnTouchEnd);
    }



    private void CheckFloorLock()
    {
        bool isUnlock = UserInfo.IsFloorValid(UserInfo.CurrentStage, _floorType);
        _lockObject.SetActive(!isUnlock);

        if (isUnlock)
            return;

        _scoreGroup.SetValue(_score, UserInfo.IsScoreValid(_score));


        _moneyGroup.SetValue(_moneyAmount, _moneyType == MoneyType.Gold ? UserInfo.IsMoneyValid(_moneyAmount) : UserInfo.IsDiaValid(_moneyAmount));
    }


    private void OnTouchStart()
    {
        _tweenTarget.TweenStop();
        _tweenTarget.transform.localScale = Vector3.one;
        _tweenTarget.TweenScale(Vector3.one * 0.9f, 0.15f, Ease.OutBack);
    }

    private void OnTouchEnd()
    {
        _tweenTarget.TweenStop();
        _tweenTarget.transform.localScale = Vector3.one * 0.9f;
        _tweenTarget.TweenScale(Vector3.one * 1f, 0.15f, Ease.OutBack);

        _uiFloorLock.SetData(_score, _moneyType, _moneyAmount, _floorType);
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFloorHandler -= CheckFloorLock;
    }

}
