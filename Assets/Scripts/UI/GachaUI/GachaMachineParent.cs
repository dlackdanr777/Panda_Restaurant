using System.Collections.Generic;
using UnityEngine;

public abstract class GachaMachineParent : MonoBehaviour
{
    [SerializeField] protected GameObject[] _machineObjects;

    protected UIGacha _uiGacha;
    protected List<GachaData> _itemDataList;
    public List<GachaData> ItemDataList => _itemDataList;
    internal IReadOnlyList<GameObject> MachineObjects => _machineObjects;

    public abstract void Init(UIGacha uiGacha);

    public abstract void Show();
    public abstract void Hide();
    public abstract void OnScreenButtonClicked();

    public abstract void OnSingleGachaButtonClicked();
    public abstract void OnTenGachaButtonClicked();

    internal static void AlignResultButton(RectTransform button, RectTransform viewport, Vector3[] corners)
    {
        if (button == null || viewport == null || corners == null || corners.Length < 4) return;
        button.anchorMin = button.anchorMax = new Vector2(1f, 0f);
        button.pivot = new Vector2(0.5f, 0.5f);
        button.sizeDelta = new Vector2(320f, 100f);
        // Machine parents move/scale during their native clips. Align the rendered
        // button to the shared view, not to the animated parent's local bounds.
        button.GetWorldCorners(corners);
        Vector3 lowerLeft = viewport.InverseTransformPoint(corners[0]);
        Vector3 upperRight = viewport.InverseTransformPoint(corners[2]);
        Vector3 offset = new Vector3(viewport.rect.xMax - 40f - upperRight.x,
            viewport.rect.yMin + 80f - lowerLeft.y, 0f);
        button.position += viewport.TransformVector(offset);
    }

    public  void SetActiveGachaMachine(bool isActive)
    {
        foreach (var obj in _machineObjects)
        {
            obj.SetActive(isActive);
        }
    }
}
