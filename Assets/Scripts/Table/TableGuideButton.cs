using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TableGuideButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Transform _worldPosTr;

    public void Init(UnityAction onButtonClicked)
    {
        _button.onClick.AddListener(onButtonClicked);
    }

    public void SetActive(bool active)
    {
        _button.gameObject.SetActive(active);
    }

    private void Update()
    {
        Vector2 worldToSceen = Camera.main.WorldToScreenPoint(_worldPosTr.position);
        _button.transform.position = worldToSceen;

    }
}
