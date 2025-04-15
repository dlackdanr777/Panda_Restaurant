using Muks.Tween;
using System.Collections;
using UnityEngine;

public class UIFever : MonoBehaviour
{
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private GameObject _feverEffects;
    [SerializeField] private RectTransform _animeStartPos;
    [SerializeField] private RectTransform _animeEndPos;
    [SerializeField] private RectTransform _feverAnimeObj;
    [SerializeField] private Animator _feverAnimator;
    [SerializeField] private RectTransform _feverShineEffectObj;


    [Header("FeverMeter")]
    [SerializeField] private UIButtonAndPressEffect _feverButton;
    [SerializeField] private UITweenFillAmountImage _fillAmountImage;
    [SerializeField] private RectTransform _feverGauge;
    [SerializeField] private RectTransform _feverGaugeEffectObj;
    [SerializeField] private Animator _feverGaugeEffectAnimator;

    private Coroutine _feverRoutine;
    private Vector3 _tmpButtonScale;
    private FeverSystem _ferverSystem;

    private int _tmpFeverGauge = 0;
    public void Init(FeverSystem ferverSystem)
    {
        _ferverSystem = ferverSystem;
        _tmpFeverGauge = _ferverSystem.FeverGauge;
        _feverEffects.SetActive(false);
        _feverGaugeEffectAnimator.gameObject.SetActive(false);
        _tmpButtonScale = _feverButton.transform.localScale;
        _fillAmountImage.SetFillAmountNoAnime(_ferverSystem.FeverGauge <= 0 ? 0 : 0.3f + ((float)_ferverSystem.FeverGauge / ConstValue.MAX_PEVER_GAUGE) * 0.7f);
        bool isActive = ConstValue.MAX_PEVER_GAUGE <= _ferverSystem.FeverGauge;
        _feverButton.interactable = isActive;
        if (isActive)
        {
            _feverButton.transform.localScale = _tmpButtonScale;
            _feverButton.TweenStop();
            _feverButton.TweenScale(_tmpButtonScale * 1.2f, 0.5f, Ease.InOutBack).Loop(LoopType.Yoyo);
        }
        else
        {
            _feverButton.TweenStop();
        }

        _feverButton.AddListener(OnFeverButtonClicked);
    }


    private void OnDestroy()
    {
    }


    public void OnChangeGauge()
    {
        if(_tmpFeverGauge == _ferverSystem.FeverGauge)
            return;

        // 필 어마운트 설정
        _tmpFeverGauge = _ferverSystem.FeverGauge;
        float fillAmount = _ferverSystem.FeverGauge <= 0 ? 0 : 0.3f + ((float)_ferverSystem.FeverGauge / _ferverSystem.CurrentMaxFeverGauge) * 0.7f;
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

            if(0.3f < fillAmount)
            {
                _feverGaugeEffectAnimator.gameObject.SetActive(true);
                Tween.Wait(0.5f, () => _feverGaugeEffectAnimator.gameObject.SetActive(false));
            }
        }
        
        // 버튼 상태 설정
        bool isActive = _ferverSystem.CurrentMaxFeverGauge <= _ferverSystem.FeverGauge;
        _feverButton.interactable = isActive;
        if (isActive)
        {
            _feverButton.transform.localScale = _tmpButtonScale;
            _feverButton.TweenStop();
            _feverButton.TweenScale(_tmpButtonScale * 0.95f, 1f, Ease.InOutBack).Loop(LoopType.Yoyo);
        }
        else
        {
            _feverButton.TweenStop();
        }
    }


    private void OnFeverButtonClicked()
    {
        if (_ferverSystem.IsFeverStart || UserInfo.IsTutorialStart || !gameObject.activeInHierarchy)
            return;

        if (_ferverSystem.FeverGauge < _ferverSystem.CurrentMaxFeverGauge)
            return;

        _feverButton.TweenStop();
        _feverButton.transform.localScale = _tmpButtonScale;

        if (_feverRoutine != null)
            StopCoroutine(_feverRoutine);
        _feverRoutine = StartCoroutine(FeverCoroutine());
    }


    private IEnumerator FeverCoroutine()
    {
        float tweenTime = 1;
        _ferverSystem.SetFeverStart(true);
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();

        _feverEffects.SetActive(true);
        _feverShineEffectObj.gameObject.SetActive(false);
        _feverAnimeObj.TweenStop();
        _feverAnimator.SetFloat("Speed", 0f);
        _feverAnimeObj.transform.position = _animeStartPos.position;
        _feverAnimeObj.TweenAnchoredPosition(_animeEndPos.anchoredPosition, tweenTime, Ease.OutBack).OnComplete(() =>{
            _feverShineEffectObj.gameObject.SetActive(true);
            _feverAnimator.SetFloat("Speed", 1f);
            GameManager.Instance.SetGameSpeed(0.5f);
        });

        yield return YieldCache.WaitForSeconds(ConstValue.PEVER_TIME + tweenTime);

        _ferverSystem.SetFeverStart(false);
        _feverAnimeObj.TweenStop();
        _feverAnimeObj.position = _animeEndPos.position;
        GameManager.Instance.SetGameSpeed(0);
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();
        _ferverSystem.SetFeverGauge(0);
        _feverShineEffectObj.gameObject.SetActive(false);
        _feverAnimator.SetFloat("Speed", 0f);
        _fillAmountImage.SetFillAmonut(_ferverSystem.FeverGauge <= 0 ? 0 : 0.3f + ((float)_ferverSystem.FeverGauge / _ferverSystem.CurrentMaxFeverGauge) * 0.7f);

        _feverAnimeObj.TweenAnchoredPosition(_animeStartPos.anchoredPosition, tweenTime, Ease.OutBack).OnComplete(() =>
        {
            _feverEffects.gameObject.SetActive(false);
        });
    }
}
