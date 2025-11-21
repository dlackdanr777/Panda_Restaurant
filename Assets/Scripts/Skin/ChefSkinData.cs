using UnityEngine;

public class ChefSkinData : StaffSkinData
{
    [SerializeField] private Sprite _backSprite;
    public Sprite BackSprite => _backSprite;

    [SerializeField] private Sprite _handSprite;
    public Sprite HandSprite => _handSprite;

    [SerializeField] private Vector2 _handOffset;
    public Vector2 HandOffset => _handOffset;


    public ChefSkinData(Sprite sprite, Sprite thumbnail, Sprite[] idleSprites, string id, string name, string description, int addScore, int addTipPerMinute, Rank rank, SalesLocationType salesLocationType, int buyPrice, StaffSkinUpgradeType upgradeType, float upgradeValue, string equipId, int duplicationToken, Sprite backSprite, Sprite handSprite, Vector2 handOffset) : base(sprite, thumbnail, idleSprites, id, name, description, addScore, addTipPerMinute, rank, salesLocationType, buyPrice, upgradeType, upgradeValue, equipId, duplicationToken)
    {
        _backSprite = backSprite;
        _handSprite = handSprite;
        _handOffset = handOffset;
    }
}
