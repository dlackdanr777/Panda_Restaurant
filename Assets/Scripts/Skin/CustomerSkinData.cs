using UnityEngine;

public class CustomerSkinData : SkinData
{
    private SkinCustomerUpgradeType _upgradeType;
    public SkinCustomerUpgradeType UpgradeType => _upgradeType;


    public CustomerSkinData(Sprite sprite, Sprite thumbnail, string id, string name, string description, int addScore, int addTipPerMinute, Rank rank, SalesLocationType salesLocationType, int buyPrice, SkinCustomerUpgradeType upgradeType, float upgradeValue, string equipId)
    {
        _sprite = sprite;
        _thumbnailSprite = thumbnail;
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
    }


}
