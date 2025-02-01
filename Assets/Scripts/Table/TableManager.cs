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

    [SerializeField] private TableData[] _tableDatas;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private KitchenSystem _kitchenSystem;


    [Space]
    [Header("Button Option")]
    [SerializeField] private Transform _buttonParent;
    [SerializeField] private Button _guideButton;
    [SerializeField] private TableButton _orderButtonPrefab;
    [SerializeField] private TableButton _servingButtonPrefab;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _callSound;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private GachaTutorial _miniGameTutorial;

    private TableButton[] _orderButtons;
    private TableButton[] _servingButtons;

    private int _totalGarbageCount => ObjectPoolManager.Instance.GetEnabledGarbageCount();
    private List<DropGarbageArea> _dropGarbageAreaList = new List<DropGarbageArea>();

    private void Start()
    {
        Init();
        UpdateTable();
    }


    private void Init()
    {
        _guideButton.onClick.AddListener(() => OnCustomerGuideButtonClicked(-1));

        List<TableData> allTableDataList = _furnitureSystem.GetAllTableDataList();
        for(int i = 0, cnt = allTableDataList.Count; i < cnt; ++i)
        {
            _dropGarbageAreaList.Add(allTableDataList[i].DropGarbageArea);
        }

        UpdateTable();
        _customerController.OnAddCustomerHandler += UpdateTable;
        _customerController.OnGuideCustomerHandler += UpdateTable;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }



    public int GetTableType(ETableState state)
    {
        for(int i = 0, cnt = _tableDatas.Length; i <  cnt; i++)
        {
            if (_tableDatas[i].TableState == state)
                return i;
        }

        return -1;
    }


    public int GetTableTypeByNeedFood(ETableState state)
    {
        for (int i = 0, cnt = _tableDatas.Length; i < cnt; i++)
        {
            if (_tableDatas[i].TableState != state)
                continue;

            if (_tableDatas[i].CurrentFood.IsDefault())
                continue;

            if (!UserInfo.IsGiveRecipe(_tableDatas[i].CurrentFood.Id))
                continue;

            return i;
        }

        return -1;
    }

    public TableData GetTableData(int index)
    {
        if(index < 0 || _tableDatas.Length <= index)
        {
            DebugLog.LogError("테이블 범위를 벗어나는 인덱스입니다: " + index + " (테이블 배열 길이: " + _tableDatas.Length + ")");
            return null;
        }

        return _tableDatas[index];
    }

    public List<NormalCustomer> GetSitCustomerList()
    {
        List<NormalCustomer> returnDataList = new List<NormalCustomer>();
        for(int i = 0, cnt =  _tableDatas.Length; i < cnt; ++i)
        {
            returnDataList.Add(_tableDatas[i].TableState != ETableState.Move ? _tableDatas[i].CurrentCustomer : null);
        }

        return returnDataList;
    }

    public List<DropCoinArea> GetDropCoinAreaList()
    {
        List<DropCoinArea> dropCoinAreaList = new List<DropCoinArea>();
        for (int i = 0, cnt = _tableDatas.Length; i < cnt; ++i)
        {
            for(int j = 0, cntJ = _tableDatas[i].DropCoinAreas.Length; j < cntJ; ++j)
            {
                if (_tableDatas[i].DropCoinAreas[j] == null)
                    continue;

                dropCoinAreaList.Add(_tableDatas[i].DropCoinAreas[j]);
            }
        }

        return dropCoinAreaList;
    }


