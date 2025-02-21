using Muks.Tween;
using Muks.MobileUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.DataBind;
using System.Collections.Generic;

public class UIMainChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _rewardValue;
    [SerializeField] private Image _moneyImage;
    [SerializeField] private Image _diaImage;
    [SerializeField] private Button _shortCutButton;
    [SerializeField] private Button _rewardButton;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Coin Animations")]
    [SerializeField] private UIMoney _uiMoney;
    [SerializeField] private UIDia _uiDia;
    [SerializeField] private int _coinCount;
    [SerializeField] private RectTransform _coinPos;
    [SerializeField] private RectTransform _coinParent;
    [SerializeField] private Transform _coinTargetPos;
    [SerializeField] private Transform _coinTargetDiaPos;
    [SerializeField] private float _coinDuration;
    [SerializeField] private Ease _coinEase;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _clearGoldSound;
    [SerializeField] private AudioClip _clearDiaSound;

    private ChallengeData _currentData;
    private Dictionary<int, RectTransform> _coinDic = new Dictionary<int, RectTransform>();
    private Dictionary<int, RectTransform> _diaDic = new Dictionary<int, RectTransform>();

    public override void Init()
    {
        _rewardButton.onClick.AddListener(OnRewardButtonClicked);
        _shortCutButton.onClick.AddListener(OnShortcutButtonClicked);

        UpdateData();
        ChallengeManager.Instance.OnMainChallengeUpdateHandler += UpdateData;
        LoadingSceneManager.OnLoadSceneHandler += DespawnCoin;
        LoadingSceneManager.OnLoadSceneHandler += DespawnDia;
        LoadingSceneManager.OnLoadSceneHandler += OnChangeSceneEvent;

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        UpdateData();
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void UpdateData()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _currentData = ChallengeManager.Instance.GetCurrentMainChallengeData();

        if (_currentData == null)
        {
            _description.text = "현재 메인 과제가 없습니다.";
            DataBind.SetTextValue("MainChallengeDescription", "현재 메인 과제가 없습니다.");

            _rewardValue.gameObject.SetActive(false);
            _moneyImage.gameObject.SetActive(false);
            _diaImage.gameObject.SetActive(false);
            _rewardButton.gameObject.SetActive(false);
            _shortCutButton.gameObject.SetActive(false);
            return;
        }

        if (_currentData.MoneyType == MoneyType.Gold)
        {
            _moneyImage.gameObject.SetActive(true);
            _diaImage.gameObject.SetActive(false);
        }
        else
        {
            _diaImage.gameObject.SetActive(true);
            _moneyImage.gameObject.SetActive(false);
        }

        if (UserInfo.GetIsDoneChallenge(_currentData.Id))
        {
            _rewardButton.gameObject.SetActive(true);
            _shortCutButton.gameObject.SetActive(false);
        }
        else
        {
            _shortCutButton.gameObject.SetActive(true);
            _rewardButton.gameObject.SetActive(false);
        }

        _rewardValue.gameObject.SetActive(true);
        _description.text = _currentData.Description;
        DataBind.SetTextValue("MainChallengeDescription", _currentData.Description);
        _rewardValue.text = Utility.ConvertToMoney(_currentData.RewardMoney);
    }


    private void OnRewardButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("메인 과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if (!UserInfo.GetIsDoneChallenge(_currentData.Id))
        {
            DebugLog.Log("메인 과제가 완료되지 않았습니다: " + _currentData.Id);
            return;
        }


        if(_currentData.MoneyType == MoneyType.Gold)
        {
            UserInfo.AddMoney(_currentData.RewardMoney);
            ChallengeClaerMoneyAnime();
        }

        else if(_currentData.MoneyType == MoneyType.Dia)
        {
            UserInfo.AddDia(_currentData.RewardMoney);
            ChallengeClaerDiaAnime();
        }


        UserInfo.ClearChallenge(_currentData.Id);
        UpdateData();
    }


    private void OnShortcutButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("메인 과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if (_currentData.ShortcutAction.Item == null)
        {
            DebugLog.Log("바로가기 메서드 정보가 없습니다.");
            return;
        }

        _currentData.ShortcutAction.Item?.Invoke();
    }

    private void ChallengeClaerMoneyAnime()
    {
        float time = 0;
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, _coinPos.transform.position, Quaternion.identity);
        SoundManager.Instance.PlayEffectAudio(_clearGoldSound);
        for (int i = 0; i < _coinCount; ++i)
        {
            int index = i;
            RectTransform coin = ObjectPoolManager.Instance.SpawnUICoin(_coinPos.transform.position, Quaternion.identity);

            int coinID = coin.GetInstanceID();
            _coinDic.Add(coinID, coin);
            Vector2 coinPos = UnityEngine.Random.insideUnitCircle * 300;
            coinPos = _coinPos.anchoredPosition + coinPos;
            coin.SetParent(_coinParent);


            coin.TweenAnchoredPosition(coinPos, 0.45f, Ease.InQuad).OnComplete(() =>
            {
                float height = 100;
                if (coin.anchoredPosition.y < 0)
                    height *= -1;

                coin.TweenJump(_coinTargetPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                {
                    if (_coinDic.ContainsKey(coinID))
                        _coinDic.Remove(coinID);

                    ObjectPoolManager.Instance.DespawnUICoin(coin);
                    _uiMoney.StartAnime();
                });
                time += 0.05f;
            });
        }
    }

    private void ChallengeClaerDiaAnime()
    {
        float time = 0;
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, _coinPos.transform.position, Quaternion.identity);
        SoundManager.Instance.PlayEffectAudio(_clearDiaSound);
        for (int i = 0; i < _coinCount; ++i)
        {
            int index = i;
            RectTransform dia = ObjectPoolManager.Instance.SpawnUIDia(_coinPos.transform.position, Quaternion.identity);

            int diaID = dia.GetInstanceID();
            _diaDic.Add(diaID, dia);

            Vector2 coinPos = UnityEngine.Random.insideUnitCircle * 300;
            coinPos = _coinPos.anchoredPosition + coinPos;
            dia.SetParent(_coinParent);
            dia.TweenAnchoredPosition(coinPos, 0.45f, Ease.InQuad).OnComplete(() =>
            {
                float height = 100;
                if (dia.anchoredPosition.y < 0)
                    height *= -1;

                dia.TweenJump(_coinTargetDiaPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                {
                    if (_diaDic.ContainsKey(diaID))
                        _diaDic.Remove(diaID);

                    ObjectPoolManager.Instance.DespawnUIDia(dia);
                    _uiDia.StartAnime();
                });
                time += 0.05f;
            });
        }
    }


    private void OnDestroy()
    {
        ChallengeManager.Instance.OnMainChallengeUpdateHandler -= UpdateData;
    }


    private void OnDisable()
    {
        DespawnCoin();
        DespawnDia();
    }


    private void OnChangeSceneEvent()
    {
        DespawnCoin();
        DespawnDia();
        ChallengeManager.Instance.OnMainChallengeUpdateHandler -= UpdateData;
        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
    }


    private void DespawnCoin()
    {
        foreach (RectTransform coin in _coinDic.Values)
        {
            if (coin == null)
                continue;

            ObjectPoolManager.Instance.DespawnUICoin(coin);
        }
        _coinDic.Clear();
    }


    private void DespawnDia()
    {
        foreach (RectTransform dia in _diaDic.Values)
        {
            if (dia == null)
                continue;

            ObjectPoolManager.Instance.DespawnUIDia(dia);
        }
        _diaDic.Clear();
    }
}
