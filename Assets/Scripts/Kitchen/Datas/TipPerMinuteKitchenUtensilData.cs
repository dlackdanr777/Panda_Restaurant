using UnityEngine;

[CreateAssetMenu(fileName = "TipPerMinuteKitchenUtensilData", menuName = "Scriptable Object/KitchenUtensilData/TipPerMinuteKitchenUtensilData")]
public class TipPerMinuteKitchenUtensilData : KitchenUtensilData
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
