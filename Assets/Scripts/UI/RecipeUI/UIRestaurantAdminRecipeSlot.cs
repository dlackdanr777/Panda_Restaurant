using System;
using UnityEngine;

public class UIRestaurantAdminRecipeSlot : UIRestaurantAdminSlot
{
    [Space]
    [Header("Recipe")]
    [SerializeField] private UIFoodType _uiFoodType;

    public void SetFoodType(FoodType foodType)
    {
        _uiFoodType.SetFoodType(foodType);
    }
}
