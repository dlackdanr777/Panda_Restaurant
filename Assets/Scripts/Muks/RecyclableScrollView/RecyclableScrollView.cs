using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Muks.RecyclableScrollView
{
    public abstract class RecyclableScrollView<T> : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] protected ScrollRect _scrollRect;
        [SerializeField] protected RectTransform _contentRect;
        [SerializeField] protected RecyclableScrollSlot<T> _slotPrefab;

        [Space]
        [Header("Option")]
        [SerializeField] protected int _bufferCount = 5;
        [SerializeField] protected float _spacing;

        protected LinkedList<RecyclableScrollSlot<T>> _slotList = new LinkedList<RecyclableScrollSlot<T>>();
        protected List<T> _dataList = new List<T>(); // 데이터를 저장하는 리스트
        protected float _itemHeight;
        protected float _itemWidth;
        protected int _poolSize;
        protected int _tmpfirstVisibleIndex;
        protected int _contentVisibleSlotCount;

        /// <summary>초기 설정</summary>
        public abstract void Init(List<T> dataList);


        /// <summary>슬롯의 정보를 갱신하는 함수</summary>
        public abstract void UpdateData(List<T> dataList);


        /// <summary>ScrollRect 이벤트와 연동하여 슬롯의 위치를 변경하는 함수</summary>
        protected abstract void OnScroll(Vector2 scrollPosition);


        /// <summary>슬롯의 데이터를 업데이트하고 위치를 갱신하는 함수</summary>
        protected abstract void UpdateSlot(RecyclableScrollSlot<T> item, int index);
    }
}



