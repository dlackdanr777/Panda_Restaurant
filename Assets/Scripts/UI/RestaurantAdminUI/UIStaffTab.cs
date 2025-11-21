using UnityEngine;
using UnityEngine.UI;

public class UIStaffTab : UIRestaurantAdminTab
{
    [SerializeField] private UIStaff _uiStaff;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;

    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;
    private bool _isInitialized = false;

    // 성능 최적화를 위한 캐시 변수들
    private readonly string[] _typeStringCache = new string[(int)EquipStaffType.Length];

    public override void Init()
    {
        if (_isInitialized) return;

        int staffTypeCount = (int)EquipStaffType.Length;
        _slots = new UITabSlot[staffTypeCount];

        // 문자열 캐싱 - 한 번만 생성
        for (int i = 0; i < staffTypeCount; i++)
        {
            _typeStringCache[i] = Utility.StaffTypeStringConverter((EquipStaffType)i);
        }

        // 슬롯 초기화
        for (int i = 0; i < staffTypeCount; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            slot.name = $"StaffTabSlot{i + 1}";
        }

        UserInfo.OnChangeStaffHandler += UpdateUIOptimized;
        UserInfo.OnChangeStaffSkinHandler += UpdateUI;
        _isInitialized = true;
    }

    public override void UpdateUI()
    {
        UpdateUIOptimized(ERestaurantFloorType.Floor1, EquipStaffType.Manager);
    }

    // 대폭 최적화된 UpdateUI
    private void UpdateUIOptimized(ERestaurantFloorType floor, EquipStaffType type)
    {
        if (!_isInitialized) return;

        int staffTypeCount = (int)EquipStaffType.Length;

        // 한 번에 모든 데이터 수집 및 변경 감지
        for (int i = 0; i < staffTypeCount; i++)
        {
            BasicData newData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, (EquipStaffType)i);
            SkinData newSkinData = newData != null ? UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, newData.Id) : null;
            Sprite newSprite = newSkinData != null ? newSkinData.ThumbnailSprite : newData?.ThumbnailSprite;
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

    public void ShowUIStaff(EquipStaffType type)
    {
        _uiStaff.ShowUIStaff(_floorType, type);
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;     
        UpdateUI();
    }

    private void OnSlotClicked(int index)
    {
        _uiStaff.ShowUIStaff(_floorType, (EquipStaffType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= UpdateUIOptimized;
        UserInfo.OnChangeStaffSkinHandler -= UpdateUI;
    }
}
