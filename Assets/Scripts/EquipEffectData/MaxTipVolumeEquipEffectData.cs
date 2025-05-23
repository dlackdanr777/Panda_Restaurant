using UnityEngine;


[CreateAssetMenu(fileName = "MaxTipVolumeEquipEffectData", menuName = "Scriptable Object/EquipEffectData/MaxTipVolumeEquipEffectData")]
public class MaxTipVolumeEquipEffectData : EquipEffectData
{

    [Space]
    [Header("MaxTipVolumeData")]
    [SerializeField] private int _maxTipVolume;
    public override int EffectValue => _maxTipVolume;

    public override void AddSlot()
    {
    }

    public override void RemoveSlot()
    {
    }
}
