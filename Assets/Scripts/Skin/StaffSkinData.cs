using UnityEngine;

public class StaffSkinData : SkinData
{
    private StaffSkinUpgradeType _upgradeType;
    public StaffSkinUpgradeType UpgradeType => _upgradeType;

    private Sprite[] _idleSprites;
    public Sprite[] IdleSprites => _idleSprites;

    [SerializeField] protected RuntimeAnimatorController _animatorController;
    public RuntimeAnimatorController AnimatorController => _animatorController;


    public StaffSkinData(Sprite sprite, Sprite thumbnail, Sprite[] idleSprites, string id, string name, string description, int addScore, int addTipPerMinute, Rank rank, SalesLocationType salesLocationType, int buyPrice, StaffSkinUpgradeType upgradeType, float upgradeValue, string equipId)
    {
        _sprite = sprite;
        _thumbnailSprite = thumbnail;
        _idleSprites = idleSprites;
        _id = id;
        _name = name;
        _description = description;
        _addScore = addScore;
        _addTipPerMinute = addTipPerMinute;
        _rank = rank;
        _salesLocationType = salesLocationType;
        _buyPrice = buyPrice;
        _upgradeValue = upgradeValue;
        _equipId = equipId;
        _upgradeType = upgradeType;
        _equipId = equipId;
        _animatorController = Resources.Load<RuntimeAnimatorController>("StaffData/Animator/" + id);
    }
}
