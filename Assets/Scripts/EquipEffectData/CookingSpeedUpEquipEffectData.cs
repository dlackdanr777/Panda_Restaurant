using UnityEngine;


[CreateAssetMenu(fileName = "CookingSpeedUpEquipEffectData", menuName = "Scriptable Object/EquipEffectData/CookingSpeedUpEquipEffectData")]
public class CookingSpeedUpEquipEffectData : EquipEffectData
{

    [Space]
    [Header("MaxTipVolumeData")]
    [SerializeField] private int _cookSpeedUpVolume;
    public override int EffectValue => _cookSpeedUpVolume;

    public override void AddSlot()
    {
    }

    public override void RemoveSlot()
    {
    }
}
