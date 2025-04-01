using Muks.PathFinding.AStar;
using Muks.Tween;
using Muks.WeightedRandom;
using System;
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


    public Vector3 GetDoorPos(Vector3 pos)
    {
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
        _customerController.OnAddCustomerHandler += UpdateTable;
        _customerController.OnGuideCustomerHandler += UpdateTable;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }


    public void OnCustomerGuideEvent(int sitPos = -1)
    {
        if (_customerController.IsEmpty())
            return;

        NormalCustomer customer = _customerController.GetFirstCustomer();
        if (customer == null)
        {
            DebugLog.LogError("손님 정보가 없습니다.");
            return;
        }

        TableData data = GetTableType(ETableState.Empty);
        if (data == null)
        {
            DebugLog.Log("남는 테이블이 없습니다.");
            UpdateTable();
            return;
        }

        ERestaurantFloorType choiceFloor = GetWeightRandomChoiceFloor(customer);
        data = GetTableType(choiceFloor, ETableState.Empty);

        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        sitPos = Mathf.Clamp(sitPos, -1, 1);
        customer.SetVisitFloor(choiceFloor);
        OnCustomerGuide(customer, data, sitPos);

    }

    public void OnCustomerGuideEventPlaySound(int sitPos = -1)
    {
        OnCustomerGuideEvent(sitPos);
        SoundManager.Instance.PlayEffectAudio(_callSound);
    }



    private void OnCustomerGuide(NormalCustomer customer, TableData data, int sitPos = -1)
    {
        data.CurrentCustomer = customer;
        customer.SetLayer("Customer", 0);
        data.TableState = ETableState.Move;
        data.OrdersCount = customer.OrderCount;

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

        Vector3 targetPos = data.ChairTrs[data.SitIndex].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);

        UpdateTable();

        _customerController.GuideCustomer(targetPos, 0, () =>
        {
            Tween.Wait(0.1f, () =>
            {
                if (data.CurrentCustomer == null)
                    return;

                customer.transform.position = data.ChairTrs[data.SitIndex].position;
                data.OrderButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);
                data.ServingButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);

                customer.SetSpriteDir(-data.SitDir);
                customer.SetLayer("SitCustomer", 0);
                customer.ChangeState(CustomerState.Sit);

                Tween.Wait(1f, () =>
                {
                    if (data.CurrentCustomer == null)
                        return;

                    if (customer.CustomerData.MaxDiscomfortIndex < _totalGarbageCount)
                    {
                        data.CurrentCustomer = null;
                        data.TableState = ETableState.Empty;
                        customer.transform.position = data.ChairTrs[data.SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
                        customer.ChangeState(CustomerState.Idle);
                        customer.StartAnger();
                        customer.HideFood();
                        customer.Move(GameManager.Instance.OutDoorPos, 0, () =>
                        {
                            customer.StopAnger();
                            ObjectPoolManager.Instance.DespawnNormalCustomer(customer);
                            customer = null;
                        });

                        UpdateTable();
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

        if (data.OrdersCount <= 0)
            EndEat(data);

        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();

        FoodData foodData = data.CurrentCustomer.CustomerData.GetRandomOrderFood();

        bool isMiniGameNeeded = foodData.MiniGameNeeded && string.IsNullOrWhiteSpace(foodData.NeedItem);
        if (isMiniGameNeeded && !UserInfo.IsMiniGameTutorialClear)
        {
            _miniGameTutorial.StartTutorial(foodData, data.transform);
        }

        int foodLevel = UserInfo.GetRecipeLevel(foodData);
        CookingData cookingData = new CookingData(foodData, Mathf.Clamp(0.5f, foodData.GetCookingTime(foodLevel) - GameManager.Instance.SubCookingTime, 100000), foodData.GetSellPrice(foodLevel), () =>
        {
            if (data.TableState != ETableState.WaitFood || data.CurrentCustomer == null)
                return;

            data.TableState = ETableState.CanServing;
            data.ServingButton.SetData(foodData);
            foodData = null;
            UpdateTable();
        });

        int tip = Mathf.FloorToInt(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul);
        data.TotalTip += tip + GameManager.Instance.AddFoodTip;
        data.CurrentFood = cookingData;

        int totalPrice = (int)((cookingData.Price + GameManager.Instance.AddFoodPrice * data.CurrentCustomer.CurrentFoodPriceMul) * GameManager.Instance.GetFoodPriceMul(data.FloorType, foodData.FoodType) * GameManager.Instance.GetFoodTypePriceMul(foodData.FoodType));
        data.TotalPrice += totalPrice;
        data.ServingButton.SetData(foodData);

        data.TableState = ETableState.Seating;
        data.OrdersCount -= 1;
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
            NormalCustomer currentCustomer = data.CurrentCustomer;
            currentCustomer.SetLayer("Customer", 0);
            data.CurrentCustomer = null;
            data.TableState = ETableState.Empty;
            currentCustomer.transform.position = data.ChairTrs[data.SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
            currentCustomer.ChangeState(CustomerState.Idle);
            currentCustomer.StartAnger();
            currentCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
            {
                currentCustomer.StopAnger();
                ObjectPoolManager.Instance.DespawnNormalCustomer(currentCustomer);
                currentCustomer = null;
            });

            UpdateTable();
            return;
        }

        _kitchenSystem.EqueueFood(data.FloorType, data.CurrentFood);
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

        FoodData foodData = FoodDataManager.Instance.GetFoodData(data.CurrentFood.FoodData.Id);
        data.TableState = ETableState.Eating;
        Tween.Wait(0.5f, () =>
        {
            if (data.CurrentCustomer == null)
                return;
            data.CurrentCustomer.transform.position = data.ChairTrs[data.SitIndex].position;
            data.CurrentCustomer.ChangeState(CustomerState.Eat);
            data.CurrentCustomer.ShowFood(foodData.Sprite);
            Tween.Wait(1.5f, () =>
            {
                if (data.CurrentCustomer == null)
                    return;

                if (data.OrdersCount <= 0)
                    EndEat(data);
                else
                    OnCustomerSeating(data);
            });
        });
        UpdateTable();
    }

    public void OnUseStaff(TableData data)
    {
        data.TableState = ETableState.UseStaff;
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
        exitCustomer.transform.position = data.ChairTrs[data.SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        DirtyTable(data);
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
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
            exitCustomer.transform.position = data.ChairTrs[data.SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);

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
        int tip = data.TotalTip;
        int totalPrice = data.TotalPrice;

        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();
        StartCoinAnime(data);
        StartGarbageAnime(data);
        Tween.Wait(0.5f, () =>
        {
            if (data.CurrentCustomer == null)
                return;

            ExitCustomer(data);
            UserInfo.AddCustomerCount();
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
        int foodPrice = data.TotalPrice;

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


    /// <summary> 직원 위치를 반환하는 함수(경호원, 청소부, 매니저, 셰프만 가능)</summary>
    public Vector2 GetStaffPos(ERestaurantFloorType floorType, EquipStaffType type)
    {
        return _furnitureSystem.GetStaffPos(floorType, type);
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

    public Vector3 GetFoodPos(ERestaurantFloorType floorType, RestaurantType type)
    {
        return _furnitureSystem.GetFoodPos(floorType, type);
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
        Vector3 targetDoorPos = GetDoorPos(startPos);
        DropGarbageArea minEqualArea = null;
        DropGarbageArea minNotEqualArea = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var area in _furnitureSystem.GetDropGarbageAreaList(floorType))
        {
            if (area.Count <= 0)
                continue;

            Vector3 doorPos = GetDoorPos(area.transform.position);
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
        Vector3 targetDoorPos = GetDoorPos(startPos);
        DropCoinArea minEqualArea = null;
        DropCoinArea minNotEqualArea = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var area in _furnitureSystem.GetDropCoinAreaList(floorType))
        {
            if (area.Count <= 0)
                continue;

            Vector3 doorPos = GetDoorPos(area.transform.position);
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
        Vector3 targetDoorPos = GetDoorPos(startPos);
        TableData minEqualTable = null;
        TableData minNotEqualTable = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var table in tableDataList)
        {
            Vector3 doorPos = GetDoorPos(table.transform.position);
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
        _customerController.OnAddCustomerHandler -= UpdateTable;
        _customerController.OnGuideCustomerHandler -= UpdateTable;
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
    }
}
