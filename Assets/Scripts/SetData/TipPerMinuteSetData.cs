using UnityEngine;

[CreateAssetMenu(fileName = "TipPerMinuteSetData", menuName = "Scriptable Object/SetData/TipPerMinuteSetData")]
public class TipPerMinuteSetData : SetData
{

    [Range(0, 10000)] [SerializeField] private int _tipPerMinuteValue;
    public override float Value => _tipPerMinuteValue;

    public override void Activate()
    {
    }

    public override void Deactivate()
    {
    }
}
