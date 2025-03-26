using UnityEngine;

public class SaveKitchenData
{
    private int _sinkBowlCount;
    public int SinkBowlCount => _sinkBowlCount;

    private int _maxSinkBowlCount = 6;
    public int MaxSinkBowlCount => _maxSinkBowlCount;

    public SaveKitchenData()
    {

    }

    public void SetMaxSinkBowlCount(int count)
    {
        _maxSinkBowlCount = Mathf.Clamp(count, 1, 30);
    }

    public void SetSinkBowlCount(int count)
    {
        _sinkBowlCount = Mathf.Clamp(count, 0, _maxSinkBowlCount);
    }



    public void AddSinkBowlCount()
    {
        _sinkBowlCount = Mathf.Clamp(_sinkBowlCount + 1, 0, _maxSinkBowlCount);
    }

    public bool GetBowlAddEnabled()
    {
        return _sinkBowlCount < _maxSinkBowlCount;
    }
}
