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

    private float _addFeverGaugeMul = 1;
    public float AddFeverGaugeMul => _addFeverGaugeMul;

    private float _addSatisfactionMul = 1;
    public float AddSatisfactionMul => _addSatisfactionMul;

    private float _tmpFoodPosX;
    private float _tmpEatAnimePosX;

    public override void Init()
    {
        _tmpFoodPosX = _foodRenderer.transform.localPosition.x;
        _tmpEatAnimePosX = _eatAnimation.transform.localPosition.x;
        _anger.Init();
        _happy.Init();
        HideFood();

        //UserInfo.OnChangeCustomerSkinHandler += OnChangeSkin;
    }


    public override void SetData(CustomerData data, CustomerController customerController, TableManager tableManager)
    {
        if (!(data is NormalCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 NormalCustomerData만 받을 수 있습니다.");
            return;
        }
        _normalCustomerData = (NormalCustomerData)data;
        base.SetData(data, customerController, tableManager);

        CustomerSkinData skinData = UserInfo.GetEquipCustomerSkin(data.Id);
        _spriteRenderer.sprite = skinData == null ? data.Sprite : skinData.Sprite;
        StopCoroutines();
        HideFood();

        _doublePricePercent = 0;
        _currentFoodPriceMul = 1;
        _orderCount = 1;
        _addFeverGaugeMul = 1;
        _addSatisfactionMul = 1;
        _anger.SetCustomer(this);
        _happy.SetCustomer(this);
        ChangeState(CustomerState.Idle);

        if (_eatAnimation != null)
            _eatAnimation.SetActive(false);

        // if (UnityEngine.Random.Range(0f, 100f) <= Mathf.Clamp(_doublePricePercent, 0, 100))
        // {
        //     _currentFoodPriceMul = _currentFoodPriceMul * _foodPriceMul;
        // }

        ApplySkinEffect(skinData);
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
        // 주문된 음식들 처리
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

        /*   // 새 스프라이트의 크기
           float newSpriteHeight = sprite.texture.height;
           float newSpriteWidth = sprite.texture.width;

           // 이전 스프라이트의 크기
           float originalHeight = _tmpFoodSpriteHeight;
           float originalWidth = _foodRenderer.sprite.texture.width;

           // 높이와 너비의 비율 계산
           float heightScaleFactor = newSpriteHeight / originalHeight;
           float widthScaleFactor = newSpriteWidth / originalWidth;

           float scaleFactor;

           if (newSpriteHeight > originalHeight || newSpriteWidth > originalWidth)
           {
               // 높이나 너비 중 더 큰 값을 기준으로 크기 조정
               scaleFactor = Mathf.Max(heightScaleFactor, widthScaleFactor);
           }
           else
           {
               // 둘 다 작거나 크기 동일: 가장 큰 값을 기준으로 크기 조정
               scaleFactor = Mathf.Max(heightScaleFactor, widthScaleFactor);
           }

           // 스프라이트 크기 조정
           _foodRenderer.transform.localScale = _tmpFoodSize / scaleFactor;
           DebugLog.Log(scaleFactor);
           // 스프라이트 활성화*/
        _foodRenderer.gameObject.SetActive(true);
    }


    public void HideFood()
    {
        _foodRenderer.gameObject.SetActive(false);
    }


    // 대기열 진입 시 호출
    public void StartWaiting()
    {
        StopCoroutines();
        _waitingCoroutine = StartCoroutine(WaitingInLineCoroutine());
    }

    public void StopWaiting()
    {
        StopCoroutines();
    }


    // 테이블에 앉고 음식 주문 시 호출
    public void StartWaitingForFood()
    {
        _orderFoodCoroutine = StartCoroutine(WaitingForFoodCoroutine());
    }


    // 음식 서빙 시 호출
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

    // 화가 나서 떠날 때 호출 (대기열에서)
    private void LeaveRestaurant()
    {
        DebugLog.Log("줄서다 나감");
        _customerController.RemoveCustomerFromLineQueue(this);
        Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(this);
        });
    }

    // 테이블에서 화가 나서 떠날 때
    private void LeaveTableAndRestaurant()
    {
        DebugLog.Log("테이블에서 나감");
        _customerController.RemoveCustomerFromLineQueue(this);
        _tableManager.AngerExitCustomer(_sitTableData);
    }



    protected override IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        _path = nodeList;

        if (1 < nodeList.Count)
            nodeList.RemoveAt(0);

        Vector3 currentPos;
        Vector2 targetVec;
        Vector2 direction;
        float distanceSqr;
        float step;
        
        foreach (Vector2 vec in nodeList)
        {
            targetVec = vec;
            
            while (true)
            {
                currentPos = _moveObj.transform.position;
                
                // 거리 제곱 계산 (GC 없음)
                float dx = targetVec.x - currentPos.x;
                float dy = targetVec.y - currentPos.y;
                distanceSqr = dx * dx + dy * dy;
                
                if (distanceSqr <= 0.01f)
                    break;
                
                // 방향 계산 및 정규화 (재사용)
                float distance = Mathf.Sqrt(distanceSqr);
                direction.x = dx / distance;
                direction.y = dy / distance;
                
                SetSpriteDir(direction.x);
                
                step = Time.deltaTime * _moveSpeed * GameManager.Instance.AddCustomerSpeedMul * 0.7f;
                
                // MoveTowards 직접 구현 (GC 없음)
                if (distance > step)
                {
                    currentPos.x += direction.x * step;
                    currentPos.y += direction.y * step;
                }
                else
                {
                    currentPos.x = targetVec.x;
                    currentPos.y = targetVec.y;
                }
                
                _moveObj.transform.position = currentPos;
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


    private void ApplySkinEffect(CustomerSkinData skinData)
    {
        if (skinData == null)
            return;

        switch (skinData.UpgradeType)
        {
            case SkinCustomerUpgradeType.Type1:
                _moveSpeed *= 1 + skinData.UpgradeValue / 100f;
                break;
            case SkinCustomerUpgradeType.Type2:
                _currentFoodPriceMul *= 1 + skinData.UpgradeValue / 100f;
                break;
            case SkinCustomerUpgradeType.Type3:
                _orderCount += (int)skinData.UpgradeValue;
                break;
            case SkinCustomerUpgradeType.Type4:
                _addSatisfactionMul *= 1 + skinData.UpgradeValue / 100f;
                break;

            case SkinCustomerUpgradeType.Type5:
                _addFeverGaugeMul *=  1 + skinData.UpgradeValue / 100f;
                break;
        }
    }


    //배치중인 손님들도 스킨을 바꾸는 함수
    // private void OnChangeSkin()
    // {
    //     if (_customerData == null || !gameObject.activeInHierarchy)
    //         return;

    //     CustomerSkinData skinData = UserInfo.GetEquipCustomerSkin(_customerData.Id);
    //     _spriteRenderer.sprite = skinData == null ? _customerData.Sprite : skinData.Sprite;
    // }

    private void OnDisable()
    {
        StopCoroutines();
    }

    // private void OnDestroy()
    // {
    //     UserInfo.OnChangeCustomerSkinHandler -= OnChangeSkin;
    // }
}
