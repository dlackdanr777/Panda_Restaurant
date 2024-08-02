using UnityEngine;

[CreateAssetMenu(fileName = "TipPerMinuteSetData", menuName = "Scriptable Object/SetData/TipPerMinuteSetData")]
public class TipPerMinuteSetData : SetData
{

    [Range(0, 10000)] [SerializeField] public int _tipPerMinuteValue;

    public override void Activate()
    {
        GameManager.Instance.AddTipPerMinute(_tipPerMinuteValue);
    }

    public override void Deactivate()
    {
        GameManager.Instance.AddTipPerMinute(-_tipPerMinuteValue);
    }
}
