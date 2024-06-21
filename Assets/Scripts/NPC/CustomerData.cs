using UnityEngine;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Scriptable Object/CustomerData")]
public class CustomerData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _description;
    public string Description => _description;

    [SerializeField] private int _minScore;
    public int MinScore => _minScore;

    [SerializeField] private string _requiredDish;
    public string RequiredDish => _requiredDish;

    [SerializeField] private string[] _orderFoods;
    public string[] OrderFoods => _orderFoods;

    [SerializeField] private float _moveSpeed = 5;
    public float MoveSpeed => _moveSpeed;


    public string GetRandomOrderFood()
    {
        if (_orderFoods.Length == 0)
            throw new System.Exception("주문 가능한 음식 리스트가 비어있습니다. 확인해주세요.");

        int randInt = UnityEngine.Random.Range(0, _orderFoods.Length);
        return _orderFoods[randInt];
    }
}
