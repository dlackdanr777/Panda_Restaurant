using UnityEngine;
using UnityEngine.UI;

public class UIKitchenTab : UIRestaurantAdminTab
{
    [Header("Components")]
    [SerializeField] private UIKitchen _uiKitchen;

    [Space]
    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;

    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;
    private bool _isInitialized = false;

    // 성능 최적화를 위한 캐시 변수들
    private readonly string[] _typeStringCache = new string[(int)KitchenUtensilType.Length];

    public override void Init()
    {
        if (_isInitialized) return;

        int typeCount = (int)KitchenUtensilType.Length;
        _slots = new UITabSlot[typeCount];

        // 문자열 캐싱 - 한 번만 생성
        for (int i = 0; i < typeCount; i++)
        {
            _typeStringCache[i] = Utility.KitchenUtensilTypeStringConverter((KitchenUtensilType)i);
        }

        // 슬롯 초기화
        for (int i = 0; i < typeCount; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            slot.name = $"KitchenTabSlot{i + 1}";
        }

        UserInfo.OnChangeKitchenUtensilHandler += UpdateUIOptimized;
        _isInitialized = true;
    }

    public override void SetAttention()
    {
        UpdateUI();
    }

    public override void SetNotAttention()
    {
        // 필요시 구현
    }

    public void ShowUIKitchen(KitchenUtensilType type)
    {
        _uiKitchen.ShowUIKitchen(_floorType, type);
    }

    public override void UpdateUI()
    {
        UpdateUIOptimized(ERestaurantFloorType.Floor1, KitchenUtensilType.Burner1);
    }

    // 대폭 최적화된 UpdateUI
    private void UpdateUIOptimized(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        if (!_isInitialized) return;

        int typeCount = (int)KitchenUtensilType.Length;

        // 한 번에 모든 데이터 수집 및 변경 감지
        for (int i = 0; i < typeCount; i++)
        {
            BasicData newData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _floorType, (KitchenUtensilType)i);
            Sprite newSprite = newData?.ThumbnailSprite;
            _slots[i].UpdateUI(newSprite, _typeStringCache[i]);
        }
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
        _uiKitchen.ShowUIKitchen(_floorType, (KitchenUtensilType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= UpdateUIOptimized;
    }
}
