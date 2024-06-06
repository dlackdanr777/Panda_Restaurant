using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    [Range(0, 10)]
    [SerializeField] private int _ownedTableCount;
    [SerializeField] private TableGuideButton _tableGuideButton;
    [SerializeField] private TableData[] _tableDatas;
    [SerializeField] private CustomerController _customerController;


    private bool _isCustomerFull;

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        UpdateTable();
        _tableGuideButton.Init(OnButtonClicked);
        _customerController.AddCustomerHandler += UpdateTable;
        _customerController.GuideCustomerHandler += UpdateTable;
    }


    private void UpdateTable()
    {
        _isCustomerFull = true;

        for (int i = 0, cnt = _ownedTableCount; i < cnt; ++i)
        {
            if (!_tableDatas[i].IsUsed)
            {
                _isCustomerFull = false;
                break;
            }
        }

        if (_customerController.IsEmpty())
            _tableGuideButton.SetActive(false);

        else
        _tableGuideButton.SetActive(!_isCustomerFull);
    }


    private void OnButtonClicked()
    {
        if (_isCustomerFull)
            return;

        if (_customerController.IsEmpty())
            return;

        for(int i = 0, cnt = _ownedTableCount; i < cnt; ++i)
        {
            if (_tableDatas[i].IsUsed)
                continue;

            Customer currentCustomer = _customerController.GetFirstCustomer();

            _tableDatas[i].IsUsed = true;
            _tableDatas[i].CurrentCustomer = currentCustomer;

            _customerController.GuideCustomer(_tableDatas[i].CustomerMoveTr.position);
            break;
        }

        UpdateTable();
    }




}
