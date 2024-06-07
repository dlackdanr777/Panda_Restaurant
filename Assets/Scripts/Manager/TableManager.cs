using UnityEngine;

public class TableManager : MonoBehaviour
{
    [Range(0, 10)]
    [SerializeField] private int _ownedTableCount;
    [SerializeField] private TableData[] _tableDatas;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private KitchenSystem _kitchenSystem;

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        UpdateTable();
        _customerController.AddCustomerHandler += UpdateTable;
        _customerController.GuideCustomerHandler += UpdateTable;

        for(int i = 0, cnt = _tableDatas.Length; i < cnt; i++)
        {
            int index = i;
            _tableDatas[index].OrderButton.onClick.AddListener(() => OnOrderButtonClicked(index));
            _tableDatas[index].GuideButton.onClick.AddListener(() => OnGuideButtonClicked(index));
            _tableDatas[index].OrderButton.gameObject.SetActive(false);
        }
    }


    private void UpdateTable()
    {
        for (int i = 0, cnt = _ownedTableCount; i < cnt; ++i)
        {
            if (!_tableDatas[i].IsUsed && _tableDatas[i].CurrentCustomer == null)
            {
                _tableDatas[i].GuideButton.gameObject.SetActive(true);
                _tableDatas[i].OrderButton.gameObject.SetActive(false);
            }
            else if (_tableDatas[i].IsUsed && _tableDatas[i].CurrentCustomer != null && !_tableDatas[i].IsOrderPlaced)
            {
                _tableDatas[i].GuideButton.gameObject.SetActive(false);
                _tableDatas[i].OrderButton.gameObject.SetActive(true);
            }
        }
    }


    private void OnGuideButtonClicked(int index)
    {
        if (_customerController.IsEmpty())
            return;

        if (_tableDatas[index].IsUsed)
            return;

        Customer currentCustomer = _customerController.GetFirstCustomer();
        _tableDatas[index].IsUsed = true;
        _tableDatas[index].GuideButton.gameObject.SetActive(false);

        _customerController.GuideCustomer(_tableDatas[index].CustomerMoveTr.position, 0, () => 
        {
            _tableDatas[index].CurrentCustomer = currentCustomer;
            _tableDatas[index].OrderButton.gameObject.SetActive(true);

            int randInt = Random.Range(0, _tableDatas[index].ChairTrs.Length);
            currentCustomer.transform.position = _tableDatas[index].ChairTrs[randInt].position;

            int dir = randInt == 0 ? 1 : -1;
            currentCustomer.SetSpriteDir(dir);

            UpdateTable();
        });

    }


    private void OnOrderButtonClicked(int index)
    {
        _tableDatas[index].OrderButton.gameObject.SetActive(false);
        CookingData data = new CookingData("À½½Ä", 1, () => ExitCustomer(index));
        _tableDatas[index].IsOrderPlaced = true;
        _kitchenSystem.EqueueFood(data);
    }



    private void ExitCustomer(int index)
    {
        _tableDatas[index].IsUsed = false;
        Customer exitCustomer = _tableDatas[index].CurrentCustomer;
        exitCustomer.transform.position = _tableDatas[index].CustomerMoveTr.position;
        _tableDatas[index].IsOrderPlaced = false;
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            _tableDatas[index].CurrentCustomer = null;
            ObjectPoolManager.Instance.DequeueCustomer(exitCustomer);
            _tableDatas[index].GuideButton.gameObject.SetActive(true);
            UpdateTable();
        });
    }




}
