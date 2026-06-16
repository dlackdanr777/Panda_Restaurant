using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class FloorLockGroup : MonoBehaviour
{
    [SerializeField] private ERestaurantFloorType _floorType;
    [SerializeField] private string _unlockChallengeId;

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
    [SerializeField] private BoxCollider2D _collider2D;
    [SerializeField] private GameObject _goldIcon;
    [SerializeField] private GameObject _diaIcon;

    [Space]
    [SerializeField] private UnityEvent _onUnlockFloorEvent;


    private void Start()
    {
        CheckFloorLock();
        UserInfo.OnChangeFloorHandler += CheckFloorLock;
        UserInfo.OnClearChallengeHandler += CheckFloorLock;
        _touchEvent.AddDownEvent(OnTouchStart);
        _touchEvent.AddUpEvent(OnTouchEnd);
    }



    private void CheckFloorLock()
    {
        bool isUnlock = UserInfo.IsFloorValid(UserInfo.CurrentStage, _floorType);
        bool challengeClear = string.IsNullOrEmpty(_unlockChallengeId) || UserInfo.GetIsClearChallenge(_unlockChallengeId);
        _lockObject.SetActive(!isUnlock);
        _collider2D.enabled = challengeClear;
        if (isUnlock)
            return;

        _scoreGroup.SetValue(_score, UserInfo.IsScoreValid(_score));
        _moneyGroup.SetValue(_moneyAmount, _moneyType == MoneyType.Gold ? UserInfo.IsMoneyValid(_moneyAmount) : UserInfo.IsDiaValid(_moneyAmount));
        _goldIcon.SetActive(_moneyType == MoneyType.Gold);
        _diaIcon.SetActive(_moneyType == MoneyType.Dia);
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

        _uiFloorLock.SetData(_score, _moneyType, _moneyAmount, _floorType, OnUnlockFloor);
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFloorHandler -= CheckFloorLock;
        UserInfo.OnClearChallengeHandler -= CheckFloorLock;
    }

    private void OnUnlockFloor()
    {
        _onUnlockFloorEvent?.Invoke();
    }

}
