using System;
using UnityEngine;

public class UIRestaurantAdminStaffSlot : UIRestaurantAdminSlot
{
    [Space]
    [Header("Staff")]
    [SerializeField] private UITextAndText _equipGroup;
    [SerializeField] private GameObject _normalFrame;
    [SerializeField] private GameObject _rareFrame;
    [SerializeField] private GameObject _uniqueFrame;
    [SerializeField] private GameObject[] _specialFrames;

    public void SetEquipText(string typeText)
    {
        _equipGroup.SetText1(typeText);
    }

    public void EquipGroupSetActive(bool value)
    {
        _equipGroup.gameObject.SetActive(value);
    }

    public void SetFrame(Rank rank)
    {
        _normalFrame.SetActive(rank == Rank.Normal1 || rank == Rank.Normal2);
        _rareFrame.SetActive(rank == Rank.Rare);
        _uniqueFrame.SetActive(rank == Rank.Unique);

        for (int i = 0; i < _specialFrames.Length; ++i)
            _specialFrames[i].SetActive(rank == Rank.Special);
    }
}
