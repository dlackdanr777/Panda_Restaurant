using UnityEngine;

public class MarketerLightStickData
{
    private float _size;
    public float Size => _size;
    private Vector2 _leftIdleHandOffset;
    public Vector2 LeftIdleHandOffset => _leftIdleHandOffset;

    private Vector2 _rightIdleHandOffset;
    public Vector2 RightIdleHandOffset => _rightIdleHandOffset;

    private Vector2 _leftActionHandOffset;
    public Vector2 LeftActionHandOffset => _leftActionHandOffset;

    private Vector2 _rightActionHandOffset;
    public Vector2 RightActionHandOffset => _rightActionHandOffset;

    public MarketerLightStickData(float size, Vector2 leftIdleHandOffset, Vector2 rightIdleHandOffset, Vector2 leftActionHandOffset, Vector2 rightActionHandOffset)
    {
        _size = size;
        _leftIdleHandOffset = leftIdleHandOffset;
        _rightIdleHandOffset = rightIdleHandOffset;
        _leftActionHandOffset = leftActionHandOffset;
        _rightActionHandOffset = rightActionHandOffset;
    }

}
