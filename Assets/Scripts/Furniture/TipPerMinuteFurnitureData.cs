using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "TipPerMinuteFurnitureData", menuName = "Scriptable Object/FurnitureData/TipPerMinuteFurnitureData")]
public class TipPerMinuteFurnitureData : FurnitureData
{

    [Space]
    [Header("TipPerMinuteData")]
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
