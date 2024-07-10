using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class KitchenStaffData
{
    private CookingData _cookingData;
    private float _cookSpeedMultiple;
    public float CookSpeedMultiple => _cookSpeedMultiple;
    private float _cookingTime;

    public float CookingTimer;


    public KitchenStaffData(float cookSpeedMultiple)
    {
        _cookSpeedMultiple = cookSpeedMultiple;
    }

    public void SetCookingData(CookingData cookingData)
    {
        _cookingData = cookingData;
        _cookingTime = cookingData.CookingTime;
        CookingTimer = _cookingTime;
    }

    public CookingData GetCookingData()
    {
        return _cookingData;
    }

}


public class KitchenSystem : MonoBehaviour
{
    private Dictionary<Staff, KitchenStaffData> _cookerDic = new Dictionary<Staff, KitchenStaffData>();
    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();

    private float _cookingTimer;
    private CookingData _currentCookingData;

    void Update()
    {
        //TODO: 병렬 수행 제거, 음식 제작 속도만 증가
        if (_cookingTimer <= 0)
        {
            DequeueFood();
        }

        else
        {
            _cookingTimer -= Time.deltaTime * GameManager.Instance.CookingSpeedMul;
        }

    }


    public void EqueueFood(CookingData cookingData)
    {
        _cookingQueue.Enqueue(cookingData);
    }

/*    public void AddCooker(Staff staff)
    {
        KitchenStaffData data = new KitchenStaffData(staff.Speed);
        _cookerDic.Add(staff, data);

    }

    public void RemoveCooker(Staff staff)
    {
        if (!_cookerDic.TryGetValue(staff, out KitchenStaffData data))
        {
            Debug.LogError("해당 쉐프가 슬롯에 존재하지 않습니다.");
            return;
        }

        if (!data.GetCookingData().IsDefault())
            _cookingQueue.Enqueue(data.GetCookingData());

        _cookerDic.Remove(staff);
    }*/


    private void DequeueFood()
    {
        if (!_currentCookingData.IsDefault())
            _currentCookingData.OnCompleted();

        if(_cookingQueue.Count == 0)
        {
            _currentCookingData = default(CookingData);
            _cookingTimer = 0;
            return;
        }

        _currentCookingData = _cookingQueue.Dequeue();
        _cookingTimer = _currentCookingData.CookingTime;
    }
}
