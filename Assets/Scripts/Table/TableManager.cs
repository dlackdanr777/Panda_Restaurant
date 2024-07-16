using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    [Range(0, 10)]
    [SerializeField] private int _ownedTableCount;
    [SerializeField] private Transform _cashTableTr;
    [SerializeField] private Transform _marketerTr;
    [SerializeField] private Transform _guardTr;

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

    private UIButtonAndImage[] _orderButtons;
    private UIButtonAndImage[] _servingButtons;
    private Button[] _cleaningButtons;


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
            cleaningButton.onClick.AddListener(() => OnCleanTable(index));

            orderButton.GetComponent<WorldToSceenPosition>().SetWorldTransform(_tableDatas[index].TableButtonTr);
            servingButton.GetComponent<WorldToSceenPosition>().SetWorldTransform(_tableDatas[index].TableButtonTr);
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
                _cleaningButtons[i].gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Move || data.TableState == ETableState.WaitFood || data.TableState == ETableState.Eating || data.TableState == ETableState.UseStaff || data.TableState == ETableState.DontUse)
            {
                _orderButtons[i].gameObject.SetActive(false);
                _servingButtons[i].gameObject.SetActive(false);
                _cleaningButtons[i].gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Seating)
            {
                _orderButtons[i].gameObject.SetActive(true);
                _servingButtons[i].gameObject.SetActive(false);
                _cleaningButtons[i].gameObject.SetActive(false);
            }
            else if(data.TableState == ETableState.CanServing)
            {
                _servingButtons[i].gameObject.SetActive(true);
                _orderButtons[i].gameObject.SetActive(false);
                _cleaningButtons[i].gameObject.SetActive(false);
            }

            else if (data.TableState == ETableState.NeedCleaning)
            {
                _cleaningButtons[i].gameObject.SetActive(true);
                _orderButtons[i].gameObject.SetActive(false);
                _servingButtons[i].gameObject.SetActive(false);
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

        UpdateTable();

        _customerController.GuideCustomer(_tableDatas[index].CustomerMoveTr.position, 0, () =>
        {
            int randInt = Random.Range(0, _tableDatas[index].ChairTrs.Length);
            currentCustomer.transform.position = _tableDatas[index].ChairTrs[randInt].position;
            int dir = randInt == 0 ? 1 : -1;
            currentCustomer.SetSpriteDir(dir);
            currentCustomer.SetLayer("SitCustomer", 0);
            currentCustomer = null;
            OnCustomerSeating(index);
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
            ExitCustomer(index);

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
                ExitCustomer(index);
            else
                OnCustomerSeating(index);
        });
        UpdateTable();
    }

    public void OnCleanTable(int index) 
    {
        if (_tableDatas[index].TableState == ETableState.DontUse)
        {
            NotFurnitureTable(index);
            return;
        }

        UserInfo.AppendTip(_tableDatas[index].TipValue);
        _tableDatas[index].TableState = ETableState.NotUse;
        UpdateTable();
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

        _tableDatas[index].TableState = ETableState.NeedCleaning;

        Customer exitCustomer = _tableDatas[index].CurrentCustomer;
        exitCustomer.transform.position = _tableDatas[index].CustomerMoveTr.position;
        exitCustomer.SetLayer("Customer", 0);
        _tableDatas[index].CurrentCustomer = null;
        UserInfo.AppendMoney((int)(_tableDatas[index].TotalPrice * GameManager.Instance.FoodPriceMul));

        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () => 
        {
            ObjectPoolManager.Instance.DequeueCustomer(exitCustomer);
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
            exitCustomer.transform.position = _tableDatas[index].CustomerMoveTr.position;

        _tableDatas[index].CurrentCustomer = null;
        exitCustomer.SetLayer("Customer", 0);

        _tableDatas[index].TableState = ETableState.DontUse;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            ObjectPoolManager.Instance.DequeueCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }

    
    private void OnChangeFurnitureEvent(FurnitureType type)
    {
        UpdateTable();
    }



}
