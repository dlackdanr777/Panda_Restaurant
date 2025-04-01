using UnityEngine;

public class SinkKitchenUtensil : KitchenUtensil
{
    [Space]
    [Header("Sink")]
    [SerializeField] private SinkGaugeBar _sinkGaugeBar;
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private AudioSource _washingSound;

    private bool _isStaffWashing;
    private bool _isTouchWashing;
    private float _washGauge;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _isTouchWashing = false;
        _washGauge = 0;
        _sinkGaugeBar.Init();
        _washingSound.Stop();

        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
        UpdateSink();

        UserInfo.OnChangeSinkBowlHandler += UpdateSink;
    }


    public void UpdateSink()
    {
        int cntBowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType);
        int maxBowlCount = UserInfo.GetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType);

        float oneBowlGauge = 1f / maxBowlCount;
        float gauge = Mathf.Clamp((float)cntBowlCount / maxBowlCount, 0, 1);
        gauge = Mathf.Clamp(gauge - (oneBowlGauge * _washGauge), 0, 1);
        _sinkGaugeBar.SetGauge(cntBowlCount, maxBowlCount, gauge);
    }




    private void FixedUpdate()
    {
        if (UserInfo.IsTutorialStart)
        {
            _isTouchWashing = false;
            _isStaffWashing = false;
        }

        if (UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType) <= 0)
        {
            _washGauge = 0;
            return;
        }

        if (!_isTouchWashing && !_isStaffWashing)
            return;

        if(1 <= _washGauge)
        {
            UserInfo.SubSinkBowlCount(UserInfo.CurrentStage, _floorType);
            _washGauge = 0;
        }

        _washGauge += Time.deltaTime / (_isTouchWashing && _isStaffWashing ? 2 : 4);
        UpdateSink();
    }

    public void StartStaffAction()
    {
        _isStaffWashing = true;
    }

    public void EndStaffAction()
    {
        _isStaffWashing = false;
    }


    private void TouchDownEvent()
    {
        _isTouchWashing = true;
    }


    private void TouchUpEvent()
    {
        _isTouchWashing = false;
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeSinkBowlHandler -= UpdateSink;
    }
}
