using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("ObjectPoolManager");
                _instance = obj.AddComponent<ObjectPoolManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static ObjectPoolManager _instance;

    private static int _customerCount = 30;
    private static GameObject _customerParent;
    private static Queue<Customer> _customerPool = new Queue<Customer>();

    private static int _coinCount = 50;
    private static GameObject _coinParent;
    private static Queue<PointerClickSpriteRenderer> _coinPool = new Queue<PointerClickSpriteRenderer>();

    private static int _garbageCount = 100;
    private static GameObject _garbageParent;
    private static Queue<PointerClickSpriteRenderer> _garbagePool = new Queue<PointerClickSpriteRenderer>();
    private static List<PointerClickSpriteRenderer> _enabledGarbagePool = new List<PointerClickSpriteRenderer>();

    private static int _tmpCount = 20;
    private static GameObject _tmpParent;
    private static Queue<TextMeshProUGUI> _tmpPool = new Queue<TextMeshProUGUI>();
    private static List<TextMeshProUGUI> _enabledTmpPool = new List<TextMeshProUGUI>();

    private static Customer _customerPrefab;
    private static PointerClickSpriteRenderer _coinPrefab;
    private static PointerClickSpriteRenderer _garbagePrefab;
    private static TextMeshProUGUI _tmpPrefab;


    private static Sprite[] _garbageImages;


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        CustomerPooling();
        CoinPooling();
        GarbagePooling();
        TmpPooling();
    }


    private static void CustomerPooling()
    {
        _customerParent = new GameObject("CustomerParent");
        _customerParent.transform.parent = _instance.transform;

        if(_customerPrefab == null)
            _customerPrefab = Resources.Load<Customer>("ObjectPool/Customer");

        for (int i = 0, count = _customerCount; i < count; i++)
        {
            Customer customer = Instantiate(_customerPrefab, Vector3.zero, Quaternion.identity, _customerParent.transform);
            _customerPool.Enqueue(customer);
            customer.gameObject.SetActive(false);
        }
    }


    private static void CoinPooling()
    {
        _coinParent = new GameObject("CoinParent");
        _coinParent.transform.parent = _instance.transform;

        if(_coinPrefab == null)
            _coinPrefab = Resources.Load<PointerClickSpriteRenderer>("ObjectPool/Coin");

        for (int i = 0, count = _coinCount; i < count; i++)
        {
            PointerClickSpriteRenderer coin = Instantiate(_coinPrefab, _coinParent.transform);
            _coinPool.Enqueue(coin);
            coin.gameObject.SetActive(false);
        }
    }


    private static void GarbagePooling()
    {
        _garbageParent = new GameObject("GarbageParent");
        _garbageParent.transform.parent = _instance.transform;

        _garbageImages = Resources.LoadAll<Sprite>("ObjectPool/Garbage/GarbageImage");

        if (_garbagePrefab == null)
            _garbagePrefab = Resources.Load<PointerClickSpriteRenderer>("ObjectPool/Garbage/Garbage");

        for (int i = 0, count = _garbageCount; i < count; i++)
        {
            PointerClickSpriteRenderer garbage = Instantiate(_garbagePrefab, _garbageParent.transform);
            _garbagePool.Enqueue(garbage);
            garbage.gameObject.SetActive(false);
        }
    }


    private static void TmpPooling()
    {
        _tmpParent = new GameObject("TmpParent");
        _tmpParent.transform.parent = _instance.transform;

        if (_tmpPrefab == null)
            _tmpPrefab = Resources.Load<TextMeshProUGUI>("ObjectPool/TMP");

        for (int i = 0, count = _tmpCount; i < count; i++)
        {
            TextMeshProUGUI tmp = Instantiate(_tmpPrefab, _tmpParent.transform);
            _tmpPool.Enqueue(tmp);
            tmp.gameObject.SetActive(false);
        }
    }


    public Customer SpawnCustomer(Vector3 pos, Quaternion rot)
    {
        Customer customer;

        if (_customerPool.Count == 0 )
        {
            customer = Instantiate(_customerPrefab, pos, rot, _customerParent.transform);
            return customer;
        }

        customer = _customerPool.Dequeue();
        customer.gameObject.SetActive(false);
        customer.gameObject.SetActive(true);
        customer.transform.position = pos;
        customer.transform.rotation = rot;
        return customer;
    }


    public void DespawnCustomer(Customer customer)
    {
        customer.gameObject.SetActive(false);
        _customerPool.Enqueue(customer);
    }


    public PointerClickSpriteRenderer SpawnCoin(Vector3 pos, Quaternion rot)
    {
        PointerClickSpriteRenderer coin;

        if (_coinPool.Count == 0)
        {
            coin = Instantiate(_coinPrefab, pos, rot, _coinParent.transform);
            return coin;
        }

        coin = _coinPool.Dequeue();
        coin.gameObject.SetActive(false);
        coin.gameObject.SetActive(true);
        coin.transform.position = pos;
        coin.transform.rotation = rot;
        return coin;
    }


    public void DespawnCoin(PointerClickSpriteRenderer coin)
    {
        coin.gameObject.SetActive(false);
        coin.TweenStop();
        coin.RemoveAllEvent();
        _coinPool.Enqueue(coin);
    }


    public PointerClickSpriteRenderer SpawnGarbage(Vector3 pos, Quaternion rot)
    {
        PointerClickSpriteRenderer garbage;

        if (_garbagePool.Count == 0)
        {
            garbage = Instantiate(_garbagePrefab, pos, rot, _garbageParent.transform);
            garbage.SpriteRenderer.sprite = _garbageImages[UnityEngine.Random.Range(0, _garbageImages.Length)];
            _enabledGarbagePool.Add(garbage);
            return garbage;
        }

        garbage = _garbagePool.Dequeue();
        garbage.gameObject.SetActive(false);
        garbage.gameObject.SetActive(true);
        garbage.transform.position = pos;
        garbage.transform.rotation = rot;
        garbage.SpriteRenderer.sprite = _garbageImages[UnityEngine.Random.Range(0, _garbageImages.Length)];
        _enabledGarbagePool.Add(garbage);
        return garbage;
    }


    public void DespawnGarbage(PointerClickSpriteRenderer garbage)
    {
        if (!_enabledGarbagePool.Contains(garbage))
        {
            DebugLog.LogError("반환하려는 오브젝트가 garbarge가 아니거나, 활성화된 Garbage Pool에 등록되있지 않습니다.");
            return;
        }

        garbage.gameObject.SetActive(false);
        garbage.TweenStop();
        garbage.RemoveAllEvent();
        _garbagePool.Enqueue(garbage);
        _enabledGarbagePool.Remove(garbage);
    }

    public int GetEnabledGarbageCount()
    {
        return _enabledGarbagePool.Count;
    }



    public TextMeshProUGUI SpawnTMP(Vector3 pos, Quaternion rot, RectTransform parent)
    {
        TextMeshProUGUI tmp;

        if (_garbagePool.Count == 0)
        {
            tmp = Instantiate(_tmpPrefab, pos, rot, _garbageParent.transform);
            tmp.transform.parent = parent;
            _enabledTmpPool.Add(tmp);
            return tmp;
        }

        tmp = _tmpPool.Dequeue();
        tmp.gameObject.SetActive(false);
        tmp.gameObject.SetActive(true);
        tmp.transform.position = pos;
        tmp.transform.rotation = rot;
        tmp.transform.SetParent(parent);
        tmp.color = Color.white;
        _enabledTmpPool.Add(tmp);
        return tmp;
    }


    public void DespawnTmp(TextMeshProUGUI tmp)
    {
        if (!_enabledTmpPool.Contains(tmp))
        {
            DebugLog.LogError("반환하려는 오브젝트가 tmp가 아니거나, 활성화된 tmp Pool에 등록되있지 않습니다.");
            return;
        }

        tmp.gameObject.SetActive(false);
        tmp.transform.SetParent(_tmpParent.transform);
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.TweenStop();
        _tmpPool.Enqueue(tmp);
        _enabledTmpPool.Remove(tmp);
    }
}
