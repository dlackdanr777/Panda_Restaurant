using Muks.DataBind;
using Muks.PathFinding.AStar;
using Muks.Tween;
using Muks.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    [Range(0, 10)]
    [Header("Transform")]
    [SerializeField] private int _ownedTableCount;
    [SerializeField] private Transform _cashTableTr;
    [SerializeField] private Transform _marketerTr;
    [SerializeField] private Transform _cleanerWaitTr;
    public Transform CleanerWaitTr => _cleanerWaitTr;
    [SerializeField] private Transform _guardTr;
    [SerializeField] private Transform _moneyUITr;

    [Space]
    [Header("Components")]

    [SerializeField] private TableData[] _tableDatas;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private KitchenSystem _kitchenSystem;


    [Space]
    [Header("Button Option")]
    [SerializeField] private Transform _buttonParent;
    [SerializeField] private Button _guideButton;
    [SerializeField] private UIButtonAndImage _orderButtonPrefab;
    [SerializeField] private UIButtonAndImage _servingButtonPrefab;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private GachaTutorial _miniGameTutorial;

    private WorldToSceenPosition[] _orderButtonsPos;
    private WorldToSceenPosition[] _sevingButtonsPos;
    private UIButtonAndImage[] _orderButtons;
    private UIButtonAndImage[] _servingButtons;

    private int _totalGarbageCount => ObjectPoolManager.Instance.GetEnabledGarbageCount();

    private List<DropGarbageArea> _dropGarbageAreaList = new List<DropGarbageArea>();

    private void Awake()
    {
        Init();
        UpdateTable();

        UserInfo.OnChangeFurnitureHandler += (type) => UpdateTable();
    }


    private void Init()
    {
        _guideButton.onClick.AddListener(() => OnCustomerGuideButtonClicked(-1));
        int tableLength = _tableDatas.Length;
        _orderButtons = new UIButtonAndImage[tableLength];
        _servingButtons = new UIButtonAndImage[tableLength];
        _orderButtonsPos = new WorldToSceenPosition[tableLength];
        _sevingButtonsPos = new WorldToSceenPosition[tableLength];

        GameObject parentObj = new GameObject("TableButtons");
        parentObj.transform.parent = _buttonParent.transform;

        for (int i = 0; i < tableLength; i++)
        {
            int index = i;

            GameObject obj = new GameObject("Table" + (index + 1));
            obj.transform.parent = parentObj.transform;

            UIButtonAndImage orderButton = Instantiate(_orderButtonPrefab, obj.transform);
            UIButtonAndImage servingButton = Instantiate(_servingButtonPrefab, obj.transform);

            orderButton.AddListener(() => OnCustomerOrder(index)); 
            servingButton.AddListener(() => OnServing(index));

            _orderButtonsPos[i] = orderButton.GetComponent<WorldToSceenPosition>();
            _sevingButtonsPos[i] = servingButton.GetComponent<WorldToSceenPosition>();

            _orderButtonsPos[i].SetWorldTransform(_tableDatas[index].ChairTrs[0]);
            _sevingButtonsPos[i].SetWorldTransform(_tableDatas[index].ChairTrs[0]);

            _orderButtons[i] = orderButton;
            _servingButtons[i] = servingButton;
            _dropGarbageAreaList.Add(_tableDatas[index].DropGarbageArea);
        }

        for(int i = 0; i < _ownedTableCount; i++)
        {
            _tableDatas[i].TableState = ETableState.NotUse;
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


    public Vector2 GetTablePos(int index)
    {
        if (index < 0 || _tableDatas.Length <= index)
            throw new System.Exception("테이블 범위를 벗어났습니다.");

        return _tableDatas[index].CustomerMoveTr.position;
    }

    public Vector2 GetStaffPos(int index, StaffType type)
    {
        if (index < 0 || _tableDatas.Length <= index)
            throw new System.Exception("테이블 범위를 벗어났습니다.");

        switch(type)
        {
            case StaffType.Waiter:
                return _tableDatas[index].LeftStaffTr.position;
                case StaffType.Server:
                return _tableDatas[index].RightStaffTr.position;
            case StaffType.Cleaner:
                return _tableDatas[index].CustomerMoveTr.position;
            case StaffType.Manager:
                return _cashTableTr.position;
            case StaffType.Marketer:
                return _marketerTr.position;
            case StaffType.Guard:
                return _guardTr.position;
        }

        Debug.LogError("직원 종류 값이 잘못 입력되었습니다.");
        return new Vector2(0,0);
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


    private void UpdateTable()
    {
        int index = GetTableType(ETableState.NotUse);

        if (index == -1)
            _guideButton.gameObject.SetActive(false);

        else if(_customerController.IsEmpty())
            _guideButton.gameObject.SetActive(false);

        else if(UserInfo.GetEquipStaff(StaffType.Manager) != null)
            _guideButton.gameObject.SetActive(false);

        else 
            _guideButton.gameObject.SetActive(true);


        TableData data = null;
        for (int i = 0, cnt = _ownedTableCount; i < cnt; ++i)
        {
            data = _tableDatas[i];

            if(UserInfo.GetEquipFurniture((FurnitureType)i) == null)
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
    }


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
                currentCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitIndex].position;
                _orderButtonsPos[index].SetWorldTransform(_tableDatas[index].ChairTrs[_tableDatas[index].SitIndex]);
                _sevingButtonsPos[index].SetWorldTransform(_tableDatas[index].ChairTrs[_tableDatas[index].SitIndex]);

                currentCustomer.SetSpriteDir(-_tableDatas[index].SitDir);
                currentCustomer.SetLayer("SitCustomer", 0);
                currentCustomer.ChangeState(CustomerState.Sit);

                Tween.Wait(1f, () =>
                {
                    if (currentCustomer.CustomerData.MaxDiscomfortIndex < _totalGarbageCount)
                    {
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
            _servingButtons[index].SetImage(foodData.ThumbnailSprite);
            foodData = null;
            UpdateTable();
        });

        int tip = Mathf.FloorToInt(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul);
        _tableDatas[index].TotalTip += tip + GameManager.Instance.AddFoodTip;
        _tableDatas[index].CurrentFood = data;
        _tableDatas[index].TotalPrice += (int)(data.Price * _tableDatas[index].CurrentCustomer.CurrentFoodPriceMul) + (int)(data.Price * GameManager.Instance.FoodPriceMul * 0.01f) + GameManager.Instance.AddFoodPrice;
        _orderButtons[index].SetImage(foodData.ThumbnailSprite);

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

        _kitchenSystem.EqueueFood(_tableDatas[index].CurrentFood);
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

        _tableDatas[index].TableState = ETableState.Eating;
        Tween.Wait(1, () => 
        {
            if (_tableDatas[index].OrdersCount <= 0)
                EndEat(index);
            else
                OnCustomerSeating(index);
        });
        UpdateTable();
    }

    private void EndEat(int index)
    {
        int tip = _tableDatas[index].TotalTip;
        int totalPrice = _tableDatas[index].TotalPrice;

        UserInfo.AppendMoney(totalPrice);
        StartCoinAnime(index);
        StartGarbageAnime(index);

        Tween.Wait(0.5f, () =>
        {
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
        int foodPrice = (int)(_tableDatas[index].TotalPrice * GameManager.Instance.FoodPriceMul);

        _tableDatas[index].DropCoinAreas[sitIndex].DropCoin(_tableDatas[index].ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0), foodPrice);
        _tableDatas[index].TotalPrice = 0;
        _tableDatas[index].TotalTip = 0;
    }


    private void StartGarbageAnime(int index)
    {
        int sitIndex = _tableDatas[index].SitIndex;
        int count = UnityEngine.Random.Range(1, 3);
        float time = 0.02f;
        for(int i = 0; i < count; i++)
        {
            Tween.Wait(time, () =>
            {
                _tableDatas[index].DropGarbageArea.DropGarbage(_tableDatas[index].ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0));
            });
            time += 0.02f;
        }
    }


    private void OnChangeFurnitureEvent(FurnitureType type)
    {
        UpdateTable();
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
        int index = GetTableType(ETableState.NotUse);

        if (index == -1)
            return;

        sitPos = Mathf.Clamp(sitPos, -1, 1);
        OnCustomerGuide(index, sitPos);
    }
}
