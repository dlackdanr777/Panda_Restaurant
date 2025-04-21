using System.Text;
using UnityEngine;
using UnityEngine.UI;

public static class Utility
{
    private static readonly string BillionFormat = "#,##0.0B";
    private static readonly string MillionFormat = "#,##0.0A";
    private static readonly string NumberFormat = "N0";

    public static string ConvertToMoney(long value)
    {
        if (value >= 1_000_000_000) // 10억 이상
        {
            return (value / 1_000_000_000f).ToString(BillionFormat);
        }
        else if (value >= 1_000_000) // 100만 이상
        {
            return (value / 1_000_000f).ToString(MillionFormat);
        }
        else // 1천 미만
        {
            return value.ToString(NumberFormat);
        }
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
            KitchenUtensilType.Sink => "싱크대",
            KitchenUtensilType.CookingTools => "도구 선반",
            KitchenUtensilType.Kitchenrack => "주방 선반",
            _ => string.Empty
        };
    }

    public static bool EqualStaffType(this EquipStaffType type1, EquipStaffType type2)
    {
        if(type1 == type2)
            return true;

        if ((type1 == EquipStaffType.Waiter1 || type1 == EquipStaffType.Waiter2) && (type2 == EquipStaffType.Waiter1 || type2 == EquipStaffType.Waiter2))
            return true;

        if ((type1 == EquipStaffType.Chef1 || type1 == EquipStaffType.Chef2) && (type2 == EquipStaffType.Chef1 || type2 == EquipStaffType.Chef2))
            return true;


        return false;
    }

    public static string StaffTypeStringConverter(EquipStaffType type)
    {
        return type switch
        {
            EquipStaffType.Manager => "매니저",
            EquipStaffType.Marketer => "치어리더",
            EquipStaffType.Waiter1 => "웨이터1",
            EquipStaffType.Waiter2 => "웨이터2",
            EquipStaffType.Cleaner => "청소부",
            EquipStaffType.Guard => "경호원",
            EquipStaffType.Chef1 => "주방장",
            EquipStaffType.Chef2 => "부주방장",
            _ => string.Empty
        };
    }

    public static string FoodTypeStringConverter(FoodType type)
    {
        return type switch
        {
            FoodType.None => "없음",
            FoodType.Natural => "내추럴",
            FoodType.Traditional => "전통적",
            FoodType.Vintage => "빈티지",
            FoodType.Luxury => "럭셔리",
            FoodType.Modern => "모던",
            FoodType.Cozy => "코지",
            FoodType.Tropical => "트로피컬",
            _ => throw new System.Exception("해당 타입이 이상합니다: " + type)
        };
    }

    public static FoodType GetFoodType(string foodTypeStr)
    {
        return foodTypeStr switch
        {
            "내추럴" => FoodType.Natural,
            "전통적" => FoodType.Traditional,
            "빈티지" => FoodType.Vintage,
            "럭셔리" => FoodType.Luxury,
            "모던" => FoodType.Modern,
            "코지" => FoodType.Cozy,
            "트로피컬" => FoodType.Tropical,
            _ => throw new System.Exception("해당 음식 문자열이 이상합니다: " + foodTypeStr)
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

    public static ERestaurantFloorType GetFloorTypeByStr(string str)
    {
        return str switch
        {
            "FLOOR01" => ERestaurantFloorType.Floor1,
            "FLOOR02" => ERestaurantFloorType.Floor2,
            "FLOOR03" => ERestaurantFloorType.Floor3,
            _ => throw new System.Exception("해당 층 수 문자열이 이상합니다: " + str)
        };
    }

    public static string GetFloorStrByType(ERestaurantFloorType type)
    {
        return type switch
        {
            ERestaurantFloorType.Floor1 => "1층",
            ERestaurantFloorType.Floor2 => "2층",
            ERestaurantFloorType.Floor3 => "3층",
            _ => throw new System.Exception("해당 층 수 타입이 이상합니다: " + type)
        };
    }

    public static string GetGachaItemEffectDescription(GachaItemData data)
    {
        if (data == null)
            throw new System.Exception("전달 받은 가챠 아이템 데이터가 null 입니다.");

        int level = UserInfo.GetGachaItemLevel(data);
        level = level <= 0 ? 1 : level;
        float upgradeValue = UserInfo.IsGachaItemMaxLevel(data) ? 0 : data.UpgradeValue;
        string effectDescription = data.UpgradeType switch
        {
            UpgradeType.UPGRADE01 => "매장 평점\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) 증가",
            UpgradeType.UPGRADE02 => "전체 손님 이동 속도\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) 증가",
            UpgradeType.UPGRADE03 => "분당 팁\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) 증가",
            UpgradeType.UPGRADE04 => "최대 팁 보유량\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) 증가",
            UpgradeType.UPGRADE05 => "판매 메뉴 가격\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) 증가",
            UpgradeType.UPGRADE06 => "메뉴 조리 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 단축",
            UpgradeType.UPGRADE07 => "메뉴 가격 두 배 확률\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) 증가",
            UpgradeType.UPGRADE08 => "주문 메뉴 당 팁\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) 증가",
            UpgradeType.UPGRADE09 => StaffTypeStringConverter(EquipStaffType.Waiter1) + " 스킬 쿨타임\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE10 => StaffTypeStringConverter(EquipStaffType.Waiter1) + " 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE11 => StaffTypeStringConverter(EquipStaffType.Waiter2) + " 스킬 쿨타임\n"+ (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE12 => StaffTypeStringConverter(EquipStaffType.Waiter2) + " 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE13 => StaffTypeStringConverter(EquipStaffType.Marketer) + " 스킬 쿨타임\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE14 => StaffTypeStringConverter(EquipStaffType.Marketer) + " 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE15 => StaffTypeStringConverter(EquipStaffType.Cleaner) + " 스킬 쿨타임\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE16 => StaffTypeStringConverter(EquipStaffType.Cleaner) + " 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE17 => StaffTypeStringConverter(EquipStaffType.Guard) + " 스킬 쿨타임\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE18 => StaffTypeStringConverter(EquipStaffType.Guard) + " 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE19 => "진상 손님에게 가하는 피해\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) 증가",
            UpgradeType.UPGRADE20 => "스페셜 손님 터치시 코인 획득량\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) 증가",
            UpgradeType.UPGRADE21 => "진상 손님 터치시 속도 감소 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE22 => "전체 직원 스킬 쿨타임\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 감소",
            UpgradeType.UPGRADE23 => "전체 직원 스킬 지속 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE24 => "미니게임 제한 시간\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "초(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "초</color>) 증가",
            UpgradeType.UPGRADE25 => "스페셜 손님 등장 확률\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) 증가",
            UpgradeType.UPGRADE26 => "최대 대기 손님\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "명(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "명</color>) 증가",
            _ => string.Empty
        };
        return effectDescription;
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


    public static string GetFurnitureFoodTypeSetEffectDescription( FoodType type)
    {

        string description = FoodTypeStringConverter(type) + $" 속성 음식 수익 증가(<color={ColorToHex(GetColor(ColorType.Positive))}>+10%</color>)";;
        return description;
    }

    public static string GetKitchenFoodTypeSetEffectDescription(FoodType type)
    {
        string description = FoodTypeStringConverter(type) + $" 속성 음식 조리 속도 증가(<color={ColorToHex(GetColor(ColorType.Positive))}>+10%</color>)";
        return description;
    }


    public static string GetStaffEffectDescription(StaffData data)
    {
        string description = string.Empty;
        int level = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;

        switch (data)
        {
            case ManagerData:
                description = $"테이블로 손님 자동 배치(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초)";
                break;

            case WaiterData:
                description = $"음식을 손님에게 자동 배달(기본 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case MarketerData:
                description = $"레스토랑으로 손님 자동 호출(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초)";
                break;

            case ServerData:
                description = $"자동 음식 주문 받기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 1테이블)";
                break;

            case CleanerData:
                description = $"쓰레기 & 코인 수집(기본 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case GuardData:
                description = $"진상 손님 퇴치(퇴치 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>)";
                break;

            case ChefData:
                description = $"주방 음식 제작 효율 상승(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
                break;
        }

        return description;
    }

    public static string GetStaffEffectDescription(StaffData data, int level)
    {
        string description = string.Empty;
        switch (data)
        {
            case ManagerData:
                description = $"테이블로 손님 자동 배치(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초)";
                break;

            case WaiterData:
                description = $"음식을 손님에게 자동 배달(기본 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case MarketerData:
                description = $"레스토랑으로 손님 자동 호출(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초)";
                break;

            case ServerData:
                description = $"자동 음식 주문 받기(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> 초당 1테이블)";
                break;

            case CleanerData:
                description = $"쓰레기 & 코인 수집(기본 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case GuardData:
                description = $"진상 손님 퇴치(퇴치 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>)";
                break;

            case ChefData:
                description = $"주방 음식 제작 효율 상승(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
                break;
        }

        return description;
    }


    public static string GetStaffSkillDescription(StaffData data)
    {
        int level = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;
        return data.Skill switch
        {
            SpeedUpSkill => $"기본 능력 속도 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}%</color> UP! (<color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.Duration}</color>초)" ,
            FoodPriceUpSkill => $"주문 당 음식 가격 <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}%</color> 증가",
            AutoCustomerGuideSkill => $"손님 자동 안내 ({data.Skill.Duration}s)",
            AddPromotionCustomerSkill => $"홍보당 손님 호출 인원 <color={ColorToHex(GetColor(ColorType.Positive))}>{(int)data.Skill.FirstValue}</color>명 증가",
            TouchAddCustomerButtonSkill => $"레스토랑 손님 자동 호출 (<color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>초)",
            _ => string.Empty
        };

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

        else if(data is NormalCustomerData)
        {
            NormalCustomerData normalCustomerData = (NormalCustomerData)data;

            if (normalCustomerData.Skill == null)
                description = "없음";

            else
            {
                if(normalCustomerData.Skill is CustomerFoodPriceUpSkill)
                    description = $"{normalCustomerData.Skill.SkillActivatePercent}%확률로 음식 가격의 <color={ColorToHex(GetColor(ColorType.Positive))}>{normalCustomerData.Skill.FirstValue}</color>배 지불한다";
                else if(normalCustomerData.Skill is CustomerOrderCountUpSkill)
                    description = $"{normalCustomerData.Skill.SkillActivatePercent}%확률로 음식을 <color={ColorToHex(GetColor(ColorType.Positive))}>{normalCustomerData.Skill.FirstValue}</color>개 주문한다";
            }      
        }

        return description;
    }


    public static string GetTendencyTypeToStr(CustomerTendencyType type)
    {
        return type switch
        {
            CustomerTendencyType.Normal => "무난함",
            CustomerTendencyType.Sensitive => "예민함",
            CustomerTendencyType.HighlySensitive => "초예민",
            _ => string.Empty
        };
    }

    public static string GetEquipEffectDescription(EquipEffectType type, int value)
    {
        return type switch
        {
            EquipEffectType.AddScore => $"평점 <color={ColorToHex(GetColor(ColorType.Positive))}>{value}</color> 점 증가",
            EquipEffectType.AddTipPerMinute => $"분당 획득 팁 <color={ColorToHex(GetColor(ColorType.Positive))}>{value}</color> 증가",
            EquipEffectType.AddCookSpeed => "요리 효율 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + value + "%</color> 증가",
            EquipEffectType.AddMaxTip => "팁 저장량 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + value + "</color> 증가",
            _ => string.Empty
        };
    }


    public static UnlockConditionType GetUnlockConditionType(string str)
    {
        switch (str)
        {
            case "TYPE01":
                return UnlockConditionType.UnlockTargetFloor;

            case "TYPE02":
                return UnlockConditionType.NeedItem;

            case "TYPE03":
                return UnlockConditionType.NeedRecipe;

            case "TYPE04":
                return UnlockConditionType.NeedCustomer;

            case "TYPE05":
                return UnlockConditionType.NeedStaff;

            case "TYPE06":
                return UnlockConditionType.NeedFurniture;

            case "TYPE07":
                return UnlockConditionType.NeedKitchenUtensil;

            default:
                return UnlockConditionType.None;
        }
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
            ColorType.AddValue => new Color(0.141f, 0.569f, 0.247f),
            _ => Color.black
        } ;
    }

    public static string ColorToHex(Color color)
    {
        int r = Mathf.Clamp(Mathf.FloorToInt(color.r * 255), 0, 255);
        int g = Mathf.Clamp(Mathf.FloorToInt(color.g * 255), 0, 255);
        int b = Mathf.Clamp(Mathf.FloorToInt(color.b * 255), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string CutStringUpToChar(string str, char delimiter)
    {
        str = str.ToUpper();
        int index = str.IndexOf(delimiter);

        if (index >= 0)
            return str.Substring(0, index);
        else
            return str;
    }

    public static string GetStringAfterChar(string str, char delimiter)
    {
        int index = str.IndexOf(delimiter);
    
        if (index >= 0 && index < str.Length - 1)
            return str.Substring(index + 1);
        else
            return string.Empty;
    }
}
