using UnityEngine;
using UnityEngine.UI;

public class UIMiniGame1GridLayout : MonoBehaviour
{
    [SerializeField] private RectTransform _rect;
    [SerializeField] private GridLayoutGroup _gridLayoutGroup;

    private Vector2 _cellSize;

    public void Init()
    {
        _cellSize = _gridLayoutGroup.cellSize;

    }

    public void SetConstraintCount(int count)
    {
        _gridLayoutGroup.constraintCount = count;
        Vector2 sizeDelta = new Vector2((_cellSize.x * count) + (_gridLayoutGroup.spacing.x * Mathf.Clamp(count - 1, 0, count)), _rect.sizeDelta.y);
        _rect.sizeDelta = sizeDelta;
    }
}
