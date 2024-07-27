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

    public Vector3 Pos => _dropArea.position;
    public int MaxCoinCount => _maxCoinCount;

    private int _currentCoinCount;
    public int CurrentCoinCount => _currentCoinCount;

    private GameObject[] _coins;
    private int _currentMoney;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        _currentCoinCount = 0;
        _currentMoney = 0;
        _coins = new GameObject[MaxCoinCount];
    }

    public void DropCoin(Vector3 startPos, int moneyValue)
    {
        _currentMoney += moneyValue;
        GameObject coin = ObjectPoolManager.Instance.SpawnCoin(startPos, Quaternion.identity);

        Vector3 targetPos = Pos;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + (( _areaRangeX / MaxCoinCount) * CurrentCoinCount), 0, 0);

        if(_currentCoinCount < _maxCoinCount)
        {
            _coins[_currentCoinCount] = coin;
            _currentCoinCount++;
        }

        coin.TweenMoveX(targetPos.x, 0.45f);
        coin.TweenMoveY(targetPos.y, 0.45f, TweenMode.EaseInBack).OnComplete(() =>
        {
            if (MaxCoinCount <= CurrentCoinCount)
            {
                coin.TweenStop();
                ObjectPoolManager.Instance.EnqueueCoin(coin);
                return;
            }
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, TweenMode.Smootherstep).Loop(LoopType.Yoyo);
        });
    }


    public void OnCoinButtonClicked()
    {
        if (_currentCoinCount == 0)
            return;

        float endTime = _coinEndTime;
        for (int i = 0; i < _currentCoinCount; i++)
        {
            int coinIndex = i;
            _coins[coinIndex].TweenStop();
            _coins[coinIndex].TweenMove(_coinEndTr.position, endTime, TweenMode.Smootherstep).
                OnComplete(() =>
                {
                    _coins[coinIndex].TweenStop();
                    ObjectPoolManager.Instance.EnqueueCoin(_coins[coinIndex]);
                    UserInfo.AppendMoney((int)((_currentMoney * GameManager.Instance.FoodPriceMul) / _currentCoinCount));
                    _coins[coinIndex] = null;

                    if (_currentCoinCount - 1 <= coinIndex)
                    {
                        _currentMoney = 0;
                        _currentCoinCount = 0;
                    }
                });

            endTime -= _coinAnimeInterval;
        }
    }
}
