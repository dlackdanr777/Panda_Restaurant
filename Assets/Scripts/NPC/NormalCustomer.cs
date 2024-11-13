using Muks.PathFinding.AStar;
using System.Collections.Generic;
using UnityEngine;


public class NormalCustomer : Customer
{

    [Space]
    [Header("NormalCustomer Components")]
    [SerializeField] private Customer_Anger _anger;


    private CustomerSkill _skill;

    private int _orderCount = 1;
    public int OrderCount => _orderCount;

    private float _foodPriceMul = 2;

    private float _currentFoodPriceMul = 1;
    public float CurrentFoodPriceMul => _currentFoodPriceMul;

    private float _doublePricePercent;
    public float DoublePricePercent => _doublePricePercent;


    public override void SetData(CustomerData data)
    {
        base.SetData(data);

        _moveSpeed *= GameManager.Instance.AddCustomerSpeedMul;
        _doublePricePercent = 0;
        _currentFoodPriceMul = 1;
        _orderCount = 1;
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

        if (Random.Range(0f, 100f) <= Mathf.Clamp(_doublePricePercent + GameManager.Instance.AddFoodDoublePricePercent, 0, 100))
        {
            _currentFoodPriceMul = _foodPriceMul;
        }

    }


    public void SetOrderCount(int value)
    {
        _orderCount = value;
    }

    public void AddFoodPricePercent(float value)
    {
        _currentFoodPriceMul = Mathf.Clamp(_currentFoodPriceMul + value, 0, 100);
        
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
