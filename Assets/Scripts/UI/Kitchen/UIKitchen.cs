using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIKitchen : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIKitchenPreview _uikitchenPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText1;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminSlot _slotPrefab;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;

    private KitchenUtensilType _currentType;
    private List<UIRestaurantAdminSlot>[] _slots = new List<UIRestaurantAdminSlot>[(int)KitchenUtensilType.Length];
    List<KitchenUtensilData> _currentTypeDataList;


    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeKitchenData(-1));
        _rightArrowButton.AddListener(() => ChangeKitchenData(1));
        _uikitchenPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for (int i = 0, cntI = (int)KitchenUtensilType.Length; i < cntI; ++i)
        {
            List<KitchenUtensilData> typeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)i);
            _slots[i] = new List<UIRestaurantAdminSlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UIRestaurantAdminSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeKitchenUtensilHandler += (type) => UpdateUI();
        UserInfo.OnGiveKitchenUtensilHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;

        SetKitchenUtensilDataData(KitchenUtensilType.Burner1);
        SetKitchenPreview();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _uiRestaurantAdmin.ShowKitchenTab();
        _uiRestaurantAdmin.MainUISetActive(false);
        transform.SetAsLastSibling();
        SetKitchenUtensilDataData(KitchenUtensilType.Burner1);
        SetKitchenPreview();

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() => 
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true; 
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _uiRestaurantAdmin.MainUISetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    public void ShowUIKitchen(KitchenUtensilType type)
    {
        _uiNav.Push("UIKitchen");
        SetKitchenUtensilDataData(type);
        SetKitchenPreview();
    }


    private void SetKitchenUtensilDataData(KitchenUtensilType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        }

        _currentType = type;
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(type);
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList(type);

        string typeStr = Utility.KitchenUtensilTypeStringConverter(type);
        _typeText1.text = typeStr;

        UpdateUI();
    }

    private void SetKitchenPreview()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(_currentType);

        if(equipData == null)
        {
            KitchenUtensilData firstSlotData = _currentTypeDataList[0];
            _uikitchenPreview.SetData(firstSlotData);
            return;
        }

        _uikitchenPreview.SetData(equipData);
    }


    private void ChangeKitchenData(int dir)
    {
        KitchenUtensilType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? KitchenUtensilType.Length - 1 : (KitchenUtensilType)((int)newTypeIndex % (int)KitchenUtensilType.Length);
        SetKitchenUtensilDataData(newTypeIndex);
        SetKitchenPreview();
    }

    
    private void OnEquipButtonClicked(ShopData data)
    {
        if (data == null)
        {
            SoundManager.Instance.PlayEffectAudio(_dequipSound);
            UserInfo.SetNullEquipKitchenUtensil(_currentType);
            SetKitchenUtensilDataData(_currentType);
            //SetKitchenPreview();
            return;
        }

        SoundManager.Instance.PlayEffectAudio(_equipSound);
        UserInfo.SetEquipKitchenUtensil(data.Id);
        SetKitchenUtensilDataData(_currentType);
        SetKitchenPreview();
    }

    private void OnBuyButtonClicked(ShopData data)
    {
        if (UserInfo.IsGiveKitchenUtensil(data.Id))
        {
            PopupManager.Instance.ShowTextError();
            return;
        }

        if (!UserInfo.IsScoreValid(data))
        {
            PopupManager.Instance.ShowTextLackScore();
            return;
        }

        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
        {
            PopupManager.Instance.ShowTextLackMoney();
            return;
        }

        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
        {
            PopupManager.Instance.ShowTextLackDia();
            return;
        }

        if (data.MoneyType == MoneyType.Gold)
            UserInfo.AddMoney(-data.BuyPrice);

        else if (data.MoneyType == MoneyType.Dia)
            UserInfo.AddDia(-data.BuyPrice);

        UserInfo.GiveKitchenUtensil(data.Id);
        PopupManager.Instance.ShowDisplayText("새로운 주방 기구를 구매했어요!");
    }




    private void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uikitchenPreview.UpdateUI();

        KitchenUtensilData equipStaffData = UserInfo.GetEquipKitchenUtensil(_currentType);
        int slotsIndex = (int)_currentType;
        KitchenUtensilData data;
        UIRestaurantAdminSlot slot;
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            if (equipStaffData != null && data.Id == equipStaffData.Id)
            {
                slot.transform.SetAsFirstSibling();
                slot.SetUse(data.ThumbnailSprite, data.Name, "배치중");
                continue;
            }

            if (UserInfo.IsGiveKitchenUtensil(data))
            {
                slot.SetOperate(data.ThumbnailSprite, data.Name, "배치하기");
                continue;
            }

            else
            {
                if (!UserInfo.IsScoreValid(data))
                {
                    slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                    continue;
                }

                if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                else if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
                continue;
            }
        }
    }


    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(data);
    }
}
