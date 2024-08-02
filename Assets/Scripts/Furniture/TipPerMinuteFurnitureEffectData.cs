using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TipPerMinuteFurnitureEffectData", menuName = "Scriptable Object/FurnitureEffectData/TipPerMinuteFurnitureEffectData")]
public class TipPerMinuteFurnitureEffectData : FurnitureEffectData
{
    [Space]
    [Header("MoneyPerMinuteData")]
    [SerializeField] private int _tipPerMinute;
    public override int EffectValue => _tipPerMinute;

    public override void AddSlot()
    {
        GameManager.Instance.AddTipPerMinute(_tipPerMinute);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.AddTipPerMinute(-_tipPerMinute);
    }
}
