using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TableData
{
    [SerializeField] private Button _guideButton;
    public Button GuideButton => _guideButton;

    [SerializeField] private Button _orderButton;
    public Button OrderButton => _orderButton;

    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    [SerializeField] private Transform[] _chairTrs;
    public Transform[] ChairTrs => _chairTrs;



    public bool IsUsed;

    public bool IsOrderPlaced;

    public Customer CurrentCustomer;
}
