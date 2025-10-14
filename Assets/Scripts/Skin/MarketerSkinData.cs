using UnityEngine;

public class MarketerSkinData : StaffSkinData
{
    [SerializeField] private Sprite _animationSprite;
    public Sprite AnimationSprite => _animationSprite;

    [SerializeField] private Sprite _leftHandSprite;
    public Sprite LeftHandSprite => _leftHandSprite;

    [SerializeField] private Sprite _rightHandSprite;
    public Sprite RightHandSprite => _rightHandSprite;

    [SerializeField] private Sprite[] _particleSprites;
    public Sprite[] ParticleSprites => _particleSprites;


    public MarketerSkinData(Sprite sprite, Sprite thumbnail, Sprite[] idleSprites, string id, string name, string description, int addScore, int addTipPerMinute, Rank rank, SalesLocationType salesLocationType, int buyPrice, StaffSkinUpgradeType upgradeType, float upgradeValue, string equipId, Sprite actionSprite, Sprite leftHand, Sprite rightHand, Sprite[] particleSprites) : base(sprite, thumbnail, idleSprites, id, name, description, addScore, addTipPerMinute, rank, salesLocationType, buyPrice, upgradeType, upgradeValue, equipId)
    {
        _animationSprite = actionSprite;
        _leftHandSprite = leftHand;
        _rightHandSprite = rightHand;
        _particleSprites = particleSprites;
    }
}
