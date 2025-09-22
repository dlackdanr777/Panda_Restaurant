using System;
using UnityEngine;
using UnityEngine.UI;

public class UIFurnitureTab : UIRestaurantAdminTab
{
    [Header("Components")]
    [SerializeField] private UIFurniture _uiFurniture;

    [Space]
    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;

    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;
    private bool _isInitialized = false;

    // 성능 최적화를 위한 캐시 변수들
    private readonly string[] _typeStringCache = new string[(int)FurnitureType.Length];

    public override void Init()
    {
        if (_isInitialized) return;

        int furnitureTypeCount = (int)FurnitureType.Length;
        _slots = new UITabSlot[furnitureTypeCount];

        // 문자열 캐싱 - 한 번만 생성
        for (int i = 0; i < furnitureTypeCount; i++)
        {
            _typeStringCache[i] = Utility.FurnitureTypeStringConverter((FurnitureType)i);
        }

        // 슬롯 초기화
        for (int i = 0; i < furnitureTypeCount; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => ShowUIFurniture(index));
            slot.name = $"FurnitureTabSlot{i + 1}";
        }

        UserInfo.OnChangeFurnitureHandler += UpdateUIOptimized;
        _isInitialized = true;
    }

    public override void UpdateUI()
    {
        UpdateUIOptimized(ERestaurantFloorType.Floor1, FurnitureType.Table1);
    }

    // 대폭 최적화된 UpdateUI
    private void UpdateUIOptimized(ERestaurantFloorType floor, FurnitureType type)
    {
        if (!_isInitialized) return;

        int furnitureTypeCount = (int)FurnitureType.Length;

        // 한 번에 모든 데이터 수집 및 변경 감지
        for (int i = 0; i < furnitureTypeCount; i++)
        {
            BasicData newData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _floorType, (FurnitureType)i);
            Sprite newSprite = newData?.ThumbnailSprite;
            _slots[i].UpdateUI(newSprite, _typeStringCache[i]);
        }
    }


    public override void SetAttention()
    {
        UpdateUI();
    }

    public override void SetNotAttention()
    {
        // 필요시 구현
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;   
        UpdateUI();
    }


    public void ShowUIFurniture(int index)
    {
        _uiFurniture.ShowUIFurniture(_floorType, (FurnitureType)index);
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _uiFurniture.ShowUIFurniture(_floorType, type);
    }

    private void OnDestroy()
    {
       UserInfo.OnChangeFurnitureHandler -= UpdateUIOptimized;
    }
}
