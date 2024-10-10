using System.Collections.Generic;
using UnityEngine;

namespace Muks.RecyclableScrollView
{
    public abstract class RecyclableHorizontalScrollView<T> : RecyclableScrollView<T>
    {
        [Space]
        [Header("HorizontalScrollView Option")]
        [SerializeField] protected int _itemsPerColumn = 1;
        [SerializeField] protected float _leftOffset;
        [SerializeField] protected float _rightOffset;
        [SerializeField] protected float _verticalOffset;

        RectTransform _scrollRectTransform;
        public override void Init(List<T> dataList)
        {
            _dataList = dataList;

            _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            // 슬롯 크기
            _itemHeight = _slotPrefab.Height;
            _itemWidth = _slotPrefab.Width;

            // 전체 너비 계산
            int totalColumns = Mathf.CeilToInt((float)_dataList.Count / _itemsPerColumn);
            float contentWidth = _itemWidth * totalColumns + (totalColumns > 0 ? (totalColumns - 1) * _spacing : 0) + _leftOffset + _rightOffset;

            //Anchor값 고정(계산 오류 방지)
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.anchorMin = new Vector2(0f, 1f);

            //contentRect의 높이 계산
            _contentVisibleSlotCount = (int)(_scrollRectTransform.rect.width / _itemWidth) * _itemsPerColumn;
            _contentRect.sizeDelta = new Vector2(contentWidth - _scrollRectTransform.rect.width, _contentRect.sizeDelta.y);

            // 슬롯 생성 및 리스트에 추가
            _poolSize = _contentVisibleSlotCount + (_bufferCount * 2 * _itemsPerColumn);
            int index = -_bufferCount * _itemsPerColumn;
            for (int i = 0; i < _poolSize; i++)
            {
                DebugLog.Log(index);
                RecyclableScrollSlot<T> item = Instantiate(_slotPrefab, _contentRect);
                _slotList.AddLast(item);
                item.Init();
                UpdateSlot(item, index++);
            }
            _scrollRect.onValueChanged.AddListener(OnScroll);
        }


        public override void UpdateData(List<T> dataList)
        {
            _dataList = dataList;

            //예비 슬롯들을 고려해 index 세팅 및 Update
            int index = _tmpfirstVisibleIndex - _bufferCount * _itemsPerColumn;
            foreach (RecyclableScrollSlot<T> item in _slotList)
            {
                UpdateSlot(item, index);
                index++;
            }
        }


        protected override void OnScroll(Vector2 scrollPosition)
        {
            float contentX = _contentRect.anchoredPosition.x;

            //현재 인덱스 위치 계산 
            int firstVisibleRowIndex = Mathf.Max(0, Mathf.FloorToInt(contentX / (_itemWidth + _spacing)));
            int firstVisibleIndex = firstVisibleRowIndex * _itemsPerColumn;

            // 만약 이전 위치와 현재 위치가 달라졌다면 슬롯 재배치
            if (_tmpfirstVisibleIndex != firstVisibleIndex)
            {
                int diffIndex = (_tmpfirstVisibleIndex - firstVisibleIndex) / _itemsPerColumn;

                // 현재 인덱스가 더 크다면 (위로 스크롤 중)
                if (diffIndex < 0)
                {
                    int lastVisibleIndex = _tmpfirstVisibleIndex + _contentVisibleSlotCount;
                    for (int i = 0, cnt = Mathf.Abs(diffIndex) * _itemsPerColumn; i < cnt; i++)
                    {
                        RecyclableScrollSlot<T> item = _slotList.First.Value;
                        _slotList.RemoveFirst();
                        _slotList.AddLast(item);

                        int newIndex = lastVisibleIndex + (_bufferCount * _itemsPerColumn) + i;
                        UpdateSlot(item, newIndex);
                    }
                }

                // 이전 인덱스가 더 크다면 (아래로 스크롤 중)
                else if (diffIndex > 0)
                {
                    for (int i = 0, cnt = Mathf.Abs(diffIndex) * _itemsPerColumn; i < cnt; i++)
                    {
                        RecyclableScrollSlot<T> item = _slotList.Last.Value;
                        _slotList.RemoveLast();
                        _slotList.AddFirst(item);

                        int newIndex = _tmpfirstVisibleIndex - (_bufferCount * _itemsPerColumn) - i;
                        UpdateSlot(item, newIndex);
                    }
                }

                _tmpfirstVisibleIndex = firstVisibleIndex;
            }
        }


        protected override void UpdateSlot(RecyclableScrollSlot<T> item, int index)
        {
            //현재 Index의 행과 열을 계산
            int column = 0 <= index ? index / _itemsPerColumn : (index - 1) / _itemsPerColumn;
            int row = Mathf.Abs(index) % _itemsPerColumn;

            Vector2 pivot = item.RectTransform.pivot;
            // 중앙 행의 기준점 (짝수와 홀수에 따라 다름)
            float rowOffsetY;
            if (_itemsPerColumn % 2 == 0) // 짝수인 경우
            {
                // 짝수일 때는 중앙값이 두 행 사이에 위치
                float middleRow = (_itemsPerColumn / 2f) - 0.5f;
                rowOffsetY = -(row - middleRow) * (_itemHeight + _spacing);
            }
            else // 홀수인 경우
            {
                // 홀수일 때는 중앙 행을 기준으로 위/아래로 정렬
                int middleRow = (_itemsPerColumn - 1) / 2;
                rowOffsetY = row == middleRow ? 0 : row < middleRow ? -(middleRow - row) * (_itemHeight + _spacing) : row > middleRow ? (row - middleRow) * (_itemHeight + _spacing) : 0;
            }

            // 피벗 보정 값
            float pivotAdjustmentY = -_itemHeight * (0.5f - pivot.y);

            // X축 및 Y축 위치 계산 (세로를 기준으로 중앙 정렬 및 피벗 보정)
            float scrollViewWidth = _contentRect.rect.width;
            float adjustedX = -(scrollViewWidth * 0.5f) + (column * (_itemWidth + _spacing)) + _itemWidth * pivot.x;
            float pivotAdjustmentX = _itemWidth * (0.5f - pivot.x);
            float adjustedY = rowOffsetY + _verticalOffset + pivotAdjustmentY;
            adjustedX += pivotAdjustmentX;
            adjustedX += _leftOffset;
            item.RectTransform.anchoredPosition = new Vector2(adjustedX, adjustedY);

            //Index가 입력된 DataList의 크기를 넘어가거나 0미만이면 슬롯을 끄고 Update를 진행하지 않는다.
            if (index < 0 || index >= _dataList.Count)
            {
                item.gameObject.SetActive(false);
            }
            else
            {
                item.UpdateSlot(_dataList[index]);
                item.gameObject.SetActive(true);
            }
        }
    }
}



