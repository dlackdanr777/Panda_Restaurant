using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;


public class UIScore : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _rt;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private ParticleSystem _animeEffect;

    private long _currentValue;
    private Vector3 _tmpScale;
    private Coroutine _animeRoutine;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        _currentValue = UserInfo.Score;
        _text.text = Utility.ConvertToMoney(UserInfo.Score);
    }


    private void Init()
    {
        _currentValue = UserInfo.Score;
        _tmpScale = _rt.localScale;

        UserInfo.OnChangeScoreHandler += OnChangeScoreEvent;
        GameManager.Instance.OnChangeScoreHandler += OnChangeScoreEvent;
    }


    public void StartAnime()
    {
        _animeEffect.Emit(Random.Range(5, 11));
        _rt.TweenStop();
        _rt.localScale = _tmpScale;
        _rt.TweenScale(_tmpScale * 1.1f, 0.05f, Ease.Constant).OnComplete(() =>
        {
            _rt.TweenScale(_tmpScale, 0.05f, Ease.Constant);
        });
    }


    private void OnChangeScoreEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        long addMoney = UserInfo.Score - _currentValue;

        if (addMoney == 0)
            return;

        _currentValue = UserInfo.Score;
       
        if(_animeRoutine != null)
            StopCoroutine( _animeRoutine );

        _animeRoutine = StartCoroutine(AnimeRoutine(addMoney));

        StartAnime();
    }


    private IEnumerator AnimeRoutine(long value)
    {
        long startValue = UserInfo.Score - value;
        long targetValue = UserInfo.Score;
        float time = 0; 

        while(time < 1)
        {
            _text.SetText(Utility.ConvertToMoney(Mathf.FloorToInt(Mathf.Lerp(startValue, targetValue, time))));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _text.SetText(Utility.ConvertToMoney(UserInfo.Score));
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeScoreHandler -= OnChangeScoreEvent;
        GameManager.Instance.OnChangeScoreHandler -= OnChangeScoreEvent;
        if (_animeRoutine != null)
            StopCoroutine(_animeRoutine);
        
        _animeRoutine = null;        
    }
}
