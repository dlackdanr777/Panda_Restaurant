using UnityEngine;

public class FloorLocker : MonoBehaviour
{
    [SerializeField] private GameObject _lockObj;
    [SerializeField] private ERestaurantFloorType _floorType;
    [SerializeField] private string _unlockChallengeId;


    private void Awake()
    {
        OnChangeFloor();
        UserInfo.OnChangeFloorHandler += OnChangeFloor;
        UserInfo.OnClearChallengeHandler += OnChangeFloor;
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeFloorHandler -= OnChangeFloor;
        UserInfo.OnClearChallengeHandler -= OnChangeFloor;
    }

    private void OnChangeFloor()
    {
        if(_floorType <= UserInfo.GetUnlockFloor(UserInfo.CurrentStage))
        {
            _lockObj.SetActive(false);
        }
        else
        {
            // 챌린지 클리어 여부 확인
            if(!string.IsNullOrEmpty(_unlockChallengeId) && UserInfo.GetIsClearChallenge(_unlockChallengeId))
            {
                _lockObj.SetActive(false);
            }
            else
            {
                _lockObj.SetActive(true);
            }
        }
    }
}
