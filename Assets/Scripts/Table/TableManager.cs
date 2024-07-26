using Muks.PathFinding.AStar;
using Muks.Tween;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    [Range(0, 10)]
    [SerializeField] private int _ownedTableCount;
    [SerializeField] private Transform _cashTableTr;
    [SerializeField] private Transform _marketerTr;
    [SerializeField] private Transform _guardTr;
    [SerializeField] private Transform _moneyUITr;

    [Space]
    [SerializeField] private TableData[] _tableDatas;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private KitchenSystem _kitchenSystem;


    [Space]
    [SerializeField] private Transform _buttonParent;
    [SerializeField] private Button _guideButton;
    [SerializeField] private UIButtonAndImage _orderButtonPrefab;
    [SerializeField] private UIButtonAndImage _servingButtonPrefab;
    [SerializeField] private Button _cleaningButtonPrefab;

    private WorldToSceenPosition[] _orderButtonsPos;
    private WorldToSceenPosition[] _sevingButtonsPos;
    private UIButtonAndImage[] _orderButtons;
    private UIButtonAndImage[] _servingButtons;
    private Button[] _cleaningButtons;

    private int _totalGarbageCount;


    private void Awake()
    {
        Init();
        UpdateTable();
    }


    private void Init()
    {
        _guideButton.onClick.AddListener(() =>
        {
            int index = GetTableType(ETableState.NotUse);

            if (index == -1)
                return;

            OnCustomerGuide(index);
        });

        int tableLength = _tableDatas.Length;
        _orderButtons = new UIButtonAndImage[tableLength];
        _servingButtons = new UIButtonAndImage[tableLength];
        _cleaningButtons = new Button[tableLength];
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
            Button cleaningButton = Instantiate(_cleaningButtonPrefab, obj.transform);

            orderButton.AddListener(() => OnCustomerOrder(index)); 
            servingButton.AddListener(() => OnServing(index));
            cleaningButton.onClick.AddListener(() => OnMoneyButtonClicked(index));

            _orderButtonsPos[i] = orderButton.GetComponent<WorldToSceenPosition>();
            _sevingButtonsPos[i] = servingButton.GetComponent<WorldToSceenPosition>();

            _orderButtonsPos[i].SetWorldTransform(_tableDatas[index].ChairTrs[0]);
            _sevingButtonsPos[i].SetWorldTransform(_tableDatas[index].ChairTrs[0]);
            cleaningButton.GetComponent<WorldToSceenPosition>().SetWorldTransform(_tableDatas[index].TableButtonTr);

            _orderButtons[i] = orderButton;
            _servingButtons[i] = servingButton;
            _cleaningButtons[i] = cleaningButton;
        }

        for(int i = 0; i < _ownedTableCount; i++)
        {
            _tableDatas[i].TableState = ETableState.NotUse;
        }

        UpdateTable();
        _customerController.AddCustomerHandler += UpdateTable;
        _customerController.GuideCustomerHandler += UpdateTable;
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


    private void UpdateTable()
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


    public void OnCustomerGuide(int index)
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

        Customer currentCustomer = _customerController.GetFirstCustomer();
        _tableDatas[index].CurrentCustomer = currentCustomer;
        currentCustomer.SetLayer("Customer", 0);
        _tableDatas[index].TableState = ETableState.Move;
        _tableDatas[index].OrdersCount = currentCustomer.OrderCount;
        _tableDatas[index].TotalPrice = 0;

        int randInt = UnityEngine.Random.Range(0, _tableDatas[index].ChairTrs.Length);
        _tableDatas[index].SitDir = randInt == 0 ? -1 : 1;
        Vector3 targetPos = _tableDatas[index].ChairTrs[randInt].position - new Vector3(0, AStar.Instance.NodeSize * 2 ,0);

        UpdateTable();

        _customerController.GuideCustomer(targetPos, 0, () =>
        {
            Tween.Wait(0.5f, () =>
            {
                currentCustomer.transform.position = _tableDatas[index].ChairTrs[randInt].position;
                _orderButtonsPos[index].SetWorldTransform(_tableDatas[index].ChairTrs[randInt]);
                _sevingButtonsPos[index].SetWorldTransform(_tableDatas[index].ChairTrs[randInt]);

                currentCustomer.SetSpriteDir(-_tableDatas[index].SitDir);
                currentCustomer.SetLayer("SitCustomer", 0);
                currentCustomer = null;
                OnCustomerSeating(index);
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

        _tableDatas[index].CurrentCustomer.ChangeState(CustomerState.Sit);

        string foodDataId = _tableDatas[index].CurrentCustomer.CustomerData.GetRandomOrderFood();
        FoodData foodData = FoodDataManager.Instance.GetFoodData(foodDataId);
        int foodLevel = UserInfo.GetRecipeLevel(foodDataId);

        CookingData data = new CookingData(foodData.Name, foodData.GetCookingTime(foodLevel), foodData.GetSellPrice(foodLevel), foodData.Sprite, () =>
        {
            _tableDatas[index].TableState = ETableState.CanServing;
            _servingButtons[index].SetImage(foodData.ThumbnailSprite);
            foodData = null;
            UpdateTable();
        });

        _tableDatas[index].SetTipValue((int)(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul * 0.01f));
        _tableDatas[index].CurrentFood = data;
        _tableDatas[index].TotalPrice += (int)(data.Price * _tableDatas[index].CurrentCustomer.FoodPriceMul);
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
        StartCoinAnime(index);

        Tween.Wait(0.6f, () =>
        {
            ExitCustomer(index);
            _tableDatas[index].TableState = ETableState.NotUse;
            _cleaningButtons[index].gameObject.SetActive(true);
            UpdateTable();
        });
    }

    public void OnMoneyButtonClicked(int index) 
    {
        _cleaningButtons[index].gameObject.SetActive(false);
        _tableDatas[index].CoinCount = 0;
        UserInfo.AppendMoney((int)(_tableDatas[index].TotalPrice * GameManager.Instance.FoodPriceMul));
        
        for(int i = 0, cnt = _tableDatas[index].CoinList.Count; i < cnt; i++)
        {
            int coinIndex = i;
            _tableDatas[index].CoinList[coinIndex].TweenStop();
            _tableDatas[index].CoinList[coinIndex].TweenMove(_moneyUITr.position, 1.5f, TweenMode.Smootherstep).
                OnComplete(() => 
                {
                    _tableDatas[index].CoinList[coinIndex].TweenStop();
                    ObjectPoolManager.Instance.EnqueueCoin(_tableDatas[index].CoinList[coinIndex]);
                    
                    if(coinIndex == _tableDatas[index].CoinList.Count - 1)
                        _tableDatas[index].CoinList.Clear();
                });
        }
    }

    public void OnUseStaff(int index)
    {
        _tableDatas[index].TableState = ETableState.UseStaff;
        UpdateTable();
    }





    private void ExitCustomer(int index)
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }


        Customer exitCustomer = _tableDatas[index].CurrentCustomer;
        exitCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);
        exitCustomer.SetLayer("Customer", 0);

        _tableDatas[index].CurrentCustomer = null;

        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () => 
        {
            ObjectPoolManager.Instance.EnqueueCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }

    private void NotFurnitureTable(int index)
    {
        _tableDatas[index].TotalPrice = 0;
        _tableDatas[index].SetTipValue(-_tableDatas[index].TipValue);

        if (_tableDatas[index].CurrentCustomer == null)
        {
            _tableDatas[index].TableState = ETableState.DontUse;
            return;
        }

        Customer exitCustomer = _tableDatas[index].CurrentCustomer;

        if (_tableDatas[index].TableState != ETableState.Move)
            exitCustomer.transform.position = _tableDatas[index].ChairTrs[_tableDatas[index].SitDir == -1 ? 0 : 1].position - new Vector3(0, AStar.Instance.NodeSize * 2, 0);

        _tableDatas[index].CurrentCustomer = null;
        exitCustomer.SetLayer("Customer", 0);

        _tableDatas[index].TableState = ETableState.DontUse;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.EnqueueCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    private void StartCoinAnime(int index)
    {
        int dir = _tableDatas[index].SitDir;
        Vector3 targetTr = 5 <= _tableDatas[index].CoinCount ?
            _tableDatas[index].CoinTr.position :
            _tableDatas[index].CoinTr.position + new Vector3((_tableDatas[index].CoinCount * 0.3f) - 0.6f, 0, 0);

        GameObject coin = ObjectPoolManager.Instance.SpawnCoin(_tableDatas[index].ChairTrs[dir == -1 ? 0 : 1].position + new Vector3(0, 1.2f, 0), Quaternion.identity);

        coin.TweenMoveX(targetTr.x, 0.45f);
        coin.TweenMoveY(targetTr.y, 0.45f, TweenMode.EaseInBack).OnComplete(() =>
        {
            if (5 <= _tableDatas[index].CoinCount)
            {
                coin.TweenStop();
                ObjectPoolManager.Instance.EnqueueCoin(coin);
                return;
            }

            coin.TweenMove(coin.transform.position + new Vector3(0, 0.3f, 0), 2f, TweenMode.Smootherstep).Loop(LoopType.Yoyo);
            _tableDatas[index].CoinCount = Mathf.Clamp(_tableDatas[index].CoinCount + 1, 0, 5);
            _tableDatas[index].CoinList.Add(coin);
        });
    }

    
    private void OnChangeFurnitureEvent(FurnitureType type)
    {
        UpdateTable();
    }



}
