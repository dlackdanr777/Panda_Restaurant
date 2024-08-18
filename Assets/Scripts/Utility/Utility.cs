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
}
