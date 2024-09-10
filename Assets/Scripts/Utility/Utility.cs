using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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

    public static string GachaItemRankStringConverter(GachaItemRank rank)
    {
        return rank switch
        {
            GachaItemRank.Normal1 => "노말",
            GachaItemRank.Normal2 => "노말",
            GachaItemRank.Rare => "레어",
            GachaItemRank.Unique => "유니크",
            GachaItemRank.Special => "스페셜",
            _ => string.Empty
        };
    }


    public static float GetGachaItemRankRange(GachaItemRank rank)
    {
        switch (rank)
        {
            case GachaItemRank.Normal1:
                return 0.375f;
            case GachaItemRank.Normal2:
                return 0.375f;
            case GachaItemRank.Rare:
                return 0.125f;
            case GachaItemRank.Unique:
                return 0.0938f;
            case GachaItemRank.Special:
                return 0.0313f;
        }
        return 0;
    }


    /// <summary>Image의 Sprite 피벗값에 맞춰 Image 컴포넌트 피벗값을 이동하는 함수</summary>
    public static void ChangeImagePivot(Image image)
    {
        // 스프라이트의 현재 피벗과 크기를 구함
        Vector2 spritePivot = image.sprite.pivot;
        Vector2 spriteSize = image.sprite.rect.size;
        Vector2 normalizedPivot = new Vector2(spritePivot.x / spriteSize.x, spritePivot.y / spriteSize.y);

        // 현재 RectTransform의 크기 저장
        RectTransform rectTransform = image.rectTransform;
        Vector2 originalSize = rectTransform.rect.size;

        // 피벗을 새로 설정하기 전의 anchoredPosition 계산
        Vector2 oldPivotOffset = new Vector2(rectTransform.pivot.x * originalSize.x, rectTransform.pivot.y * originalSize.y);
        Vector2 newPivotOffset = new Vector2(normalizedPivot.x * originalSize.x, normalizedPivot.y * originalSize.y);
        Vector2 pivotDelta = newPivotOffset - oldPivotOffset;

        // 새로운 피벗 설정
        rectTransform.pivot = normalizedPivot;

        // Stretch 모드인지 확인 (anchorMin과 anchorMax가 같지 않으면 Stretch 모드임)
        bool isStretch = rectTransform.anchorMin != rectTransform.anchorMax;

        // Stretch 모드일 때만 anchoredPosition을 조정하여 피벗 변경에 따른 위치 변화를 상쇄함
        if (isStretch)
            rectTransform.anchoredPosition -= pivotDelta;
    }


    public static Color GetColor(ColorType type)
    {
        return type switch
        {
            ColorType.None => Color.white,
            ColorType.NoGive => new Color(0.2f, 0.2f, 0.2f),
            ColorType.Give => Color.white,
            ColorType.Negative => new Color(0.83f, 0.28f, 0.25f),
            ColorType.Positive => new Color(0.24f, 0.57f, 1),
            _=> Color.black
        };
    }

    public static string ColorToHex(Color color)
    {
        int r = Mathf.Clamp(Mathf.FloorToInt(color.r * 255), 0, 255);
        int g = Mathf.Clamp(Mathf.FloorToInt(color.g * 255), 0, 255);
        int b = Mathf.Clamp(Mathf.FloorToInt(color.b * 255), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
