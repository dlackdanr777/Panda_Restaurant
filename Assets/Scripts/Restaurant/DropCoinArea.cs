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

    private PointerClickSpriteRenderer[] _coins;
    private int _currentMoney;
    private int _currentCoinCount;
    public int Count => _currentCoinCount;
    private bool _isAnimeStartEnabled;

    private void Start()
    {
        Init();
    }


    private void Init()
    {
        _currentCoinCount = 0;
        _currentMoney = 0;
        _coins = new PointerClickSpriteRenderer[_maxCoinCount];
    }


    public void DropCoin(Vector3 startPos, int moneyValue)
    {
        _isAnimeStartEnabled = false;
        _currentMoney += moneyValue;
        PointerClickSpriteRenderer coin = ObjectPoolManager.Instance.SpawnCoin(startPos, Quaternion.identity);
        coin.AddEvent(GiveCoin);

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
            _isAnimeStartEnabled = true;
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

        if (!_isAnimeStartEnabled)
            return;

        float endTime = _coinEndTime;
        int currentMoney = _currentMoney;
        int currentCoinCount = _currentCoinCount;
        _isAnimeStartEnabled = false;
        _currentMoney = 0;
        _currentCoinCount = 0;

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
                    _isAnimeStartEnabled = true;
                });

            endTime -= _coinAnimeInterval;
        }
    }


    public void GiveCoin()
    {
        if (_currentCoinCount == 0)
            return;

        if (!_isAnimeStartEnabled)
            return;

        float endTime = _coinEndTime;
        int currentMoney = _currentMoney;
        int currentCoinCount = _currentCoinCount;
        _isAnimeStartEnabled = false;
        _currentMoney = 0;
        _currentCoinCount = 0;

        for (int i = 0; i < currentCoinCount; i++)
        {
            int coinIndex = i;
            _coins[coinIndex].TweenStop();
            _coins[coinIndex].TweenMove(_coinEndTr.position, endTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    _coins[coinIndex].TweenStop();
                    ObjectPoolManager.Instance.DespawnCoin(_coins[coinIndex]);
                    UserInfo.AppendMoney(currentMoney / currentCoinCount);               
                    _coins[coinIndex] = null;
                    _isAnimeStartEnabled = true;
                });

            endTime -= _coinAnimeInterval;
        }
    }
}
