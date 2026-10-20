using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FoodDistributionCounter : MonoBehaviour
{
    [Header("Options")]

    [Space]
    [Header("Components")]
    [SerializeField] private GameObject _bubble;
    [SerializeField] private TextMeshPro _bubbleText;

    [Space]
    [Header("Prefab Options")]
    [SerializeField] private GameObject _foodPrefab;
    [SerializeField] private Transform _foodStartTr;
    [SerializeField] private float _foodInterval;


    private ERestaurantFloorType _floorType;
    private List<GameObject> _foodList = new List<GameObject>();
    private readonly List<TableOrderIdentity> _displayOrders = new List<TableOrderIdentity>();
    internal IReadOnlyList<TableOrderIdentity> DisplayOrders => _displayOrders.AsReadOnly();
    private List<TableData> _tableDataList;
    private List<System.Action<ETableState>> _tableStateHandlers = new List<System.Action<ETableState>>();

    public void Init(ERestaurantFloorType floor, List<TableData> tableDataList)
    {
        Unsubscribe();
        foreach (var old in _foodList) if (old != null) Destroy(old);
        _floorType = floor;
        _tableDataList = tableDataList;
        _foodList.Clear();
        for (int i = 0, cnt = tableDataList.Count; i < cnt; ++i)
        {
            int index = i;
            GameObject food = Instantiate(_foodPrefab, _foodStartTr.position + new Vector3(-_foodInterval * i, 0, 0), Quaternion.identity, _foodStartTr);
            _foodList.Add(food);

            food.SetActive(false);
            System.Action<ETableState> handler = (state) => OnChangeTableStateEvent();
            _tableStateHandlers.Add(handler);
            tableDataList[i].OnChangeTableStateHandler += handler;
            tableDataList[i].OnServiceChangedHandler += OnChangeTableStateEvent;
        }

        OnChangeTableStateEvent();
    }

    private void OnChangeTableStateEvent()
    {
        _displayOrders.Clear();
        for(int i = 0, cnt = _tableDataList.Count; i < cnt; ++i)
        {
            var table = _tableDataList[i];
            if (table.FloorType != _floorType) continue;
            if (table.PartyOrders != null)
            {
                foreach (var food in table.PartyOrders.Orders)
                    if (TableManager.IsFoodAtCounter(food.Identity)) _displayOrders.Add(food.Identity);
            }
            else if (TableManager.IsFoodAtCounter(table.CurrentFood.Identity)) _displayOrders.Add(table.CurrentFood.Identity);
        }
        int activeCount = _displayOrders.Count;
        while (_foodList.Count < activeCount)
            _foodList.Add(Instantiate(_foodPrefab, _foodStartTr));

        for(int i = 0, cnt = _foodList.Count; i < cnt; ++i)
        {
            if(activeCount <= i)
            {
                _foodList[i].SetActive(false);
                continue;
            }

            _foodList[i].transform.localPosition = Vector3.left * _foodInterval * i;
            _foodList[i].SetActive(true);
        }

        if(activeCount <= 0)
        {
            _bubble.SetActive(false);
            return;
        }
        else
        {
            _bubble.SetActive(true);
            _bubbleText.SetText("x{0}", activeCount);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_tableDataList == null) return;
        for (int i = 0; i < _tableDataList.Count && i < _tableStateHandlers.Count; i++)
        {
            _tableDataList[i].OnChangeTableStateHandler -= _tableStateHandlers[i];
            _tableDataList[i].OnServiceChangedHandler -= OnChangeTableStateEvent;
        }
        _tableStateHandlers.Clear();
    }

}

