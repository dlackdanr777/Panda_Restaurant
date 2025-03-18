using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MarketerData", menuName = "Scriptable Object/Staff/Marketer")]
public class MarketerData : StaffData
{
    [SerializeField] private MarketerLevelData[] _marketerLevelData;

    [Header("Animation Option")]
    [SerializeField] private Sprite _uiSprite;
    public Sprite UISprite => _uiSprite;

    [SerializeField] private Sprite _animationSprite;
    public Sprite AnimationSprite => _animationSprite;

    [SerializeField] private Sprite _leftHandSprite;
    public Sprite LeftHandSprite => _leftHandSprite;

    [SerializeField] private Sprite _rightHandSprite;
    public Sprite RightHandSprite => _rightHandSprite;

    [Space]
    [Header("Particle Option")]

    [SerializeField] private int _particleCount;
    public int ParticleCount => _particleCount;

    [SerializeField] private Sprite[] _particleSprites;
    public Sprite[] ParticleSprites => _particleSprites;



    public override float SecondValue => 0;
    public override int MaxLevel => _marketerLevelData.Length;


    public override float GetSpeed(int level) => _speed;

    public override float GetActionValue(int level)
    {
        level -= 1;
        if (_marketerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _marketerLevelData[level].MarketingTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new MarketerAction(staff, customerController);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _marketerLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level -= 1;
        if (_marketerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");
        return _marketerLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level -= 1;
        if (_marketerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");
        return _marketerLevelData[level].UpgradeMoneyData;
    }
}


[Serializable]
public class MarketerLevelData : StaffLevelData
{
    [SerializeField] private float _marketingTime;
    public float MarketingTime => _marketingTime;
}
