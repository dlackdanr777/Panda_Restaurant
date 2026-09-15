using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;


public class UIItemGacha : GachaMachineParent
{

    [Header("Components")]
    [SerializeField] private UIBouncingBall _bouncingBall;
    [SerializeField] private ScrollingImage _scrollImage;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _singleButton;
    public Button SingleButton => _singleButton;
    [SerializeField] private Button _tenButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Image _getItemImage;
    [SerializeField] private UIGachaCard _skinGachaCard;
    [SerializeField] private GachaCapsule _capsule;

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
    [SerializeField] private AudioClip _leverSound;
    [SerializeField] private AudioClip _shakeCapsuleSound;
    [SerializeField] private AudioClip _fallCapsuleSound;
    [SerializeField] private AudioClip _openDoorSound;
    [SerializeField] private AudioClip _boomSound;
    [SerializeField] private AudioClip _getNormalItemSound;
    [SerializeField] private AudioClip _getSpecialItemSound;


    private List<UIGachaCardSlot> _getItemSlotList = new List<UIGachaCardSlot>();
    private List<GachaItemData> _getItemList = new List<GachaItemData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getItemIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;
    private AudioClip _getItemSound;
    private bool _purchaseInProgress;


    public void PlayLeverSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _leverSound);
    }

    public void PlayShakeCapsuleSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _shakeCapsuleSound);
    }

    public void PlayFallCapsuleSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _fallCapsuleSound);
    }

    public void PlayOpenDoorSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _openDoorSound);
    }

    public void PlayBoomSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _boomSound);
    }

    public void PlayGetItemSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _getItemSound);
    }

    public override void Init(UIGacha uiGacha)
    {
        _uiGacha = uiGacha;
        _scrollImage.Init();
        _skinGachaCard.Init();
        _itemDataList = ItemManager.Instance.GetSortGachaItemDataList(GradeSortType.GradeDescending).Select((data) => (GachaData)data).ToList();

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
        _capsule.gameObject.SetActive(false);
        _bouncingBall.ResetBalls();
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
        _capsule.gameObject.SetActive(false);

        _gachaMacineAnimator.enabled = false;

        SetStep(1);
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


    public override void OnScreenButtonClicked()
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
                    _skinGachaCard.SetData(_getItemList[_getItemIndex - 1]);
                    _isPlayTextAnime = false;
                    _screenTouchWaitTime = 0.5f;
                    return;
                }
                if (_getItemList.Count() <= _getItemIndex - 1)
                {
                    OnSkipButtonClicked();
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
            
        _uiGacha.StartGachaStepEvent(_currentStep);
        switch (step)
        {
            case 1:
                _currentStep = 1;
                StopAllCoroutines();

                _uiGacha.SetActiveUIComponents(true);
                _uiGacha.SetStartGacha(false);
                _singleButton.gameObject.SetActive(true);
                _tenButton.gameObject.SetActive(true);
                _screenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _getItemIndex = 0;
                _isCapsuleColorChanged = true;
                _skinGachaCard.TweenStop();
                _uiGacha.SetActiveGachaMachine(true);
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    _getItemSlotList[i].gameObject.SetActive(false);
                    _getItemSlotList[i].TweenStop();
                }
                CapsuleSetSibilingIndex(1);
                break;

            case 2:
                _currentStep = 2;
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _uiGacha.SetActiveUIComponents(false);
                _uiGacha.SetStartGacha(true);
                _singleButton.gameObject.SetActive(false);
                _tenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(1);
                break;

            case 3:
                _currentStep = 3;
               
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;

                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;

                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(true);
    
                _skipButton.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);

                _isCapsuleColorChanged = true;

                _isPlayTextAnime = true;
                if (10 <= _getItemList.Count() && _getItemList.Count() <= _getItemIndex + 1)
                {
                    OnSkipButtonClicked();
                }
                else
                {
                    _screenTouchWaitTime = 0.2f;
                    _getItemSound = _getItemList[_getItemIndex].Rank == Rank.Unique || _getItemList[_getItemIndex].Rank == Rank.Special ? _getSpecialItemSound : _getNormalItemSound;
                    PlayGetItemSound();

                    _skinGachaCard.gameObject.SetActive(true);
                    _skinGachaCard.ResetScale();
                    _getItemImage.gameObject.SetActive(true);
                    _skinGachaCard.SetData(_getItemList[_getItemIndex]);
                    _getItemImage.sprite = _getItemList[_getItemIndex].ThumbnailSprite;
                    Utility.ChangeImagePivot(_getItemImage);
                    _skinGachaCard.SetPosition(new Vector3(0, 0, 0));
                }

                _getItemIndex++;
                break;
        }
    }

    public bool StartAddItem(GachaItemData data)
    {
        // Only the current item tutorial may use this free, fixed result path. The
        // existing count/ownership is also a replay fence after a view is recreated.
        if (_purchaseInProgress || !GachaTutorial.IsCurrentItemTutorialQuest()
            || data == null || data.Id != "GOTCHA91" || !IsRegisteredPurchaseItem(data)
            || UserInfo.IsGiveGachaItem(data) || UserInfo.TotalUseGachaMachineCount == int.MaxValue)
            return false;

        _purchaseInProgress = true;
        try
        {
            if (!UserInfo.GiveGachaItem(data)) return false;
            UserInfo.AddUserGachaMachineCount();
            PreparePurchasePresentation();
            _getItemList.Clear();
            _getItemIndex = 0;
            _getItemList.Add(data);
            StartTutorialPresentation();
            return true;
        }
        finally { _purchaseInProgress = false; }
    }

    protected virtual void StartTutorialPresentation() => _gachaMacineAnimator.SetTrigger("Start");

    public override void OnSingleGachaButtonClicked()
    {
        Purchase(1, 10);
    }


    public override void OnTenGachaButtonClicked()
    {
        Purchase(11, 100);
    }

    private void Purchase(int count, int cost)
    {
        if (_purchaseInProgress) return;
        _purchaseInProgress = true;
        try
        {
            if (!UserInfo.IsDiaValid(cost))
            { ReportPurchaseError("현재 다이아를 사용할 수 없습니다."); return; }
            // Resolve the whole batch before deduction. Never charge for a null, unregistered,
            // wrong-type or partially selected result; retain the previous presentation on rejection.
            List<GachaItemData> results;
            try
            {
                var source = _itemDataList == null ? null : new List<GachaData>(_itemDataList);
                if (source == null || source.Count == 0 || source.Any(item => !IsRegisteredPurchaseItem(item)))
                { ReportPurchaseError("아이템 뽑기 자료를 확인할 수 없습니다."); return; }
                results = new List<GachaItemData>(count);
                for (int index = 0; index < count; index++)
                {
                    GachaData selected = DrawPurchaseResult(source);
                    if (!source.Any(item => ReferenceEquals(item, selected)) || !IsRegisteredPurchaseItem(selected))
                    { ReportPurchaseError("아이템 뽑기 결과를 확인할 수 없습니다."); return; }
                    results.Add((GachaItemData)selected);
                }
                if (results.Any(item => !IsRegisteredPurchaseItem(item)))
                { ReportPurchaseError("아이템 뽑기 자료가 변경되었습니다."); return; }
            }
            catch (Exception exception)
            {
                DebugLog.Log(exception.Message);
                ReportPurchaseError("아이템 뽑기 자료를 확인할 수 없습니다.");
                return;
            }
            if (!UserInfo.TrySpendDia(cost, out string error))
            { ReportPurchaseError(error); return; }
            ApplyPurchaseResults(results);
        }
        finally { _purchaseInProgress = false; }
    }

    private static bool IsRegisteredPurchaseItem(GachaData value)
    {
        if (!(value is GachaItemData item) || string.IsNullOrWhiteSpace(item.Id)
            || item.Rank < Rank.Normal1 || item.Rank >= Rank.Length) return false;
        return ReferenceEquals(ItemManager.Instance.GetGachaItemData(item.Id), item);
    }

    protected virtual GachaData DrawPurchaseResult(List<GachaData> source)
        => ItemManager.Instance.GetRandomGachaData(source);

    private void ApplyPurchaseResults(List<GachaItemData> results)
    {
        PreparePurchasePresentation();
        _getItemList.Clear();
        _getItemIndex = 0;
        _getItemList.AddRange(results);
        if (results.Count == 1)
        {
            if (!UserInfo.GiveGachaItem(results[0]))
            { ReportPurchaseError("아이템 지급 결과를 확인할 수 없습니다."); return; }
        }
        else UserInfo.GiveGachaItem(_getItemList);
        CompletePurchasePresentationAndSave(results.Count);
    }

    protected virtual void PreparePurchasePresentation()
    {
        _uiGacha.SetActiveGachaMachine(false);
        SetActiveGachaMachine(true);
    }

    protected virtual void CompletePurchasePresentationAndSave(int count)
    {
        _gachaMacineAnimator.SetTrigger("Start");
        UserInfo.AddUserGachaMachineCount(count);
        GameManager.Instance.AsyncSaveGameData();
        PaymentInfo.AddGachaData($"Normal Item Gacha {count}");
        PaymentInfo.SavePaymentData();
    }

    protected virtual void ReportPurchaseError(string error) => PopupManager.Instance.ShowDisplayText(error);

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
        _screenTouchWaitTime = 5f;
        _getItemIndex = _getItemList.Count - 1;
        StopAllCoroutines();
        StartCoroutine(SkipRoutine());
    }
    

    private IEnumerator SkipRoutine()
    {
        _skinGachaCard.gameObject.SetActive(false);
        _getItemImage.gameObject.SetActive(false);
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        _getItemSlotFrame.gameObject.SetActive(true);

  for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
        {
            _getItemSlotList[i].gameObject.SetActive(false);
        }
        yield return null; ;
        _skinGachaCard.gameObject.SetActive(false);
        for (int i = 0, cnt = _getItemList.Count - 1; i < cnt; i++)
        {
            _getItemSlotList[i].SetData(_getItemList[i]);
            _getItemSlotList[i].gameObject.SetActive(true);
            _getItemSlotList[i].TweenStop();
            _getItemSlotList[i].transform.localScale = Vector3.one * 1.2f;
            _getItemSlotList[i].TweenScale(Vector3.one, 0.2f, Ease.OutBack);
            _getItemSlotList[i].ChangeImagePivot();
            yield return YieldCache.WaitForSeconds(0.1f);
        }
        yield return YieldCache.WaitForSeconds(0.1f);
        _capsule.gameObject.SetActive(true);
        _capsule.SetCapsuleColor(_capsuleColors[UnityEngine.Random.Range(0, _capsuleColors.Length)]);
        _capsule.TweenStop();
        _capsule.SetAnchoredPosition(new Vector2(0, -2000));
        _capsule.TweenAnchoredPosition(new Vector2(0, 0), 1f, Ease.Smoothstep);

        yield return YieldCache.WaitForSeconds(1.5f);

        _capsule.StartOpen();
        _capsule.SetSprite(_getItemList[_getItemList.Count - 1].ThumbnailSprite);

        yield return YieldCache.WaitForSeconds(1.5f);

        _capsule.gameObject.SetActive(false);
        _skinGachaCard.gameObject.SetActive(true);
        _skinGachaCard.SetData(_getItemList[_getItemList.Count - 1]);
        _skinGachaCard.SetPosition(new Vector3(600, 0, 0));
        _skinGachaCard.TweenStop();

        _skinGachaCard.transform.localScale = Vector3.one * 1.3f;
        _skinGachaCard.TweenScale(Vector3.one * 1f, 0.2f, Ease.OutBack);
        
        _getItemSound = _getItemList[_getItemList.Count - 1].Rank == Rank.Unique || _getItemList[_getItemList.Count - 1].Rank == Rank.Special ? _getSpecialItemSound : _getNormalItemSound;
        PlayGetItemSound();
    }
}
