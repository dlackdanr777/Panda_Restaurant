using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;

[System.Serializable]
public class Capsule
{
    [SerializeField] private Sprite _upperCapsule;
    public Sprite UpperCapsule => _upperCapsule;

    [SerializeField] private Sprite _lowerCapsule;
    public Sprite LowerCapsule => _lowerCapsule;
}

public class UIGacha : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private UIImageAndText _gachaItemName;
    [SerializeField] private Button _screenButton;
    [SerializeField] private ButtonPressEffect _singleButton;
    [SerializeField] private ButtonPressEffect _tenButton;
    [SerializeField] private ButtonPressEffect _listButton;
    [SerializeField] private ButtonPressEffect _skipButton;
    [SerializeField] private GameObject _listImage;
    [SerializeField] private GameObject _gachaMachineBackgroundImage;
    [SerializeField] private Image _getItemImage;
    [SerializeField] private UIItemStar _itemStar;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _slotParent;

    [SerializeField] private Transform _getItemSlotFrame;
    [SerializeField] private Transform _getItemSlotParent;
    [SerializeField] private GameObject _gachaResultText;
    [SerializeField] private UIGachaItemSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;
    [SerializeField] private Capsule[] _capsuleColors;


    private List<GachaItemData> _itemDataList;
    private List<UIGachaItemSlot> _slotList = new List<UIGachaItemSlot>();
    private List<UIGachaItemSlot> _getItemSlotList = new List<UIGachaItemSlot>();
    private List<GachaItemData> _getItemList = new List<GachaItemData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getItemIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;

    public override void Init()
    {
        _itemDataList = ItemManager.Instance.GetSortGachaItemDataList();

        for(int i = 0, cnt = _itemDataList.Count; i < cnt; ++i)
        {
            UIGachaItemSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetData(_itemDataList[i]);
            _slotList.Add(slot);
        }

        for(int i = 0; i < 10; ++i)
        {
            UIGachaItemSlot slot = Instantiate(_slotPrefab, _getItemSlotParent);
            _getItemSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _singleButton.AddListener(OnSingleGachaButtonClicked);
        _tenButton.AddListener(OnTenGachaButtonClicked);
        _listButton.AddListener(OnListButtonClicked);
        _skipButton.AddListener(OnSkipButtonClicked);

        SetStep(1);
        _gachaItemName.gameObject.SetActive(false);
        _listImage.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }


    public override void Show()
    {
        SetStep(1);
        _screenTouchWaitTime = 0;
        OnScreenButtonClicked();
        gameObject.SetActive(true);
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        _screenTouchWaitTime = 0;
        gameObject.SetActive(false);
    }


    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }

    public void OnScreenButtonClicked()
    {
        if (0 < _screenTouchWaitTime)
        {
            DebugLog.Log("아직 터치할 수 없습니다.");
            return;
        }

        switch (_currentStep)
        {
            case 1:
                _gachaMacineAnimator.SetTrigger("Stop");
                break;

            case 2:
                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;

            case 3:
                _gachaMacineAnimator.SetTrigger("CapsuleOpen");
                break;

            case 5:

                if (_getItemList.Count <= _getItemIndex)
                {
                    _gachaMacineAnimator.SetTrigger("Stop");
                    return;
                }

                if(_isPlayTextAnime)
                {
                    _gachaItemName.TweenStop();
                    _gachaItemName.SetText(_getItemList[_getItemIndex - 1].Name);
                    _isPlayTextAnime = false;
                    _screenTouchWaitTime = 0.5f;
                    return;
                }

                GachaItemData currentItem = _getItemList[_getItemIndex - 1];

                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    _getItemSlotList[i].gameObject.SetActive(true);
                    _getItemSlotList[i].SetData(currentItem);
                    break;
                }
                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;
        
        }

    }

    public void SetStep(int step)
    {
        if (_currentStep == step)
            return;

        bool setActive = false;
        switch (step)
        {
            case 1:
                _currentStep = 1;
                _getItemIndex = 0;
                _isCapsuleColorChanged = true;

                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    _getItemSlotList[i].gameObject.SetActive(false);
                }

                _getItemSlotFrame.gameObject.SetActive(false);
                _gachaResultText.gameObject.SetActive(false);
                _gachaMachineBackgroundImage.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);
                CapsuleSetSibilingIndex(1);
                break;
            case 2:
                _currentStep = 2;

                CapsuleColorChange();
                _screenTouchWaitTime = 0.2f;
                _getItemSlotFrame.gameObject.SetActive(false);
                _gachaMachineBackgroundImage.gameObject.SetActive(false);
                _gachaResultText.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(1);
                break;
            case 3:
                _currentStep = 3;

                CapsuleColorChange();
                _screenTouchWaitTime = 0.2f;
                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }

                _getItemSlotFrame.gameObject.SetActive(setActive);
                _gachaMachineBackgroundImage.gameObject.SetActive(true);
                _gachaResultText.gameObject.SetActive(setActive);
                _skipButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }
                _itemStar.SetStar(_getItemList[_getItemIndex].GachaItemRank);
                _getItemSlotFrame.gameObject.SetActive(setActive);
                _gachaResultText.gameObject.SetActive(setActive);
                _gachaMachineBackgroundImage.gameObject.SetActive(true);
                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);

                _skipButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;
                _screenTouchWaitTime = 0.2f;
                _isCapsuleColorChanged = true;

                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }

                _getItemSlotFrame.gameObject.SetActive(setActive);
                _gachaResultText.gameObject.SetActive(setActive);
                _gachaMachineBackgroundImage.gameObject.SetActive(true);
                _isPlayTextAnime = true;
                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                _gachaItemName.SetText(string.Empty);
                _gachaItemName.TweenCharacter(_getItemList[_getItemIndex].Name, 0.07f, Ease.Constant, () =>  _isPlayTextAnime = false);
                _getItemIndex++;

                _skipButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(11);
                break;
        }
    }



    private void OnSingleGachaButtonClicked()
    {
        _gachaMacineAnimator.SetTrigger("Start");
        _getItemList.Clear();
        _getItemIndex = 0;

        GachaItemData item = ItemManager.Instance.GetRandomGachaItem(_itemDataList);
        _getItemList.Add(item);
        UserInfo.GiveGachaItem(item);
    }


    private void OnTenGachaButtonClicked()
    {
        _gachaMacineAnimator.SetTrigger("Start");
        _getItemList.Clear();
        _getItemIndex = 0;

        GachaItemData item;
        for (int i = 0, cnt = 11; i < cnt; i++)
        {
            item = ItemManager.Instance.GetRandomGachaItem(_itemDataList);
            _getItemList.Add(item);
        }
        UserInfo.GiveGachaItem(_getItemList);
    }


    private void OnListButtonClicked()
    {
        _listImage.gameObject.SetActive(!_listImage.gameObject.activeSelf);
    }


    private void CapsuleColorChange()
    {
        if (!_isCapsuleColorChanged)
            return;

        int randInt = Random.Range(0, _capsuleColors.Length);
        _upperCapsule.sprite = _capsuleColors[randInt].UpperCapsule;
        _lowerCapsule.sprite = _capsuleColors[randInt].LowerCapsule;
        _isCapsuleColorChanged = false;
    }


    private void OnSkipButtonClicked()
    {
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        CapsuleSetSibilingIndex(12);
        _getItemImage.sprite = _getItemList[_getItemList.Count - 1].Sprite;
        Utility.ChangeImagePivot(_getItemImage);
        _gachaItemName.TweenStop();
        _gachaItemName.SetText(_getItemList[_getItemList.Count - 1].Name);
        CapsuleSetSibilingIndex(9);

        if (_getItemList.Count <= 1)
        {
            _getItemSlotParent.gameObject.SetActive(false);
            return;
        }

        _getItemSlotParent.gameObject.SetActive(true);
        for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
        {
            _getItemSlotList[i].gameObject.SetActive(false);
        }

        for(int i = 0, cnt = _getItemList.Count - 1; i < cnt; i++)
        {
            _getItemSlotList[i].SetData(_getItemList[i]);
            _getItemSlotList[i].gameObject.SetActive(true);
        }
        _getItemIndex = _getItemList.Count - 1;
    }
}
