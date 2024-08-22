using UnityEngine;

public class GachaItemData
{
    public GachaItemRank GachaItemRank => _rank - 1 < 0 ? throw new System.Exception("Rank가 범위 밖에 있습니다.") : (int)GachaItemRank.Length <= _rank - 1 ? throw new System.Exception("Rank가 범위 밖에 있습니다.") : (GachaItemRank)_rank - 1;

    private string _id;
    public string Id => _id;

    private string _name;
    public string Name => _name;

    private string _description;
    public string Description => _description;

    private int _addScore;
    public int AddScore => _addScore;

    private int _minutePerTip;
    public int MinutePerTip => _minutePerTip;

    private int _rank;
    public int Rank => _rank;

    private int _exchangeCount;
    public int ExchangeCount => _exchangeCount;

    private int _duplicatePaymentCount;
    public int DuplicatePaymentCount => _duplicatePaymentCount;

    private Sprite _sprite;
    public Sprite Sprite => _sprite;


    public GachaItemData(string id, string name, string description, int addScore, int minutePerTip, int rank, int exchangeCount, int duplicatePaymentCount, Sprite sprite)
    {
        _id = id;
        _name = name;
        _description = description;
        _addScore = addScore;
        _minutePerTip = minutePerTip;
        _rank = rank;
        _exchangeCount = exchangeCount;
        _duplicatePaymentCount = duplicatePaymentCount;
        _sprite = sprite;
    }
}
