using UnityEngine;
using UnityEngine.EventSystems;

public class UIScroll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Scroll Settings")]
    [SerializeField] private RectTransform _targetImage; // 이동시킬 이미지 (null이면 자동으로 자신을 사용)
    [SerializeField] private Vector2 _minPosition; // 이동 범위 최소값
    [SerializeField] private Vector2 _maxPosition; // 이동 범위 최대값
    [SerializeField][Range(0.01f, 1f)] private float _outOfBoundsResistance = 0.1f; // 범위 밖 저항 (낮을수록 빡빡함)
    
    [Header("Visual Settings")]
    [SerializeField] private bool _showBoundsInEditor = true; // 에디터에서 범위 표시
    [SerializeField] private Color _boundsColor = Color.green; // 범위 색상
    
    [Header("Return Settings")]
    [SerializeField] private float _returnSpeed = 10f; // 범위 안으로 돌아오는 속도

    private Vector2 _previousPointerPosition;
    private bool _isDragging;
    private bool _isReturning;
    private RectTransform _rectTransform;

    private void Awake()
    {
        // Target Image가 설정되지 않았으면 자신의 RectTransform 사용
        if (_targetImage == null)
        {
            _targetImage = GetComponent<RectTransform>();
        }
        _rectTransform = _targetImage;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_targetImage == null) return;
        
        DebugLog.Log("Pointer Down");
        _isDragging = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _targetImage.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out _previousPointerPosition
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        DebugLog.Log("Pointer Up");
        _isDragging = false;
        
        // 범위 밖에 있으면 범위 안으로 돌아가기 시작
        if (_targetImage != null && IsOutOfBounds())
        {
            _isReturning = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _targetImage == null) return;

        Vector2 currentPointerPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _targetImage.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out currentPointerPosition
        );

        Vector2 delta = currentPointerPosition - _previousPointerPosition;
        Vector2 newPosition = _targetImage.anchoredPosition + delta;

        // 범위 체크 및 저항 적용
        newPosition = ApplyBoundsResistance(newPosition, delta);

        _targetImage.anchoredPosition = newPosition;
        _previousPointerPosition = currentPointerPosition;
    }

    private void Update()
    {
        if (_isReturning && _targetImage != null)
        {
            Vector2 currentPos = _targetImage.anchoredPosition;
            Vector2 targetPos = GetClampedPosition(currentPos);
            
            _targetImage.anchoredPosition = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * _returnSpeed);
            
            // 목표 위치에 거의 도달하면 정확히 맞추고 종료
            if (Vector2.Distance(_targetImage.anchoredPosition, targetPos) < 0.1f)
            {
                _targetImage.anchoredPosition = targetPos;
                _isReturning = false;
            }
        }
    }

    private bool IsOutOfBounds()
    {
        Vector2 pos = _targetImage.anchoredPosition;
        return pos.x < _minPosition.x || pos.x > _maxPosition.x || 
               pos.y < _minPosition.y || pos.y > _maxPosition.y;
    }

    private Vector2 GetClampedPosition(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, _minPosition.x, _maxPosition.x),
            Mathf.Clamp(position.y, _minPosition.y, _maxPosition.y)
        );
    }

    private Vector2 ApplyBoundsResistance(Vector2 newPosition, Vector2 delta)
    {
        Vector2 resultPosition = newPosition;

        // X축 체크
        if (newPosition.x < _minPosition.x || newPosition.x > _maxPosition.x)
        {
            // 범위 밖이면 저항 적용
            resultPosition.x = _targetImage.anchoredPosition.x + (delta.x * _outOfBoundsResistance);
        }

        // Y축 체크
        if (newPosition.y < _minPosition.y || newPosition.y > _maxPosition.y)
        {
            // 범위 밖이면 저항 적용
            resultPosition.y = _targetImage.anchoredPosition.y + (delta.y * _outOfBoundsResistance);
        }

        return resultPosition;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showBoundsInEditor || _targetImage == null) return;

        // 부모 RectTransform의 월드 포지션 가져오기
        RectTransform parent = _targetImage.parent as RectTransform;
        if (parent == null) return;

        Vector3 worldMin = parent.TransformPoint(new Vector3(_minPosition.x, _minPosition.y, 0));
        Vector3 worldMax = parent.TransformPoint(new Vector3(_maxPosition.x, _maxPosition.y, 0));

        // 범위 박스 그리기
        Gizmos.color = _boundsColor;
        
        Vector3 center = (worldMin + worldMax) * 0.5f;
        Vector3 size = worldMax - worldMin;
        
        // 와이어 큐브로 범위 표시
        DrawWireRect(worldMin, worldMax);
    }

    private void DrawWireRect(Vector3 min, Vector3 max)
    {
        Vector3 topLeft = new Vector3(min.x, max.y, min.z);
        Vector3 topRight = new Vector3(max.x, max.y, min.z);
        Vector3 bottomLeft = new Vector3(min.x, min.y, min.z);
        Vector3 bottomRight = new Vector3(max.x, min.y, min.z);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
#endif
}
