﻿using Muks.PathFinding.AStar;
using Muks.Tween;
using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    public event Action OnTableUpdateHandler;

    [Header("Transform")]
    [SerializeField] private Transform _moneyUITr;

    [Space]
    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private KitchenSystem _kitchenSystem;
    [SerializeField] private SatisfactionSystem _satisfactionSystem;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private FeverSystem _feverSystem;


    [Space]
    [Header("Button Option")]
    [SerializeField] private Button _guideButton;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _callSound;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private GachaTutorial _miniGameTutorial;

    private int _totalGarbageCount => ObjectPoolManager.Instance.GetEnabledGarbageCount();


    public Vector3 GetDoorPos(RestaurantType type, Vector3 pos)
    {
        if (type == RestaurantType.Kitchen)
            return _kitchenSystem.GetDoorPos(pos);

        else
            return _furnitureSystem.GetDoorPos(pos);
    }


    private void Start()
    {
        Init();
        UpdateTable();
    }


    private void Init()
    {
        _guideButton.onClick.AddListener(() => OnCustomerGuideEventPlaySound(-1));

        UpdateTable();
        _customerController.OnChangeCustomerHandler += UpdateTable;
        _customerController.OnGuideCustomerHandler += UpdateTable;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }


    public bool OnCustomerGuideEvent(int sitPos = -1)
    {
        if (_customerController.IsEmpty())
            return false;

        NormalCustomer customer = _customerController.GetFirstCustomer();
        if (customer == null)
        {
            DebugLog.LogError("손님 정보가 없습니다.");
            return false;
        }

        TableData data = GetTableType(ETableState.Empty);
        if (data == null)
        {
            DebugLog.Log("남는 테이블이 없습니다.");
            UpdateTable();
            return false;
        }

        ERestaurantFloorType choiceFloor = GetWeightRandomChoiceFloor(customer);
        data = GetTableType(choiceFloor, ETableState.Empty);

        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return false;
        }

        sitPos = Mathf.Clamp(sitPos, -1, 1);
        customer.SetVisitFloor(choiceFloor);
        OnCustomerGuide(customer, data, sitPos);
        return true;
    }

    public void OnCustomerGuideEventPlaySound(int sitPos = -1)
    {
        if(OnCustomerGuideEvent(sitPos))
            SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _callSound);
    }



    private void OnCustomerGuide(NormalCustomer customer, TableData data, int sitPos = -1)
    {
        data.CurrentCustomer = customer;
        customer.SetLayer("Customer", 0);
        customer.StopWaiting();
        data.TableState = ETableState.Move;
        data.OrdersCount = customer.OrderCount;
        data.Satisfaction = 0;
        if (sitPos != 0 && sitPos != 1)
        {
            int randInt = UnityEngine.Random.Range(0, data.ChairTrs.Length);
            data.SitDir = randInt == 0 ? -1 : 1;
            data.SitIndex = randInt;
        }
        else
        {
            data.SitDir = sitPos == 0 ? -1 : 1;
            data.SitIndex = sitPos;
        }

        Vector3 targetPos = data.ChairTrs[data.SitIndex].position;
        targetPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
        UpdateTable();

        _customerController.GuideCustomer(targetPos, 0, () =>
        {
            Tween.Wait(0.1f, () =>
            {
                DebugLog.Log(data.name + " Sit " + "11");
                if (data.CurrentCustomer == null)
                    return;
                DebugLog.Log(data.name + " Sit " + "22");
                customer.transform.position = data.ChairTrs[data.SitIndex].position;
                customer.SetSitTableData(data);
                data.OrderButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);
                data.ServingButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);

                customer.SetSpriteDir(-data.SitDir);
                customer.SetLayer("SitCustomer", 0);
                customer.ChangeState(CustomerState.Sit);

                Tween.Wait(1f, () =>
                {
                    DebugLog.Log(data.name + " Sit " + "33");
                    if (data.CurrentCustomer == null)
                        return;

                    DebugLog.Log(data.name + " Sit " + "44");
                    if (!_satisfactionSystem.CheckCustomerTendency(customer.NormalCustomerData.TendencyType))
                    {
                        AngerExitCustomer(data);
                        return;
                    }

                    OnCustomerSeating(data);
                });
            });
        });
    }



    public void OnCustomerSeating(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        if (data.OrdersCount <= 0)
            EndEat(data);

        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();

        FoodData foodData = data.CurrentCustomer.NormalCustomerData.GetRandomOrderFood();

        bool isMiniGameNeeded = foodData.MiniGameNeeded && string.IsNullOrWhiteSpace(foodData.NeedItem);
        if (isMiniGameNeeded && !UserInfo.IsMiniGameTutorialClear)
        {
            _miniGameTutorial.StartTutorial(foodData, data.transform);
        }

        int foodLevel = UserInfo.GetRecipeLevel(foodData);
        CookingData cookingData = new CookingData(foodData, data, Mathf.Clamp(0.5f, foodData.GetCookingTime(foodLevel) - GameManager.Instance.SubCookingTime, 100000), foodData.GetSellPrice(foodLevel), () =>
        {
            if (data.CurrentCustomer == null)
            {
                DebugLog.Log("현재 손님이 비어있습니다.");
                return;
            }    

            data.TableState = ETableState.CanServing;
            data.ServingButton.SetData(foodData);
            foodData = null;
            UpdateTable();
        });

        int tip = Mathf.FloorToInt(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul * _satisfactionSystem.AddCustomerTipMul(data.CurrentCustomer.NormalCustomerData.TendencyType));
        data.TotalTip += tip + GameManager.Instance.AddFoodTip;
        data.CurrentFood = cookingData;

        int totalPrice = (int)((cookingData.Price + GameManager.Instance.AddFoodPrice * data.CurrentCustomer.CurrentFoodPriceMul) * GameManager.Instance.GetFoodPriceMul(data.FloorType, foodData.FoodType) * GameManager.Instance.GetFoodTypePriceMul(foodData.FoodType));
        data.TotalPrice += totalPrice;
        data.ServingButton.SetData(foodData);

        data.TableState = ETableState.Seating;
        data.OrdersCount -= 1;

        data.CurrentCustomer.StartWaitingForFood();

        //만약 설거지거리가 쌓여있는 상태에서 주문이 발생하면 만족도 -4점 감소
        if(!UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, data.FloorType))
        {
            UserInfo.AddSatisfaction(UserInfo.CurrentStage, -4);
        }

        //만약 주문 요리와 손님의 등장요리가 같다면 만족도 2점 증가
        if (data.CurrentFood.FoodData.Id.Equals(data.CurrentCustomer.NormalCustomerData.RequiredDish))
        {
            data.Satisfaction += 2;
        }

        UpdateTable();
    }




    public void OnCustomerOrder(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        string orderFoodId = data.CurrentFood.FoodData.Id;
        if (!UserInfo.IsGiveRecipe(orderFoodId))
        {
            AngerExitCustomer(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        _kitchenSystem.EqueueFood(data.FloorType, data.CurrentFood);
        data.TableState = ETableState.WaitFood;
        UpdateTable();
    }

    public void OnWaitFood(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        data.TableState = ETableState.WaitFood;
        UpdateTable();
    }


    public void OnServing(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        data.CurrentCustomer.StopWaitingForFood();
        FoodData foodData = FoodDataManager.Instance.GetFoodData(data.CurrentFood.FoodData.Id);
        data.TableState = ETableState.Eating;
        StartCoroutine(EatRoutine(data, foodData));
        UpdateTable();
    }


    private IEnumerator EatRoutine(TableData data, FoodData foodData)
    {
        DebugLog.Log(data.name + " 11");
        yield return YieldCache.WaitForSeconds(0.5f);
        if (data.CurrentCustomer == null)
            yield break;

        DebugLog.Log(data.name + " 22");
        data.CurrentCustomer.transform.position = data.ChairTrs[data.SitIndex].position;
        data.CurrentCustomer.ChangeState(CustomerState.Eat);
        data.CurrentCustomer.ShowFood(foodData.Sprite);

        yield return YieldCache.WaitForSeconds(1.5f);
        DebugLog.Log(data.name + " 33");

        if (data.OrdersCount <= 0)
        {
            EndEat(data);
            DebugLog.Log(data.name + " 44");
        }

        else
        {
            OnCustomerSeating(data);
            DebugLog.Log(data.name + " 55");
        }


    }

    public void OnUseStaff(TableData data)
    {
        data.TableState = ETableState.UseStaff;
        UpdateTable();
    }

    public void OnServigStaff(TableData data)
    {
        data.TableState = ETableState.StaffServing;
        UpdateTable();
    }


    public void ExitCustomer(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;
        exitCustomer.StopWaitingForFood();
        exitCustomer.StopWaiting();
        Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
        customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
        exitCustomer.transform.position = customerPos;
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        UserInfo.AddSatisfaction(UserInfo.CurrentStage, data.Satisfaction);
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        data.Satisfaction = 0;
        DirtyTable(data);
        UpdateTable();
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


        public void AngerExitCustomer(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;
        exitCustomer.StopWaitingForFood();
        exitCustomer.StopWaiting();
        Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
        customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
        exitCustomer.transform.position = customerPos;
        exitCustomer.ChangeState(CustomerState.Idle);
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        exitCustomer.StartAnger();
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        data.TableState = ETableState.Empty;

        UserInfo.AddSatisfaction(UserInfo.CurrentStage, -5);
        UpdateTable();
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            exitCustomer.StopAnger();
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    public void NotFurnitureTable(TableData data)
    {
        if (data.TableType == TableType.Table1)
            return;

        data.TotalTip = 0;
        data.TotalPrice = 0;

        if (data.CurrentCustomer == null)
        {
            data.TableState = ETableState.DontUse;
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;

        if (data.TableState != ETableState.Move)
        {
            Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
            customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
            exitCustomer.transform.position = customerPos;
        }

        data.CurrentCustomer = null;
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();

        data.TableState = ETableState.DontUse;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    private void EndEat(TableData data)
    {
        if (data.CurrentCustomer == null)
        {
            DebugLog.Log("손님이 없습니다.");
            return;
        }

        int tip = data.TotalTip;
        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();
        UserInfo.CustomerVisits(data.CurrentCustomer.CustomerData);
        UserInfo.AddCustomerCount();

        StartCoinAnime(data);
        StartGarbageAnime(data);
        Tween.Wait(0.5f, () =>
        {
            if (data.CurrentCustomer == null)
                return;

            ExitCustomer(data);
 
            UserInfo.AddTip(UserInfo.CurrentStage, tip);
            UpdateTable();
        });
    }


    private void DirtyTable(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        data.TableState = ETableState.NeedCleaning;
        UpdateTable();
    }


    private void StartCoinAnime(TableData data)
    {
        int sitIndex = data.SitIndex;

        //피버상태일 경우 2배의 가격을 지불한다.
        int foodPrice = (int)(data.TotalPrice * (_feverSystem.IsFeverStart ? 2f : 1f));
        data.DropCoinAreas[sitIndex].DropCoin(data.ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0), foodPrice);
        data.TotalPrice = 0;
        data.TotalTip = 0;
    }


    private void StartGarbageAnime(TableData data)
    {
        int sitIndex = data.SitIndex;
        int count = UnityEngine.Random.Range(1, 3);
        float time = 0.02f;
        for (int i = 0; i < count; i++)
        {
            Tween.Wait(time, () =>
            {
                data.DropGarbageArea.DropGarbage(data.ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0));
            });
            time += 0.02f;
        }
    }



    private void OnChangeFurnitureEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        UpdateTable();
    }


    public void UpdateTable()
    {
        OnTableUpdateHandler?.Invoke();
        if (_customerController.IsEmpty())
        {
            _guideButton.gameObject.SetActive(false);
            return;
        }

        for (int i = 0, cnt = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage); i <= cnt; ++i)
        {
            TableData data = GetTableType((ERestaurantFloorType)i, ETableState.Empty);
            if (data == null)
            {
                _guideButton.gameObject.SetActive(false);
                continue;
            }

            _guideButton.gameObject.SetActive(true);
            break;
        }
    }


    /// <summary> 직원 위치를 반환하는 함수</summary>
    public Vector2 GetStaffPos(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (type == EquipStaffType.Chef1 || type == EquipStaffType.Chef2)
        {
            return _kitchenSystem.GetStaffPos(floorType, type);
        }
        else
        {
            return _furnitureSystem.GetStaffPos(floorType, type);
        }
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
        return _furnitureSystem.GetTableType(floorType, state);
    }

    public TableData GetTableType(ETableState state)
    {
        return _furnitureSystem.GetTableType(state);
    }

    public TableData GetTableData(ERestaurantFloorType floorType, TableType type)
    {
        return _furnitureSystem.GetTableType(floorType, type);
    }

    public List<TableData> GetTableDataList(ERestaurantFloorType floorType)
    {
        return _furnitureSystem.GetTableDataList(floorType);
    }

    public List<DropCoinArea> GetDropCoinAreaList(ERestaurantFloorType floorType)
    {
        return _furnitureSystem.GetDropCoinAreaList(floorType);
    }

    public Vector3 GetFoodPos(ERestaurantFloorType floorType, RestaurantType type, Vector3 pos)
    {
        return _furnitureSystem.GetFoodPos(floorType, type, pos);
    }

    public List<TableData> GetTableDataList(ERestaurantFloorType floorType, ETableState state)
    {
        List<TableData> returnDataList = new List<TableData>();
        List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);
        for (int i = 0, cnt = tableDataList.Count; i < cnt; ++i)
        {
            if (tableDataList[i].TableState != state)
                continue;

            returnDataList.Add(tableDataList[i]);
        }

        return returnDataList;
    }

    public TableData GetTableTypeByNeedFood(ERestaurantFloorType floorType, ETableState state)
    {
        List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);

        for (int i = 0, cnt = tableDataList.Count; i < cnt; i++)
        {
            if (tableDataList[i].TableState != state)
                continue;

            if (tableDataList[i].CurrentFood.IsDefault())
                continue;

            if (!UserInfo.IsGiveRecipe(tableDataList[i].CurrentFood.FoodData.Id))
                continue;

            return tableDataList[i];
        }

        return null;
    }




    public DropGarbageArea GetMinDistanceGarbageArea(ERestaurantFloorType floorType, Vector3 startPos)
    {
        Vector3 targetDoorPos = GetDoorPos(RestaurantType.Hall, startPos);
        DropGarbageArea minEqualArea = null;
        DropGarbageArea minNotEqualArea = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var area in _furnitureSystem.GetDropGarbageAreaList(floorType))
        {
            if (area.Count <= 0)
                continue;

            Vector3 doorPos = GetDoorPos(RestaurantType.Hall, area.transform.position);
            float doorDistance = Vector3.Distance(targetDoorPos, doorPos);
            float startDistance = Vector2.Distance(area.transform.position, startPos);

            if (doorDistance <= 0.5f)
            {
                if (startDistance < minEqualDist)
                {
                    minEqualDist = startDistance;
                    minEqualArea = area;
                }
            }
            else
            {
                float areaDoorDistance = Vector3.Distance(area.transform.position, doorPos);
                if (startDistance < minNotEqualDist)
                {
                    minNotEqualDist = areaDoorDistance;
                    minNotEqualArea = area;
                }
            }
        }

        return minEqualArea ?? minNotEqualArea;
    }

    public DropCoinArea GetMinDistanceCoinArea(ERestaurantFloorType floorType, Vector3 startPos)
    {
        Vector3 targetDoorPos = GetDoorPos(RestaurantType.Hall, startPos);
        DropCoinArea minEqualArea = null;
        DropCoinArea minNotEqualArea = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var area in _furnitureSystem.GetDropCoinAreaList(floorType))
        {
            if (area.Count <= 0)
                continue;

            Vector3 doorPos = GetDoorPos(RestaurantType.Hall, area.transform.position);
            float doorDistance = Vector3.Distance(targetDoorPos, doorPos);
            float startDistance = Vector2.Distance(area.transform.position, startPos);

            if (doorDistance <= 0.5f)
            {
                if (startDistance < minEqualDist)
                {
                    minEqualDist = startDistance;
                    minEqualArea = area;
                }
            }
            else
            {
                float areaDoorDistance = Vector3.Distance(area.transform.position, doorPos);
                if (startDistance < minNotEqualDist)
                {
                    minNotEqualDist = areaDoorDistance;
                    minNotEqualArea = area;
                }
            }
        }

        return minEqualArea ?? minNotEqualArea;
    }


    public TableData GetMinDistanceTable(ERestaurantFloorType floorType, Vector3 startPos, List<TableData> tableDataList)
    {
        Vector3 targetDoorPos = GetDoorPos(RestaurantType.Hall, startPos);
        TableData minEqualTable = null;
        TableData minNotEqualTable = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var table in tableDataList)
        {
            Vector3 doorPos = GetDoorPos(RestaurantType.Hall, table.transform.position);
            float doorDistance = Vector3.Distance(targetDoorPos, doorPos);
            float startDistance = Vector2.Distance(table.transform.position, startPos);

            if (doorDistance <= 1f)
            {
                if (startDistance < minEqualDist)
                {
                    minEqualDist = startDistance;
                    minEqualTable = table;
                }
            }
            else
            {
                float tableDoorDistance = Vector3.Distance(table.transform.position, doorPos);
                if (tableDoorDistance < minNotEqualDist)
                {
                    minNotEqualDist = tableDoorDistance;
                    minNotEqualTable = table;
                }
            }
        }

        return minEqualTable ?? minNotEqualTable;
    }


    public KitchenBurnerData GetMinDistanceBurner(ERestaurantFloorType floorType, Vector3 startPos, List<KitchenBurnerData> dataList)
    {
        Vector3 targetDoorPos = GetDoorPos(RestaurantType.Kitchen, startPos);
        KitchenBurnerData minEqualData = null;
        KitchenBurnerData minNotEqualData = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var data in dataList)
        {
            Vector3 doorPos = GetDoorPos(RestaurantType.Kitchen, data.KitchenUtensil.transform.position);
            float doorDistance = Vector3.Distance(targetDoorPos, doorPos);
            float startDistance = Vector2.Distance(data.KitchenUtensil.transform.position, startPos);

            if (doorDistance <= 1f)
            {
                if (startDistance < minEqualDist)
                {
                    minEqualDist = startDistance;
                    minEqualData = data;
                }
            }
            else
            {
                float tableDoorDistance = Vector3.Distance(data.KitchenUtensil.transform.position, doorPos);
                if (tableDoorDistance < minNotEqualDist)
                {
                    minNotEqualDist = tableDoorDistance;
                    minNotEqualData = data;
                }
            }
        }

        return minEqualData ?? minNotEqualData;
    }


    private ERestaurantFloorType GetWeightRandomChoiceFloor(NormalCustomer customer)
    {
        //선호 음식 가중치를 계산 후 랜덤으로 Floor을 지정한다.
        WeightedRandom<FoodType> foodRandom = customer.GetFoodTypeWeightDic();

        Dictionary<FoodType, List<ERestaurantFloorType>> floorDataDic = new();
        HashSet<FoodType> enabledFoodTypes = new();

        int maxFloorIndex = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        for (int i = 0; i <= maxFloorIndex; i++)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            TableData tableData = GetTableType(floor, ETableState.Empty);
            if (tableData == null) continue;

            FoodType foodType = _furnitureSystem.GetFoodType(floor);
            if (!floorDataDic.ContainsKey(foodType))
                floorDataDic.Add(foodType, new List<ERestaurantFloorType>());

            floorDataDic[foodType].Add(floor);
            foodRandom.Add(foodType, 0.1f);
            enabledFoodTypes.Add(foodType);
        }

        // 활성화되지 않은 FoodType 한 번만 제거 (불필요한 전체 루프 제거)
        foodRandom.RemoveAll(f => !enabledFoodTypes.Contains(f));

        // 확률 기반 FoodType 선택
        FoodType choiceFoodType = foodRandom.GetRamdomItem();
        List<ERestaurantFloorType> availableFloors = floorDataDic[choiceFoodType];
        ERestaurantFloorType choiceFloor = availableFloors[UnityEngine.Random.Range(0, availableFloors.Count)];
        return choiceFloor;
    }

    private void OnDestroy()
    {
        _customerController.OnChangeCustomerHandler -= UpdateTable;
        _customerController.OnGuideCustomerHandler -= UpdateTable;
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
    }
}
