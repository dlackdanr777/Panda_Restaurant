using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFurniturePreview : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private GameObject[] _hideObjs;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _setEffectObj;
    [SerializeField] private TextMeshProUGUI _setEffectDescription;
    [SerializeField] private GameObject _buyMinScoreObj;
    [SerializeField] private TextMeshProUGUI _buyMinScoreDescription;
    [SerializeField] private TextMeshProUGUI _addScoreDescription;
    [SerializeField] private GameObject _addTipObj;
    [SerializeField] private GameObject _tipVolumeObj;
    [SerializeField] private TextMeshProUGUI _addTipDescription;
    [SerializeField] private TextMeshProUGUI _tipVolumeDescription;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;

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
        _buyMinScoreObj.SetActive(false);
        _setEffectObj.SetActive(false);

        if (data == null)
        {
            for(int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
            {
                _hideObjs[i].SetActive(false);
            }
            return;
        }

        for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
        {
            _hideObjs[i].SetActive(true);
        }

        _image.sprite = data.ThumbnailSprite;
        _nameText.text = data.Name;
        _addScoreDescription.text = data.AddScore.ToString();

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setEffectDescription.text = setData != null ? setData.Description : string.Empty;

        if (data.EffectData is TipPerMinuteFurnitureEffectData)
        {
            _addTipObj.SetActive(true);
            _tipVolumeObj.gameObject.SetActive(false);

            _addTipDescription.text = data.EffectData.EffectValue.ToString();
        }
            

        else if (data.EffectData is MaxTipVolumeFurnitureEffectData)
        {
            _tipVolumeObj.gameObject.SetActive(true);
            _addTipObj.SetActive(false);

            _tipVolumeDescription.text = data.EffectData.EffectValue.ToString();
        }

        else
        {
            _tipVolumeObj.gameObject.SetActive(false);
            _addTipObj.SetActive(false);
        }
        

        if (UserInfo.IsEquipFurniture(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용 중");
            _usingButton.Interactable(false);
            _image.color = new Color(1, 1, 1);
            _setEffectObj.SetActive(true);
        }
        else
        {
            if(UserInfo.IsGiveFurniture(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용 하기");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
                _image.color = new Color(1, 1, 1);
                _setEffectObj.SetActive(true);
            }
            else
            {
                _image.color = new Color(0, 0, 0);

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
                _buyMinScoreObj.SetActive(true);
                _buyMinScoreDescription.text = Utility.ConvertToNumber(data.BuyScore);
            }
        }
    }

    private void UpdateFurniture()
    {
        SetFurnitureData(_currentData);
    }
}
