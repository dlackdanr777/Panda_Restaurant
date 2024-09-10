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
