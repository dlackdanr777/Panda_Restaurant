using System.Collections;
using System.Collections.Generic;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIScoreImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _uiRect;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private ParticleSystem _animeEffect;

    private Vector3 _tmpScale;
    private int _currentScore;
    private Coroutine _moneyAnimeRoutine;

    public void StartAnime()
    {
        _animeEffect.Emit(Random.Range(5, 11));
        _uiRect.TweenStop();
        _uiRect.localScale = _tmpScale;
        _uiRect.TweenScale(_tmpScale * 1.1f, 0.05f, Ease.Constant).OnComplete(() =>
        {
            _uiRect.TweenScale(_tmpScale, 0.05f, Ease.Constant);
        });
    }

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        _tmpScale = _uiRect.localScale;
        OnScoreChangeEvent();
        UserInfo.OnChangeScoreHandler += OnScoreChangeEvent;
        GameManager.Instance.OnChangeScoreHandler += OnScoreChangeEvent;
    }


    private void OnScoreChangeEvent()
    {
    if (!gameObject.activeInHierarchy)
            return;

        int addDia = UserInfo.Dia - _currentScore;

        if (addDia == 0)
            return;

        _currentScore = UserInfo.Dia;

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = StartCoroutine(AddAnimeRoutine(addDia));

        StartAnime();
    }


    private IEnumerator AddAnimeRoutine(int addDia)
    {
        int startDia = UserInfo.Dia - addDia;
        int targetDia = UserInfo.Dia;
        float time = 0;

        while (time < 1)
        {
            int currentDia = Mathf.FloorToInt(Mathf.Lerp(startDia, targetDia, time));
            _scoreText.SetText(Utility.ConvertToMoney(currentDia));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _scoreText.SetText(Utility.ConvertToMoney(UserInfo.Dia));
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeScoreHandler -= OnScoreChangeEvent;

        if (GameManager.Instance != null)
            GameManager.Instance.OnChangeScoreHandler -= OnScoreChangeEvent;

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = null;
    }
}
