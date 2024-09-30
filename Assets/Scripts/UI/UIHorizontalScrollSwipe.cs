using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHorizontalScrollSwipe : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Scrollbar _scrollBar;
    [SerializeField] private RectTransform _contentParent;
    [SerializeField] private float _swipeTime = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float _swipeDistanceMul = 0.5f;


    public event Action<int> OnChangePageHandler;
    public int CurrentPage => _currentPage;

    private float[] _scrollPageValues;
    private float _swipeDistance;
    private float _valueDistance;
    private int _currentPage;
    private int _maxPage;
    private float _startTouchX;
    private float _endTouchX;
    private bool _isSwipeMode;
    private Coroutine _swipeCoroutine;


    private void Awake()
    {
        _maxPage = _contentParent.childCount;

        if(_maxPage <= 0)
        {
            DebugLog.LogError("현재 자식이 0입니다. 다시 확인해주세요.");
            return;
        }    

        _scrollPageValues = new float[_maxPage];
        _valueDistance = 1f / (_maxPage - 1f);
        for (int i = 0; i < _maxPage; ++i)
        {
            _scrollPageValues[i] = _valueDistance * i;
        }

        _swipeDistance = _contentParent.GetChild(0).GetComponent<RectTransform>().sizeDelta.x * _swipeDistanceMul;
    }

    private void OnDisable()
    {
        if (_swipeCoroutine != null)
            StopCoroutine(_swipeCoroutine);
    }

    public void ChagneIndex(int index)
    {
        _currentPage = index;
        _scrollBar.value = _scrollPageValues[index];
        OnChangePageHandler?.Invoke(_currentPage);
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isSwipeMode)
            return;

        _startTouchX = Input.mousePosition.x;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isSwipeMode)
            return;

        _endTouchX = Input.mousePosition.x;
        UpdateSwipe();
    }

    private void UpdateSwipe()
    {
        if(Mathf.Abs(_startTouchX - _endTouchX) < _swipeDistance)
        {
            if(_swipeCoroutine != null)
                StopCoroutine(_swipeCoroutine);
            _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));
            return;
        }

        bool isLeft = _startTouchX < _endTouchX;

        if(isLeft)
        {
            if (_currentPage == 0)
                return;

            _currentPage--;
        }

        else
        {
            if (_currentPage == _maxPage - 1)
                return;

            _currentPage++;
        }

        if (_swipeCoroutine != null)
            StopCoroutine(_swipeCoroutine);
        _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));

        OnChangePageHandler?.Invoke(_currentPage);
    }


    private IEnumerator OnSwipeOneStep(int index)
    {
        float start = _scrollBar.value;
        float current = 0;
        float percent = 0;

        _isSwipeMode = true;

        while(percent < 1)
        {
            current += Time.deltaTime;
            percent = current / _swipeTime;

            _scrollBar.value = Mathf.Lerp(start, _scrollPageValues[index], percent);

            yield return null;
        }

        _isSwipeMode = false;
    }
}
