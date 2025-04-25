using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;

public class UIMoney : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _uiMoney;
    [SerializeField] private TextMeshProUGUI _moneyText;
    [SerializeField] private ParticleSystem _animeEffect;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _effectSpawnPos;
    public RectTransform EffectSpawnPos => _effectSpawnPos;
    [SerializeField] private RectTransform _animeParent;
    [SerializeField] private float _moveY;
    [SerializeField] private float _moveDuration;
    [SerializeField] private Ease _moveEase;
    [SerializeField] private Color _startColor;

    private long _currentMoney;
    private Vector3 _tmpScale;
    private Coroutine _moneyAnimeRoutine;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        _currentMoney = UserInfo.Money;
        _moneyText.text = Utility.ConvertToMoney(UserInfo.Money);
    }


    private void Init()
    {

        _currentMoney = UserInfo.Money;
        _tmpScale = _uiMoney.localScale;

        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
    }


    public void StartAnime()
    {
        _animeEffect.Emit(Random.Range(5, 11));
        _uiMoney.TweenStop();
        _uiMoney.localScale = _tmpScale;
        _uiMoney.TweenScale(_tmpScale * 1.1f, 0.05f, Ease.Constant).OnComplete(() =>
        {
            _uiMoney.TweenScale(_tmpScale, 0.05f, Ease.Constant);
        });
    }


    private void OnChangeMoneyEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        long addMoney = UserInfo.Money - _currentMoney;

        if (addMoney == 0)
            return;

        _currentMoney = UserInfo.Money;
       
        if(_moneyAnimeRoutine != null)
            StopCoroutine( _moneyAnimeRoutine );

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));

        StartAnime();

        string sign = addMoney < 0 ? "-" : "+";
        Vector3 spawnPos = _animeParent.transform.position;
        TextMeshProUGUI tmp = ObjectPoolManager.Instance.SpawnTMP(spawnPos, Quaternion.identity, _animeParent);

        tmp.SetText(sign + Utility.ConvertToMoney(addMoney));
        tmp.fontSize = _moneyText.fontSize;
        tmp.rectTransform.sizeDelta = _moneyText.rectTransform.sizeDelta;
        tmp.rectTransform.localScale = _moneyText.rectTransform.localScale;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = _startColor;

        tmp.TweenAlpha(0, _moveDuration, _moveEase);
        tmp.TweenMoveY(spawnPos.y + _moveY, _moveDuration, _moveEase).OnComplete(() => ObjectPoolManager.Instance.DespawnTmp(tmp));
        tmp.rectTransform.SetAsLastSibling();
    }


    private IEnumerator AddMoneyAnime(long addMoney)
    {
        long startMoney = UserInfo.Money - addMoney;
        long targetMoney = UserInfo.Money;
        float time = 0; 

        while(time < 1)
        {
            _moneyText.SetText(Utility.ConvertToMoney(Mathf.FloorToInt(Mathf.Lerp(startMoney, targetMoney, time))));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _moneyText.SetText(Utility.ConvertToMoney(UserInfo.Money));
    }

    
}
