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
    [SerializeField] private GameObject _listImage;
    [SerializeField] private Image _getItemImage;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _slotParent;
    [SerializeField] private Transform _getItemSlotParent;
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
    private int _currentStep;
    private bool _isCapsuleColorChanged;

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

        SetStep(1);
        _gachaItemName.gameObject.SetActive(false);
        _listImage.gameObject.SetActive(false);
        gameObject.SetActive(false);

    }

    public override void Show()
    {
        SetStep(1);
        OnScreenButtonClicked();
        gameObject.SetActive(true);
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }


    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }

    public void OnScreenButtonClicked()
    {
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
                if (_getItemList.Count <= 0)
                {
                    _gachaMacineAnimator.SetTrigger("Stop");
                    return;
                }
                GachaItemData currentItem = _getItemList[0];
                _getItemList.RemoveAt(0);
                if(_getItemList.Count <= 0 )
                {
                    _gachaMacineAnimator.SetTrigger("Stop");
                    return;
                }

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
                _isCapsuleColorChanged = true;

                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    _getItemSlotList[i].gameObject.SetActive(false);
                }
                _getItemSlotParent.gameObject.SetActive(false);
                CapsuleSetSibilingIndex(1);
                break;
            case 2:
                _currentStep = 2;

                CapsuleColorChange();
                _getItemSlotParent.gameObject.SetActive(false);
                CapsuleSetSibilingIndex(1);
                break;
            case 3:
                _currentStep = 3;

                CapsuleColorChange();
                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }

                _getItemSlotParent.gameObject.SetActive(setActive);
                CapsuleSetSibilingIndex(9);
                break;

            case 4:
                _currentStep = 4;

                CapsuleColorChange();
                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }
                _getItemImage.sprite = _getItemList[0].Sprite;
                _getItemSlotParent.gameObject.SetActive(setActive);
                CapsuleSetSibilingIndex(9);
                break;

            case 5:
                _currentStep = 5;
                _isCapsuleColorChanged = true;

                setActive = false;
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    if (!_getItemSlotList[i].gameObject.activeSelf)
                        continue;

                    setActive = true;
                    break;
                }

                _getItemImage.sprite = _getItemList[0].Sprite;
                _gachaItemName.SetText(string.Empty);
                _gachaItemName.TweenText(_getItemList[0].Name, 0.5f);
                CapsuleSetSibilingIndex(9);
                break;
        }
    }



    private void OnSingleGachaButtonClicked()
    {
        _gachaMacineAnimator.SetTrigger("Start");
        _getItemList.Clear();

        _getItemList.Add(ItemManager.Instance.GetRandomGachaItem(_itemDataList));
    }


    private void OnTenGachaButtonClicked()
    {
        _gachaMacineAnimator.SetTrigger("Start");
        _getItemList.Clear();

        for(int i = 0, cnt = 11; i < cnt; i++)
        {
            _getItemList.Add(ItemManager.Instance.GetRandomGachaItem(_itemDataList));
        }

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



}
