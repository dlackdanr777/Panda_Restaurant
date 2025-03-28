using UnityEngine;

public class SinkKitchenUtensil : KitchenUtensil
{
    [Space]
    [Header("Sink")]
    [SerializeField] private SinkGaugeBar _sinkGaugeBar;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        UserInfo.OnChangeSinkBowlHandler += UpdateSink;
        _sinkGaugeBar.Init();
        UpdateSink();
    }


    public void UpdateSink()
    {
        int cntBowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType);
        int maxBowlCount = UserInfo.GetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType);
        _sinkGaugeBar.SetGauge(cntBowlCount, maxBowlCount);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeSinkBowlHandler -= UpdateSink;
    }
}
