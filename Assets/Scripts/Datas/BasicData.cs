using UnityEngine;

public class BasicData : ScriptableObject
{
    [Header("DefaultData")]
    [SerializeField] protected Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] protected Sprite _thumbnailSprite;
    public Sprite ThumbnailSprite => _thumbnailSprite;

    [SerializeField] protected string _name;
    public string Name => _name;

    [SerializeField] protected string _id;
    public string Id => _id;

    [TextArea][SerializeField] protected string _description;
    public string Description => _description;

    [SerializeField] protected Rank _rank;
    public Rank Rank => _rank;

    /// <summary>
    /// 아이템 레벨 (히든 레시피 등의 강화 레벨)
    /// </summary>
    protected int _level = 1;
    public int Level => _level;

    /// <summary>
    /// 등급별 환급 보상 계산
    /// 노멀: 코인, 레어: 코인, 유니크: 다이아 1, 스페셜: 다이아 2
    /// </summary>
    public virtual (MoneyType moneyType, int amount) GetRefundReward()
    {
        switch (_rank)
        {
            case Rank.Normal1:
            case Rank.Normal2:
                return (MoneyType.Gold, GetNormalRefundCoin());
            case Rank.Rare:
                return (MoneyType.Gold, GetRareRefundCoin());
            case Rank.Unique:
                return (MoneyType.Dia, 1);
            case Rank.Special:
                return (MoneyType.Dia, 2);
            default:
                return (MoneyType.Gold, 0);
        }
    }

    /// <summary>
    /// 노멀 등급 환급 코인 (기본 값, 오버라이드 가능)
    /// </summary>
    protected virtual int GetNormalRefundCoin()
    {
        return 100; // 기본 값
    }

    /// <summary>
    /// 레어 등급 환급 코인 (기본 값, 오버라이드 가능)
    /// </summary>
    protected virtual int GetRareRefundCoin()
    {
        return 500; // 기본 값
    }

    /// <summary>
    /// 강화 레벨에 따른 환급 가치 증가 (히든 레시피용)
    /// </summary>
    public virtual (MoneyType moneyType, int amount) GetRefundRewardWithLevel(int level)
    {
        var baseReward = GetRefundReward();
        
        // 레벨이 높을수록 환급 가치 증가
        int levelMultiplier = Mathf.Max(1, level);
        int enhancedAmount = baseReward.amount * levelMultiplier;
        
        return (baseReward.moneyType, enhancedAmount);
    }
}
