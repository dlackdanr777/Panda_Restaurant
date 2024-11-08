using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIMiniGameTutorial : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject[] _pages;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;
    [SerializeField] private Button _startButton;
 
    private int _currentPage;

    private void Start()
    {
        Init();
    }

    public void Init()
    {
        _currentPage = 0;
        OnTurnOverPage(0);

        _leftArrowButton.onClick.RemoveAllListeners();
        _rightArrowButton.onClick.RemoveAllListeners(); 
        _leftArrowButton.onClick.AddListener(OnLeftArrowButtonClicked);
        _rightArrowButton.onClick.AddListener(OnRightArrowButtonClicked);
    }

    public void StartTutorial(Action onCompleted = null)
    {
        gameObject.SetActive(true);
        _currentPage = 0;
        OnTurnOverPage(0);
        _startButton.onClick.RemoveAllListeners();
        _startButton.onClick.AddListener(() => onCompleted?.Invoke());
    }

    private void OnTurnOverPage(int page)
    {
        _currentPage = Mathf.Clamp(page, 0, _pages.Length -1);
        for(int i = 0, cnt =  _pages.Length; i < cnt; ++i)
        {
            _pages[i].SetActive(false);
        }

        _pages[_currentPage].SetActive(true);
        ArrowButtonSetActive();

    }

    private void OnLeftArrowButtonClicked()
    {
        OnTurnOverPage(_currentPage - 1);
    }

    private void OnRightArrowButtonClicked()
    {
        OnTurnOverPage(_currentPage + 1);
    }


    private void ArrowButtonSetActive()
    {
        _leftArrowButton.gameObject.SetActive(0 < _currentPage);
        _rightArrowButton.gameObject.SetActive(_currentPage < _pages.Length - 1);
    }
}
