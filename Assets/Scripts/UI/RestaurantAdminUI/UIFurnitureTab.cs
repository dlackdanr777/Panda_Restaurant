using UnityEngine;

public class UIFurnitureTab : MonoBehaviour
{
    [SerializeField] private UIStaff _uiStaff;

    [Header("Slots")]
    [SerializeField] private UIFurnitureTabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UIFurnitureTabSlot[] _slots;

    public void Init()
    {
        _slots = new UIFurnitureTabSlot[(int)FurnitureType.Length];
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            int index = i;
            UIFurnitureTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(FurnitureType.Table1 + index));
            slot.SetData(FurnitureType.Table1 + index);
        }

        UserInfo.OnChangeFurnitureHandler += SlotUpdate;
    }


    public void SlotUpdate(FurnitureType type)
    {
        _slots[(int)type].SetData(type);
    }


    private void OnSlotClicked(FurnitureType type)
    {
        /*  switch (type)
          {
              case StaffType.Manager:
                  _uiStaff.ShowUIStaffManager();
                  break;

              case StaffType.Waiter:
                  _uiStaff.ShowUIStaffWaiter();
                  break;

              case StaffType.Chef:
                  _uiStaff.ShowUIStaffChef();
                  break;

              case StaffType.Cleaner:
                  _uiStaff.ShowUIStaffCleaner();
                  break;

              case StaffType.Marketer:
                  _uiStaff.ShowUIStaffMarketer();
                  break;

              case StaffType.Guard:
                  _uiStaff.ShowUIStaffGuard();
                  break;

              case StaffType.Server:
                  _uiStaff.ShowUIStaffServer();
                  break;
          }
      }*/

    }
}
