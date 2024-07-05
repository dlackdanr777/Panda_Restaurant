using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MoneyPerMinuteFurnitureData", menuName = "Scriptable Object/FurnitureData/MoneyPerMinuteFurnitureData")]
public class MoneyPerMinuteFurnitureData : FurnitureData
{
    [Space]
    [Header("MoneyPerMinuteData")]
    [SerializeField] private int _moneyPerMinute;
    public override int EffectValue => _moneyPerMinute;

    public override void AddSlot()
    {
        GameManager.Instance.AddMoneyPerMinute(_moneyPerMinute);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.AddMoneyPerMinute(-_moneyPerMinute);
    }
}
