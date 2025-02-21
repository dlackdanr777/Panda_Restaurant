using Muks.PathFinding.AStar;
using Muks.Tween;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    public event Action OnTableUpdateHandler;


    [Range(0, 10)]
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
        _guideButton.onClick.AddListener(() => OnCustomerGuideButtonClicked(-1));

        UpdateTable();
        _customerController.OnAddCustomerHandler += UpdateTable;
        _customerController.OnGuideCustomerHandler += UpdateTable;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }


    public void OnCustomerGuide(TableData data, int sitPos = -1)
    {
        if (_customerController.IsEmpty())
            return;

        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (data.TableState != ETableState.Empty)
            return;

        NormalCustomer currentCustomer = _customerController.GetFirstCustomer();
        data.CurrentCustomer = currentCustomer;
        currentCustomer.SetLayer("Customer", 0);
        data.TableState = ETableState.Move;
        data.OrdersCount = currentCustomer.OrderCount;

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

                currentCustomer.transform.position = data.ChairTrs[data.SitIndex].position;
                data.OrderButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);
                data.ServingButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);

                currentCustomer.SetSpriteDir(-data.SitDir);
                currentCustomer.SetLayer("SitCustomer", 0);
                currentCustomer.ChangeState(CustomerState.Sit);

                Tween.Wait(1f, () =>
                {
                    if (data.CurrentCustomer == null)
                        return;

                    if (currentCustomer.CustomerData.MaxDiscomfortIndex < _totalGarbageCount)
                    {
                        data.CurrentCustomer = null;
                        data.TableState = ETableState.Empty;
                        currentCustomer.transform.position = data.ChairTrs[data.SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
                        currentCustomer.ChangeState(CustomerState.Idle);
                        currentCustomer.StartAnger();
                        currentCustomer.HideFood();
                        currentCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
                        {
                            currentCustomer.StopAnger();
                            ObjectPoolManager.Instance.DespawnNormalCustomer(currentCustomer);
                            currentCustomer = null;
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

        string foodDataId = data.CurrentCustomer.CustomerData.GetRandomOrderFood();
        FoodData foodData = FoodDataManager.Instance.GetFoodData(foodDataId);

        bool isMiniGameNeeded = foodData.MiniGameNeeded && string.IsNullOrWhiteSpace(foodData.NeedItem);
        if (isMiniGameNeeded && !UserInfo.IsMiniGameTutorialClear)
        {
            _miniGameTutorial.StartTutorial(foodData, data.transform);
        }

        int foodLevel = UserInfo.GetRecipeLevel(foodDataId);
        CookingData cookingData = new CookingData(foodData.Id, Mathf.Clamp(0.5f, foodData.GetCookingTime(foodLevel) - GameManager.Instance.SubCookingTime, 100000), foodData.GetSellPrice(foodLevel), foodData.Sprite, () =>
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

        int totalPrice = (int)((cookingData.Price + GameManager.Instance.AddFoodPrice * data.CurrentCustomer.CurrentFoodPriceMul) * GameManager.Instance.FoodPriceMul * GameManager.Instance.GetFoodTypePriceMul(foodData.FoodType));
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

        string orderFoodId = data.CurrentFood.Id;
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

        FoodData foodData = FoodDataManager.Instance.GetFoodData(data.CurrentFood.Id);
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
        data.TableState = ETableState.Empty;
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    public void NotFurnitureTable(TableData data)
    {
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


    public Vector2 GetStaffPos(TableData data, StaffType type)
    {
        return _furnitureSystem.GetStaffPos(data, type);
    }

    /// <summary> 직원 위치를 반환하는 함수(경호원, 청소부, 매니저, 셰프만 가능)</summary>
    public Vector2 GetStaffPos(ERestaurantFloorType floorType, StaffType type)
    {
        return _furnitureSystem.GetStaffPos(floorType, type);
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
        return _furnitureSystem.GetTableType(floorType, state);
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

            if (!UserInfo.IsGiveRecipe(tableDataList[i].CurrentFood.Id))
                continue;

            return tableDataList[i];
        }

        return null;
    }

   


    public DropGarbageArea GetMinDistanceGarbageArea(ERestaurantFloorType floorType, Vector3 startPos)
    {
        Vector3 targetDoorPos = GetDoorPos(startPos);
        List<DropGarbageArea> equalFloorArea = new List<DropGarbageArea>();
        List<DropGarbageArea> notEqualFloorArea = new List<DropGarbageArea>();

        List<DropGarbageArea> floorGarbageAreaList = _furnitureSystem.GetDropGarbageAreaList(floorType);
        for (int i = 0, cnt = floorGarbageAreaList.Count; i < cnt; i++)
        {
            Vector3 doorPos = GetDoorPos(floorGarbageAreaList[i].transform.position);
            if (floorGarbageAreaList[i].Count <= 0)
                continue;

            if (Vector3.Distance(targetDoorPos, doorPos) <= 0.5f)
                equalFloorArea.Add(floorGarbageAreaList[i]);

            else
                notEqualFloorArea.Add(floorGarbageAreaList[i]);
        }


        if (equalFloorArea.Count == 0 && notEqualFloorArea.Count == 0)
            return null;

        else if (0 < equalFloorArea.Count)
        {
            float minDis = 10000000;
            int minIndex = 0;
            for (int i = 0, cnt = equalFloorArea.Count; i < cnt; i++)
            {
                if (Vector2.Distance(equalFloorArea[i].transform.position, startPos) < minDis)
                {
                    minDis = Vector2.Distance(equalFloorArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }
            return equalFloorArea[minIndex];
        }


        else
        {
            float minDis = 10000000;
            int minIndex = 0;

            for (int i = 0, cnt = notEqualFloorArea.Count; i < cnt; i++)
            {
                Vector3 doorPos = GetDoorPos(floorGarbageAreaList[i].transform.position);
                if (Vector2.Distance(notEqualFloorArea[i].transform.position, doorPos) < minDis)
                {
                    minDis = Vector2.Distance(notEqualFloorArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }

            return notEqualFloorArea[minIndex];
        }
    }

    public void OnCustomerGuideButtonClicked(int sitPos = -1)
    {
        TableData data = GetTableType(ERestaurantFloorType.Floor1, ETableState.Empty);

        if (data == null)
            return;

        sitPos = Mathf.Clamp(sitPos, -1, 1);
        OnCustomerGuide(data, sitPos);
        SoundManager.Instance.PlayEffectAudio(_callSound);
    }


    private void OnDestroy()
    {
        _customerController.OnAddCustomerHandler -= UpdateTable;
        _customerController.OnGuideCustomerHandler -= UpdateTable;
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
    }
}
