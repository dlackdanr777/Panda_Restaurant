using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;

public class UIGacha : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private Button _screenButton;
    [SerializeField] private ButtonPressEffect _singleButton;
    [SerializeField] private ButtonPressEffect _tenButton;
    [SerializeField] private ButtonPressEffect _listButton;
    [SerializeField] private GameObject _listImage;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _slotParent;
    [SerializeField] private UIGachaItemSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;


    private List<GachaItemData> _itemDataList;
    private List<UIGachaItemSlot> _slotList = new List<UIGachaItemSlot>();
    private List<GachaItemData> _getItemList = new List<GachaItemData>();
    private int _currentStep;

    public override void Init()
    {
        _itemDataList = ItemManager.Instance.GetSortGachaItemDataList();

        for(int i = 0, cnt = _itemDataList.Count; i < cnt; ++i)
        {
            UIGachaItemSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetData(_itemDataList[i]);
            _slotList.Add(slot);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _singleButton.AddListener(OnSingleGachaButtonClicked);
        _listButton.AddListener(OnListButtonClicked);

        SetStep(1);
        _listImage.gameObject.SetActive(false);
        gameObject.SetActive(false);

    }

    public override void Show()
    {
        _canvasGroup.blocksRaycasts = true;
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
            case 2:
                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;

            case 3:
                _gachaMacineAnimator.SetTrigger("CapsuleOpen");
                break;
        
        }

    }

    public void SetStep(int step)
    {

        switch (step)
        {
            case 1:
                _currentStep = 1;
                _screenButton.gameObject.SetActive(false);
                CapsuleSetSibilingIndex(1);
                break;
            case 2:
                _currentStep = 2;
                _screenButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(1);
                break;
            case 3:
                _currentStep = 3;
                _screenButton.gameObject.SetActive(true);
                CapsuleSetSibilingIndex(7);
                break;

            case 4:
                _currentStep = 4;
                _screenButton.gameObject.SetActive(false);
                CapsuleSetSibilingIndex(7);
                break;
        }
    }



    private void OnSingleGachaButtonClicked()
    {
        _canvasGroup.blocksRaycasts = false;
        Tween.Wait(1, () => _canvasGroup.blocksRaycasts = true);

        _gachaMacineAnimator.SetTrigger("Start");
        _getItemList.Clear();

        _getItemList.Add(ItemManager.Instance.GetRandomGachaItem(_itemDataList));
        DebugLog.Log(_getItemList[0].Name);
    }


    private void OnListButtonClicked()
    {
        _listImage.gameObject.SetActive(!_listImage.gameObject.activeSelf);
    }



}
