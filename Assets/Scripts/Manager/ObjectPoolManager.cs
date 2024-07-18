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

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        CustomerPooling();
    }




    private static void CustomerPooling()
    {
        _customerParent = new GameObject("CustomerParent");
        DontDestroyOnLoad (_customerParent);
        Customer customerPrefab = Resources.Load<Customer>("Customer");

        for (int i = 0, count = _customerCount; i < count; i++)
        {
            Customer customer = Instantiate(customerPrefab, Vector3.zero, Quaternion.identity, _customerParent.transform);
            _customerPool.Enqueue(customer);
            customer.gameObject.SetActive(false);
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


}
