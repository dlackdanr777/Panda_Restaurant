using System;
using UnityEngine;

public class UIFoodType : MonoBehaviour
{
    [SerializeField] private UIImageAndText _foodTypeImage;
    [SerializeField] private Sprite _cozySprite;
    [SerializeField] private Sprite _vintageSprite;
    [SerializeField] private Sprite _naturalSprite;
    [SerializeField] private Sprite _tropicalSprite;
    [SerializeField] private Sprite _modernSprite;
    [SerializeField] private Sprite _luxurySprite;
    [SerializeField] private Sprite _traditionalSprite;

    private FoodType _foodType = FoodType.Length;
    public FoodType FoodType => _foodType;


    public void SetFoodType(FoodType type)
    {
        if (_foodType == type)
            return;

        _foodType = type;
        _foodTypeImage.SetText(Utility.FoodTypeStringConverter(type));
        switch (_foodType)
        {
            case FoodType.Cozy:
                _foodTypeImage.SetSprite(_cozySprite); break;
            case FoodType.Vintage:
                _foodTypeImage.SetSprite(_vintageSprite); break;
            case FoodType.Tropical:
                _foodTypeImage.SetSprite(_tropicalSprite); break;
            case FoodType.Natural:
                _foodTypeImage.SetSprite(_naturalSprite); break;
            case FoodType.Modern:
                _foodTypeImage.SetSprite(_modernSprite); break;
            case FoodType.Luxury:
                _foodTypeImage.SetSprite(_luxurySprite); break;
            case FoodType.Traditional:
                _foodTypeImage.SetSprite(_traditionalSprite); break;

            default:
                throw new Exception("타입이 오류입니다: " + _foodType);

        }
    }
}
