using UnityEngine;

public class BurnerKitchenUtensil : KitchenUtensil
{
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private AudioClip _touchSound;
    [SerializeField] private AudioSource _audioSource;


    private KitchenBurnerData _burnerData;

    bool _isTouch = false;

    public float CookSpeedMul => _isTouch ? 2f : 1f;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
        _audioSource.clip = _touchSound;
    }

    public void SetData(KitchenBurnerData data)
    {
        _burnerData = data;

    }

    private void TouchDownEvent()
    {
        _isTouch = true;
        DebugLog.Log("Touch Down on Burner");
    }


    private void TouchUpEvent()
    {
        _isTouch = false;
        _audioSource.Stop();
        DebugLog.Log("Touch Up on Burner");
    }

    private void FixedUpdate()
    {
        if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, _floorType))
            return;
            
        if (_data == null && Type != KitchenUtensilType.Burner1)
                return;

        DebugLog.Log($"Is Touch: {_isTouch}" +
                     $" | Is Default: {_burnerData.CookingData.IsDefault()}" +
                     $" | Audio Playing: {_audioSource.isPlaying}");
        if (!_burnerData.CookingData.IsDefault() && _isTouch && !_audioSource.isPlaying)
        {
            _audioSource.Play();
        }

        else if ((_burnerData.CookingData.IsDefault() && _audioSource.isPlaying) || (!_isTouch && _audioSource.isPlaying))
        {
            _audioSource.Stop();
        }
    }
}
