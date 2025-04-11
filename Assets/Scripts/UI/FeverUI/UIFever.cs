using Muks.Tween;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIFever : MonoBehaviour
{
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIButtonAndPressEffect _feverButton;
    [SerializeField] private UITweenFillAmountImage _fillAmountImage;
    [SerializeField] private GameObject _feverEffects;
    [SerializeField] private RectTransform _animeStartPos;
    [SerializeField] private RectTransform _feverAnimeObj;

    private Coroutine _feverRoutine;

    private Vector3 _tmpButtonScale;


    private void Awake()
    {
        _feverEffects.SetActive(false);
        _tmpButtonScale = _feverButton.transform.localScale;
        _fillAmountImage.SetFillAmountNoAnime(GameManager.Instance.FeverGaguge <= 0 ? 0 : 0.3f + ((float)GameManager.Instance.FeverGaguge / ConstValue.MAX_PEVER_GAUGE) * 0.7f);
        bool isActive = ConstValue.MAX_PEVER_GAUGE <= GameManager.Instance.FeverGaguge;
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
        UserInfo.OnAddCustomerCountHandler += OnVisitCustomerEvent;
    }


    private void OnDestroy()
    {
        UserInfo.OnAddCustomerCountHandler -= OnVisitCustomerEvent;
    }


    private void OnVisitCustomerEvent()
    {
        if (_mainScene.IsFeverStart || UserInfo.IsTutorialStart || !gameObject.activeInHierarchy)
            return;

        GameManager.Instance.FeverGaguge = Mathf.Clamp(GameManager.Instance.FeverGaguge + 1, 0, ConstValue.MAX_PEVER_GAUGE);
        _fillAmountImage.SetFillAmonut(GameManager.Instance.FeverGaguge <= 0 ? 0 : 0.3f + ((float)GameManager.Instance.FeverGaguge / ConstValue.MAX_PEVER_GAUGE) * 0.7f);
        bool isActive = ConstValue.MAX_PEVER_GAUGE <= GameManager.Instance.FeverGaguge;
        _feverButton.interactable = isActive;
        if (isActive)
        {
            _feverButton.transform.localScale = _tmpButtonScale;
            _feverButton.TweenStop();
            _feverButton.TweenScale(_tmpButtonScale * 0.95f, 0.5f, Ease.InOutBack).Loop(LoopType.Yoyo);
        }
        else
        {
            _feverButton.TweenStop();
        }
        

    }


    private void OnFeverButtonClicked()
    {
        if (_mainScene.IsFeverStart || UserInfo.IsTutorialStart || !gameObject.activeInHierarchy)
            return;

        if (GameManager.Instance.FeverGaguge < ConstValue.MAX_PEVER_GAUGE)
            return;

        _feverButton.TweenStop();
        _feverButton.transform.localScale = _tmpButtonScale;

        if (_feverRoutine != null)
            StopCoroutine(_feverRoutine);
        _feverRoutine = StartCoroutine(FeverCoroutine());
    }


    private IEnumerator FeverCoroutine()
    {
        _mainScene.SetFever(true);
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();
        GameManager.Instance.SetGameSpeed(1);
        _feverEffects.SetActive(true);
        _feverAnimeObj.TweenStop();
        _feverAnimeObj.transform.position = _animeStartPos.position;
        _feverAnimeObj.TweenAnchoredPosition(new Vector2(0, 0), 0.5f, Ease.OutBack);

        yield return YieldCache.WaitForSeconds(ConstValue.PEVER_TIME);

        _feverAnimeObj.TweenStop();
        _feverAnimeObj.position = _animeStartPos.position;
        _feverEffects.SetActive(false);
        GameManager.Instance.SetGameSpeed(0);
        _mainScene.SetFever(false);
        _feverButton.interactable = false;
        _mainScene.PlayMainMusic();
        GameManager.Instance.FeverGaguge = 0;
        _fillAmountImage.SetFillAmonut(GameManager.Instance.FeverGaguge <= 0 ? 0 : 0.3f + ((float)GameManager.Instance.FeverGaguge / ConstValue.MAX_PEVER_GAUGE) * 0.7f);
    }
}
