using System.Collections.Generic;
using UnityEngine;

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
            // 슬롯 크기
            _itemHeight = _slotPrefab.Height;
            _itemWidth = _slotPrefab.Width;

            // 전체 높이 계산
            int totalRows = Mathf.CeilToInt((float)_dataList.Count / _itemsPerRow);
            float contentHeight = _itemHeight * totalRows + ((totalRows - 1) * _spacing) + _topOffset + _bottomOffset;

            //Anchor값 고정(계산 오류 방지)
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.anchorMin = new Vector2(0f, 1f);

            //contentRect의 높이 계산
            _contentVisibleSlotCount = (int)(scrollRectTransform.rect.height / _itemHeight) * _itemsPerRow;
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);

            // 슬롯 생성 및 리스트에 추가
            _poolSize = _contentVisibleSlotCount + (_bufferCount * 2 * _itemsPerRow);
            int index = -_bufferCount * _itemsPerRow;
            for (int i = 0; i < _poolSize; i++)
            {
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
            int index = _tmpfirstVisibleIndex - _bufferCount * _itemsPerRow;
            foreach (RecyclableScrollSlot<T> item in _slotList)
            {
                UpdateSlot(item, index);
                index++;
            }
        }


        protected override void OnScroll(Vector2 scrollPosition)
        {
            float contentY = _contentRect.anchoredPosition.y;

            //현재 인덱스 위치 계산 
            int firstVisibleRowIndex = Mathf.Max(0, Mathf.FloorToInt(contentY / (_itemHeight + _spacing)));
            int firstVisibleIndex = firstVisibleRowIndex * _itemsPerRow;

            // 만약 이전 위치와 현재 위치가 달라졌다면 슬롯 재배치
            if (_tmpfirstVisibleIndex != firstVisibleIndex)
            {
                int diffIndex = (_tmpfirstVisibleIndex - firstVisibleIndex) / _itemsPerRow;

                // 현재 인덱스가 더 크다면 (위로 스크롤 중)
                if (diffIndex < 0)
                {
                    int lastVisibleIndex = _tmpfirstVisibleIndex + _contentVisibleSlotCount;
                    for (int i = 0, cnt = Mathf.Abs(diffIndex) * _itemsPerRow; i < cnt; i++)
                    {
                        RecyclableScrollSlot<T> item = _slotList.First.Value;
                        _slotList.RemoveFirst();
                        _slotList.AddLast(item);

                        int newIndex = lastVisibleIndex + (_bufferCount * _itemsPerRow) + i;
                        UpdateSlot(item, newIndex);
                    }
                }

                // 이전 인덱스가 더 크다면 (아래로 스크롤 중)
                else if (diffIndex > 0)
                {
                    for (int i = 0, cnt = Mathf.Abs(diffIndex) * _itemsPerRow; i < cnt; i++)
                    {
                        RecyclableScrollSlot<T> item = _slotList.Last.Value;
                        _slotList.RemoveLast();
                        _slotList.AddFirst(item);

                        int newIndex = _tmpfirstVisibleIndex - (_bufferCount * _itemsPerRow) - i;
                        UpdateSlot(item, newIndex);
                    }
                }

                _tmpfirstVisibleIndex = firstVisibleIndex;
            }
        }


        protected override void UpdateSlot(RecyclableScrollSlot<T> item, int index)
        {
            //현재 Index의 행과 열을 계산
            int row = 0 <= index ? index / _itemsPerRow : (index - 1) / _itemsPerRow;
            int column = Mathf.Abs(index) % _itemsPerRow;

            // X축 및 Y축 위치 계산 (가로를 기준으로 중앙 정렬 및 피벗 보정)
            Vector2 pivot = item.RectTransform.pivot;
            float totalWidth = (_itemsPerRow * (_itemWidth + _spacing)) - _spacing;
            float contentWidth = _contentRect.rect.width;
            float offsetX = (contentWidth - totalWidth) / 2f;
            float adjustedY = -(row * (_itemHeight + _spacing)) - _itemHeight * (1 - pivot.y);
            float adjustedX = column * (_itemWidth + _spacing) + _itemWidth * pivot.x;
            adjustedX += offsetX + _horizontalOffset;
            adjustedY -= _topOffset;
            item.RectTransform.localPosition = new Vector3(adjustedX, adjustedY, 0);

            //Index가 입력된 DataList의 크기를 넘어가거나 0미만이면 슬롯을 끄고 Update를 진행하지 않는다.
            if (index < 0 || index >= _dataList.Count)
            {
                item.gameObject.SetActive(false);
                return;
            }
            else
            {
                item.UpdateSlot(_dataList[index]);
                item.gameObject.SetActive(true);
            }
        }
    }
}



