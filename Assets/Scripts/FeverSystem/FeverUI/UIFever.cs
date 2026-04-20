using Muks.Tween;
using System.Collections;
using UnityEngine;

public class UIFever : MonoBehaviour
{
    private const int AdTime = 120; // 2분
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private GameObject _feverEffects;
    [SerializeField] private RectTransform _animeStartPos;
    [SerializeField] private RectTransform _animeEndPos;
    [SerializeField] private RectTransform _feverAnimeObj;
    [SerializeField] private Animator _feverAnimator;
    [SerializeField] private RectTransform _feverShineEffectObj;
    [SerializeField] private Animator _mirrorBallAnimator;
    [SerializeField] private WatchAdButton _feverAdButton;


    [Header("FeverMeter")]
    [SerializeField] private UIButtonAndPressEffect _feverButton;
    [SerializeField] private Vector3 _feverButtonNormalScale = Vector3.one;
    [SerializeField] private UITweenFillAmountImage _fillAmountImage;
    [SerializeField] private RectTransform _feverGauge;
    [SerializeField] private RectTransform _feverGaugeEffectObj;
    [SerializeField] private Animator _feverGaugeEffectAnimator;

    private Vector3 _tmpButtonScale = new Vector3(-1, 1, 1);
    private FeverSystem _ferverSystem;

    private float _tmpFeverGauge = 0;
    private int _tmpMaxFeverGauge = 0;


    public void Init(FeverSystem ferverSystem)
    {
        _ferverSystem = ferverSystem;
        _tmpFeverGauge = FeverSystem.FeverGauge;
        _tmpMaxFeverGauge = FeverSystem.CurrentMaxFeverGauge;
        _feverEffects.SetActive(false);
        _feverGaugeEffectAnimator.gameObject.SetActive(false);
        _feverButton.TweenStop();
        _feverButton.transform.localScale = _tmpButtonScale;
        _fillAmountImage.SetFillAmountNoAnime(FeverSystem.FeverGauge <= 0 ? 0 : 0.3f + ((float)FeverSystem.FeverGauge / FeverSystem.CurrentMaxFeverGauge) * 0.7f);
        bool isActive = FeverSystem.CurrentMaxFeverGauge <= FeverSystem.FeverGauge;
        _feverButton.interactable = isActive;
        if (isActive)
        {
            _feverButton.transform.localScale = _tmpButtonScale;
            _feverButton.TweenStop();
            _feverButton.TweenScale(_tmpButtonScale * 1.2f, 0.5f, Ease.InOutBack).Loop(LoopType.Yoyo);
            _mirrorBallAnimator.SetBool("Action", true);
        }
        else
        {
            _feverButton.TweenStop();
            _mirrorBallAnimator.SetBool("Action", false);
        }

        _feverButton.AddListener(OnFeverButtonClicked);
        _ferverSystem.OnStartFeverHandler += StartFeverEvent;
        _ferverSystem.OnEndFeverHandler += EndFeverEvent;

        _feverAdButton.OnAdRewarded += OnAdButtonClicked;
        _feverAdButton.OnDiaRewarded += OnDiaButtonClicked;
        TimeManager.Instance.OnRemoveTimeHandler += OnRemoveTimeEvent;

        OnUpdateAdButtonEvent();
        FeverAdButtonSetActive();

        InvokeRepeating(nameof(FeverAdButtonSetActive), 0, 1f);
    }


    private void OnDestroy()
    {
    }

    public void OnChangeGaugeNoAnime(float gaugeValue)
    {
        float fillAmount = gaugeValue <= 0 ? 0 : 0.3f + gaugeValue * 0.7f;
        _fillAmountImage.SetFillAmonut(fillAmount);
    }

