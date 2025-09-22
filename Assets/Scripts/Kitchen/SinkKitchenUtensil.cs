using UnityEngine;

public class SinkKitchenUtensil : KitchenUtensil
{
    [Space]
    [Header("Sink")]
    [SerializeField] private SinkGaugeBar _sinkGaugeBar;
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private AudioSource _washingSound;
    [SerializeField] private GameObject _washingEffect;

    private bool _isStaffWashing;
    public bool IsStaffWashing => _isStaffWashing;
    private bool _isTouchWashing;
    private float _washGauge;

    public Staff UseStaff => _useStaff;
    public void SetUseStaff(Staff staff) => _useStaff = staff;
    private Staff _useStaff;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _isStaffWashing = false;
        _isTouchWashing = false;
        _washGauge = 0;
        _sinkGaugeBar.Init(floor);
        _washingSound.Stop();
        _washingEffect.SetActive(false);
        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
        UpdateSink();

        UserInfo.OnChangeSinkBowlHandler += UpdateSink;
        UserInfo.OnChangeMaxSinkBowlHandler += OnChangeMaxBowlCount;
    }

    private void OnChangeMaxBowlCount()
    { 
        _sinkGaugeBar.OnChangeMaxBowlCount();
        UpdateSink();
    }


    private void UpdateSink()
    {
        int cntBowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType);
        int maxBowlCount = UserInfo.GetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType);

        float oneBowlGauge = 1f / maxBowlCount;
        float gauge = (float)cntBowlCount / maxBowlCount;
        gauge = Mathf.Clamp(gauge - (oneBowlGauge * _washGauge), 0, 1);
        _sinkGaugeBar.SetGauge(cntBowlCount, maxBowlCount, gauge);
    }


    private void FixedUpdate()
    {
        if (UserInfo.IsTutorialStart)
        {
            _isTouchWashing = false;
            _isStaffWashing = false;
            _washingEffect.gameObject.SetActive(false);
            _washingSound.Stop();
        }

        if (UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _floorType) <= 0)
        {
            _washGauge = 0;

            if (_washingEffect.activeSelf)
            {
                _washingEffect.SetActive(false);
                _washingSound.Stop();
            }
            return;
        }

        if (!_isTouchWashing && !_isStaffWashing)
        {
            _washingEffect.SetActive(false);
            _washingSound.Stop();
            return;
        }

        if (!_washingEffect.activeSelf)
        {
            _washingEffect.SetActive(true);
            _washingSound.Play();
        }

        if (1 <= _washGauge)
        {
            UserInfo.SubSinkBowlCount(UserInfo.CurrentStage, _floorType);
            _washGauge = 0;
        }

        _washGauge += Time.deltaTime / (_isTouchWashing && _isStaffWashing ? 1 : 2);
        UpdateSink();
    }

    public void StartStaffAction()
    {
        _washingEffect.SetActive(true);
        _washingSound.Play();
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

    protected override void SetRendererScale(KitchenUtensilData data)
    {
        base.SetRendererScale(data);
        if (_batchType == KitchenUtensilBatchType.Lower)
        {
            _spriteRenderer.transform.localPosition -= new Vector3(0, 0.7f, 0);
        }

    }


    private void OnDestroy()
    {
        UserInfo.OnChangeSinkBowlHandler -= UpdateSink;
    }
}
