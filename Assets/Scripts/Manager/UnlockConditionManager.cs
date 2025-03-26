public static class UnlockConditionManager
{

    public static bool GetConditionEnabled(UnlockConditionData data)
    {

        switch (data.UnlockType)
        {
            case UnlockConditionType.None:
                return true;

            case UnlockConditionType.UnlockTargetFloor:
                return GetUnlockTargetFloor(data);

            case UnlockConditionType.NeedItem:
                return GetNeedItem(data);

            case UnlockConditionType.NeedRecipe:
                return GetNeedRecipe(data);

            case UnlockConditionType.NeedFurniture:
                return GetNeedFurniture(data);

            case UnlockConditionType.NeedKitchenUtensil:
                return GetNeedKitchenUtensil(data);

            default:
                DebugLog.LogError("«ÿ¥Á ¡∂∞«¿Ã ¿ÃªÛ«’¥œ¥Ÿ: " + data.UnlockType);
                return false;

        }
    }

    public static string GetConditionStr(UnlockConditionData data)
    {
        switch (data.UnlockType)
        {
            case UnlockConditionType.None:
                return "æ¯¿Ω";

            case UnlockConditionType.UnlockTargetFloor:
                ERestaurantFloorType floor = Utility.GetFloorTypeByStr(data.UnlockId);
                return Utility.GetFloorStrByType(floor) + " «ÿ¡¶";

            case UnlockConditionType.NeedItem:
                GachaItemData itemData = ItemManager.Instance.GetGachaItemData(data.UnlockId);
                return itemData.Name + " »πµÊ";

            case UnlockConditionType.NeedRecipe:
                FoodData foodData = FoodDataManager.Instance.GetFoodData(data.UnlockId);

                return foodData.Name + " »πµÊ";

            case UnlockConditionType.NeedFurniture:
                FurnitureData furnitureData = FurnitureDataManager.Instance.GetFurnitureData(data.UnlockId);  
                return furnitureData.Name + " »πµÊ";

            case UnlockConditionType.NeedKitchenUtensil:
                KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(data.UnlockId);
                return kitchenData.Name + " »πµÊ";

            default:
                throw new System.Exception("«ÿ¥Á ¡∂∞«¿Ã ¿ÃªÛ«’¥œ¥Ÿ: " + data.UnlockType);

        }
    }


    private static bool GetUnlockTargetFloor(UnlockConditionData data)
    {
        ERestaurantFloorType floor = Utility.GetFloorTypeByStr(data.UnlockId);
        return UserInfo.IsFloorValid(UserInfo.CurrentStage, floor);
    }

    private static bool GetNeedItem(UnlockConditionData data)
    {
        GachaItemData itemData = ItemManager.Instance.GetGachaItemData(data.UnlockId);
        return UserInfo.IsGiveGachaItem(itemData);
    }

    private static bool GetNeedRecipe(UnlockConditionData data)
    {
        FoodData foodData = FoodDataManager.Instance.GetFoodData(data.UnlockId);
        return UserInfo.IsGiveRecipe(foodData);
    }

    private static bool GetNeedFurniture(UnlockConditionData data)
    {
        FurnitureData furnitureData = FurnitureDataManager.Instance.GetFurnitureData(data.UnlockId);
        return UserInfo.IsGiveFurniture(UserInfo.CurrentStage, furnitureData);
    }

    private static bool GetNeedKitchenUtensil(UnlockConditionData data)
    {
        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(data.UnlockId);
        return UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, kitchenData);
    }
}
