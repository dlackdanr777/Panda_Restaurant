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
    [SerializeField] private TextMeshProUGUI _typeText2;

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
    [SerializeField] private UISlot _slotPrefab;

    private KitchenUtensilType _currentType;
    private List<UISlot>[] _slots = new List<UISlot>[(int)KitchenUtensilType.Length];
    List<KitchenUtensilData> _currentTypeDataList;

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeKitchenData(-1));
        _rightArrowButton.SetAction(() => ChangeKitchenData(1));
        _uikitchenPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for (int i = 0, cntI = (int)KitchenUtensilType.Length; i < cntI; ++i)
        {
            List<KitchenUtensilData> typeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)i);
            _slots[i] = new List<UISlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UISlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeKitchenUtensilHandler += (type) => OnSlotUpdate(false);
        UserInfo.OnGiveKitchenUtensilHandler += () => OnSlotUpdate(false);
        UserInfo.OnChangeMoneyHandler += () => OnSlotUpdate(false);
        UserInfo.OnChangeScoreHanlder += () => OnSlotUpdate(false);

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _uiRestaurantAdmin.MainUISetActive(false);

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
        _typeText2.text = typeStr;

        OnSlotUpdate(true);
    }

    private void SetKitchenPreview()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(_currentType);
        _uikitchenPreview.SetData(equipData);
    }


    private void ChangeKitchenData(int dir)
    {
        KitchenUtensilType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? KitchenUtensilType.Length - 1 : (KitchenUtensilType)((int)newTypeIndex % (int)KitchenUtensilType.Length);
        SetKitchenUtensilDataData(newTypeIndex);
        SetKitchenPreview();
    }

    
    private void OnEquipButtonClicked(KitchenUtensilData data)
    {
        UserInfo.SetEquipKitchenUtensil(data);
        SetKitchenUtensilDataData(_currentType);
        SetKitchenPreview();
    }

    private void OnBuyButtonClicked(KitchenUtensilData data)
    {
        if (UserInfo.IsGiveKitchenUtensil(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if (UserInfo.Score < data.BuyScore)
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (UserInfo.Money < data.BuyPrice)
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AppendMoney(-data.BuyPrice);
        UserInfo.GiveKitchenUtensil(data);
        TimedDisplayManager.Instance.ShowText("새로운 주방 용품을 구매했어요!");
    }




    private void OnSlotUpdate(bool changeOutline)
    {
        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0 || !gameObject.activeSelf)
            return;

        KitchenUtensilData equipStaffData = UserInfo.GetEquipKitchenUtensil(_currentType);

        int slotsIndex = (int)_currentType;
        KitchenUtensilData data;
        UISlot slot;
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            if (equipStaffData != null && data.Id == equipStaffData.Id)
            {
                slot.transform.SetAsFirstSibling();
                slot.SetUse(data.ThumbnailSprite, data.Name, "사용중");
                if (changeOutline) _slots[slotsIndex][i].SetOutline(true);
                continue;
            }

            if (changeOutline) _slots[slotsIndex][i].SetOutline(false);
            if (UserInfo.IsGiveKitchenUtensil(data))
            {
                slot.SetOperate(data.ThumbnailSprite, data.Name, "사용");
                continue;
            }

            else
            {
                if (data.BuyScore <= UserInfo.Score && data.BuyPrice <= UserInfo.Money)
                {
                    slot.SetEnoughMoney(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyPrice));
                    continue;
                }

                slot.SetLowReputation(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyScore));
                continue;
            }
        }
    }


    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(data);
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; i++)
        {
            bool outlineEnabled = _currentTypeDataList[i] == data ? true : false;
            _slots[(int)_currentType][i].SetOutline(outlineEnabled);
        }
    }
}
