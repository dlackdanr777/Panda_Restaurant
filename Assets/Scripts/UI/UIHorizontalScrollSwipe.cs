using System;
using System.Collections;
using System.Linq;
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
        InitializeScrollPages();
    }

    private void OnDisable()
    {
        if (_swipeCoroutine != null)
            StopCoroutine(_swipeCoroutine);
    }

    // 활성화된 자식들만 고려하여 스크롤 페이지 초기화
    private void InitializeScrollPages()
    {
        UpdateActiveChildrenCount();
        
        if (_maxPage <= 0)
        {
            DebugLog.LogError("현재 활성화된 자식이 0입니다. 다시 확인해주세요.");
            return;
        }

        _scrollPageValues = new float[_maxPage];
        
        if (_maxPage == 1)
        {
            _scrollPageValues[0] = 0f;
            _valueDistance = 0f;
        }
        else
        {
            _valueDistance = 1f / (_maxPage - 1f);
            for (int i = 0; i < _maxPage; ++i)
            {
                _scrollPageValues[i] = _valueDistance * i;
            }
        }

        // 첫 번째 활성화된 자식의 크기로 스와이프 거리 계산
        RectTransform firstActiveChild = GetFirstActiveChild();
        if (firstActiveChild != null)
        {
            _swipeDistance = firstActiveChild.sizeDelta.x * _swipeDistanceMul;
        }
    }

    // 활성화된 자식 수 업데이트
    private void UpdateActiveChildrenCount()
    {
        _maxPage = 0;
        for (int i = 0; i < _contentParent.childCount; i++)
        {
            if (_contentParent.GetChild(i).gameObject.activeSelf)
            {
                _maxPage++;
            }
        }
    }

    // 첫 번째 활성화된 자식 찾기
    private RectTransform GetFirstActiveChild()
    {
        for (int i = 0; i < _contentParent.childCount; i++)
        {
            Transform child = _contentParent.GetChild(i);
            if (child.gameObject.activeSelf)
            {
                return child.GetComponent<RectTransform>();
            }
        }
        return null;
    }

    // 활성화된 자식들 중에서 실제 인덱스 찾기
    private int GetActiveChildIndex(int logicalIndex)
    {
        int activeCount = 0;
        for (int i = 0; i < _contentParent.childCount; i++)
        {
            if (_contentParent.GetChild(i).gameObject.activeSelf)
            {
                if (activeCount == logicalIndex)
                    return i;
                activeCount++;
            }
        }
        return -1;
    }

    // 실제 인덱스에서 논리적 인덱스 찾기
    private int GetLogicalIndex(int actualIndex)
    {
        int logicalIndex = 0;
        for (int i = 0; i < actualIndex && i < _contentParent.childCount; i++)
        {
            if (_contentParent.GetChild(i).gameObject.activeSelf)
            {
                logicalIndex++;
            }
        }
        return logicalIndex;
    }

    public void ChangeIndex(int index)
    {
        // 자식 상태 업데이트 후 인덱스 변경
        UpdateActiveChildrenCount();
        InitializeScrollPages();
        
        if (_maxPage <= 0)
            return;

        // 인덱스 범위 체크
        index = Mathf.Clamp(index, 0, _maxPage - 1);
        _currentPage = index;
        
        if (_scrollPageValues != null && index < _scrollPageValues.Length)
        {
            _scrollBar.value = _scrollPageValues[index];
            OnChangePageHandler?.Invoke(_currentPage);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isSwipeMode)
            return;

        // 드래그 시작 전 자식 상태 업데이트
        UpdateActiveChildrenCount();
        if (_maxPage <= 1) // 활성화된 자식이 1개 이하면 스와이프 불가
            return;

        _startTouchX = Input.mousePosition.x;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isSwipeMode || _maxPage <= 1)
            return;

        _endTouchX = Input.mousePosition.x;
        UpdateSwipe();
    }

    private void UpdateSwipe()
    {
        // 현재 페이지가 유효한 범위인지 확인
        _currentPage = Mathf.Clamp(_currentPage, 0, _maxPage - 1);
        
        if (Mathf.Abs(_startTouchX - _endTouchX) < _swipeDistance)
        {
            if (_swipeCoroutine != null)
                StopCoroutine(_swipeCoroutine);
            _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));
            return;
        }

        bool isLeft = _startTouchX < _endTouchX;

        if (isLeft)
        {
            if (_currentPage == 0)
            {
                // 현재 위치로 스냅백
                if (_swipeCoroutine != null)
                    StopCoroutine(_swipeCoroutine);
                _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));
                return;
            }
            _currentPage--;
        }
        else
        {
            if (_currentPage == _maxPage - 1)
            {
                // 현재 위치로 스냅백
                if (_swipeCoroutine != null)
                    StopCoroutine(_swipeCoroutine);
                _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));
                return;
            }
            _currentPage++;
        }

        if (_swipeCoroutine != null)
            StopCoroutine(_swipeCoroutine);
        _swipeCoroutine = StartCoroutine(OnSwipeOneStep(_currentPage));

        OnChangePageHandler?.Invoke(_currentPage);
    }

    private IEnumerator OnSwipeOneStep(int index)
    {
        if (_scrollPageValues == null || index >= _scrollPageValues.Length)
            yield break;

        float start = _scrollBar.value;
        float current = 0;
        float percent = 0;

        _isSwipeMode = true;

        while (percent < 1)
        {
            current += Time.deltaTime;
            percent = current / _swipeTime;

            _scrollBar.value = Mathf.Lerp(start, _scrollPageValues[index], percent);

            yield return null;
        }

        _isSwipeMode = false;
    }

    // 외부에서 자식 활성화 상태가 변경되었을 때 호출할 메서드
    public void RefreshPages()
    {
        InitializeScrollPages();
        
        // 현재 페이지가 범위를 벗어났다면 조정
        if (_currentPage >= _maxPage)
        {
            _currentPage = Mathf.Max(0, _maxPage - 1);
            if (_maxPage > 0)
            {
                _scrollBar.value = _scrollPageValues[_currentPage];
                OnChangePageHandler?.Invoke(_currentPage);
            }
        }
    }

    // 디버깅용 메서드
    [ContextMenu("Debug Active Children")]
    private void DebugActiveChildren()
    {
        UpdateActiveChildrenCount();
        DebugLog.Log($"총 자식 수: {_contentParent.childCount}, 활성화된 자식 수: {_maxPage}");
        
        for (int i = 0; i < _contentParent.childCount; i++)
        {
            Transform child = _contentParent.GetChild(i);
            DebugLog.Log($"자식 {i}: {child.name} - 활성화: {child.gameObject.activeSelf}");
        }
    }
}
