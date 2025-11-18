using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Linq;


public class UISkinGacha : GachaMachineParent
{
    public event Action<int> GachaStepHandler;

    [Header("Components")]
    [SerializeField] private ScrollingImage _scrollImage;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _singleButton;
    public Button SingleButton => _singleButton;
    [SerializeField] private Button _tenButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Image _getItemImage;
    [SerializeField] private UIGachaCard _skinGachaCard;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _getItemSlotFrame;
    [SerializeField] private UIGachaCardSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;
    [SerializeField] private Capsule[] _capsuleColors;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _getNormalItemSound;
    [SerializeField] private AudioClip _getSpecialItemSound;
    [SerializeField] private AudioSource _gachaSound;

    private List<UIGachaCardSlot> _getItemSlotList = new List<UIGachaCardSlot>();
    private List<SkinData> _getItemList = new List<SkinData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getItemIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;
    private AudioClip _getItemSound;



    public void PlayGetItemSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _getItemSound);
    }

    public void PlayGachaSound()
    {
        _gachaSound.Play();
    }

    public override void Init(UIGacha uiGacha)
    {
        _uiGacha = uiGacha;
        _scrollImage.Init();
        _itemDataList = SkinDataManager.Instance.GetSortSkinDataList(GradeSortType.GradeDescending).Select(data => (GachaData)data).ToList();

        for (int i = 0; i < 10; ++i)
        {
            UIGachaCardSlot slot = Instantiate(_slotPrefab, _getItemSlotFrame);
            _getItemSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
        _skipButton.onClick.AddListener(OnSkipButtonClicked);

        SetStep(1);
        _skinGachaCard.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }


    public override void Show()
    {
        gameObject.SetActive(true);
        _singleButton.gameObject.SetActive(true);
        _tenButton.gameObject.SetActive(true);
        _uiGacha.SetActiveUIComponents(true);
        _scrollImage.gameObject.SetActive(true);
        _screenButton.gameObject.SetActive(false);
        _skinGachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        CapsuleSetSibilingIndex(1);

        SetStep(1);
        _screenTouchWaitTime = 0;
        _gachaMacineAnimator.enabled = true;
        OnScreenButtonClicked();
    }


    public override void Hide()
    {
        gameObject.SetActive(true);
        _screenTouchWaitTime = 0;
        _singleButton.gameObject.SetActive(false);
        _tenButton.gameObject.SetActive(false);
        _uiGacha.SetActiveUIComponents(false);
        _scrollImage.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _skinGachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);

        _gachaMacineAnimator.enabled = false;
        SetStep(1);
    }


    public void GetItem(SkinData data)
    {
        _getItemList.Clear();
        _getItemIndex = 0;

        _getItemList.Add(data);
        UserInfo.GiveSkin(data);

        _gachaMacineAnimator.SetTrigger("Start");
    }

    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }


    public override void OnScreenButtonClicked()
    {
        if (0 < _screenTouchWaitTime)
        {
            DebugLog.Log("아직 터치할 수 없습니다.");
            return;
        }
        _gachaSound.Stop();
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
                    _skinGachaCard.TweenStop();
                    _skinGachaCard.SetData(_getItemList[_getItemIndex - 1]);
                    _isPlayTextAnime = false;
                    _screenTouchWaitTime = 0.5f;
                    return;
                }
                if (_getItemList.Count() <= _getItemIndex - 1)
                {
                    for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                    {
                        if (_getItemSlotList[i].gameObject.activeSelf)
                            continue;

                        _getItemSlotList[i].gameObject.SetActive(true);
                        _getItemSlotList[i].SetData(_getItemList[i]);
                        break;
                    }
                }
                else
                {
                    for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                    {
                        _getItemSlotList[i].gameObject.SetActive(false);
                    }
                }

                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;
        
        }

    }

    public void SetStep(int step)
    {
        if (_currentStep == step)
            return;

        switch (step)
        {
            case 1:
                _currentStep = 1;

                _uiGacha.SetActiveUIComponents(true);
                _uiGacha.SetActiveGachaMachine(true);
                _singleButton.gameObject.SetActive(true);
                _tenButton.gameObject.SetActive(true);
                _screenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);

                _getItemIndex = 0;
                _isCapsuleColorChanged = true;

                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    _getItemSlotList[i].gameObject.SetActive(false);
                }

                CapsuleSetSibilingIndex(1);
                break;

            case 2:
                _currentStep = 2;
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(true);
                _uiGacha.SetActiveUIComponents(false);
                _singleButton.gameObject.SetActive(false);
                _tenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(1);
                break;

            case 3:
                _currentStep = 3;

                _screenButton.gameObject.SetActive(true);
                _getItemImage.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(true);
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].ThumbnailSprite;
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;

                _skipButton.gameObject.SetActive(true);
                _getItemImage.gameObject.SetActive(true);
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].ThumbnailSprite;
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;

                _skinGachaCard.gameObject.SetActive(true);
                _getItemSlotFrame.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(false);
                _getItemImage.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                _isCapsuleColorChanged = true;

                _isPlayTextAnime = true;
                _skinGachaCard.SetData(_getItemList[_getItemIndex]);
                _getItemImage.sprite = _getItemList[_getItemIndex].ThumbnailSprite;
                _getItemSound = _getItemList[_getItemIndex].Rank == Rank.Unique || _getItemList[_getItemIndex].Rank == Rank.Special ? _getSpecialItemSound : _getNormalItemSound;
                PlayGetItemSound();
                _getItemIndex++;
                CapsuleSetSibilingIndex(11);
                break;
        }

        GachaStepHandler?.Invoke(_currentStep);
    }



    public override void OnSingleGachaButtonClicked()
    {

        if(UserInfo.IsDiaValid(1))
        {
            _uiGacha.SetActiveGachaMachine(false);
            SetActiveGachaMachine(true);
        
            _getItemList.Clear();
            _getItemIndex = 0;

            SkinData item = (SkinData)ItemManager.Instance.GetRandomGachaData(_itemDataList);
            _getItemList.Add(item);
            UserInfo.GiveSkin(item);

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-1);
            UserInfo.AddUserGachaMachineCount();
            GameManager.Instance.SaveGameData();
        }

        else
        {
            PopupManager.Instance.ShowTextLackDia();
        }
    }


    public override void OnTenGachaButtonClicked()
    {
        if(UserInfo.IsDiaValid(10))
        {
            _uiGacha.SetActiveGachaMachine(false);
            SetActiveGachaMachine(true);
        
            _getItemList.Clear();
            _getItemIndex = 0;

            SkinData item;
            int i = 0;
            while (i < 11)
            {
                item = (SkinData)ItemManager.Instance.GetRandomGachaData(_itemDataList);

                // if (!UserInfo.CanAddMoreItems(item))
                //     continue;

                _getItemList.Add(item);
                i++;
            }

            UserInfo.GiveSkinList(_getItemList);

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-10);
            UserInfo.AddUserGachaMachineCount(11);
            GameManager.Instance.SaveGameData();
        }
        else
        {
            PopupManager.Instance.ShowTextLackDia();
        }

    }


    private void CapsuleColorChange()
    {
        if (!_isCapsuleColorChanged)
            return;

        int randInt = UnityEngine.Random.Range(0, _capsuleColors.Length);
        _upperCapsule.sprite = _capsuleColors[randInt].UpperCapsule;
        _lowerCapsule.sprite = _capsuleColors[randInt].LowerCapsule;
        _isCapsuleColorChanged = false;
    }


    private void OnSkipButtonClicked()
    {
        _skinGachaCard.gameObject.SetActive(true);
        _getItemImage.gameObject.SetActive(false);
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        CapsuleSetSibilingIndex(12);
        _skinGachaCard.SetData(_getItemList[_getItemList.Count - 1]);
        CapsuleSetSibilingIndex(9);
        _gachaSound.Stop();
        _getItemSlotFrame.gameObject.SetActive(true);
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
