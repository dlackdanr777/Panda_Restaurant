using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class Utility
{
    public static string ConvertToNumber(float value)
    {
        string text = 1000 <= value ? (value / 1000).ToString("#,##0.0") + 'K' : ((int)value).ToString();
        return text;
    }

    /// <summary>총문자열 갯수와 문자열을 받아 문자열 앞쪽에 -를 넣어주는 함수</summary>
    public static string StringAddHyphen(string str, int strLength)
    {
        if (strLength <= str.Length)
            return str;

        StringBuilder strBuilder = new StringBuilder();
        int cnt = strLength - str.Length;

        for(int i = 0; i < cnt; ++i)
            strBuilder.Append("-");

        strBuilder.Append(str);
        return strBuilder.ToString();
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
            KitchenUtensilType.Fridge => "냉장고",
            KitchenUtensilType.Cabinet => "장식장",
            KitchenUtensilType.Window => "창문",
            KitchenUtensilType.Plate => "그릇 선반",
            KitchenUtensilType.Sink => "싱크대",
            KitchenUtensilType.CookingTools => "도구 선반",
            KitchenUtensilType.Kitchenrack => "주방 선반",
            _ => string.Empty
        };
    }


    public static string StaffTypeStringConverter(StaffType type)
    {
        return type switch
        {
            StaffType.Manager => "매니저",
            StaffType.Marketer => "치어리더",
            StaffType.Waiter => "웨이터",
            StaffType.Server => "서버",
            StaffType.Cleaner => "청소부",
            StaffType.Guard => "경호원",
            StaffType.Chef => "셰프",
            _ => string.Empty
        };
    }
}
