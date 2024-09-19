using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIScoreImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform _starParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject _fullStarPrefab;
    [SerializeField] private GameObject _halfStarPrefab;

    private List<GameObject> _starList = new List<GameObject>();
    private List<GameObject> _fullStarList = new List<GameObject>();
    private GameObject _halfStar;

    private int _createStarCount = 5;
    private int _currentScore;
    private int _currentFullStarCount;
    private int _currentHalfStarCount;

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        for(int i = 0; i < _createStarCount; ++i)
        {
            GameObject fullStar = Instantiate(_fullStarPrefab, _starParent);
            _fullStarList.Add(fullStar);
            fullStar.SetActive(false);

        }

        _halfStar = Instantiate(_halfStarPrefab, _starParent);
        _halfStar.transform.SetAsLastSibling();
        _halfStar.SetActive(false);
        OnScoreChangeEvent();
        UserInfo.OnChangeScoreHandler += OnScoreChangeEvent;
    }


    private void OnScoreChangeEvent()
    {
        if (_currentScore == UserInfo.Score)
            return;

        _currentScore = UserInfo.Score;

        int fullStarCount = _currentScore / 100 < _createStarCount ? _currentScore / 100 : _createStarCount;
        int haifStarCount = 50 <= (_currentScore % 100) && fullStarCount < _createStarCount ? 1 : 0;

        if (_currentFullStarCount == fullStarCount && _currentHalfStarCount == haifStarCount)
            return;

        _currentFullStarCount = fullStarCount;
        _currentHalfStarCount = haifStarCount;

       for(int i = 0; i < _createStarCount; ++i)
       {
            _fullStarList[i].gameObject.SetActive(false);
       }

       for(int i = 0; i < fullStarCount; ++i)
        {
            _fullStarList[i].gameObject.SetActive(true);
        }

        if (_currentHalfStarCount == 1)
            _halfStar.gameObject.SetActive(true);
        else
            _halfStar.gameObject.SetActive(false);
    }
}
