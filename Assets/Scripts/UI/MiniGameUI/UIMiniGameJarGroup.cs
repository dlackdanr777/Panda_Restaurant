using UnityEngine;

public class UIMiniGameJarGroup : MonoBehaviour
{
    [SerializeField] private Transform _stick;

    private int _lastIndex;
    private int _firstIndex;

    public void Init()
    {
        _lastIndex = transform.childCount;
        _firstIndex = _lastIndex - 5;
    }

    public void StickSetSiblingIndex(int index)
    {
        SetSiblingIndex(_stick, index);
    }

    private void SetSiblingIndex(Transform tr, int index)
    {
        index = Mathf.Clamp(index, _firstIndex, _lastIndex);
        tr.SetSiblingIndex(index);
    }
}
