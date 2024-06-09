using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KitchenSystem : MonoBehaviour
{
    [SerializeField] private Image _testImage;

    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();

    private float _cookingTimer;
    private float _cookingTime;
    private CookingData _currentCookingData;

    // Update is called once per frame
    void Update()
    {
        if (_cookingTimer <= 0)
        {
            DequeueFood();
            _testImage.fillAmount = 0;
        }

        else
        {
            _cookingTimer -= Time.deltaTime;
            float normalizedValue = 1 - (_cookingTimer / _cookingTime);
            _testImage.fillAmount = normalizedValue;
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
        _cookingTime = _currentCookingData.CookingTime;
    }
}
