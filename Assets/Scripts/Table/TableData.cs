using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TableData
{
    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    public bool IsUsed;

    public Customer CurrentCustomer;
}
