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
    [Header("���� �ɼ�")]

    [SerializeField] private int _minScore;
    public int MinScore => _minScore;

    [SerializeField] private string _requiredDish;
    public string RequiredDish => _requiredDish;

    [SerializeField] private string _requiredItem;
    public string RequiredItem => _requiredItem;


    [Space]
    [Header("�ֹ� ����")]

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
    [Header("�Ҹ���")]

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

        // 90% Ȯ���� '������ �ִ� ���� ����Ʈ' (giveFoodList) ����,
        // ��, noGiveFoodList�� ������ ���� 10% Ȯ���� ����.
        // ���� noGiveFoodList�� ����ִٸ� giveFoodList�� ���.
        List<FoodData> selectedList = (0 < giveFoodList.Count && UnityEngine.Random.Range(0f, 1f) < 0.9f)
                                      ? giveFoodList
                                      : (0 < noGiveFoodList.Count ? noGiveFoodList : giveFoodList);

        if (selectedList.Count == 0)
            throw new System.Exception("�ֹ� ������ ������ �����ϴ�.");

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

        // �⺻ ���İ� _orderFoods �迭�� ���� �߰�
        AddFoodIfValid(_requiredDish);
    
        // �湮 Ƚ���� ���� �߰� ���� ��� �߰�
        if (visitCount >= 100) AddFoodIfValid(_visitCount100Food);
        if (visitCount >= 200) AddFoodIfValid(_visitCount200Food);
        if (visitCount >= 300) AddFoodIfValid(_visitCount300Food);
        if (visitCount >= 400) AddFoodIfValid(_visitCount400Food);
        if (visitCount >= 500) AddFoodIfValid(_visitCount500Food);

        if (orderFoodList.Count == 0)
        {
            DebugLog.LogError("�ֹ� ������ �丮 ���� 0�� �Դϴ�.");
        }

        return orderFoodList;
    }
}
