using UnityEngine;

public class StaffChef : Staff
{
    [Header("Chef Components")]
    [SerializeField] private GameObject _handParent;
    [SerializeField] private SpriteRenderer _handSprite;


    private Sprite _backSprite;

    public override void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        base.Init(type, tableManager, kitchenSystem, customerController, feverSystem);
    }

    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        base.SetStaffData(staffData, equipFloorType);

        if (staffData == null)
            return;

        if (!(staffData is ChefData))
            throw new System.Exception("셰프 스탭에게 셰프 데이터가 들어오지 않았습니다.");

        StaffSkinData data = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, _staffData);
        if (data == null)
        {
            ChefData chefData = (ChefData)staffData;
            _sprite = _staffData.Sprite;
            _idleSprites = _staffData.IdleSprites;
            _backSprite = chefData.BackSprite;
            _handSprite.sprite = chefData.HandSprite;
            _handParent.transform.localPosition = chefData.HandOffset;

            if (_state == EStaffState.None)
            {
                _spriteRenderer.sprite = _sprite;
            }
            else if (_state == EStaffState.Action)
            {
                _spriteRenderer.sprite = _backSprite;
            }
            return;
        }
        ChefSkinData chefSkinData = (ChefSkinData)data;
        _sprite = chefSkinData.Sprite;
        _idleSprites = chefSkinData.IdleSprites;
        _backSprite = chefSkinData.BackSprite;
        _handSprite.sprite = chefSkinData.HandSprite;
        _handParent.transform.localPosition = chefSkinData.HandOffset;

        if (_state == EStaffState.None)
        {
            _spriteRenderer.sprite = _sprite;
        }
        else if(_state == EStaffState.Action)
        {
            _spriteRenderer.sprite = _backSprite;
        }
    }

    protected override void OnChangeSkinEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_staffData == null)
            return;

        DebugLog.Log("스탭 스킨 변경: " + name + " - " + _staffData.Id);

        StaffSkinData data = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, _staffData);
        if (data == null)
        {
            ChefData chefData = (ChefData)_staffData;
            _sprite = _staffData.Sprite;
            _idleSprites = _staffData.IdleSprites;
            _backSprite = chefData.BackSprite;
            _handSprite.sprite = chefData.HandSprite;
            _handParent.transform.localPosition = chefData.HandOffset;

            if (_state == EStaffState.None)
            {
                _spriteRenderer.sprite = _sprite;
            }
            else if (_state == EStaffState.Action)
            {
                _spriteRenderer.sprite = _backSprite;
            }
            return;
        }
        DebugLog.Log("셰프 스킨 변경 적용: " + name + " - " + data.Id);
        ChefSkinData chefSkinData = (ChefSkinData)data;
        _sprite = chefSkinData.Sprite;
        _idleSprites = chefSkinData.IdleSprites;
        _backSprite = chefSkinData.BackSprite;
        _handSprite.sprite = chefSkinData.HandSprite;
        _handParent.transform.localPosition = chefSkinData.HandOffset;
        _spriteRenderer.sprite = _sprite;
        DebugLog.Log(" - Hand Position: " + _handParent.transform.localPosition);
        if (_state == EStaffState.None)
        {
            _spriteRenderer.sprite = _sprite;
        }
        else if(_state == EStaffState.Action)
        {
            _spriteRenderer.sprite = _backSprite;
        }
    }

    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _spriteRenderer.sprite = state == EStaffState.Action ? _backSprite : _sprite;
        _animator.SetInteger("State", (int)_state);
    }
}

