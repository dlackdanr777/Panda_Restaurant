using UnityEngine;
using UnityEngine.UI;

public class UICustomerBlackImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIImageAndText _needItemSlot;
    [SerializeField] private UIImageAndText _needFoodSlot;
    [SerializeField] private UITextAndText _needScoreSlot;
    [SerializeField] private Material _grayMaterial;

    private Color _needItemSlotTextColor;
    private Color _needFoodSlotTextColor;
    private Color _needScoreSlotTextColor;

    public void Init()
    {
        _needItemSlotTextColor = _needItemSlot.TextColor;
        _needFoodSlotTextColor = _needFoodSlot.TextColor;
        _needScoreSlotTextColor = _needScoreSlot.TextColor2;
    }


    public void SetData(CustomerData data)
    {
        if (data == null)
            return;

        CustomerVisitState state = UserInfo.GetCustomerVisitState(data);

        if (string.IsNullOrWhiteSpace(data.RequiredItem))
        {
            _needItemSlot.gameObject.SetActive(false);
        }
        else
        {
            GachaItemData itemData = ItemManager.Instance.GetGachaItemData(data.RequiredItem);
            _needItemSlot.gameObject.SetActive(true);
            _needItemSlot.SetSprite(itemData.Sprite);

            _needItemSlot.TextColor = state.IsGiveItem ? _needItemSlotTextColor : Utility.GetColor(ColorType.Negative);
            _needItemSlot.SetImageMaterial(state.IsGiveItem ? null : _grayMaterial);
        }


        if (string.IsNullOrWhiteSpace(data.RequiredDish))
        {
            _needFoodSlot.gameObject.SetActive(false);
        }
        else
        {
            FoodData foodData = FoodDataManager.Instance.GetFoodData(data.RequiredDish);
            _needFoodSlot.gameObject.SetActive(true);
            _needFoodSlot.SetSprite(foodData.Sprite);

            _needFoodSlot.TextColor = state.IsGiveRecipe ? _needFoodSlotTextColor : Utility.GetColor(ColorType.Negative);
            _needFoodSlot.SetImageMaterial(state.IsGiveRecipe ? null : _grayMaterial);
        }

        if(data.MinScore <= 0)
        {
            _needScoreSlot.gameObject.SetActive(false);
        }
        else
        {
            _needScoreSlot.gameObject.SetActive(true);
            _needScoreSlot.SetText1(data.MinScore.ToString());
            _needScoreSlot.TextColor2 = state.IsScoreValid ? _needScoreSlotTextColor : Utility.GetColor(ColorType.Negative);
        }


    }
}
