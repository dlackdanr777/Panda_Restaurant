using UnityEngine;

[System.Serializable]
public class SaveKitchenData
{
    public ERestaurantFloorType FloorType { get; private set; }

    public int SinkBowlCount { get; private set; }

    public int MaxSinkBowlCount { get; private set; } = 6;

    public SaveKitchenData()
    {

    }

    public void SetFloorType(ERestaurantFloorType floor)
    {
        FloorType = floor;
    }

    public void SetMaxSinkBowlCount(int count)
    {
        MaxSinkBowlCount = Mathf.Clamp(count, 1, 30);
    }

    public void SetSinkBowlCount(int count)
    {
        SinkBowlCount = Mathf.Clamp(count, 0, MaxSinkBowlCount);
    }



    public void AddSinkBowlCount()
    {
        SinkBowlCount = Mathf.Clamp(SinkBowlCount + 1, 0, MaxSinkBowlCount);
    }

    public void SubSinkBowlCount()
    {
        SinkBowlCount = Mathf.Clamp(SinkBowlCount - 1, 0, MaxSinkBowlCount);
    }

    public bool GetBowlAddEnabled()
    {
        return SinkBowlCount < MaxSinkBowlCount;
    }
}
