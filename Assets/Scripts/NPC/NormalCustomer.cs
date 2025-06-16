using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NormalCustomer : Customer
{

    [Space]
    [Header("NormalCustomer Components")]
    [SerializeField] private Customer_Anger _anger;
    [SerializeField] private Customer_Happy _happy;
    [SerializeField] private GameObject _eatAnimation;
    [SerializeField] private SpriteRenderer _foodRenderer;

    private NormalCustomerData _normalCustomerData;
    public NormalCustomerData NormalCustomerData => _normalCustomerData;
    private CustomerSkill _skill;

    private TableData _sitTableData;

    private Coroutine _waitingCoroutine;
    private Coroutine _orderFoodCoroutine;

    private int _orderCount = 1;
    public int OrderCount => _orderCount;

    private float _foodPriceMul = 2;

    private float _currentFoodPriceMul = 1;
    public float CurrentFoodPriceMul => _currentFoodPriceMul;

    private float _doublePricePercent;
    public float DoublePricePercent => _doublePricePercent;

    private float _tmpFoodPosX;
    private float _tmpEatAnimePosX;

    public override void Init() 
    {
        _tmpFoodPosX = _foodRenderer.transform.localPosition.x;
        _tmpEatAnimePosX = _eatAnimation.transform.localPosition.x;
        HideFood();
    }


    public override void SetData(CustomerData data, CustomerController customerController, TableManager tableManager)
    {
        if (!(data is NormalCustomerData))
        {
            DebugLog.LogError("�ش� ������Ʈ�� NormalCustomerData�� ���� �� �ֽ��ϴ�.");
            return;
        }
        _normalCustomerData = (NormalCustomerData)data;
        base.SetData(data, customerController, tableManager);


        StopCoroutines();
        HideFood();

        _doublePricePercent = 0;
        _currentFoodPriceMul = 1;
        _orderCount = 1;
        _anger.Init();
        _happy.Init();
        ChangeState(CustomerState.Idle);

        if (_eatAnimation != null)
            _eatAnimation.SetActive(false);

        if (_skill != null)
            _skill.Deactivate(this);

        if(_normalCustomerData.Skill != null)
        {
            _skill = _normalCustomerData.Skill;
            _normalCustomerData.Skill.Activate(this);
        }

        if (UnityEngine.Random.Range(0f, 100f) <= Mathf.Clamp(_doublePricePercent, 0, 100))
        {
            _currentFoodPriceMul = _currentFoodPriceMul * _foodPriceMul;
        }
    }

    public void SetSitTableData(TableData tableData)
    {
        _sitTableData = tableData;
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

    public WeightedRandom<FoodType> GetFoodTypeWeightDic()
    {
        WeightedRandom<FoodType> _foodRandom = new WeightedRandom<FoodType>();
        List<FoodData> orderFoodDataList = _normalCustomerData.GetGiveOrderFoodList();
        for (int i = 0, cnt = (int)FoodType.Length; i < cnt; ++i)
        {
            _foodRandom.Add((FoodType)i, 0);
        }
        // �ֹ��� ���ĵ� ó��
        foreach (FoodData foodData in orderFoodDataList)
        {
            _foodRandom.Add(foodData.FoodType, 1);
        }

        return _foodRandom;
    }


    public void AddOrderCount(int value)
    {
        _orderCount += value;
    }

    public void AddFoodPricePercent(float value)
    {
        _currentFoodPriceMul = Mathf.Clamp(value, 0, 100);
    }

    public void StartAnger()
    {
        _anger.StartAnime();
        _happy.StopAnime();
    }


    public void StopAnger()
    {
        _anger.StopAnime();
    }

    public void StartHappy()
    {
        _happy.StartAnime();
        _anger.StopAnime();
    }

    public void StopHappy()
    {
        _happy.StopAnime();
    }


    public void ShowFood(Sprite sprite)
    {
        _foodRenderer.sprite = sprite;

        /*   // �� ��������Ʈ�� ũ��
           float newSpriteHeight = sprite.texture.height;
           float newSpriteWidth = sprite.texture.width;

           // ���� ��������Ʈ�� ũ��
           float originalHeight = _tmpFoodSpriteHeight;
           float originalWidth = _foodRenderer.sprite.texture.width;

           // ���̿� �ʺ��� ���� ���
           float heightScaleFactor = newSpriteHeight / originalHeight;
           float widthScaleFactor = newSpriteWidth / originalWidth;

           float scaleFactor;

           if (newSpriteHeight > originalHeight || newSpriteWidth > originalWidth)
           {
               // ���̳� �ʺ� �� �� ū ���� �������� ũ�� ����
               scaleFactor = Mathf.Max(heightScaleFactor, widthScaleFactor);
           }
           else
           {
               // �� �� �۰ų� ũ�� ����: ���� ū ���� �������� ũ�� ����
               scaleFactor = Mathf.Max(heightScaleFactor, widthScaleFactor);
           }

           // ��������Ʈ ũ�� ����
           _foodRenderer.transform.localScale = _tmpFoodSize / scaleFactor;
           DebugLog.Log(scaleFactor);
           // ��������Ʈ Ȱ��ȭ*/
        _foodRenderer.gameObject.SetActive(true);
    }


    public void HideFood()
    {
        _foodRenderer.gameObject.SetActive(false);
    }


    // ��⿭ ���� �� ȣ��
    public void StartWaiting()
    {
        StopCoroutines();
        _waitingCoroutine = StartCoroutine(WaitingInLineCoroutine());
    }

    public void StopWaiting()
    {
        StopCoroutines();
    }
    

    // ���̺� �ɰ� ���� �ֹ� �� ȣ��
    public void StartWaitingForFood()
    {
        _orderFoodCoroutine = StartCoroutine(WaitingForFoodCoroutine());
    }

    
    // ���� ���� �� ȣ��
    public void StopWaitingForFood()
    {
        StopCoroutines();
    }
    

    private IEnumerator WaitingInLineCoroutine()
    {
        float elapsedTime = 0f;
        float maxWaitTime = _normalCustomerData.WaitTime;
        
        while (elapsedTime < maxWaitTime)
        {
            elapsedTime += 0.02f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        
        StartAnger();
        LeaveRestaurant();
    }
    

    private IEnumerator WaitingForFoodCoroutine()
    {
        float elapsedTime = 0f;
        float maxWaitTime = _normalCustomerData.OrderFoodTime;
        
        while (elapsedTime < maxWaitTime)
        {
            elapsedTime += 0.02f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        
        StartAnger();
        LeaveTableAndRestaurant();
    }
    
    // ȭ�� ���� ���� �� ȣ�� (��⿭����)
    private void LeaveRestaurant()
    {
        DebugLog.Log("�ټ��� ����");
        _customerController.RemoveCustomerFromLineQueue(this);
        Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(this);
        });
    }
    
    // ���̺��� ȭ�� ���� ���� ��
    private void LeaveTableAndRestaurant()
    {
        DebugLog.Log("���̺��� ����");
        _customerController.RemoveCustomerFromLineQueue(this);
        _tableManager.AngerExitCustomer(_sitTableData);
    }



    protected override IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        _path = nodeList;

        if (1 < nodeList.Count)
            nodeList.RemoveAt(0);

        foreach (Vector2 vec in nodeList)
        {
            while ((vec - (Vector2)_moveObj.transform.position).sqrMagnitude > 0.01f)
            {
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                SetSpriteDir(dir.x);
                float step = Time.deltaTime * _moveSpeed * GameManager.Instance.AddCustomerSpeedMul * 0.7f;
                _moveObj.transform.position = Vector2.MoveTowards(_moveObj.transform.position, vec, step);
                ChangeState(CustomerState.Run);
                yield return null;
            }
        }

        ChangeState(CustomerState.Idle);
        SetSpriteDir(_moveEndDir);
        onCompleted?.Invoke();

        if (_isStairsMove)
            yield break;

        _moveCompleted?.Invoke();
        _moveCompleted = null;
    }

    private void StopCoroutines()
    {
        if (_waitingCoroutine != null)
        {
            StopCoroutine(_waitingCoroutine);
            _waitingCoroutine = null;
        }
        
        if (_orderFoodCoroutine != null)
        {
            StopCoroutine(_orderFoodCoroutine);
            _orderFoodCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopCoroutines();
    }
}
