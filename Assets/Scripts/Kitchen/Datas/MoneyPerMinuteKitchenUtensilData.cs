using UnityEngine;

[CreateAssetMenu(fileName = "MoneyPerMinuteKitchenUtensilData", menuName = "Scriptable Object/KitchenUtensilData/MoneyPerMinuteKitchenUtensilData")]
public class MoneyPerMinuteKitchenUtensilData : KitchenUtensilData
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
