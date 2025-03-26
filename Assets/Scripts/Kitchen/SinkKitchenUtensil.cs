using UnityEngine;

public class SinkKitchenUtensil : KitchenUtensil
{
    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        UserInfo.OnAddSinkBowlHandler += UpdateSink;
        UpdateSink();
    }


    public void UpdateSink()
    {
        int cntBowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType);
        int maxBowlCount = UserInfo.GetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType);

        DebugLog.Log(cntBowlCount + "/" +  maxBowlCount);
    }

    private void OnDestroy()
    {
        UserInfo.OnAddSinkBowlHandler -= UpdateSink;
    }
}
