using Muks.Tween;
using System.Collections.Generic;
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


    private CoinAreaData _data;
    private List<PointerDownSpriteRenderer> _coinList = new List<PointerDownSpriteRenderer>();
    private long _currentMoney;
    public long CurrentMoney => _currentMoney;

    public int Count => _coinList.Count;


    public void Init(CoinAreaData data)
    {
        _data = data;
        _currentMoney = 0;
        for (int i = 0; i < _coinList.Count; i++)
        {
            ObjectPoolManager.Instance.DespawnCoin(_coinList[i]);
        }
        _coinList.Clear();

        LoadingSceneManager.OnLoadSceneHandler += OnChangeSceneEvent;
        LoadData(data);
    }


    public void LoadData(CoinAreaData data)
    {
        int count = Mathf.Clamp(data.CoinCount, 0, _maxCoinCount);
        _currentMoney = data.Money;

        for(int i = 0, cnt = _coinList.Count; i < cnt; ++i)
        {
            ObjectPoolManager.Instance.DespawnCoin(_coinList[i]);
        }
        _coinList.Clear();

        for(int i = 0, cnt = count;  i < cnt; i++)
        {
            int index = i;
            Vector3 targetPos = _dropArea.position;
            targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxCoinCount) * index), 0, 0);

            PointerDownSpriteRenderer coin = ObjectPoolManager.Instance.SpawnCoin(targetPos, Quaternion.identity);
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
            coin.AddEvent(GiveCoin);
            _coinList.Add(coin);
        }
    }


    public void DropCoin(Vector3 startPos, int moneyValue)
    {
        _currentMoney += moneyValue;
        _data.SetMoney(_currentMoney);
        PointerDownSpriteRenderer coin = ObjectPoolManager.Instance.SpawnCoin(startPos, Quaternion.identity);
        coin.AddEvent(GiveCoin);
        SoundManager.Instance.PlayEffectAudio(_dropCoinSound, 0.05f);
        Vector3 targetPos = _dropArea.position;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + (( _areaRangeX / _maxCoinCount) * Count), 0, 0);

        if(Count < _maxCoinCount)
        {
            _coinList.Add(coin);
            _data.SetCoinCount(Mathf.Clamp(Count, 0, _maxCoinCount));
        }

        coin.TweenStop();
        coin.TweenMoveX(targetPos.x, 0.45f);
        coin.TweenMoveY(targetPos.y, 0.45f, Ease.InBack).OnComplete(() =>
        {
            if (_maxCoinCount < Count)
            {
                coin.TweenStop();
                ObjectPoolManager.Instance.DespawnCoin(coin);
                return;
            }

            coin.transform.position = targetPos;
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
        });

    }

    public void OnCoinStealEvent(Vector3 targetPos)
    {
        if (_coinList.Count <= 0)
            return;

        float endTime = _coinEndTime;
        long currentMoney = _currentMoney;
        _currentMoney = 0;
        _data.SetMoney(_currentMoney);
        _data.SetCoinCount(0);
        SoundManager.Instance.PlayEffectAudio(SoundEffectType.GoldSound);

        for (int i = 0; i < _coinList.Count; i++)
        {
            int coinIndex = i;
            PointerDownSpriteRenderer coin = _coinList[coinIndex];
            coin.TweenStop();
            coin.TweenMove(targetPos, 0.2f, Ease.Smoothstep).
                OnComplete(() =>
                {
                    coin.TweenStop();
                    ObjectPoolManager.Instance.DespawnCoin(coin);
                });

            endTime -= _coinAnimeInterval;
        }
        _coinList.Clear();
    }


    public void GiveCoin()
    {
        if (_coinList.Count <= 0)
            return;

        float endTime = _coinEndTime;
        long currentMoney = _currentMoney;
        int currentCoinCount = Count;
        _currentMoney = 0;
        _data.SetMoney(_currentMoney);
        _data.SetCoinCount(0);
        SoundManager.Instance.PlayEffectAudio(SoundEffectType.GoldSound);

        for (int i = 0; i < _coinList.Count; i++)
        {
            int coinIndex = i;
            PointerDownSpriteRenderer coin = _coinList[coinIndex];
            coin.TweenStop();
            coin.TweenMove(_coinEndTr.position, endTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    coin.TweenStop();
                    ObjectPoolManager.Instance.DespawnCoin(coin);
                    UserInfo.AddMoney(currentMoney / currentCoinCount);               
                });

            endTime -= _coinAnimeInterval;
        }
        _coinList.Clear();
    }


    private void OnChangeSceneEvent()
    {
        DespawnCoin();
        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
    }


    private void DespawnCoin()
    {
        for(int i = 0, cnt = _coinList.Count; i < cnt; ++i)
        {
            ObjectPoolManager.Instance.DespawnCoin(_coinList[i]);         
        }

        _coinList?.Clear();
    }
}
