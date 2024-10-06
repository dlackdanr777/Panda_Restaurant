using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

namespace Muks.RecyclableScrollView
{
    public abstract class RecyclableVerticalScrollView<T> : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] protected ScrollRect _scrollRect; // ScrollRect 컴포넌트
        [SerializeField] protected RectTransform _contentRect; // Content 트랜스폼
        [SerializeField] protected RecyclableScrollSlot<T> _slotPrefab; // 슬롯 프리팹

        [Space]
        [Header("Option")]
        [SerializeField] protected int _bufferRowCount = 5; // 여유 있게 로드할 슬롯 세로 줄수
        [SerializeField] protected float _left;
        [SerializeField] protected float _right;
        [SerializeField] protected float _top;
        [SerializeField] protected float _bottom;
        [SerializeField] protected float _spacing;

        protected LinkedList<RecyclableScrollSlot<T>> _slotList = new LinkedList<RecyclableScrollSlot<T>>();
        protected List<T> _dataList = new List<T>(); // 데이터를 저장하는 리스트
        protected float _itemHeight;
        protected float _itemWidth;
        protected int _poolSize;
        protected int _tmpfirstVisibleIndex;
        protected int _contentVisibleSlotCount;
        protected int _itemsPerRow; // 한 줄에 들어가는 아이템 개수

        public virtual void Init(List<T> dataList)
        {
            _dataList = dataList; // 데이터를 설정
            // 슬롯 크기
            _itemHeight = _slotPrefab.Height;
            _itemWidth = _slotPrefab.Width;

            // 가로에 들어가는 아이템 개수 계산
            _itemsPerRow = Mathf.FloorToInt(_scrollRect.GetComponent<RectTransform>().rect.width / _itemWidth);

            // 전체 높이 계산
            int totalRows = Mathf.CeilToInt((float)_dataList.Count / _itemsPerRow);
            float contentHeight = _itemHeight * totalRows + ((totalRows - 1) * _spacing) + _top + _bottom;

            _contentVisibleSlotCount = (int)(_scrollRect.GetComponent<RectTransform>().rect.height / _itemHeight) * _itemsPerRow;
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);

            // 슬롯 생성 및 리스트에 추가
            _poolSize = _contentVisibleSlotCount + (_bufferRowCount * 2 * _itemsPerRow);
            int index = -_bufferRowCount * _itemsPerRow;
            for (int i = 0; i < _poolSize; i++)
            {
                RecyclableScrollSlot<T> item = Instantiate(_slotPrefab, _contentRect);
                _slotList.AddLast(item);
                item.Init();
                UpdateSlot(item, index++);
            }
            _scrollRect.onValueChanged.AddListener(OnScroll);
        }


        protected void UpdateData(List<T> dataList)
        {
            _dataList = dataList;
            int index = _tmpfirstVisibleIndex - _bufferRowCount * _itemsPerRow;
            foreach (RecyclableScrollSlot<T> item in _slotList)
            {
                UpdateSlot(item, index);
                index++;
            }
        }


        /// <summary>슬롯의 위치를 변경하는 함수</summary>
        private void OnScroll(Vector2 scrollPosition)
        {
            float contentY = _contentRect.anchoredPosition.y;

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

                        int newIndex = lastVisibleIndex + (_bufferRowCount * _itemsPerRow) + i;
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

                        int newIndex = _tmpfirstVisibleIndex - (_bufferRowCount * _itemsPerRow) - i;
                        UpdateSlot(item, newIndex);
                    }
                }

                _tmpfirstVisibleIndex = firstVisibleIndex;
            }
        }

        /// <summary>슬롯의 데이터를 업데이트하고 위치를 갱신하는 함수</summary>
        private void UpdateSlot(RecyclableScrollSlot<T> item, int index)
        {
            int row = 0 <= index ? index / _itemsPerRow : (index - 1) / _itemsPerRow;
            int column = Mathf.Abs(index) % _itemsPerRow;
            Vector2 pivot = item.RectTransform.pivot;

            float totalWidth = (_itemsPerRow * (_itemWidth + _spacing)) - _spacing;
            float contentWidth = _contentRect.rect.width;
            float offsetX = (contentWidth - totalWidth) / 2f;
            float adjustedY = -(row * (_itemHeight + _spacing)) - _itemHeight * (1 - pivot.y);
            float adjustedX = column * (_itemWidth + _spacing) + _itemWidth * pivot.x;
            adjustedX += offsetX + _left - _right;
            adjustedY -= _top;
            item.RectTransform.localPosition = new Vector3(adjustedX, adjustedY, 0);

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



