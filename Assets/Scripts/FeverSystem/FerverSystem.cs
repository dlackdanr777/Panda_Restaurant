using System;
using System.Collections;
using UnityEngine;

public class FeverSystem : MonoBehaviour
{
    public event Action OnStartFeverHandler;
    public event Action OnEndFeverHandler;

    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIFever _uiFever;
    [SerializeField] private CustomerController _customerController;

    private bool _isFeverStart = false;
    public bool IsFeverStart => _isFeverStart;

    private int _feverGauge = 0;
    public int FeverGauge => _feverGauge;
    public void SetFeverGauge(int value) => _feverGauge = value;

    private int _currentMaxFeverGauge = 500;
    public int CurrentMaxFeverGauge => _currentMaxFeverGauge;
    private int[] _maxFeverGauges = new int[]{10, 10, 10, 10, 10, 10, 10, 600, 700, 700, 800, 800, 900, 900, 1000, 1000};

    private Coroutine _feverRoutine = null;

    public void AddFeverGauge()
    {
        if (_isFeverStart || UserInfo.IsTutorialStart)
            return;

        _feverGauge = Mathf.Clamp(_feverGauge + ConstValue.ADD_PEVER_GAUGE, 0, _currentMaxFeverGauge);
        DebugLog.Log($"Fever Gauge : {_feverGauge} / {_currentMaxFeverGauge}");
        _uiFever.OnChangeGauge();
    }


    public void FeverStart()
    {
        if(_isFeverStart)
            return;

        _isFeverStart = true;

        if(_feverRoutine != null)
        {
            StopCoroutine(_feverRoutine);
            _feverRoutine = null;
        }

        _feverRoutine = StartCoroutine(StartFeverRoutine());

    }

    private void Awake()
    {
        _uiFever.Init(this);
        _feverGauge = 0;
        _isFeverStart = false;
        UserInfo.OnAddCustomerCountHandler += AddFeverGauge;
        UserInfo.OnChangeFurnitureHandler += OnEquipFurnitureEvent;
    }

    private void Start()
    {
        OnEquipFurnitureEvent(ERestaurantFloorType.Floor1, FurnitureType.Table1);
    }


    private void OnEnable()
    {
        _uiFever.OnChangeGauge();
    }

    private void OnDestroy()
    {
        UserInfo.OnAddCustomerCountHandler -= AddFeverGauge;
        UserInfo.OnChangeFurnitureHandler -= OnEquipFurnitureEvent;
    }


    private void OnEquipFurnitureEvent(ERestaurantFloorType floor, FurnitureType type)
    {
        int equipTableCount = 0;
        for(int i = 0, cnt = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage); i <= cnt; ++i)
        {
            ERestaurantFloorType floorType = (ERestaurantFloorType)i;
            for(int j = 0, cntJ = (int)FurnitureType.Table5; j <= cntJ; ++j)
            {
                FurnitureType furnitureType = (FurnitureType)j;
                if (UserInfo.IsEquipFurniture(UserInfo.CurrentStage, floorType, furnitureType))
                {
                    equipTableCount++;
                }
            }
        }
        _currentMaxFeverGauge = Mathf.Clamp(_maxFeverGauges[equipTableCount], _maxFeverGauges[0], ConstValue.MAX_PEVER_GAUGE);
        _uiFever.OnChangeGauge();
    }


    private IEnumerator StartFeverRoutine()
    {
        float time = ConstValue.PEVER_TIME;
        float timer = 0;
        OnStartFeverHandler?.Invoke();
        _mainScene.PlayMainMusic();
        GameManager.Instance.SetGameSpeed(1f);
        while (timer < time)
        {
            yield return YieldCache.WaitForSeconds(0.5f);

            if(!_customerController.IsMaxCount)
            {
                _customerController.AddTabCount();
            }

            timer += 0.5f;
        }

        _isFeverStart = false;
        _mainScene.PlayMainMusic();
        GameManager.Instance.SetGameSpeed(0);
        OnEndFeverHandler?.Invoke();
    }
}
