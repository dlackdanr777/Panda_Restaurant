public class UnlockConditionData
{
    private UnlockConditionType _unlockType;
    public UnlockConditionType UnlockType => _unlockType;

    private string _unlockId;
    public string UnlockId => _unlockId;

    private int _unlockCount;
    public int UnlockCount => _unlockCount;

    public UnlockConditionData(UnlockConditionType unlockType, string unlockId, int unlockCount)
    {
        _unlockType = unlockType;
        _unlockId = unlockId.Replace(" ", "");
        _unlockCount = unlockCount;
    }
}
