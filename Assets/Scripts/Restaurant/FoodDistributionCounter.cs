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
    private List<ETableState> _beforeTableStateList = new List<ETableState>();
    private List<TableData> _tableDataList;

    public void Init(ERestaurantFloorType floor, List<TableData> tableDataList)
    {
        _floorType = floor;
        _tableDataList = tableDataList;
        _foodList.Clear();
        for (int i = 0, cnt = tableDataList.Count; i < cnt; ++i)
        {
            int index = i;
            GameObject food = Instantiate(_foodPrefab, _foodStartTr.position + new Vector3(-_foodInterval * i, 0, 0), Quaternion.identity, _foodStartTr);
            _beforeTableStateList.Add(tableDataList[i].TableState);
            _foodList.Add(food);

            food.SetActive(false);
            tableDataList[i].OnChangeTableStateHandler += (state) => OnChangeTableStateEvent();
        }

        OnChangeTableStateEvent();
    }

    private void OnChangeTableStateEvent()
    {
        int activeCount = 0;
        for(int i = 0, cnt = _tableDataList.Count; i < cnt; ++i)
        {
            if (_tableDataList[i].TableState == ETableState.CanServing || (_tableDataList[i].TableState == ETableState.UseStaff && _tableDataList[i].BeforeTableState == ETableState.CanServing))
                activeCount++;
        }

        for(int i = 0, cnt = _foodList.Count; i < cnt; ++i)
        {
            if(activeCount <= i)
            {
                _foodList[i].SetActive(false);
                continue;
            }

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
            _bubbleText.SetText("x" + activeCount);
        }
    }

}

