using UnityEngine;


[CreateAssetMenu(fileName = "MaxTipVolumeFurnitureEffectData", menuName = "Scriptable Object/FurnitureEffectData/MaxTipVolumeFurnitureEffectData")]
public class MaxTipVolumeFurnitureEffectData : FurnitureEffectData
{

    [Space]
    [Header("MaxTipVolumeData")]
    [SerializeField] private int _maxTipVolume;
    public override int EffectValue => _maxTipVolume;

    public override void AddSlot()
    {
        GameManager.Instance.SetMaxTipVolume(_maxTipVolume);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.SetMaxTipVolume(-_maxTipVolume);
    }
}
