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
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;

    private Action<FurnitureData> _onBuyButtonClicked;
    private Action<FurnitureData> _onEquipButtonClicked;
    private Action<FurnitureData> _onUpgradeButtonClicked;
    private FurnitureData _currentData;
    private Dictionary<string, FurnitureSetData> _furnitureSetDataDic;

    public void Init(Action<FurnitureData> onEquipButtonClicked, Action<FurnitureData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
        _furnitureSetDataDic = FurnitureDataManager.Instance.GetFurnitureSetDic();

        UserInfo.OnChangeMoneyHandler += UpdateFurniture;
        UserInfo.OnChangeScoreHanlder += UpdateFurniture;
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

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _addScoreDescription.text = data.AddScore.ToString();

        if (data is MoneyPerMinuteFurnitureData)
        {
            _effectText.text = "분당 수입 :";
            _effectDescription.text = data.EffectValue.ToString();
        }
            
        else if (data is TipPerMinuteFurnitureData)
        {
            _effectText.text = "분당 팁 :";
            _effectDescription.text = data.EffectValue.ToString();
        }

        else if (data is MaxTipVolumeFurnitureData)
        {
            _effectText.text = "팁 저장량 :";
            _effectDescription.text = data.EffectValue.ToString();
        }

        else
        {
            _effectText.text = "기본 효과 :";
            _effectDescription.text = string.Empty;
        }
        

        if (UserInfo.IsEquipFurniture(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용 중");
            _usingButton.Interactable(false);
            _image.color = new Color(1, 1, 1);
            _setEffectObj.SetActive(true);
            _setEffectDescription.text = _furnitureSetDataDic[data.SetId].EffectDescription;
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
                _setEffectDescription.text = _furnitureSetDataDic[data.SetId].EffectDescription;
            }
            else
            {
                _image.color = new Color(0, 0, 0);

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyMinPrice));
                _buyMinScoreObj.SetActive(true);
                _buyMinScoreDescription.text = Utility.ConvertToNumber(data.BuyMinScore);
            }
        }
    }

    private void UpdateFurniture()
    {
        SetFurnitureData(_currentData);
    }
}
