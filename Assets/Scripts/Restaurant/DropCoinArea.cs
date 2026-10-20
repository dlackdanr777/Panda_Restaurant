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
    // A collecting image outlives its area during scene changes. A later spawn
    // invalidates only that image lease, not the already-clicked payment.
    private static readonly Dictionary<PointerDownSpriteRenderer, object> _collections = new Dictionary<PointerDownSpriteRenderer, object>();
    private long _currentMoney;
    public long CurrentMoney => _currentMoney;

    public int Count => _coinList.Count;

    private ERestaurantFloorType _floor;


    public void Init(CoinAreaData data, ERestaurantFloorType floor)
    {
        _data = data;
        _currentMoney = 0;
        _floor = floor;
        for (int i = 0; i < _coinList.Count; i++)
        {
            ObjectPoolManager.Instance.DespawnCoin(_coinList[i]);
        }
        _coinList.Clear();

        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
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
            _collections.Remove(coin);
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
            coin.AddEvent(GiveCoin);
            _coinList.Add(coin);
        }
    }


    public void DropCoin(Vector3 startPos, int moneyValue, bool isolateSoundFailure = false)
    {
        _currentMoney += moneyValue;
        _data.SetMoney(_currentMoney);
        if (isolateSoundFailure)
        {
            try { PlayDropSound(); }
            catch (System.Exception error) { LastDropDiagnostic = error.ToString(); }
        }
        else PlayDropSound();
        // Existing displayed coins also represent subsequent drops at the display cap.
        // Do not create an untracked pooled image which collection cannot return.
        if (Count >= _maxCoinCount) return;
        PointerDownSpriteRenderer coin = ObjectPoolManager.Instance.SpawnCoin(startPos, Quaternion.identity);
        _collections.Remove(coin);
        coin.AddEvent(GiveCoin);
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
            if (!_coinList.Contains(coin)) return;

            coin.transform.position = targetPos;
            coin.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
        });

    }

    public string LastDropDiagnostic { get; private set; }
    private void PlayDropSound()
    {
        if (UserInfo.IsFirstTutorialClear)
        {
            EffectType effectType = SoundManager.Instance.GetHallEffectType(_floor, RestaurantType.Hall);
            SoundManager.Instance.PlayEffectAudio(effectType, _dropCoinSound, 0.05f);
        }
        else
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _dropCoinSound, 0.05f);
        }

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
        EffectType effectType = SoundManager.Instance.GetHallEffectType(_floor, RestaurantType.Hall);
        SoundManager.Instance.PlayEffectAudio(effectType, SoundEffectType.GoldSound);

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
        var backend = Muks.BackEnd.BackendManager.ExistingInstance;
        var account = backend == null ? null : backend.CurrentMailReceiveQuery;
        _currentMoney = 0;
        _data.SetMoney(_currentMoney);
        _data.SetCoinCount(0);

        if(UserInfo.IsFirstTutorialClear)
        {
            EffectType effectType = SoundManager.Instance.GetHallEffectType(_floor, RestaurantType.Hall);
            SoundManager.Instance.PlayEffectAudio(effectType, SoundEffectType.GoldSound);
        }
        else
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, SoundEffectType.GoldSound);
        }

        for (int i = 0; i < _coinList.Count; i++)
        {
            int coinIndex = i;
            PointerDownSpriteRenderer coin = _coinList[coinIndex];
            bool collected = false;
            var imageLease = new object();
            _collections[coin] = imageLease;
            coin.TweenStop();
            coin.TweenMove(_coinEndTr.position, endTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    if (collected) return;
                    collected = true;
                    if (_collections.TryGetValue(coin, out var owner) && ReferenceEquals(owner, imageLease))
                    {
                        _collections.Remove(coin);
                        if (coin != null && coin.gameObject.activeSelf)
                        {
                            coin.TweenStop();
                            ObjectPoolManager.Instance.DespawnCoin(coin);
                        }
                    }
                    // Money belongs to the account that clicked. A normal stage
                    // transition or area destruction must not discard that amount.
                    if (!ReferenceEquals(backend, Muks.BackEnd.BackendManager.ExistingInstance)
                        || (!ReferenceEquals(backend, null) && (backend == null || account == null || !backend.IsCurrentGameDataQuery(account)))) return;
                    UserInfo.AddMoney(currentMoney / currentCoinCount
                        + (coinIndex == currentCoinCount - 1 ? currentMoney % currentCoinCount : 0));
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
