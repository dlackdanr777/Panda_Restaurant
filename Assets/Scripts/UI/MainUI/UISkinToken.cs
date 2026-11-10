using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;

public class UISkinToken : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _rect;
    [SerializeField] private TextMeshProUGUI _tokenText;
    [SerializeField] private ParticleSystem _animeEffect;

    private long? _currentMoney;
    private Vector3 _tmpScale;
    private Coroutine _moneyAnimeRoutine;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        RefreshTokens(UserInfo.PandaTokens, false);
    }

    private void Update()
    {
        RefreshTokens(UserInfo.PandaTokens, true);
    }

    private void Init()
    {
        _tmpScale = _rect.localScale;
    }


    public void StartAnime()
    {
        _animeEffect.Emit(Random.Range(5, 11));
        _rect.TweenStop();
        _rect.localScale = _tmpScale;
        _rect.TweenScale(_tmpScale * 1.1f, 0.05f, Ease.Constant).OnComplete(() =>
        {
            _rect.TweenScale(_tmpScale, 0.05f, Ease.Constant);
        });
    }


    private void RefreshTokens(long? balance, bool animate)
    {
        if (animate && balance == _currentMoney)
            return;

        long? previous = _currentMoney;
        StopMoneyAnime();
        _currentMoney = balance;
        if (!animate || !previous.HasValue || !balance.HasValue)
        {
            _tokenText.SetText(balance.HasValue ? Utility.ConvertToMoney(balance.Value) : "-");
            return;
        }

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(previous.Value, balance.Value));
        StartAnime();
    }


    private IEnumerator AddMoneyAnime(long startMoney, long targetMoney)
    {
        float time = 0; 

        while(time < 1)
        {
            // PandaTokens is a 64-bit balance; float/int interpolation can overflow or lose precision.
            long value = (long)decimal.Floor(startMoney + ((decimal)targetMoney - startMoney) * (decimal)time);
            _tokenText.SetText(Utility.ConvertToMoney(value));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _tokenText.SetText(Utility.ConvertToMoney(targetMoney));
        _moneyAnimeRoutine = null;
    }

    private void OnDisable()
    {
        StopMoneyAnime();
        _rect.TweenStop();
        _rect.localScale = _tmpScale;
    }

    private void StopMoneyAnime()
    {
        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);
        _moneyAnimeRoutine = null;
    }
}
