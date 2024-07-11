using UnityEngine;

[CreateAssetMenu(fileName = "MoneyPerMinuteSetData", menuName = "Scriptable Object/SetData/MoneyPerMinuteSetData")]
public class MoneyPerMinuteSetData : SetData
{

    [Range(0, 10000)] [SerializeField] public int _moneyPerMinuteValue;

    public override void Activate()
    {
        GameManager.Instance.AddMoneyPerMinute(_moneyPerMinuteValue);
    }

    public override void Deactivate()
    {
        GameManager.Instance.AddMoneyPerMinute(-_moneyPerMinuteValue);
    }
}
