using Muks.PathFinding.AStar;
using System.Collections.Generic;
using UnityEngine;


public class NormalCustomer : Customer
{

    [Space]
    [Header("NormalCustomer Components")]
    [SerializeField] private Customer_Anger _anger;
    [SerializeField] private GameObject _eatAnimation;
    [SerializeField] private SpriteRenderer _foodRenderer;

    private CustomerSkill _skill;

    private int _orderCount = 1;
    public int OrderCount => _orderCount;

    private float _foodPriceMul = 2;

    private float _currentFoodPriceMul = 1;
    public float CurrentFoodPriceMul => _currentFoodPriceMul;

    private float _doublePricePercent;
    public float DoublePricePercent => _doublePricePercent;


    private Vector3 _tmpFoodSize;
    private float _tmpFoodSpriteHeight;
    private float _tmpFoodPosX;
    private float _tmpEatAnimePosX;

    public override void Init() 
    {
        _tmpFoodSize = _foodRenderer.transform.localScale;
        _tmpFoodSpriteHeight = _foodRenderer.sprite.texture.height;
        _tmpFoodPosX = _foodRenderer.transform.localPosition.x;
        _tmpEatAnimePosX = _eatAnimation.transform.localPosition.x;
        HideFood();
    }


    public override void SetData(CustomerData data)
    {
        base.SetData(data);
        HideFood();
        _moveSpeed *= GameManager.Instance.AddCustomerSpeedMul;
        _doublePricePercent = 0;
        _currentFoodPriceMul = 1;
        _orderCount = 1;
        _anger.Init();
        ChangeState(CustomerState.Idle);

        if (_eatAnimation != null)
            _eatAnimation.SetActive(false);

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

    public override void ChangeState(CustomerState state)
    {
        base.ChangeState(state);
        if (_eatAnimation != null)
            _eatAnimation.SetActive(_currentState == CustomerState.Eat);
    }

    public override void SetSpriteDir(float dir)
    {
        base.SetSpriteDir(dir);

        if (dir < 0)
        {
            _eatAnimation.transform.localPosition = new Vector3(_tmpEatAnimePosX, _eatAnimation.transform.localPosition.y, 0);
            _foodRenderer.transform.localPosition = new Vector3(_tmpFoodPosX, _foodRenderer.transform.localPosition.y, 0);
        }
        else if (0 < dir)
        {
            _eatAnimation.transform.localPosition = new Vector3(-_tmpEatAnimePosX, _eatAnimation.transform.localPosition.y, 0);
            _foodRenderer.transform.localPosition = new Vector3(-_tmpFoodPosX, _foodRenderer.transform.localPosition.y, 0);
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


    public void ShowFood(Sprite sprite)
    {
        _foodRenderer.sprite = sprite;

        float newSpriteHeight = sprite.texture.height;

        if (newSpriteHeight > _tmpFoodSpriteHeight)
        {
            float scaleFactor = newSpriteHeight / _tmpFoodSpriteHeight;
            _foodRenderer.transform.localScale = _tmpFoodSize * scaleFactor;
        }
        else
        {
            _foodRenderer.transform.localScale = _tmpFoodSize;
        }

        _foodRenderer.gameObject.SetActive(true);
    }


    public void HideFood()
    {
        _foodRenderer.gameObject.SetActive(false);
    }
}
