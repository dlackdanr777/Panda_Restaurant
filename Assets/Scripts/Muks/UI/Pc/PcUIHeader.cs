using UnityEngine;
using UnityEngine.EventSystems;


namespace Muks.PcUI
{
    /// <summary>PC UI 상단 헤더에 붙여 Target을 드래그할 수 있게 해주는 클래스</summary>
    public class PcUIHeader : MonoBehaviour, IBeginDragHandler, IDragHandler
    { 
        [Header("Components")]
        [SerializeField] private RectTransform _moveTarget;

        private Vector2 _rectBegin;
        private Vector2 _moveBegin;
        private Vector2 _moveOffset;


        public void OnBeginDrag(PointerEventData eventData)
        {
            _rectBegin = _moveTarget.anchoredPosition;
            _moveBegin = eventData.position;
        }


        public void OnDrag(PointerEventData eventData)
        {
            _moveOffset = eventData.position - _moveBegin;
            _moveTarget.anchoredPosition = _rectBegin + _moveOffset;
        }
    }
}