/*    public void UpdateTable()
    {
        int index = GetTableType(ETableState.NotUse);

        if (index == -1)
            _guideButton.gameObject.SetActive(false);

        else if(_customerController.IsEmpty())
            _guideButton.gameObject.SetActive(false);

        else 
            _guideButton.gameObject.SetActive(true);


        TableData data = null;
        for (int i = 0, cnt = _ownedTableCount; i < cnt; ++i)
        {
            data = _tableDatas[i];

            //TODO: 나중에 바꾸기
            if(UserInfo.GetEquipFurniture(ERestaurantFloorType.Floor1, (FurnitureType)i) == null)
            {
                NotFurnitureTable(i);
            }

            else if(data.TableState == ETableState.DontUse)
            {
                data.TableState = ETableState.NotUse;
            }

            if (data.TableState == ETableState.NotUse)
            {
                _orderButtons[i].gameObject.SetActive(false);
                _servingButtons[i].gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Move || data.TableState == ETableState.WaitFood || data.TableState == ETableState.Eating || data.TableState == ETableState.UseStaff || data.TableState == ETableState.DontUse)
            {
                _orderButtons[i].gameObject.SetActive(false);
                _servingButtons[i].gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Seating)
            {
                _orderButtons[i].gameObject.SetActive(true);
                _servingButtons[i].gameObject.SetActive(false);
            }
            else if(data.TableState == ETableState.CanServing)
            {
                _servingButtons[i].gameObject.SetActive(true);
                _orderButtons[i].gameObject.SetActive(false);
            }
        }
    }*/


    public void OnCustomerGuide(int index, int sitPos = -1)
    {
        if (_customerController.IsEmpty())
            return;

        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        if (_tableDatas[index].TableState != ETableState.NotUse)
            return;

        NormalCustomer currentCustomer = _customerController.GetFirstCustomer();
        _tableDatas[index].CurrentCustomer = currentCustomer;
        currentCustomer.SetLayer("Customer", 0);
        _tableDatas[index].TableState = ETableState.Move;
        _tableDatas[index].OrdersCount = currentCustomer.OrderCount;

        if(sitPos != 0 && sitPos != 1)
        {
            int randInt = UnityEngine.Random.Range(0, _tableDatas[index].ChairTrs.Length);
            _tableDatas[index].SitDir = randInt == 0 ? -1 : 1;
            _tableDatas[index].SitIndex = randInt;
        }
        else
        {
            _tableDatas[index].SitDir = sitPos == 0 ? -1 : 1;
            _tableDatas[index].SitIndex = sitPos;
        }

        Vector3 targetPos = _tableDatas[index].ChairTrs[_tableDatas[index].SitIndex].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);

        UpdateTable();

        _customerController.GuideCustomer(targetPos, 0, () =>
        {
            Tween.Wait(0.1f, () =>
            {
                if (_tableDatas[index].CurrentCustomer == null)
                    return;

                currentCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitIndex].position;
                _orderButtons[index].SetWorldTransform(_tableDatas[index].ChairTrs[_tableDatas[index].SitIndex]);
                _servingButtons[index].SetWorldTransform(_tableDatas[index].ChairTrs[_tableDatas[index].SitIndex]);

                currentCustomer.SetSpriteDir(-_tableDatas[index].SitDir);
                currentCustomer.SetLayer("SitCustomer", 0);
                currentCustomer.ChangeState(CustomerState.Sit);

                Tween.Wait(1f, () =>
                {
                    if (_tableDatas[index].CurrentCustomer == null)
                        return;

                    if (currentCustomer.CustomerData.MaxDiscomfortIndex < _totalGarbageCount)
                    {
                        _tableDatas[index].CurrentCustomer = null;
                        _tableDatas[index].TableState = ETableState.NotUse;
                        currentCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
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

                    OnCustomerSeating(index);
                });
            });
        });
    }

    public void OnCustomerSeating(int index)
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        if (_tableDatas[index].OrdersCount <= 0)
            EndEat(index);

        _tableDatas[index].CurrentCustomer.ChangeState(CustomerState.Idle);
        _tableDatas[index].CurrentCustomer.HideFood();

        string foodDataId = _tableDatas[index].CurrentCustomer.CustomerData.GetRandomOrderFood();
        FoodData foodData = FoodDataManager.Instance.GetFoodData(foodDataId);
        
        bool isMiniGameNeeded = foodData.MiniGameNeeded && string.IsNullOrWhiteSpace(foodData.NeedItem);
        if(isMiniGameNeeded && !UserInfo.IsMiniGameTutorialClear)
        {
            _miniGameTutorial.StartTutorial(foodData, _orderButtons[index].transform);
        }

        int foodLevel = UserInfo.GetRecipeLevel(foodDataId);
        CookingData data = new CookingData(foodData.Id, Mathf.Clamp(0.5f, foodData.GetCookingTime(foodLevel) - GameManager.Instance.SubCookingTime, 100000), foodData.GetSellPrice(foodLevel), foodData.Sprite, () =>
        {
            if (_tableDatas[index].TableState != ETableState.WaitFood || _tableDatas[index].CurrentCustomer == null)
                return;

            _tableDatas[index].TableState = ETableState.CanServing;
            _servingButtons[index].SetData(foodData);
            foodData = null;
            UpdateTable();
        });

        int tip = Mathf.FloorToInt(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul);
        _tableDatas[index].TotalTip += tip + GameManager.Instance.AddFoodTip;
        _tableDatas[index].CurrentFood = data;

        int totalPrice = (int)((data.Price + GameManager.Instance.AddFoodPrice * _tableDatas[index].CurrentCustomer.CurrentFoodPriceMul) * GameManager.Instance.FoodPriceMul * GameManager.Instance.GetFoodTypePriceMul(foodData.FoodType));
        _tableDatas[index].TotalPrice += totalPrice;
        _orderButtons[index].SetData(foodData);

        _tableDatas[index].TableState = ETableState.Seating;
        _tableDatas[index].OrdersCount -= 1;
        UpdateTable();
    }


    public void OnCustomerOrder(int index)
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        string orderFoodId = _tableDatas[index].CurrentFood.Id;
        if(!UserInfo.IsGiveRecipe(orderFoodId))
        {
            NormalCustomer currentCustomer = _tableDatas[index].CurrentCustomer;
            currentCustomer.SetLayer("Customer", 0);
            _tableDatas[index].CurrentCustomer = null;
            _tableDatas[index].TableState = ETableState.NotUse;
            currentCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
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

        //TODO: 나중에 층수 추가시 변경 예정
        _kitchenSystem.EqueueFood(ERestaurantFloorType.Floor1, _tableDatas[index].CurrentFood);
        _tableDatas[index].TableState = ETableState.WaitFood;
        UpdateTable();
    }

    public void OnServing(int index)
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        FoodData foodData = FoodDataManager.Instance.GetFoodData(_tableDatas[index].CurrentFood.Id);
        _tableDatas[index].TableState = ETableState.Eating;
        Tween.Wait(0.5f, () =>
        {
            if (_tableDatas[index].CurrentCustomer == null)
                return;
            _tableDatas[index].CurrentCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitIndex].position;
            _tableDatas[index].CurrentCustomer.ChangeState(CustomerState.Eat);
            _tableDatas[index].CurrentCustomer.ShowFood(foodData.Sprite);
            Tween.Wait(1.5f, () =>
            {
                if (_tableDatas[index].CurrentCustomer == null)
                    return;

                if (_tableDatas[index].OrdersCount <= 0)
                    EndEat(index);
                else
                    OnCustomerSeating(index);
            });
        });
        UpdateTable();
    }

    private void EndEat(int index)
    {
        int tip = _tableDatas[index].TotalTip;
        int totalPrice = _tableDatas[index].TotalPrice;

        _tableDatas[index].CurrentCustomer.ChangeState(CustomerState.Idle);
        _tableDatas[index].CurrentCustomer.HideFood();
        StartCoinAnime(index);
        StartGarbageAnime(index);

        Tween.Wait(0.5f, () =>
        {
            if (_tableDatas[index].CurrentCustomer == null)
                return;

            ExitCustomer(index);
            UserInfo.AddCustomerCount();
            UserInfo.AddTip(tip);
            UpdateTable();
        });
    }


    public void OnUseStaff(int index)
    {
        _tableDatas[index].TableState = ETableState.UseStaff;
        UpdateTable();
    }


    public void ExitCustomer(int index)
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        NormalCustomer exitCustomer = _tableDatas[index].CurrentCustomer;
        exitCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        _tableDatas[index].TableState = ETableState.NotUse;
        _tableDatas[index].CurrentCustomer = null;
        _tableDatas[index].TotalTip = 0;
        _tableDatas[index].TotalPrice = 0;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () => 
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }

    private void NotFurnitureTable(int index)
    {
        _tableDatas[index].TotalTip = 0;
        _tableDatas[index].TotalPrice = 0;

        if (_tableDatas[index].CurrentCustomer == null)
        {
            _tableDatas[index].TableState = ETableState.DontUse;
            return;
        }

        NormalCustomer exitCustomer = _tableDatas[index].CurrentCustomer;

        if (_tableDatas[index].TableState != ETableState.Move)
            exitCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);

        _tableDatas[index].CurrentCustomer = null;
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();

        _tableDatas[index].TableState = ETableState.DontUse;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    private void StartCoinAnime(int index)
    {
        int sitIndex = _tableDatas[index].SitIndex;
        int foodPrice = _tableDatas[index].TotalPrice;

        _tableDatas[index].DropCoinAreas[sitIndex].DropCoin(_tableDatas[index].ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0), foodPrice);
        _tableDatas[index].TotalPrice = 0;
        _tableDatas[index].TotalTip = 0;
    }


    private void StartGarbageAnime(int index)
    {
        int sitIndex = _tableDatas[index].SitIndex;
        int count = UnityEngine.Random.Range(1, 3);
        float time = 0.02f;
        for (int i = 0; i < count; i++)
        {
            Tween.Wait(time, () =>
            {
                _tableDatas[index].DropGarbageArea.DropGarbage(_tableDatas[index].ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0));
            });
            time += 0.02f;
        }
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

        if (data.TableState != ETableState.NotUse)
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
                        data.TableState = ETableState.NotUse;
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
            data.TableState = ETableState.NotUse;
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

        //TODO: 나중에 층수 추가시 변경 예정
        _kitchenSystem.EqueueFood(ERestaurantFloorType.Floor1, data.CurrentFood);
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
        data.TableState = ETableState.NotUse;
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
            UserInfo.AddTip(tip);
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
        int index = GetTableType(ETableState.NotUse);

        if (index == -1)
            _guideButton.gameObject.SetActive(false);

        else if (_customerController.IsEmpty())
            _guideButton.gameObject.SetActive(false);

        else
            _guideButton.gameObject.SetActive(true);

        OnTableUpdateHandler?.Invoke();
    }


    public Vector2 GetStaffPos(TableData data, StaffType type)
    {
        return _furnitureSystem.GetStaffPos(data, type);
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
        return _furnitureSystem.GetTableType(floorType, state);
    }


    public DropGarbageArea GetMinDistanceGarbageArea(Vector3 startPos)
    {
        int moveObjFloor = AStar.Instance.GetTransformFloor(startPos);
        List<DropGarbageArea> equalFloorGarbageArea = new List<DropGarbageArea>();
        List<DropGarbageArea> notEqualFloorGarbageArea = new List<DropGarbageArea>();

        for (int i = 0, cnt = _dropGarbageAreaList.Count; i < cnt; i++)
        {
            int targetFloor = AStar.Instance.GetTransformFloor(_dropGarbageAreaList[i].transform.position);
            if (_dropGarbageAreaList[i].Count <= 0)
                continue;

            if (moveObjFloor == targetFloor)
                equalFloorGarbageArea.Add(_dropGarbageAreaList[i]);

            else if (moveObjFloor != targetFloor)
                notEqualFloorGarbageArea.Add(_dropGarbageAreaList[i]);
        }


        if (equalFloorGarbageArea.Count == 0 && notEqualFloorGarbageArea.Count == 0)
            return null;

        else if (0 < equalFloorGarbageArea.Count)
        {
            float minDis = 10000000;
            int minIndex = 0;
            for (int i = 0, cnt = equalFloorGarbageArea.Count; i < cnt; i++)
            {
                if (Vector2.Distance(equalFloorGarbageArea[i].transform.position, startPos) < minDis)
                {
                    minDis = Vector2.Distance(equalFloorGarbageArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }
            return equalFloorGarbageArea[minIndex];
        }


        else
        {
            float minDis = 10000000;
            int minIndex = 0;

            for (int i = 0, cnt = notEqualFloorGarbageArea.Count; i < cnt; i++)
            {
                int targetFloor = AStar.Instance.GetTransformFloor(notEqualFloorGarbageArea[i].transform.position);
                Vector2 floorDoorPos = AStar.Instance.GetFloorPos(targetFloor);

                if (Vector2.Distance(notEqualFloorGarbageArea[i].transform.position, floorDoorPos) < minDis)
                {
                    minDis = Vector2.Distance(notEqualFloorGarbageArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }

            return notEqualFloorGarbageArea[minIndex];
        }
    }

    public void OnCustomerGuideButtonClicked(int sitPos = -1)
    {
        TableData data = GetTableType(ERestaurantFloorType.Floor1, ETableState.NotUse);

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
