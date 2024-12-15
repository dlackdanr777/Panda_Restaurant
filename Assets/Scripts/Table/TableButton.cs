using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TableButton : MonoBehaviour
{
    [SerializeField] private Image _foodImage;
    [SerializeField] private Button _button;
    [SerializeField] private WorldToSceenPosition _worldToSceenPos;
    [SerializeField] private Material _grayMat;

    private FoodData _currentData;

    public void Init()
    {
        UserInfo.OnGiveRecipeHandler += UpdateFoodImage;
    }


    public void AddListener(UnityAction action)
    {
        _button.onClick.AddListener(action);
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }

    public void Interactable(bool value)
    {
        _button.interactable = value;
    }

    public void SetWorldTransform(Transform tr)
    {
        _worldToSceenPos.SetWorldTransform(tr);
    }


    public void SetData(FoodData data)
    {
        if (data == null)
        {
            _currentData = null;
            return;
        }

        if (data == _currentData)
            return;

        _currentData = data;
        _foodImage.sprite = data.ThumbnailSprite;
        _foodImage.material = UserInfo.IsGiveRecipe(data) ? null : _grayMat;
    }


    private void UpdateFoodImage()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_currentData == null)
            return;

        _foodImage.material = UserInfo.IsGiveRecipe(_currentData) ? null : _grayMat;
    }
}
