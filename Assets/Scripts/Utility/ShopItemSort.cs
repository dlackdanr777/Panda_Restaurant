using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ShopItemSort
{

    public static List<FurnitureData> SortByPrice(List<FurnitureData> itemList, bool isAscending)
    {
        return SortShopDataByPrice(itemList, isAscending);
    }

    public static List<KitchenUtensilData> SortByPrice(List<KitchenUtensilData> itemList, bool isAscending)
    {
        return SortShopDataByPrice(itemList, isAscending);
    }

    public static List<StaffData> SortByPrice(List<StaffData> itemList, bool isAscending)
    {
        return SortShopDataByPrice(itemList, isAscending);
    }

        public static List<FoodData> SortByPrice(List<FoodData> itemList, bool isAscending)
    {
        return SortShopDataByPrice(itemList, isAscending);
    }


    private static List<T> SortShopDataByPrice<T>(List<T> itemList, bool isAscending) where T : ShopData
    {
        // 기존 ShopData 리스트를 리턴하는 대신 T 타입으로 바로 정렬하여 반환
        var result = new List<T>();
        
        // 골드 가격 아이템 정렬
        var goldItems = isAscending
            ? itemList.Where(item => item.MoneyType == MoneyType.Gold)
                     .OrderBy(item => item.BuyPrice)
                     .ThenBy(item => item.Name)
            : itemList.Where(item => item.MoneyType == MoneyType.Gold)
                     .OrderByDescending(item => item.BuyPrice)
                     .ThenBy(item => item.Name);
        
        // 다이아 가격 아이템 정렬
        var diaItems = isAscending
            ? itemList.Where(item => item.MoneyType == MoneyType.Dia)
                     .OrderBy(item => item.BuyPrice)
                     .ThenBy(item => item.Name)
            : itemList.Where(item => item.MoneyType == MoneyType.Dia)
                     .OrderByDescending(item => item.BuyPrice)
                     .ThenBy(item => item.Name);
                     
        // 결과 합치기
        result.AddRange(goldItems);
        result.AddRange(diaItems);
        
        return result;
    }
}