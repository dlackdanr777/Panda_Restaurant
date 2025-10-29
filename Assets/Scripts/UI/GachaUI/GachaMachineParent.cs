using System.Collections.Generic;
using UnityEngine;

public abstract class GachaMachineParent : MonoBehaviour
{
    protected UIGacha _uiGacha;
    protected List<GachaData> _itemDataList;
    public List<GachaData> ItemDataList => _itemDataList;

    public abstract void Init(UIGacha uiGacha);

    public abstract void Show();
    public abstract void Hide();
    public abstract void OnScreenButtonClicked();

    public abstract void OnSingleGachaButtonClicked();
    public abstract void OnTenGachaButtonClicked();
}
