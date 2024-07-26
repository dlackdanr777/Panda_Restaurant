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

    private static int _coinCount = 30;
    private static GameObject _coinParent;
    private static Queue<GameObject> _coinPool = new Queue<GameObject>();



    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        CustomerPooling();
        CoinPooling();
    }




    private static void CustomerPooling()
    {
        _customerParent = new GameObject("CustomerParent");
        _customerParent.transform.parent = _instance.transform;
        Customer customerPrefab = Resources.Load<Customer>("Customer");

        for (int i = 0, count = _customerCount; i < count; i++)
        {
            Customer customer = Instantiate(customerPrefab, Vector3.zero, Quaternion.identity, _customerParent.transform);
            _customerPool.Enqueue(customer);
            customer.gameObject.SetActive(false);
        }
    }


    private static void CoinPooling()
    {
        _coinParent = new GameObject("CoinParent");
        _coinParent.transform.parent = _instance.transform;
        GameObject coinPrefab = Resources.Load<GameObject>("Coin");

        for (int i = 0, count = _coinCount; i < count; i++)
        {
            GameObject coin = Instantiate(coinPrefab, _coinParent.transform);
            _coinPool.Enqueue(coin);
            coin.gameObject.SetActive(false);
        }
    }


    public Customer SpawnCustomer(Vector3 pos, Quaternion rot)
    {
        Customer customer;

        if (_customerPool.Count == 0 )
        {
            Customer customerPrefab = Resources.Load<Customer>("Customer");

            customer = Instantiate(customerPrefab, pos, rot, _customerParent.transform);
            customer.transform.position = pos;
            customer.transform.rotation = rot;
            return customer;
        }

        customer = _customerPool.Dequeue();

        customer.gameObject.SetActive(false);
        customer.gameObject.SetActive(true);
        customer.transform.position = pos;
        customer.transform.rotation = rot;
        return customer;
    }


    public void EnqueueCustomer(Customer customer)
    {
        customer.gameObject.SetActive(false);
        _customerPool.Enqueue(customer);
    }


    public GameObject SpawnCoin(Vector3 pos, Quaternion rot)
    {
        GameObject coin;

        if (_coinPool.Count == 0)
        {
            GameObject customerPrefab = Resources.Load<GameObject>("Coin");

            coin = Instantiate(customerPrefab, pos, rot, _customerParent.transform);
            coin.transform.position = pos;
            coin.transform.rotation = rot;
            return coin;
        }

        coin = _coinPool.Dequeue();

        coin.gameObject.SetActive(false);
        coin.gameObject.SetActive(true);
        coin.transform.position = pos;
        coin.transform.rotation = rot;
        return coin;
    }


    public void EnqueueCoin(GameObject coin)
    {
        coin.gameObject.SetActive(false);
        coin.TweenStop();
        _coinPool.Enqueue(coin);
    }
}
