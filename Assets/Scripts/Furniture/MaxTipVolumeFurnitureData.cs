using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "MaxTipVolumeFurnitureData", menuName = "Scriptable Object/FurnitureData/MaxTipVolumeFurnitureData")]
public class MaxTipVolumeFurnitureData : FurnitureData
{

    [Space]
    [Header("MaxTipVolumeData")]
    [SerializeField] private int _maxTipVolume;
    public int MaxTipVolume => _maxTipVolume;

    public override void AddSlot()
    {
        GameManager.Instance.SetMaxTipVolume(MaxTipVolume);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.SetMaxTipVolume(-MaxTipVolume);
    }
}
