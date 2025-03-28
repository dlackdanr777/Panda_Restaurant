using UnityEngine;

[System.Serializable]
public class SaveTableData
{
    public ERestaurantFloorType FloorType { get; private set; }
    public TableType TableType { get; private set; }
    public bool NeedCleaning { get; private set; }
    public CoinAreaData[] CoinAreaDatas { get; private set; } = new CoinAreaData[2];
    public GarbageAreaData GarbageAreaData { get; private set; } = new GarbageAreaData();

    public void SetFloorType(ERestaurantFloorType floorType) => FloorType = floorType;

    public void SetTableType(TableType tableType) => TableType = tableType;

    public void SetNeedCleaning(bool needCleaning) => NeedCleaning = needCleaning;

    public void SetCoinAreaData(int index, CoinAreaData data) => CoinAreaDatas[index] = data;

    public void SetGarbageAreaData(GarbageAreaData data) => GarbageAreaData = data;

    public SaveTableData(ERestaurantFloorType floorType, TableType tableType, bool needCleaning = false)
    {
        for (int i = 0, cnt = CoinAreaDatas.Length; i < cnt; ++i)
        {
            CoinAreaDatas[i] = new CoinAreaData();
        }
        FloorType = floorType;
        TableType = tableType;
        NeedCleaning = needCleaning;
    }
}
