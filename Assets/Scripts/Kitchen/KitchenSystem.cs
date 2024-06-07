using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KitchenSystem : MonoBehaviour
{
    [SerializeField] private Image _testImage;

    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();

    private float _cookingTimer;
    private CookingData _currentCookingData;

    // Update is called once per frame
    void Update()
    {
        if (_cookingTimer <= 0)
        {
            DequeueFood();
        }
           
        else
        {
            _cookingTimer -= Time.deltaTime;
        }
    }


    public void EqueueFood(CookingData cookingData)
    {
        _cookingQueue.Enqueue(cookingData);
    }


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
