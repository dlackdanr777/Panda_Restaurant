using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Muks.RecyclableScrollView
{
    public abstract class RecyclableVerticalScrollView<T> : RecyclableScrollView<T>
    {
        [Space]
        [Header("VerticalScrollView Option")]
        [SerializeField] protected int _itemsPerRow = 1;
        [SerializeField] protected float _topOffset;
        [SerializeField] protected float _bottomOffset;
        [SerializeField] protected float _horizontalOffset;


        public override void Init(List<T> dataList)
        {
            _dataList = dataList;

            RectTransform scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            _itemHeight = _slotPrefab.Height;
            _itemWidth = _slotPrefab.Width;

            int totalRows = Mathf.CeilToInt((float)_dataList.Count / _itemsPerRow);
            float contentHeight = _itemHeight * totalRows + ((totalRows - 1) * _spacing) + _topOffset + _bottomOffset;

            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.anchorMin = new Vector2(0f, 1f);

            // 가시 영역 계산 개선 (더 많은 여유분)
            float viewportHeight = scrollRectTransform.rect.height;
            int visibleRows = Mathf.CeilToInt((viewportHeight + _spacing) / (_itemHeight + _spacing)) + 4; // +4 여유분
            _contentVisibleSlotCount = visibleRows * _itemsPerRow;

            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);

            // 훨씬 큰 풀 크기로 설정
            int minBufferCount = Mathf.Max(_bufferCount, 8); // 최소 8행 버퍼
            _poolSize = _contentVisibleSlotCount + (minBufferCount * 3 * _itemsPerRow); // 3배 버퍼

            Debug.Log($"풀 크기 설정: {_poolSize}, 가시 슬롯: {_contentVisibleSlotCount}");

            int index = -minBufferCount * _itemsPerRow;
            for (int i = 0; i < _poolSize; i++)
            {
                RecyclableScrollSlot<T> item = Instantiate(_slotPrefab, _contentRect);
                _slotList.AddLast(item);
                item.Init();
                UpdateSlot(item, index++);
            }

            _scrollRect.onValueChanged.AddListener(OnScroll);
            InitializeScrollView();
            AddInit();
        }

        public virtual void AddInit()
        {
            
        }

        public override void UpdateData(List<T> dataList)
        {
            _dataList = dataList;

            // 데이터 개수가 변경되었다면 컨텐츠 높이 재계산
            int totalRows = Mathf.CeilToInt((float)_dataList.Count / _itemsPerRow);
            float contentHeight = _itemHeight * totalRows + ((totalRows - 1) * _spacing) + _topOffset + _bottomOffset;
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);

            // 현재 스크롤 위치에 따른 가시 영역 다시 계산
            float contentY = _contentRect.anchoredPosition.y;
            float adjustedContentY = Mathf.Max(0, contentY - _topOffset);
            int firstVisibleRowIndex = Mathf.FloorToInt(adjustedContentY / (_itemHeight + _spacing));
            int firstVisibleIndex = firstVisibleRowIndex * _itemsPerRow;
            
            _tmpfirstVisibleIndex = firstVisibleIndex;

            // 기존 슬롯들을 현재 가시 영역 기준으로 업데이트
            int index = firstVisibleIndex - _bufferCount * _itemsPerRow;
            foreach (RecyclableScrollSlot<T> item in _slotList)
            {
                UpdateSlot(item, index);
                index++;
            }
        }


        protected override void OnScroll(Vector2 scrollPosition)
        {
            float contentY = _contentRect.anchoredPosition.y;
            float adjustedContentY = Mathf.Max(0, contentY - _topOffset);
            int firstVisibleRowIndex = Mathf.FloorToInt(adjustedContentY / (_itemHeight + _spacing));
            int firstVisibleIndex = firstVisibleRowIndex * _itemsPerRow;

            if (_tmpfirstVisibleIndex != firstVisibleIndex)
            {
                int indexDiff = firstVisibleIndex - _tmpfirstVisibleIndex;
                int rowDiff = indexDiff / _itemsPerRow;
                
                // 스크롤 속도에 관계없이 전체 재배치
                if (Mathf.Abs(rowDiff) >= 1)
                {
                    // 모든 경우에 전체 재배치 사용 (더 안정적)
                    RearrangeAllSlots(firstVisibleIndex);
                }

                _tmpfirstVisibleIndex = firstVisibleIndex;
            }
        }

        // 전체 슬롯 재배치 개선
        private void RearrangeAllSlots(int firstVisibleIndex)
        {
            // 더 큰 버퍼로 시작 인덱스 계산
            int extendedBuffer = Mathf.Max(_bufferCount, 5); // 최소 5행 버퍼
            int startIndex = firstVisibleIndex - (extendedBuffer * _itemsPerRow);
            
            var slotArray = new RecyclableScrollSlot<T>[_slotList.Count];
            int arrayIndex = 0;
            
            // LinkedList를 배열로 변환 (성능 최적화)
            foreach (var slot in _slotList)
            {
                slotArray[arrayIndex] = slot;
                arrayIndex++;
            }
            
            // 모든 슬롯 한 번에 업데이트
            for (int i = 0; i < slotArray.Length; i++)
            {
                UpdateSlot(slotArray[i], startIndex + i);
            }
            
            // 강제 레이아웃 업데이트
            Canvas.ForceUpdateCanvases();
        }

        // 한 행을 아래로 이동
        private void MoveRowDown(int firstVisibleIndex, int offset)
        {
            int lastVisibleIndex = firstVisibleIndex + _contentVisibleSlotCount - _itemsPerRow + (offset * _itemsPerRow);
            
            for (int i = 0; i < _itemsPerRow; i++)
            {
                RecyclableScrollSlot<T> item = _slotList.First.Value;
                _slotList.RemoveFirst();
                _slotList.AddLast(item);

                int newIndex = lastVisibleIndex + (_bufferCount * _itemsPerRow) + i;
                UpdateSlot(item, newIndex);
            }
        }

        // 한 행을 위로 이동
        private void MoveRowUp(int firstVisibleIndex, int offset)
        {
            for (int i = 0; i < _itemsPerRow; i++)
            {
                RecyclableScrollSlot<T> item = _slotList.Last.Value;
                _slotList.RemoveLast();
                _slotList.AddFirst(item);

                int newIndex = firstVisibleIndex - (_bufferCount * _itemsPerRow) - (_itemsPerRow * (offset + 1)) + i;
                UpdateSlot(item, newIndex);
            }
        }

        protected override void UpdateSlot(RecyclableScrollSlot<T> item, int index)
        {
            // 범위 체크를 먼저 수행
            bool isValidIndex = (index >= 0 && index < _dataList.Count);
            
            if (!isValidIndex)
            {
                // 유효하지 않은 인덱스는 즉시 비활성화
                if (item.gameObject.activeSelf)
                    item.gameObject.SetActive(false);
                return;
            }

            // 위치 계산 (유효한 인덱스만)
            int row = index / _itemsPerRow;
            int column = index % _itemsPerRow;

            // 위치 계산 최적화
            Vector2 pivot = item.RectTransform.pivot;
            float totalWidth = (_itemsPerRow * (_itemWidth + _spacing)) - _spacing;
            float contentWidth = _contentRect.rect.width;
            float offsetX = (contentWidth - totalWidth) / 2f;
            
            float posX = offsetX + (column * (_itemWidth + _spacing)) + (_itemWidth * pivot.x) + _horizontalOffset;
            float posY = -((row * (_itemHeight + _spacing)) + (_itemHeight * (1 - pivot.y)) + _topOffset);
            
            item.RectTransform.localPosition = new Vector3(posX, posY, 0);

            // 데이터 업데이트 및 활성화
            item.UpdateSlot(_dataList[index]);
            if (!item.gameObject.activeSelf)
                item.gameObject.SetActive(true);
        }

        // 스크롤 뷰 초기화 함수
        private void InitializeScrollView()
        {
            // 콘텐츠 위치 초기화
            ScrollRect scrollRect = GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                // 스크롤 위치를 상단으로 재설정
                scrollRect.normalizedPosition = new Vector2(0, 1);
                
                // 콘텐츠 사이즈 업데이트 강제
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }
        }

        // 아이템 추가 후 호출할 함수
        private void RefreshLayout()
        {
            // 캔버스 강제 업데이트
            Canvas.ForceUpdateCanvases();
            
            // 레이아웃 그룹 업데이트
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            
            // 스크롤 위치 재설정 (필요시)
            _scrollRect.normalizedPosition = new Vector2(0, 1);
        }

        [ContextMenu("Debug Current State")]
        private void DebugCurrentState()
        {
            Debug.Log($"=== 스크롤뷰 상태 ===");
            Debug.Log($"데이터 개수: {_dataList.Count}");
            Debug.Log($"아이템 크기: {_itemWidth}x{_itemHeight}, 간격: {_spacing}");
            Debug.Log($"한 줄당 아이템: {_itemsPerRow}");
            Debug.Log($"가시 슬롯 수: {_contentVisibleSlotCount}");
            Debug.Log($"풀 크기: {_poolSize}");
            Debug.Log($"첫 번째 가시 인덱스: {_tmpfirstVisibleIndex}");
            Debug.Log($"콘텐츠 위치: {_contentRect.anchoredPosition}");
            
            int activeCount = 0;
            foreach (var slot in _slotList)
            {
                if (slot.gameObject.activeSelf) activeCount++;
            }
            Debug.Log($"활성 슬롯: {activeCount}/{_slotList.Count}");
        }
    }
}



