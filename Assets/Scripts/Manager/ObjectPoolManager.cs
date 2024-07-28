using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;
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

    private static Customer _customerPrefab;
    private static PointerClickSpriteRenderer _coinPrefab;
    private static PointerClickSpriteRenderer _garbagePrefab;


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
    }


    private static void CustomerPooling()
    {
        _customerParent = new GameObject("CustomerParent");
        _customerParent.transform.parent = _instance.transform;

        if(_customerPrefab == null)
            _customerPrefab = Resources.Load<Customer>("Customer");

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
            _coinPrefab = Resources.Load<PointerClickSpriteRenderer>("Coin");

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

        _garbageImages = Resources.LoadAll<Sprite>("Garbage/GarbageImage");

        if (_garbagePrefab == null)
            _garbagePrefab = Resources.Load<PointerClickSpriteRenderer>("Garbage/Garbage");

        for (int i = 0, count = _garbageCount; i < count; i++)
        {
            PointerClickSpriteRenderer garbage = Instantiate(_garbagePrefab, _garbageParent.transform);
            _garbagePool.Enqueue(garbage);
            garbage.gameObject.SetActive(false);
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
            return garbage;
        }

        garbage = _garbagePool.Dequeue();
        garbage.gameObject.SetActive(false);
        garbage.gameObject.SetActive(true);
        garbage.transform.position = pos;
        garbage.transform.rotation = rot;
        garbage.SpriteRenderer.sprite = _garbageImages[UnityEngine.Random.Range(0, _garbageImages.Length)];
        return garbage;
    }


    public void DespawnGarbage(PointerClickSpriteRenderer garbage)
    {
        garbage.gameObject.SetActive(false);
        garbage.TweenStop();
        garbage.RemoveAllEvent();
        _garbagePool.Enqueue(garbage);
    }
}
