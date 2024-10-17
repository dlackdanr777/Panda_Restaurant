using System.Text;
using UnityEngine;
using UnityEngine.UI;

public static class Utility
{
    public static string ConvertToMoney(float value)
    {
        //string text = 1000 <= value ? (value / 1000).ToString("#,##0.0") + 'K' : ((int)value).ToString();
        string text = value.ToString("N0");
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

    public static string SetStringColor(string str, ColorType colorType)
    {
        return "<color=" + ColorToHex(GetColor(colorType)) + ">" + str + "</color>";
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

    public static string GetEquipEffectDescription(EquipEffectData effectData)
    {
        string description = string.Empty;

        switch (effectData)
        {
            case TipPerMinuteEquipEffectData:
                description = "분당 획득 팁 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (effectData.EffectValue.ToString()) + "</color> 증가";
                break;

            case MaxTipVolumeEquipEffectData:
                description = "팁 저장량 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + effectData.EffectValue + "</color> 증가";
                break;

            case CookingSpeedUpEquipEffectData:
                description = "요리 효율 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + effectData.EffectValue + "%</color> 증가";
                break;

            default:
                description = "알 수 없는 효과";
                break;
        }

        return description;
    }


    public static string GetSetEffectDescription(SetData setData)
    {
        string description = string.Empty;

        switch (setData)
        {
            case TipPerMinuteSetData:
                description = "분당 획득 팁 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (setData.Value) + "</color> 증가";
                break;

            case CookingSpeedUpSetData:
                description = "요리 효율 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (setData.Value) + "%</color> 증가";
                break;

            default:
                description = "알 수 없는 효과";
                break;
        }

        return description;
    }


    public static string GetStaffEffectDescription(StaffData data)
    {
        string description = string.Empty;
        int level = UserInfo.IsGiveStaff(data) ? UserInfo.GetStaffLevel(data) : 1;

        switch(data)
        {
            case ManagerData:
                description = $"자동 손님 테이블로 안내하기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 1명)";
                break;

            case WaiterData:
                description = $"자동 음식 서빙하기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 1개)";
                break;

            case MarketerData:
                description = $"자동 손님 호출하기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.SecondValue}%</color> 확률)";
                break;

            case ServerData:
                description = $"자동 음식 주문 받기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 1테이블)";
                break;

            case CleanerData:
                description = $"자동 쓰레기 청소 하기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.SecondValue}</color>초)";
                break;

            case GuardData:
                description = $"도둑 퇴치(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>초 소요)";
                break;

            case ChefData:
                description = $"요리 속도 상승(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
                break;
        }

        return description;
    }


    public static string GetCustomerEffectDescription(CustomerData data)
    {
        string description = string.Empty;
        if (data is SpecialCustomerData)
        {
            SpecialCustomerData specialCustomerData = (SpecialCustomerData)data;
            description = $"{specialCustomerData.ActiveDuration}초 동안 돌아다니며 \n터치시 <color={ColorToHex(GetColor(ColorType.Positive))}>{specialCustomerData.TouchAddMoney}</color>골드를 획득한다";
        }

        else if(data is GatecrasherCustomerData)
        {
            GatecrasherCustomerData gatecrasherCustomerData = (GatecrasherCustomerData)data;
            description = data is GatecrasherCustomer1Data ? $"{gatecrasherCustomerData.ActiveDuration}초 동안 돌아다니며 동전을 훔친다" : $"{gatecrasherCustomerData.ActiveDuration}초 동안 고성방가로 손님들을 쫒아낸다";
        }

        else
        {
            if (data.Skill == null)
                description = "없음";

            else
            {
                if(data.Skill is CustomerFoodPriceUpSkill)
                    description = $"{data.Skill.SkillActivatePercent}% 확률로 음식 가격의 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>배 지불한다";
                else if(data.Skill is CustomerOrderCountUpSkill)
                    description = $"{data.Skill.SkillActivatePercent}% 확률로 음식을 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>개 주문한다";
            }      
        }

        return description;
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
            ColorType.Positive => new Color(0.16f, 0.41f, 0.72f),
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
