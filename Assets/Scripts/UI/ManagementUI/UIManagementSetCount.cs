using TMPro;
using UnityEngine;

public class UIManagementEquipSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private UIFoodType _foodType;


    public void SetData(string text, FoodType foodType)
    {
        _nameText.SetText(text);
        _foodType.SetFoodType(foodType);
    }

}
