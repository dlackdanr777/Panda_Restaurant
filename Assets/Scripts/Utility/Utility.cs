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
            KitchenUtensilType.Plate => "�׸� ����",
            KitchenUtensilType.Sink => "��ũ��",
            KitchenUtensilType.CookingTools => "���� ����",
            KitchenUtensilType.Kitchenrack => "�ֹ� ����",
            _ => string.Empty
        };
    }


    public static string StaffTypeStringConverter(StaffType type)
    {
        return type switch
        {
            StaffType.Manager => "�Ŵ���",
            StaffType.Marketer => "ġ���",
            StaffType.Waiter => "������",
            StaffType.Server => "����",
            StaffType.Cleaner => "û�Һ�",
            StaffType.Guard => "��ȣ��",
            StaffType.Chef => "����",
            _ => string.Empty
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
            UpgradeType.UPGRADE09 => StaffTypeStringConverter(StaffType.Waiter) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE10 => StaffTypeStringConverter(StaffType.Waiter) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE11 => StaffTypeStringConverter(StaffType.Server) + " ��ų ��Ÿ��\n"+ (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE12 => StaffTypeStringConverter(StaffType.Server) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE13 => StaffTypeStringConverter(StaffType.Marketer) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE14 => StaffTypeStringConverter(StaffType.Marketer) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE15 => StaffTypeStringConverter(StaffType.Cleaner) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE16 => StaffTypeStringConverter(StaffType.Cleaner) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE17 => StaffTypeStringConverter(StaffType.Guard) + " ��ų ��Ÿ��\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
            UpgradeType.UPGRADE18 => StaffTypeStringConverter(StaffType.Guard) + " ��ų ���� �ð�\n" + (data.DefaultValue + data.UpgradeValue * (level - 1)) + "��(<color=" + ColorToHex(GetColor(ColorType.AddValue)) + ">+" + upgradeValue + "��</color>) ����",
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

    public static string GetEquipEffectDescription(EquipEffectData effectData)
    {
        string description = string.Empty;

        switch (effectData)
        {
            case TipPerMinuteEquipEffectData:
                description = "�д� ȹ�� �� <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (effectData.EffectValue.ToString()) + "</color> ����";
                break;

            case MaxTipVolumeEquipEffectData:
                description = "�� ���差 <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + effectData.EffectValue + "</color> ����";
                break;

            case CookingSpeedUpEquipEffectData:
                description = "�丮 ȿ�� <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + effectData.EffectValue + "%</color> ����";
                break;

            default:
                description = "�� �� ���� ȿ��";
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
                description = "�д� ȹ�� �� <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (setData.Value) + "</color> ����";
                break;

            case CookingSpeedUpSetData:
                description = "�丮 ȿ�� <color=" + ColorToHex(GetColor(ColorType.Positive)) + ">" + (setData.Value) + "%</color> ����";
                break;

            default:
                description = "�� �� ���� ȿ��";
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
                description = $"�ִ� ��� �մ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{Mathf.FloorToInt(data.GetActionValue(level))}</color> �� �߰�)";
                break;

            case WaiterData:
                description = $"�ڵ� ���� �����ϱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> �ʴ� 1��)";
                break;

            case MarketerData:
                description = $"�ڵ� �մ� ȣ���ϱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> �ʴ� <color={ColorToHex(GetColor(ColorType.Positive))}>{data.SecondValue}%</color> Ȯ��)";
                break;

            case ServerData:
                description = $"�ڵ� ���� �ֹ� �ޱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color> �ʴ� 1���̺�)";
                break;

            case CleanerData:
                description = $"�ڵ� ������ û�� �ϱ�(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.SecondValue}</color>��)";
                break;

            case GuardData:
                description = $"���� ��ġ(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}</color>�� �ҿ�)";
                break;

            case ChefData:
                description = $"�丮 �ӵ� ���(<color={ColorToHex(GetColor(ColorType.Positive))}>{data.GetActionValue(level)}%</color>)";
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
            description = $"{specialCustomerData.ActiveDuration}�� ���� ���ƴٴϸ� \n��ġ�� <color={ColorToHex(GetColor(ColorType.Positive))}>{specialCustomerData.TouchAddMoney}</color>��带 ȹ���Ѵ�";
        }

        else if(data is GatecrasherCustomerData)
        {
            GatecrasherCustomerData gatecrasherCustomerData = (GatecrasherCustomerData)data;
            description = data is GatecrasherCustomer1Data ? $"{gatecrasherCustomerData.ActiveDuration}�� ���� ���ƴٴϸ� ������ ��ģ��" : $"{gatecrasherCustomerData.ActiveDuration}�� ���� ���氡�� �մԵ��� �i�Ƴ���";
        }

        else
        {
            if (data.Skill == null)
                description = "����";

            else
            {
                if(data.Skill is CustomerFoodPriceUpSkill)
                    description = $"{data.Skill.SkillActivatePercent}% Ȯ���� ���� ������ <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>�� �����Ѵ�";
                else if(data.Skill is CustomerOrderCountUpSkill)
                    description = $"{data.Skill.SkillActivatePercent}% Ȯ���� ������ <color={ColorToHex(GetColor(ColorType.Positive))}>{data.Skill.FirstValue}</color>�� �ֹ��Ѵ�";
            }      
        }

        return description;
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
}
