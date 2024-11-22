using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;

public class UIDia : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _uiDia;
    [SerializeField] private TextMeshProUGUI _diaText;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _effectSpawnPos;
    [SerializeField] private RectTransform _animeParent;
    [SerializeField] private float _moveY;
    [SerializeField] private float _moveDuration;
    [SerializeField] private Ease _moveEase;
    [SerializeField] private Color _startColor;

    private int _currentDia;
    private Vector3 _tmpScale;
    private Coroutine _moneyAnimeRoutine;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        OnChangeDiaEvent();
    }


    private void Init()
    {
        _diaText.text = Utility.ConvertToMoney(UserInfo.Dia);
        _currentDia = UserInfo.Dia;
        _tmpScale = _uiDia.localScale;

        UserInfo.OnChangeDiaHandler += OnChangeDiaEvent;
    }


    public void StartAnime()
    {
        _uiDia.TweenStop();
        _uiDia.localScale = _tmpScale;
        _uiDia.TweenScale(_tmpScale * 1.1f, 0.05f, Ease.Constant).OnComplete(() =>
        {
            _uiDia.TweenScale(_tmpScale, 0.05f, Ease.Constant);
        });
    }


    private void OnChangeDiaEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        int addDia = UserInfo.Dia - _currentDia;

        if (addDia == 0)
            return;

        _currentDia = UserInfo.Dia;
       
        if(_moneyAnimeRoutine != null)
            StopCoroutine( _moneyAnimeRoutine );

        _moneyAnimeRoutine = StartCoroutine(AddDiaAnime(addDia));

        StartAnime();

        string sign = addDia < 0 ? "-" : "+";
        Vector3 spawnPos = _animeParent.transform.position;
        TextMeshProUGUI tmp = ObjectPoolManager.Instance.SpawnTMP(spawnPos, Quaternion.identity, _animeParent);

        tmp.text = sign + Utility.ConvertToMoney(addDia);
        tmp.fontSize = _diaText.fontSize;
        tmp.rectTransform.sizeDelta = _diaText.rectTransform.sizeDelta;
        tmp.rectTransform.localScale = _diaText.rectTransform.localScale;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = _startColor;

        tmp.TweenAlpha(0, _moveDuration, _moveEase);
        tmp.TweenMoveY(spawnPos.y + _moveY, _moveDuration, _moveEase).OnComplete(() => ObjectPoolManager.Instance.DespawnTmp(tmp));
        tmp.rectTransform.SetAsLastSibling();
    }


    private IEnumerator AddDiaAnime(int addDia)
    {
        int startMoney = UserInfo.Dia - addDia;
        int targetMoney = UserInfo.Dia;
        float time = 0; 

        while(time < 1)
        {
            _diaText.text = Utility.ConvertToMoney(Mathf.Lerp(startMoney, targetMoney, time));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _diaText.text = Utility.ConvertToMoney(UserInfo.Dia);  
    }

    
}
