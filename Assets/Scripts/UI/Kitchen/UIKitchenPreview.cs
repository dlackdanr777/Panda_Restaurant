using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchenPreview : MonoBehaviour
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

    private Action<KitchenUtensilData> _onBuyButtonClicked;
    private Action<KitchenUtensilData> _onEquipButtonClicked;
    private KitchenUtensilData _currentData;
    private Dictionary<string, FurnitureSetData> _setDataDic;

    public void Init(Action<KitchenUtensilData> onEquipButtonClicked, Action<KitchenUtensilData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
        _setDataDic = FurnitureDataManager.Instance.GetFurnitureSetDic();

        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHanlder += UpdateUI;
        UserInfo.OnChangeFurnitureHandler += (type) => UpdateUI();
        UserInfo.OnGiveFurnitureHandler += UpdateUI;
    }


    public void SetData(KitchenUtensilData data)
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

        if (data is CookingSpeedUpKitchenUtensilData)
        {
            _effectText.text = "요리 효율 :";
            _effectDescription.text = data.EffectValue.ToString() + "%";
        }
            
        else if (data is MoneyPerMinuteKitchenUtensilData)
        {
            _effectText.text = "분당 수입 :";
            _effectDescription.text = data.EffectValue.ToString();
        }

        else
        {
            _effectText.text = "기본 효과 :";
            _effectDescription.text = string.Empty;
        }
        

        if (UserInfo.IsEquipKitchenUtensil(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용 중");
            _usingButton.Interactable(false);
            _image.color = new Color(1, 1, 1);
            _setEffectObj.SetActive(true);
            _setEffectDescription.text = _setDataDic[data.SetId].Description;
        }
        else
        {
            if(UserInfo.IsGiveKitchenUtensil(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용 하기");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
                _image.color = new Color(1, 1, 1);
                _setEffectObj.SetActive(true);
                _setEffectDescription.text = _setDataDic[data.SetId].Description;
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

    private void UpdateUI()
    {
        SetData(_currentData);
    }
}
