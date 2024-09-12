using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFurniturePreview : MonoBehaviour
{
    [SerializeField] private UIImageAndText _selectGroup;
    [SerializeField] private UIImageAndText _scoreGroup;
    [SerializeField] private UIImageAndText _tipPerMinuteGroup;
    [SerializeField] private UIImageAndText _cookSpeedGroup;
    [SerializeField] private UITextAndText _setGroup;
    [SerializeField] private GameObject _effetGroup;

    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    private Action<FurnitureData> _onBuyButtonClicked;
    private Action<FurnitureData> _onEquipButtonClicked;
    private Action<FurnitureData> _onUpgradeButtonClicked;
    private FurnitureData _currentData;

    public void Init(Action<FurnitureData> onEquipButtonClicked, Action<FurnitureData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        UserInfo.OnChangeMoneyHandler += UpdateFurniture;
        UserInfo.OnChangeScoreHandler += UpdateFurniture;
        UserInfo.OnChangeFurnitureHandler += (type) => UpdateFurniture();
        UserInfo.OnGiveFurnitureHandler += UpdateFurniture;
    }


    public void SetFurnitureData(FurnitureData data)
    {
        _currentData = data;

        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _tipPerMinuteGroup.gameObject.SetActive(false);
        _cookSpeedGroup.gameObject.SetActive(false);

        if (data == null)
        {
            _scoreGroup.gameObject.SetActive(false);
            _setGroup.gameObject.SetActive(false);
            _effetGroup.gameObject.SetActive(false);
            _selectGroup.ImageColor = new Color(1,1,1,0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _scoreGroup.gameObject.SetActive(true);
            _setGroup.gameObject.SetActive(true);
            _effetGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;

        }

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _scoreGroup.SetText(data.AddScore.ToString());

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setGroup.SetText1(setData.Name);
        _setGroup.SetText2(setData != null ? setData.Description : string.Empty);

        if (data.EffectData is TipPerMinuteFurnitureEffectData)
        {
            _tipPerMinuteGroup.gameObject.SetActive(true);
            _cookSpeedGroup.gameObject.SetActive(false);

            _tipPerMinuteGroup.SetText("∫–¥Á »πµÊ ∆¡ <color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + (data.EffectData.EffectValue.ToString())  + "</color> ¡ı∞°");
        }        
        else if (data.EffectData is MaxTipVolumeFurnitureEffectData)
        {
            _tipPerMinuteGroup.gameObject.SetActive(false);
            _cookSpeedGroup.gameObject.SetActive(true);

            _cookSpeedGroup.SetText("∆¡ ¿˙¿Â∑Æ <color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.EffectData.EffectValue + "</color> ¡ı∞°");
        }
        else
        {
            _tipPerMinuteGroup.SetText(string.Empty);
        }
        

        if (UserInfo.IsEquipFurniture(data))
        {
            _usingButton.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        else
        {
            if(UserInfo.IsGiveFurniture(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
                _selectGroup.ImageColor = Color.white;
            }
            else
            {
                _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);

                if (!UserInfo.IsScoreValid(data))
                {
                    _scoreButton.gameObject.SetActive(true);
                    _scoreButton.SetText(data.BuyScore.ToString());
                    return;
                }
                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
            }
        }
    }

    private void UpdateFurniture()
    {
        SetFurnitureData(_currentData);
    }
}
