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

    [SerializeField] private float _moveSpeed = 4f;
    public float MoveSpeed => _moveSpeed;

    [SerializeField] private float _scale = 1;
    public float Scale => _scale;

    [Range(1, 25)][SerializeField] private int _maxDiscomfortIndex;
    public int MaxDiscomfortIndex => _maxDiscomfortIndex;

    [Space]
    [Header("���� �ɼ�")]

    [SerializeField] private int _minScore;
    public int MinScore => _minScore;

    [SerializeField] private string _requiredDish;
    public string RequiredDish => _requiredDish;

    [SerializeField] private string _requiredItem;
    public string RequiredItem => _requiredItem;

    [SerializeField] private string[] _orderFoods;
    public string[] OrderFoods => _orderFoods;




    [Space]
    [SerializeField] private CustomerSkill _skill;
    public CustomerSkill Skill => _skill;


    public FoodData GetRandomOrderFood()
    {
        var giveFoodList = new List<FoodData>();
        var noGiveFoodList = new List<FoodData>();

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
        foreach (var dish in _orderFoods)
        {
            AddFood(dish);
        }

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

        void AddFoodIfValid(string dish)
        {
            if (string.IsNullOrWhiteSpace(dish)) return;

            FoodData foodData = FoodDataManager.Instance.GetFoodData(dish);
            if (foodData != null && UserInfo.IsGiveRecipe(foodData))
            {
                orderFoodList.Add(foodData);
            }
        }

        AddFoodIfValid(_requiredDish);
        foreach (var dish in _orderFoods)
        {
            AddFoodIfValid(dish);
        }

        if (orderFoodList.Count == 0)
        {
            DebugLog.LogError("�ֹ� ������ �丮 ���� 0�� �Դϴ�.");
        }

        return orderFoodList;
    }
}
