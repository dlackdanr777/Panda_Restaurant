using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIScoreImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _scoreText;

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        OnScoreChangeEvent();
        UserInfo.OnChangeScoreHandler += OnScoreChangeEvent;
        GameManager.Instance.OnChangeScoreHandler += OnScoreChangeEvent;
    }


    private void OnScoreChangeEvent()
    {
        _scoreText.text = UserInfo.Score.ToString();
    }
}
