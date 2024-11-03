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
    [Header("등장 옵션")]

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


    public string GetRandomOrderFood()
    {
        List<string> orderFoodList = new List<string>();
        
        if(!string.IsNullOrWhiteSpace(_requiredDish))
            orderFoodList.Add(_requiredDish);   

        for (int i = 0, cnt = _orderFoods.Length; i < cnt; i++)
        {
                orderFoodList.Add(_orderFoods[i]);
        }

        if (_orderFoods.Length == 0 || orderFoodList.Count == 0)
            throw new System.Exception("주문 가능한 음식 리스트가 비어있습니다. 확인해주세요.");

        int randInt = UnityEngine.Random.Range(0, orderFoodList.Count);
        return orderFoodList[randInt];
    }
}
