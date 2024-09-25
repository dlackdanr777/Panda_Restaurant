using Muks.PathFinding.AStar;
using UnityEngine;


public class NormalCustomer : Customer
{

    [Space]
    [Header("NormalCustomer Components")]
    [SerializeField] private Customer_Anger _anger;


    private CustomerSkill _skill;

    private int _orderCount = 1;
    public int OrderCount => _orderCount;

    private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;


    public override void SetData(CustomerData data)
    {
        base.SetData(data);
        _customerData = data;
        _spriteParent.transform.localPosition = new Vector3(0, -AStar.Instance.NodeSize * 2, 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.sprite = data.Sprite;
        _anger.Init();
        _animator.SetBool("Run", false);
        _animator.SetBool("Eat", false);
        if (_skill != null)
            _skill.Deactivate(this);

        if(data.Skill != null)
        {
            _skill = data.Skill;
            data.Skill.Activate(this);
        }
        else
        {
            SetOrderCount(1);
            SetFoodPriceMul(1);
        }
    }


    public void SetOrderCount(int value)
    {
        _orderCount = value;
    }

    public void SetFoodPriceMul(float value)
    {
        _foodPriceMul = value;
    }

    public void StartAnger()
    {
        _anger.StartAnime();
    }


    public void StopAnger()
    {
        _anger.StopAnime();
    }
}
