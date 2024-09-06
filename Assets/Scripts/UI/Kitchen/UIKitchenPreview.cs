using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchenPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UISelectSlot _selectSlot;
    [SerializeField] private UITitleAndDescription _setNameGroup;
    [SerializeField] private UITitleAndDescription _setEffectGroup;
    [SerializeField] private UITitleAndDescription _effectGroup;
    [SerializeField] private UIImageAndText _needScore;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIImageAndText _addScoreGroup;

    private Action<KitchenUtensilData> _onBuyButtonClicked;
    private Action<KitchenUtensilData> _onEquipButtonClicked;
    private KitchenUtensilData _currentData;

    public void Init(Action<BasicData> onEquipButtonClicked, Action<BasicData> onBuyButtonClicked)
    {
        _selectSlot.OnButtonClicked(onEquipButtonClicked);
        _onBuyButtonClicked = onBuyButtonClicked;

        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        UserInfo.OnChangeKitchenUtensilHandler += (type) => UpdateUI();
        UserInfo.OnGiveKitchenUtensilHandler += UpdateUI;
    }


    public void SetData(KitchenUtensilData data)
    {
        _currentData = data;
        _needScore.gameObject.SetActive(false);
        _addScoreGroup.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);

        if (data == null)
        {
            _setNameGroup.SetText(string.Empty, string.Empty);
            _setEffectGroup.SetText(string.Empty, string.Empty);
            _effectGroup.SetText(string.Empty, string.Empty);
            _selectSlot.SetData(null, false, false);
            return;
        }

        //효과 관련 코드
        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setNameGroup.SetText("세트", setData != null ? setData.Name : string.Empty);
        _setEffectGroup.SetText("세트 효과", setData != null ? setData.Description : string.Empty);
        if (data is CookingSpeedUpKitchenUtensilData || data is TipPerMinuteKitchenUtensilData)
        {
            _effectGroup.SetActive(true);
            _effectGroup.SetText("보유 효과", data.EffectDescription);
        }
        else
        {
            _effectGroup.SetActive(false);
        }

        //하단 설명창 관련 코드
        bool isUse = UserInfo.IsEquipKitchenUtensil(data);
        bool isHave = UserInfo.IsGiveKitchenUtensil(data);
        _selectSlot.SetData(data, isUse, isHave);

        if (!isHave)
        {
            if (UserInfo.IsScoreValid(data))
            {
                _buyButton.gameObject.SetActive(true);
                _needScore.gameObject.SetActive(false);

                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
                return;
            }

            _needScore.gameObject.SetActive(true);
            _needScore.SetText(data.BuyScore.ToString());
        }

        else
        {
            _addScoreGroup.gameObject.SetActive(true);
            _addScoreGroup.SetText(Utility.ConvertToNumber(data.AddScore));
        }
    }

    private void UpdateUI()
    {
        SetData(_currentData);
    }
}
