using Muks.Tween;
using UnityEngine;

public class DropCoinArea : MonoBehaviour
{
    [Header("Area Options")]
    [SerializeField] private Transform _dropArea;
    [SerializeField] private float _areaRangeX;

    [Space]
    [Header("Coin Options")]
    [SerializeField] private Transform _coinEndTr;
    [SerializeField] private int _maxCoinCount;
    [SerializeField] private float _coinEndTime;
    [SerializeField] private float _coinAnimeInterval;

    [Space]
    [SerializeField] private AudioClip _dropCoinSound;
    [SerializeField] private AudioClip _getCoinSound;

    private PointerClickSpriteRenderer[] _coins;
    private int _currentMoney;
    public int CurrentMoney => _currentMoney;

    private int _currentCoinCount;
    public int Count => _currentCoinCount;

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        _currentCoinCount = 0;
        _currentMoney = 0;
        _coins = new PointerClickSpriteRenderer[_maxCoinCount];
    }


    public void LoadData(int coinCount, int money)
    {
        _currentCoinCount = Mathf.Clamp(coinCount, 0, _maxCoinCount);
        _currentMoney = money;

        for(int i = 0, cnt = _coins.Length; i < cnt; ++i)
        {
            if (_coins[i] == null)
                continue;

            ObjectPoolManager.Instance.DespawnCoin(_coins[i]);
        }

        for(int i = 0, cnt = _currentCoinCount;  i < cnt; i++)
        {
            int index = i;
            Vector3 targetPos = _dropArea.position;
            targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxCoinCount) * index), 0, 0);

            _coins[index] = ObjectPoolManager.Instance.SpawnCoin(targetPos, Quaternion.identity);
            _coins[index].TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
            _coins[index].AddEvent(GiveCoin);
        }
    }


    public void DropCoin(Vector3 startPos, int moneyValue)
    {
        _currentMoney += moneyValue;
        PointerClickSpriteRenderer coin = ObjectPoolManager.Instance.SpawnCoin(startPos, Quaternion.identity);
        coin.AddEvent(GiveCoin);
        SoundManager.Instance.PlayEffectAudio(_dropCoinSound, 0.05f);
        Vector3 targetPos = _dropArea.position;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + (( _areaRangeX / _maxCoinCount) * _currentCoinCount), 0, 0);

        if(_currentCoinCount < _maxCoinCount)
        {
            _coins[_currentCoinCount] = coin;
            _currentCoinCount++;
        }

        coin.TweenMoveX(targetPos.x, 0.45f);
        coin.TweenMoveY(targetPos.y, 0.45f, Ease.InBack).OnComplete(() =>
        {
            if (_maxCoinCount <= _currentCoinCount)
            {
                coin.TweenStop();
                ObjectPoolManager.Instance.DespawnCoin(coin);
                return;
            }
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
        });

    }

    public void OnCoinStealEvent(Vector3 targetPos)
    {
        if (_currentCoinCount == 0)
            return;

        float endTime = _coinEndTime;
        int currentMoney = _currentMoney;
        int currentCoinCount = _currentCoinCount;
        _currentMoney = 0;
        _currentCoinCount = 0;
        SoundManager.Instance.PlayEffectAudio(_getCoinSound);

        for (int i = 0; i < currentCoinCount; i++)
        {
            int coinIndex = i;
            _coins[coinIndex].TweenStop();
            _coins[coinIndex].TweenMove(targetPos, 0.2f, Ease.Smoothstep).
                OnComplete(() =>
                {
                    _coins[coinIndex].TweenStop();
                    ObjectPoolManager.Instance.DespawnCoin(_coins[coinIndex]);
                    _coins[coinIndex] = null;
                });

            endTime -= _coinAnimeInterval;
        }
    }


    public void GiveCoin()
    {
        if (_currentCoinCount == 0)
            return;

        float endTime = _coinEndTime;
        int currentMoney = _currentMoney;
        int currentCoinCount = _currentCoinCount;
        _currentMoney = 0;
        _currentCoinCount = 0;
        SoundManager.Instance.PlayEffectAudio(_getCoinSound);

        for (int i = 0; i < currentCoinCount; i++)
        {
            int coinIndex = i;
            _coins[coinIndex].TweenStop();
            _coins[coinIndex].TweenMove(_coinEndTr.position, endTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    _coins[coinIndex].TweenStop();
                    ObjectPoolManager.Instance.DespawnCoin(_coins[coinIndex]);
                    UserInfo.AddMoney(currentMoney / currentCoinCount);               
                    _coins[coinIndex] = null;
                });

            endTime -= _coinAnimeInterval;
        }
    }
}
