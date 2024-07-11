using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    public static string ConvertToNumber(float value)
    {
        string text = 1000 <= value ? (value / 1000).ToString("F1") + 'K' : ((int)value).ToString();
        return text;
    }

    public static string FurnitureTypeStringConverter(FurnitureType type)
    {
        return type switch
        {
            FurnitureType.Table1 => "테이블1",
            FurnitureType.Table2 => "테이블2",
            FurnitureType.Table3 => "테이블3",
            FurnitureType.Table4 => "테이블4",
            FurnitureType.Table5 => "테이블5",
            FurnitureType.Counter => "카운터",
            FurnitureType.Rack => "선반",
            FurnitureType.Frame => "액자",
            FurnitureType.Flower => "화분",
            FurnitureType.Acc => "조명",
            FurnitureType.Wallpaper => "벽지",
            _ => string.Empty
        };
    }

    public static string KitchenUtensilTypeStringConverter(KitchenUtensilType type)
    {
        return type switch
        {
            KitchenUtensilType.Burner1 => "조리기1",
            KitchenUtensilType.Burner2 => "조리기2",
            KitchenUtensilType.Burner3 => "조리기3",
            KitchenUtensilType.Burner4 => "조리기4",
            KitchenUtensilType.Burner5 => "조리기5",
            KitchenUtensilType.Refrigerator => "냉장고",
            KitchenUtensilType.Cabinet => "장식장",
            KitchenUtensilType.Window => "창문",
            KitchenUtensilType.Shelf => "선반",
            KitchenUtensilType.Sink => "싱크대",
            KitchenUtensilType .CookingTools => "조리도구",
            _ => string.Empty
        };
    }
}
