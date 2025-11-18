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

    private long _currentMoney;
    private Vector3 _tmpScale;
    private Coroutine _moneyAnimeRoutine;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        _currentMoney = UserInfo.SkinToken;
        _tokenText.text = Utility.ConvertToMoney(UserInfo.SkinToken);
    }


    private void Init()
    {

        _currentMoney = UserInfo.SkinToken;
        _tmpScale = _rect.localScale;

        UserInfo.OnChangeSkinTokenHandler += OnChangeSkinToken;
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


    private void OnChangeSkinToken()
    {
        if (!gameObject.activeInHierarchy)
            return;

        long addMoney = UserInfo.SkinToken - _currentMoney;

        if (addMoney == 0)
            return;

        _currentMoney = UserInfo.SkinToken;
       
        if(_moneyAnimeRoutine != null)
            StopCoroutine( _moneyAnimeRoutine );

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));

        StartAnime();
    }


    private IEnumerator AddMoneyAnime(long addMoney)
    {
        long startMoney = UserInfo.SkinToken - addMoney;
        long targetMoney = UserInfo.SkinToken;
        float time = 0; 

        while(time < 1)
        {
            _tokenText.SetText(Utility.ConvertToMoney(Mathf.FloorToInt(Mathf.Lerp(startMoney, targetMoney, time))));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _tokenText.SetText(Utility.ConvertToMoney(UserInfo.SkinToken));
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeSkinTokenHandler -= OnChangeSkinToken;
        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);
        
        _moneyAnimeRoutine = null;        
    }
}
