using Muks.WeightedRandom;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Scriptable Object/CustomerData")]
public class CustomerData : ScriptableObject
{
    [Header("Status")]
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _id;
    public string Id => _id;

    [TextArea][SerializeField] private string _description;
    public string Description => _description;

    [SerializeField] private float _moveSpeed = 6f;
    public float MoveSpeed => _moveSpeed;

    [SerializeField] private float _scale = 1;
    public float Scale => _scale;



    [Space]
    [Header("등장 옵션")]

    [SerializeField] private int _minScore;
    public int MinScore => _minScore;

    [SerializeField] private string _requiredDish;
    public string RequiredDish => _requiredDish;

    [SerializeField] private string _requiredItem;
    public string RequiredItem => _requiredItem;


    [Space]
    [Header("주문 음식")]

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

    [Space]
    [SerializeField] private CustomerSkill _skill;
    public CustomerSkill Skill => _skill;


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
        if (visitCount >= 100) AddFood(_visitCount100Food);
        if (visitCount >= 200) AddFood(_visitCount200Food);
        if (visitCount >= 300) AddFood(_visitCount300Food);
        if (visitCount >= 400) AddFood(_visitCount400Food);
        if (visitCount >= 500) AddFood(_visitCount500Food);

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
    
        // 방문 횟수에 따른 추가 음식 목록 추가
        if (visitCount >= 100) AddFoodIfValid(_visitCount100Food);
        if (visitCount >= 200) AddFoodIfValid(_visitCount200Food);
        if (visitCount >= 300) AddFoodIfValid(_visitCount300Food);
        if (visitCount >= 400) AddFoodIfValid(_visitCount400Food);
        if (visitCount >= 500) AddFoodIfValid(_visitCount500Food);

        if (orderFoodList.Count == 0)
        {
            DebugLog.LogError("주문 가능한 요리 수가 0개 입니다.");
        }

        return orderFoodList;
    }
}
