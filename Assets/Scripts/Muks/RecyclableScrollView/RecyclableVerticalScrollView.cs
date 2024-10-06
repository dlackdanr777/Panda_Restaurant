using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

namespace Muks.RecyclableScrollView
{
    public abstract class RecyclableVerticalScrollView<T> : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] protected ScrollRect _scrollRect; // ScrollRect ������Ʈ
        [SerializeField] protected RectTransform _contentRect; // Content Ʈ������
        [SerializeField] protected RecyclableScrollSlot<T> _slotPrefab; // ���� ������

        [Space]
        [Header("Option")]
        [SerializeField] protected int _bufferRowCount = 5; // ���� �ְ� �ε��� ���� ���� �ټ�
        [SerializeField] protected float _left;
        [SerializeField] protected float _right;
        [SerializeField] protected float _top;
        [SerializeField] protected float _bottom;
        [SerializeField] protected float _spacing;

        protected LinkedList<RecyclableScrollSlot<T>> _slotList = new LinkedList<RecyclableScrollSlot<T>>();
        protected List<T> _dataList = new List<T>(); // �����͸� �����ϴ� ����Ʈ
        protected float _itemHeight;
        protected float _itemWidth;
        protected int _poolSize;
        protected int _tmpfirstVisibleIndex;
        protected int _contentVisibleSlotCount;
        protected int _itemsPerRow; // �� �ٿ� ���� ������ ����

        public virtual void Init(List<T> dataList)
        {
            _dataList = dataList; // �����͸� ����
            // ���� ũ��
            _itemHeight = _slotPrefab.Height;
            _itemWidth = _slotPrefab.Width;

            // ���ο� ���� ������ ���� ���
            _itemsPerRow = Mathf.FloorToInt(_scrollRect.GetComponent<RectTransform>().rect.width / _itemWidth);

            // ��ü ���� ���
            int totalRows = Mathf.CeilToInt((float)_dataList.Count / _itemsPerRow);
            float contentHeight = _itemHeight * totalRows + ((totalRows - 1) * _spacing) + _top + _bottom;

            _contentVisibleSlotCount = (int)(_scrollRect.GetComponent<RectTransform>().rect.height / _itemHeight) * _itemsPerRow;
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);

            // ���� ���� �� ����Ʈ�� �߰�
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


        /// <summary>������ ��ġ�� �����ϴ� �Լ�</summary>
        private void OnScroll(Vector2 scrollPosition)
        {
            float contentY = _contentRect.anchoredPosition.y;

            int firstVisibleRowIndex = Mathf.Max(0, Mathf.FloorToInt(contentY / (_itemHeight + _spacing)));
            int firstVisibleIndex = firstVisibleRowIndex * _itemsPerRow;

            // ���� ���� ��ġ�� ���� ��ġ�� �޶����ٸ� ���� ���ġ
            if (_tmpfirstVisibleIndex != firstVisibleIndex)
            {
                int diffIndex = (_tmpfirstVisibleIndex - firstVisibleIndex) / _itemsPerRow;

                // ���� �ε����� �� ũ�ٸ� (���� ��ũ�� ��)
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

                // ���� �ε����� �� ũ�ٸ� (�Ʒ��� ��ũ�� ��)
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

        /// <summary>������ �����͸� ������Ʈ�ϰ� ��ġ�� �����ϴ� �Լ�</summary>
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



