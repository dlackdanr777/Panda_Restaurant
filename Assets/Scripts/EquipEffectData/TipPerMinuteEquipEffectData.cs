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
        GameManager.Instance.AddTipPerMinute(_tipPerMinute);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.AddTipPerMinute(-_tipPerMinute);
    }
}
