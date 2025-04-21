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
        if (value >= 1_000_000_000) // 10�� �̻�
        {
            return (value / 1_000_000_000f).ToString(BillionFormat);
        }
        else if (value >= 1_000_000) // 100�� �̻�
        {
            return (value / 1_000_000f).ToString(MillionFormat);
        }
        else // 1õ �̸�
        {
            return value.ToString(NumberFormat);
        }
    }



    /// <summary>�ѹ��ڿ� ������ ���ڿ��� �޾� ���ڿ� ���ʿ� -�� �־��ִ� �Լ�</summary>
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
            FurnitureType.Table1 => "���̺�1",
            FurnitureType.Table2 => "���̺�2",
            FurnitureType.Table3 => "���̺�3",
            FurnitureType.Table4 => "���̺�4",
            FurnitureType.Table5 => "���̺�5",
            FurnitureType.Counter => "ī����",
            FurnitureType.Rack => "����",
            FurnitureType.Frame => "����",
            FurnitureType.Flower => "ȭ��",
            FurnitureType.Acc => "����",
            FurnitureType.Wallpaper => "����",
            _ => string.Empty
        };
    }

    public static string KitchenUtensilTypeStringConverter(KitchenUtensilType type)
    {
        return type switch
        {
            KitchenUtensilType.Burner1 => "������1",
            KitchenUtensilType.Burner2 => "������2",
            KitchenUtensilType.Burner3 => "������3",
            KitchenUtensilType.Burner4 => "������4",
            KitchenUtensilType.Burner5 => "������5",
            KitchenUtensilType.Fridge => "�����",
            KitchenUtensilType.Cabinet => "�����",
            KitchenUtensilType.Window => "â��",
            KitchenUtensilType.Sink => "��ũ��",
            KitchenUtensilType.CookingTools => "���� ����",
            KitchenUtensilType.Kitchenrack => "�ֹ� ����",
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
            EquipStaffType.Manager => "�Ŵ���",
            EquipStaffType.Marketer => "ġ���",
            EquipStaffType.Waiter1 => "������1",
            EquipStaffType.Waiter2 => "������2",
            EquipStaffType.Cleaner => "û�Һ�",
            EquipStaffType.Guard => "��ȣ��",
            EquipStaffType.Chef1 => "�ֹ���",
            EquipStaffType.Chef2 => "���ֹ���",
            _ => string.Empty
        };
    }

    public static string FoodTypeStringConverter(FoodType type)
    {
        return type switch
        {
            FoodType.None => "����",
            FoodType.Natural => "���߷�",
            FoodType.Traditional => "������",
            FoodType.Vintage => "��Ƽ��",
            FoodType.Luxury => "���Ÿ�",
            FoodType.Modern => "���",
            FoodType.Cozy => "����",
            FoodType.Tropical => "Ʈ������",
            _ => throw new System.Exception("�ش� Ÿ���� �̻��մϴ�: " + type)
        };
    }

    public static FoodType GetFoodType(string foodTypeStr)
    {
        return foodTypeStr switch
        {
            "���߷�" => FoodType.Natural,
            "������" => FoodType.Traditional,
            "��Ƽ��" => FoodType.Vintage,
            "���Ÿ�" => FoodType.Luxury,
            "���" => FoodType.Modern,
            "����" => FoodType.Cozy,
            "Ʈ������" => FoodType.Tropical,
            _ => throw new System.Exception("�ش� ���� ���ڿ��� �̻��մϴ�: " + foodTypeStr)
        };
    }


    public static string GachaItemRankStringConverter(GachaItemRank rank)
    {
        return rank switch
        {
            GachaItemRank.Normal1 => "�븻",
            GachaItemRank.Normal2 => "�븻",
            GachaItemRank.Rare => "����",
            GachaItemRank.Unique => "����ũ",
            GachaItemRank.Special => "�����",
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
            _ => throw new System.Exception("�ش� �� �� ���ڿ��� �̻��մϴ�: " + str)
        };
    }

    public static string GetFloorStrByType(ERestaurantFloorType type)
    {
        return type switch
        {
            ERestaurantFloorType.Floor1 => "1��",
            ERestaurantFloorType.Floor2 => "2��",
            ERestaurantFloorType.Floor3 => "3��",
            _ => throw new System.Exception("�ش� �� �� Ÿ���� �̻��մϴ�: " + type)
        };
    }

    public static string GetGachaItemEffectDescription(GachaItemData data)
    {
        if (data == null)
            throw new System.Exception("���� ���� ��í ������ �����Ͱ� null �Դϴ�.");

        int level = UserInfo.GetGachaItemLevel(data);
        level = level <= 0 ? 1 : level;
        float upgradeValue = UserInfo.IsGachaItemMaxLevel(data) ? 0 : data.UpgradeValue;
        string effectDescription = data.UpgradeType switch
        {
            UpgradeType.UPGRADE01 => "���� ����\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) ����",
            UpgradeType.UPGRADE02 => "��ü �մ� �̵� �ӵ�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) ����",
            UpgradeType.UPGRADE03 => "�д� ��\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) ����",
            UpgradeType.UPGRADE04 => "�ִ� �� ������\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) ����",
            UpgradeType.UPGRADE05 => "�Ǹ� �޴� ����\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) ����",
            UpgradeType.UPGRADE06 => "�޴� ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE07 => "�޴� ���� �� �� Ȯ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) ����",
            UpgradeType.UPGRADE08 => "�ֹ� �޴� �� ��\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) ����",
            UpgradeType.UPGRADE09 => StaffTypeStringConverter(EquipStaffType.Waiter1) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE10 => StaffTypeStringConverter(EquipStaffType.Waiter1) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE11 => StaffTypeStringConverter(EquipStaffType.Waiter2) + " ��ų ��Ÿ��\n"+ (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE12 => StaffTypeStringConverter(EquipStaffType.Waiter2) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE13 => StaffTypeStringConverter(EquipStaffType.Marketer) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE14 => StaffTypeStringConverter(EquipStaffType.Marketer) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE15 => StaffTypeStringConverter(EquipStaffType.Cleaner) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE16 => StaffTypeStringConverter(EquipStaffType.Cleaner) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE17 => StaffTypeStringConverter(EquipStaffType.Guard) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE18 => StaffTypeStringConverter(EquipStaffType.Guard) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE19 => "���� �մԿ��� ���ϴ� ����\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) ����",
            UpgradeType.UPGRADE20 => "����� �մ� ��ġ�� ���� ȹ�淮\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "</color>) ����",
            UpgradeType.UPGRADE21 => "���� �մ� ��ġ�� �ӵ� ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE22 => "��ü ���� ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE23 => "��ü ���� ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE24 => "�̴ϰ��� ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE25 => "����� �մ� ���� Ȯ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "%(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "%</color>) ����",
            UpgradeType.UPGRADE26 => "�ִ� ��� �մ�\n" + ((int)data.DefaultValue + (int)data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + (int)upgradeValue + "��</color>) ����",
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

        string description = FoodTypeStringConverter(type) + $" �Ӽ� ���� ���� ����(<color={ColorToHex(GetColor(ColorType.Positive))}>+10%</color>)";;
        return description;
    }

    public static string GetKitchenFoodTypeSetEffectDescription(FoodType type)
    {
        string description = FoodTypeStringConverter(type) + $" �Ӽ� ���� ���� �ӵ� ����(<color={ColorToHex(GetColor(ColorType.Positive))}>+10%</color>)";
        return description;
    }


    public static string GetStaffEffectDescription(StaffData data)
    {
        string description = string.Empty;
        int level = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;

        switch (data)
        {
            case ManagerData:
                description = $"���̺�� �մ� �ڵ� ��ġ(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> ��)";
                break;

            case WaiterData:
                description = $"������ �մԿ��� �ڵ� ���(�⺻ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case MarketerData:
                description = $"����������� �մ� �ڵ� ȣ��(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> ��)";
                break;

            case ServerData:
                description = $"�ڵ� ���� �ֹ� �ޱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> �ʴ� 1���̺�)";
                break;

            case CleanerData:
                description = $"������ & ���� ����(�⺻ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case GuardData:
                description = $"���� �մ� ��ġ(��ġ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>)";
                break;

            case ChefData:
                description = $"�ֹ� ���� ���� ȿ�� ���(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
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
                description = $"���̺�� �մ� �ڵ� ��ġ(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> ��)";
                break;

            case WaiterData:
                description = $"������ �մԿ��� �ڵ� ���(�⺻ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case MarketerData:
                description = $"����������� �մ� �ڵ� ȣ��(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> ��)";
                break;

            case ServerData:
                description = $"�ڵ� ���� �ֹ� �ޱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> �ʴ� 1���̺�)";
                break;

            case CleanerData:
                description = $"������ & ���� ����(�⺻ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetSpeed(level)}</color>)";
                break;

            case GuardData:
                description = $"���� �մ� ��ġ(��ġ �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>)";
                break;

            case ChefData:
                description = $"�ֹ� ���� ���� ȿ�� ���(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
                break;
        }

        return description;
    }


    public static string GetStaffSkillDescription(StaffData data)
    {
        int level = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;
        return data.Skill switch
        {
            SpeedUpSkill => $"�⺻ �ɷ� �ӵ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}%</color> UP! (<color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.Duration}</color>��)" ,
            FoodPriceUpSkill => $"�ֹ� �� ���� ���� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}%</color> ����",
            AutoCustomerGuideSkill => $"�մ� �ڵ� �ȳ� ({data.Skill.Duration}s)",
            AddPromotionCustomerSkill => $"ȫ���� �մ� ȣ�� �ο� <color={ColorToHex(GetColor(ColorType.Positive))}>{(int)data.Skill.FirstValue}</color>�� ����",
            TouchAddCustomerButtonSkill => $"������� �մ� �ڵ� ȣ�� (<color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>��)",
            _ => string.Empty
        };

    }


    public static string GetCustomerEffectDescription(CustomerData data)
    {
        string description = string.Empty;
        if (data is SpecialCustomerData)
        {
            SpecialCustomerData specialCustomerData = (SpecialCustomerData)data;
            description = $"{specialCustomerData.ActiveDuration}�� ���� ���ƴٴϸ� \n��ġ�� <color={ColorToHex(GetColor(ColorType.Positive))}>{specialCustomerData.TouchAddMoney}</color>��带 ȹ���Ѵ�";
        }

        else if(data is GatecrasherCustomerData)
        {
            GatecrasherCustomerData gatecrasherCustomerData = (GatecrasherCustomerData)data;
            description = data is GatecrasherCustomer1Data ? $"{gatecrasherCustomerData.ActiveDuration}�� ���� ���ƴٴϸ� ������ ��ģ��" : $"{gatecrasherCustomerData.ActiveDuration}�� ���� ���氡�� �մԵ��� �i�Ƴ���";
        }

        else if(data is NormalCustomerData)
        {
            NormalCustomerData normalCustomerData = (NormalCustomerData)data;

            if (normalCustomerData.Skill == null)
                description = "����";

            else
            {
                if(normalCustomerData.Skill is CustomerFoodPriceUpSkill)
                    description = $"{normalCustomerData.Skill.SkillActivatePercent}%Ȯ���� ���� ������ <color={ColorToHex(GetColor(ColorType.Positive))}>{normalCustomerData.Skill.FirstValue}</color>�� �����Ѵ�";
                else if(normalCustomerData.Skill is CustomerOrderCountUpSkill)
                    description = $"{normalCustomerData.Skill.SkillActivatePercent}%Ȯ���� ������ <color={ColorToHex(GetColor(ColorType.Positive))}>{normalCustomerData.Skill.FirstValue}</color>�� �ֹ��Ѵ�";
            }      
        }

        return description;
    }


    public static string GetTendencyTypeToStr(CustomerTendencyType type)
    {
        return type switch
        {
            CustomerTendencyType.Normal => "������",
            CustomerTendencyType.Sensitive => "������",
            CustomerTendencyType.HighlySensitive => "�ʿ���",
            _ => string.Empty
        };
    }

    public static string GetEquipEffectDescription(EquipEffectType type, int value)
    {
        return type switch
        {
            EquipEffectType.AddScore => $"���� <color={ColorToHex(GetColor(ColorType.Positive))}>{value}</color> �� ����",
            EquipEffectType.AddTipPerMinute => $"�д� ȹ�� �� <color={ColorToHex(GetColor(ColorType.Positive))}>{value}</color> ����",
            EquipEffectType.AddCookSpeed => "�丮 ȿ�� <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + value + "%</color> ����",
            EquipEffectType.AddMaxTip => "�� ���差 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + value + "</color> ����",
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


    /// <summary>Image�� Sprite �ǹ����� ���� Image ������Ʈ �ǹ����� �̵��ϴ� �Լ�</summary>
    public static void ChangeImagePivot(Image image)
    {
        // ��������Ʈ�� ���� �ǹ��� ũ�⸦ ����
        Vector2 spritePivot = image.sprite.pivot;
        Vector2 spriteSize = image.sprite.rect.size;
        Vector2 normalizedPivot = new Vector2(spritePivot.x / spriteSize.x, spritePivot.y / spriteSize.y);

        // ���� RectTransform�� ũ�� ����
        RectTransform rectTransform = image.rectTransform;
        Vector2 originalSize = rectTransform.rect.size;

        // �ǹ��� ���� �����ϱ� ���� anchoredPosition ���
        Vector2 oldPivotOffset = new Vector2(rectTransform.pivot.x * originalSize.x, rectTransform.pivot.y * originalSize.y);
        Vector2 newPivotOffset = new Vector2(normalizedPivot.x * originalSize.x, normalizedPivot.y * originalSize.y);
        Vector2 pivotDelta = newPivotOffset - oldPivotOffset;

        // ���ο� �ǹ� ����
        rectTransform.pivot = normalizedPivot;

        // Stretch ������� Ȯ�� (anchorMin�� anchorMax�� ���� ������ Stretch �����)
        bool isStretch = rectTransform.anchorMin != rectTransform.anchorMax;

        // Stretch ����� ���� anchoredPosition�� �����Ͽ� �ǹ� ���濡 ���� ��ġ ��ȭ�� �����
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
