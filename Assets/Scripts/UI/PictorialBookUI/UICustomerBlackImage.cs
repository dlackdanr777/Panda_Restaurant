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
        if (string.IsNullOrWhiteSpace(data.RequiredItem))
        {
            _needItemSlot.gameObject.SetActive(false);
        }
        else
        {
            GachaItemData itemData = ItemManager.Instance.GetGachaItemData(data.RequiredItem);
            _needItemSlot.gameObject.SetActive(true);
            _needItemSlot.SetSprite(itemData.Sprite);

            bool isGiveItem = UserInfo.IsGiveGachaItem(data.RequiredItem);
            _needItemSlot.TextColor = isGiveItem ? _needItemSlotTextColor : Utility.GetColor(ColorType.Negative);
            _needItemSlot.SetImageMaterial(isGiveItem ? null : _grayMaterial);
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

            bool isGiveRecipe = UserInfo.IsGiveRecipe(data.RequiredDish);
            _needFoodSlot.TextColor = isGiveRecipe ? _needFoodSlotTextColor : Utility.GetColor(ColorType.Negative);
            _needFoodSlot.SetImageMaterial(isGiveRecipe ? null : _grayMaterial);
        }

        if(data.MinScore <= 0)
        {
            _needScoreSlot.gameObject.SetActive(false);
        }
        else
        {
            _needScoreSlot.gameObject.SetActive(true);
            _needScoreSlot.SetText1(data.MinScore.ToString());
            _needScoreSlot.TextColor2 = UserInfo.IsScoreValid(data.MinScore) ? _needScoreSlotTextColor : Utility.GetColor(ColorType.Negative);
        }


    }
}
