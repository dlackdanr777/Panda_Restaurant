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
    [SerializeField] private Image _testImage;

    private Dictionary<Staff, KitchenStaffData> _cookerDic = new Dictionary<Staff, KitchenStaffData>();
    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();

    // Update is called once per frame
    void Update()
    {
        foreach(KitchenStaffData data in _cookerDic.Values)
        {
            Debug.Log(data.CookSpeedMultiple);
            if (data.CookingTimer <= 0)
            {
                DequeueFood(data);
            }

            else
            {
                data.CookingTimer -= Time.deltaTime * data.CookSpeedMultiple;
            }
        }

    }


    public void EqueueFood(CookingData cookingData)
    {
        _cookingQueue.Enqueue(cookingData);
    }

    public void AddCooker(Staff staff)
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
    }


    private void DequeueFood(KitchenStaffData data)
    {
        if (!data.GetCookingData().IsDefault())
            data.GetCookingData().OnCompleted();

        if(_cookingQueue.Count == 0)
        {
            data.SetCookingData(default(CookingData));
            data.CookingTimer = 0;
            return;
        }

        data.SetCookingData(_cookingQueue.Dequeue());
    }
}
