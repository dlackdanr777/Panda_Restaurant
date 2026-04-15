using Muks.WeightedRandom;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Scriptable Object/NormalCustomerData")]
public class NormalCustomerData : CustomerData
{
    [Space]
    [Header("주문 음식")]

    [SerializeField] private string _visitCount25Food;
    public string VisitCount25Food => _visitCount25Food;

    [SerializeField] private string _visitCount50Food;
    public string VisitCount50Food => _visitCount50Food;

    [SerializeField] private string _visitCount100Food;
    public string VisitCount100Food => _visitCount100Food;

    [SerializeField] private string _visitCount200Food;
    public string VisitCount200Food => _visitCount200Food;

    [SerializeField] private string _visitCount300Food;
    public string VisitCount300Food => _visitCount300Food;

    [SerializeField] private string _visitCount400Food;
    public string VisitCount400Food => _visitCount400Food;

    [SerializeField] private string _visitCount500Food;
    public string VisitCount500Food => _visitCount500Food;


    [Space]
    [Header("불만도")]

    [SerializeField] CustomerTendencyType _tendencyType;
    public CustomerTendencyType TendencyType => _tendencyType;

    [SerializeField] private int _orderFoodTime;
    public int OrderFoodTime => _orderFoodTime;

    [SerializeField] private int _wiatTime;
    public int WaitTime => _wiatTime;

    private Dictionary<int, string> _visitCountFoodDic = new Dictionary<int, string>();



    public NormalCustomerData(Sprite sprite, Sprite thumbnailSprite, string id, string name, string description, float moveSpeed, int minScore, string requiredDish, string requiredItem, string visitCount25Food, string visitCount50Food, string visitCount100Food, string visitCount200Food, string visitCount300Food, string visitCount400Food, string visitCount500Food, CustomerTendencyType tendencyType, int orderFoodTime, int waitTime) : base(sprite, thumbnailSprite, id, name, description, moveSpeed, minScore, requiredDish, requiredItem)
    {
        _visitCount25Food = visitCount25Food;
        _visitCount50Food = visitCount50Food;
        _visitCount100Food = visitCount100Food;
        _visitCount200Food = visitCount200Food;
        _visitCount300Food = visitCount300Food;
        _visitCount400Food = visitCount400Food;
        _visitCount500Food = visitCount500Food;

        _tendencyType = tendencyType;
        _orderFoodTime = orderFoodTime;
        _wiatTime = waitTime;

        _visitCountFoodDic.Add(25, visitCount25Food);
        _visitCountFoodDic.Add(50, visitCount50Food);
        _visitCountFoodDic.Add(100, visitCount100Food);
        _visitCountFoodDic.Add(200, visitCount200Food);
        _visitCountFoodDic.Add(300, visitCount300Food);
        _visitCountFoodDic.Add(400, visitCount400Food);
        _visitCountFoodDic.Add(500, visitCount500Food);
    }
    
    public Dictionary<int, string> GetVisitCountFoodDic()
    {
        return _visitCountFoodDic;
    }



    public FoodData GetRandomOrderFood()
    {
        var giveFoodList = new List<FoodData>();
        var noGiveFoodList = new List<FoodData>();
        int visitCount = UserInfo.GetVisitedCustomerCount(_id);

        void AddFood(string dish)
        {
            if (string.IsNullOrWhiteSpace(dish)) return;

            FoodData foodData = FoodDataManager.Instance.GetFoodData(dish);
            if (foodData == null) return;

            if (UserInfo.IsGiveRecipe(foodData))
                giveFoodList.Add(foodData);
            else
                noGiveFoodList.Add(foodData);
        }

        AddFood(_requiredDish);

        foreach (var food in _visitCountFoodDic)
        {
            if(food.Key <= visitCount)
            {
                AddFood(food.Value);
            }
        }

        // 90% 확률로 '가지고 있는 음식 리스트' (giveFoodList) 선택,
        // 단, noGiveFoodList가 존재할 때만 10% 확률로 선택.
        // 만약 noGiveFoodList가 비어있다면 giveFoodList를 사용.
        List<FoodData> selectedList = (0 < giveFoodList.Count && UnityEngine.Random.Range(0f, 1f) < 0.9f)
                                      ? giveFoodList
                                      : (0 < noGiveFoodList.Count ? noGiveFoodList : giveFoodList);

        if (selectedList.Count == 0)
            throw new System.Exception("주문 가능한 음식이 없습니다.");

        return selectedList[UnityEngine.Random.Range(0, selectedList.Count)];
    }



    public List<FoodData> GetGiveOrderFoodList()
    {
        var orderFoodList = new List<FoodData>();
        int visitCount = UserInfo.GetVisitedCustomerCount(_id);
        
        void AddFoodIfValid(string dish)
        {
            if (string.IsNullOrWhiteSpace(dish)) return;

            FoodData foodData = FoodDataManager.Instance.GetFoodData(dish);
            if (foodData != null && UserInfo.IsGiveRecipe(foodData))
            {
                orderFoodList.Add(foodData);
            }
        }

        // 기본 음식과 _orderFoods 배열의 음식 추가
        AddFoodIfValid(_requiredDish);


        foreach (var food in _visitCountFoodDic)
        {
            if(food.Key <= visitCount)
            {
                AddFoodIfValid(food.Value);
            }
        }

        if (orderFoodList.Count == 0)
        {
            DebugLog.LogError("주문 가능한 요리 수가 0개 입니다.");
        }

        return orderFoodList;
    }
}
