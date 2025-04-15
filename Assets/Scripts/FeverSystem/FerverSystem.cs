using UnityEngine;

public class FeverSystem : MonoBehaviour
{
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIFever _uiFever;

    private bool _isFeverStart = false;
    public bool IsFeverStart => _isFeverStart;
    public bool SetFeverStart(bool value) => _isFeverStart = value;

    private int _feverGauge = 0;
    public int FeverGauge => _feverGauge;
    public void SetFeverGauge(int value) => _feverGauge = value;

    private int _currentMaxFeverGauge = 500;
    public int CurrentMaxFeverGauge => _currentMaxFeverGauge;
    private int[] _maxFeverGauges = new int[]{10, 10, 10, 10, 10, 10, 10, 600, 700, 700, 800, 800, 900, 900, 1000, 1000};


    public void AddFeverGauge()
    {
        if (_isFeverStart || UserInfo.IsTutorialStart)
            return;

        _feverGauge = Mathf.Clamp(_feverGauge + ConstValue.ADD_PEVER_GAUGE, 0, _currentMaxFeverGauge);
        DebugLog.Log($"Fever Gauge : {_feverGauge} / {_currentMaxFeverGauge}");
        _uiFever.OnChangeGauge();
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
}
