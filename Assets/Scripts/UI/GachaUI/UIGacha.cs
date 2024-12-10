using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

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
    public event Action<int> GachaStepHandler;

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIBouncingBall _bouncingBall;
    [SerializeField] private ScrollingImage _scrollImage;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private UIImageAndText _gachaItemName;
    [SerializeField] private UIGachaItemList _gachaItemList;
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _singleButton;
    public Button SingleButton => _singleButton;
    [SerializeField] private Button _tenButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private GameObject _uiComponents;
    [SerializeField] private Image _getItemImage;
    [SerializeField] private UIItemStar _itemStar;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _getItemSlotFrame;
    [SerializeField] private UIGachaItemSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;
    [SerializeField] private Capsule[] _capsuleColors;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _backgroundAudio;
    [SerializeField] private AudioClip _mainAudio;
    [SerializeField] private AudioClip _leverSound;
    [SerializeField] private AudioClip _shakeCapsuleSound;
    [SerializeField] private AudioClip _fallCapsuleSound;
    [SerializeField] private AudioClip _openDoorSound;
    [SerializeField] private AudioClip _boomSound;
    [SerializeField] private AudioClip _getNormalItemSound;
    [SerializeField] private AudioClip _getSpecialItemSound;


    private List<GachaItemData> _itemDataList;
    private List<UIGachaItemSlot> _slotList = new List<UIGachaItemSlot>();
    private List<UIGachaItemSlot> _getItemSlotList = new List<UIGachaItemSlot>();
    private List<GachaItemData> _getItemList = new List<GachaItemData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getItemIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;
    private AudioClip _getItemSound;


    public void PlayLeverSound()
    {
        SoundManager.Instance.PlayEffectAudio(_leverSound);
    }

    public void PlayShakeCapsuleSound()
    {
        SoundManager.Instance.PlayEffectAudio(_shakeCapsuleSound);
    }

    public void PlayFallCapsuleSound()
    {
        SoundManager.Instance.PlayEffectAudio(_fallCapsuleSound);
    }

    public void PlayOpenDoorSound()
    {
        SoundManager.Instance.PlayEffectAudio(_openDoorSound);
    }

    public void PlayBoomSound()
    {
        SoundManager.Instance.PlayEffectAudio(_boomSound);
    }

    public void PlayGetItemSound()
    {
        SoundManager.Instance.PlayEffectAudio(_getItemSound);
    }

    public override void Init()
    {
        _scrollImage.Init();
        _itemDataList = ItemManager.Instance.GetSortGachaItemDataList();
        _gachaItemList.Init(_itemDataList);

        for (int i = 0; i < 10; ++i)
        {
            UIGachaItemSlot slot = Instantiate(_slotPrefab, _getItemSlotFrame);
            _getItemSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
        _skipButton.onClick.AddListener(OnSkipButtonClicked);

        SetStep(1);
        _gachaItemName.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_backgroundAudio, 0.5f);
        gameObject.SetActive(true);
        _singleButton.gameObject.SetActive(true);
        _tenButton.gameObject.SetActive(true);
        _uiComponents.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _scrollImage.gameObject.SetActive(false);
        _gachaItemName.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        _bouncingBall.ResetBalls();
        CapsuleSetSibilingIndex(1);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
            _uiComponents.SetActive(true);
            _scrollImage.gameObject.SetActive(true);
            _gachaMacineAnimator.enabled = true;
        });

        SetStep(1);
        _screenTouchWaitTime = 0;
        _gachaMacineAnimator.enabled = false;
        OnScreenButtonClicked();
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        SoundManager.Instance.PlayBackgroundAudio(_mainAudio, 0.5f);
        _screenTouchWaitTime = 0;
        gameObject.SetActive(false);
    }


    public void GetItem(GachaItemData data)
    {
        _getItemList.Clear();
        _getItemIndex = 0;

        _getItemList.Add(data);
        UserInfo.GiveGachaItem(data);

        _gachaMacineAnimator.SetTrigger("Start");
    }

    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }

    public void StartBallBounce()
    {
        _bouncingBall.NoSpeedDamping = false;
        _bouncingBall.StartBounce();
    }

    public void StopBallBounce()
    {
        _bouncingBall.NoSpeedDamping = true;
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
                StopBallBounce();
                break;

            case 3:
                _gachaMacineAnimator.SetTrigger("CapsuleOpen");
                StopBallBounce();
                break;

            case 5:
                StopBallBounce();
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
                    _getItemSlotList[i].UpdateSlot(currentItem);
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

        switch (step)
        {
            case 1:
                _currentStep = 1;

                _uiComponents.SetActive(true);
                _screenButton.gameObject.SetActive(false);
                _gachaItemName.gameObject.SetActive(false);
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
                _uiComponents.SetActive(false);
                _gachaItemName.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(1);
                break;

            case 3:
                _currentStep = 3;

                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(true);
                _gachaItemName.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;

                _gachaItemName.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;

                _gachaItemName.gameObject.SetActive(true);
                _getItemSlotFrame.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                _isCapsuleColorChanged = true;

                _isPlayTextAnime = true;
                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                _itemStar.SetStar(_getItemList[_getItemIndex].GachaItemRank);
                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                _getItemSound = _getItemList[_getItemIndex].GachaItemRank == GachaItemRank.Unique || _getItemList[_getItemIndex].GachaItemRank == GachaItemRank.Special ? _getSpecialItemSound : _getNormalItemSound;
                PlayGetItemSound();
                _gachaItemName.SetText(string.Empty);
                _gachaItemName.TweenCharacter(_getItemList[_getItemIndex].Name, 0.08f, Ease.Constant).OnComplete(() => _isPlayTextAnime = false);
                _getItemIndex++;
                CapsuleSetSibilingIndex(11);
                break;
        }

        GachaStepHandler?.Invoke(_currentStep);
    }



    private void OnSingleGachaButtonClicked()
    {

        if(1 <= UserInfo.Dia)
        {
            _getItemList.Clear();
            _getItemIndex = 0;

            GachaItemData item = ItemManager.Instance.GetRandomGachaItem(_itemDataList);
            _getItemList.Add(item);
            UserInfo.GiveGachaItem(item);

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-1);
        }

        else
        {
            PopupManager.Instance.ShowTextLackDia();
        }

    }


    private void OnTenGachaButtonClicked()
    {
        if(10 <= UserInfo.Dia)
        {
            _getItemList.Clear();
            _getItemIndex = 0;

            GachaItemData item;
            int i = 0;
            while (i < 11)
            {
                item = ItemManager.Instance.GetRandomGachaItem(_itemDataList);

                if (!UserInfo.CanAddMoreItems(item))
                    continue;

                _getItemList.Add(item);
                i++;
            }

            UserInfo.GiveGachaItem(_getItemList);

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-10);
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
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        CapsuleSetSibilingIndex(12);
        _getItemImage.sprite = _getItemList[_getItemList.Count - 1].Sprite;
        Utility.ChangeImagePivot(_getItemImage);
        _gachaItemName.TweenStop();
        _gachaItemName.SetText(_getItemList[_getItemList.Count - 1].Name);
        CapsuleSetSibilingIndex(9);

        _getItemSlotFrame.gameObject.SetActive(true);
        for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
        {
            _getItemSlotList[i].gameObject.SetActive(false);
        }

        for(int i = 0, cnt = _getItemList.Count - 1; i < cnt; i++)
        {
            _getItemSlotList[i].UpdateSlot(_getItemList[i]);
            _getItemSlotList[i].gameObject.SetActive(true);
        }
        _getItemIndex = _getItemList.Count - 1;
    }
}
