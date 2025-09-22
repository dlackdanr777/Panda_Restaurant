using System.Collections.Generic;
using UnityEngine;

public class UIStatus : MonoBehaviour
{
    [SerializeField] private UIStatusPreview _statusPreview;


    [Space]
    [Header("Slots")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIStatusSlot _slotPrefab;

    private List<UIStatusSlot> _slots = new List<UIStatusSlot>();


    public void Init()
    {
        _statusPreview.Init();
        for (int i = 0; i < (int)UpgradeType.Length; ++i)
        {
            UpgradeType upgradeType = (UpgradeType)i;
            UIStatusSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(OnButtonClicked);
            slot.SetData(upgradeType);

            _slots.Add(slot);
        }
    }


    public void UpdateUI()
    {
        _statusPreview.UpdateUI();
        for (int i = 0, cnt = _slots.Count; i < cnt; ++i)
        {
            _slots[i].UpdateUI();
        }
    }

    public void Show()
    {
        _statusPreview.SetData(UpgradeType.UPGRADE01); // Default to the first upgrade type
    }


    private void OnButtonClicked(UpgradeType upgradeType)
    {
        _statusPreview.SetData(upgradeType);
    }
}
