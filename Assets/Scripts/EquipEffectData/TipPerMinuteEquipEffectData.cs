using UnityEngine;

[CreateAssetMenu(fileName = "TipPerMinuteEquipEffectData", menuName = "Scriptable Object/EquipEffectData/TipPerMinuteEquipEffectData")]
public class TipPerMinuteEquipEffectData : EquipEffectData
{
    [Space]
    [Header("MoneyPerMinuteData")]
    [SerializeField] private int _tipPerMinute;
    public override int EffectValue => _tipPerMinute;

    public override void AddSlot()
    {
    }

    public override void RemoveSlot()
    {
    }
}