    public void OnChangeGauge()
    {
        if (_tmpFeverGauge == FeverSystem.FeverGauge && _tmpMaxFeverGauge == FeverSystem.CurrentMaxFeverGauge)
            return;

        // 필 어마운트 설정
        _tmpFeverGauge = FeverSystem.FeverGauge;
        _tmpMaxFeverGauge = FeverSystem.CurrentMaxFeverGauge;
        float fillAmount = FeverSystem.FeverGauge <= 0 ? 0.3f : 0.3f + ((float)FeverSystem.FeverGauge / FeverSystem.CurrentMaxFeverGauge) * 0.7f;
        _fillAmountImage.SetFillAmonut(fillAmount);

        // 이펙트 위치 조정 - 특정 좌표로 매핑
        if (_feverGaugeEffectObj != null)
        {
            // 필 어마운트 0.3 -> x좌표 80, 필 어마운트 1.0 -> x좌표 210으로 매핑
            // 0.3과 1.0 사이의 값을 정규화
            float normalizedValue = 0f;
            if (fillAmount >= 0.3f)
            {
                // 0.3~1.0 범위를 0~1 범위로 정규화
                normalizedValue = (fillAmount - 0.3f) / 0.7f;
            }

            // 80에서 210 사이의 값으로 보간
            float xPos = Mathf.Lerp(80f, 210f, normalizedValue);

            // 이펙트 위치 설정
            _feverGaugeEffectObj.anchoredPosition = new Vector2(xPos, 0);

            if (0.3f < fillAmount)
            {
                _feverGaugeEffectAnimator.gameObject.SetActive(true);
                Tween.Wait(0.5f, () => _feverGaugeEffectAnimator.gameObject.SetActive(false));
            }
        }

        // 버튼 상태 설정
        bool isActive = FeverSystem.CurrentMaxFeverGauge <= FeverSystem.FeverGauge;
        _feverButton.interactable = isActive;
        if (isActive)
        {
            _feverButton.transform.localScale = _tmpButtonScale;
            _feverButton.TweenStop();
            _feverButton.TweenScale(_tmpButtonScale * 0.95f, 1f, Ease.InOutBack).Loop(LoopType.Yoyo);
            _mirrorBallAnimator.SetBool("Action", true);
        }
        else
        {
            _feverButton.TweenStop();
            _mirrorBallAnimator.SetBool("Action", false);
        }
    }


    private void OnFeverButtonClicked()
    {
        if (_ferverSystem.IsFeverStart || UserInfo.IsTutorialStart || !gameObject.activeInHierarchy)
            return;

        if (FeverSystem.FeverGauge < FeverSystem.CurrentMaxFeverGauge)
            return;

        _feverButton.TweenStop();
        _feverButton.transform.localScale = _tmpButtonScale;
        _ferverSystem.FeverStart();
        _feverAdButton.gameObject.SetActive(false);
    }


    private void StartFeverEvent()
    {
        float tweenTime = 1;
        _feverAdButton.gameObject.SetActive(false);
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();
        _feverEffects.SetActive(true);
        _feverShineEffectObj.gameObject.SetActive(false);
        _feverAnimeObj.TweenStop();
        _feverAnimator.SetFloat("Speed", 0f);
        _feverAnimeObj.transform.position = _animeStartPos.position;
        _feverAnimeObj.TweenAnchoredPosition(_animeEndPos.anchoredPosition, tweenTime, Ease.OutBack).OnComplete(() =>
        {
            _feverShineEffectObj.gameObject.SetActive(true);
            _feverAnimator.SetFloat("Speed", 1f);
        });
    }

    private void EndFeverEvent()
    {
        float tweenTime = 1;
        _feverAdButton.gameObject.SetActive(true);
        _feverAnimeObj.TweenStop();
        _feverAnimeObj.position = _animeEndPos.position;
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();
        _ferverSystem.SetFeverGauge(0);
        _tmpFeverGauge = 0;
        _feverShineEffectObj.gameObject.SetActive(false);
        _feverAnimator.SetFloat("Speed", 0f);
        _fillAmountImage.SetFillAmonut(FeverSystem.FeverGauge <= 0 ? 0 : 0.3f + ((float)FeverSystem.FeverGauge / FeverSystem.CurrentMaxFeverGauge) * 0.7f);
        _mirrorBallAnimator.SetBool("Action", false);
        _feverAnimeObj.TweenAnchoredPosition(_animeStartPos.anchoredPosition, tweenTime, Ease.OutBack).OnComplete(() =>
        {
            _feverEffects.gameObject.SetActive(false);
        });
    } 

      private void OnRemoveTimeEvent(string key)
    {
        if (key != ConstValue.AD_TIME_FEVER) return;

        OnUpdateAdButtonEvent();
    }


    private void OnUpdateAdButtonEvent()
    {
        _feverAdButton.gameObject.SetActive(true);
    }

    private void FeverAdButtonSetActive()
    {
        _feverAdButton.gameObject.SetActive(UserInfo.IsFeverTutorialClear);
    }


    private void OnAdButtonClicked()
    {
        TimeManager.Instance.SetTime(ConstValue.AD_TIME_FEVER, AdTime);
        UserInfo.AddFeverAdCount();
        _ferverSystem.SetFeverGauge(FeverSystem.CurrentMaxFeverGauge);
        OnChangeGauge();
        //OnUpdateAdButtonEvent();
    }

    private void OnDiaButtonClicked()
    {
        TimeManager.Instance.SetTime(ConstValue.AD_TIME_FEVER, AdTime);
        UserInfo.AddFeverDiaCount();
        _ferverSystem.SetFeverGauge(FeverSystem.CurrentMaxFeverGauge);
        OnChangeGauge();
        DebugLog.Log("UIFever Dia Clicked");
        //OnUpdateAdButtonEvent();
    }
}
