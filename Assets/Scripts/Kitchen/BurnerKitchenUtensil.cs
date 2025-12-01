using UnityEngine;

public class BurnerKitchenUtensil : KitchenUtensil
{
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private AudioClip _touchSound;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private ParticleSystem _chefEffect;


    private KitchenBurnerData _burnerData;

    bool _isTouch = false;

    public float CookSpeedMul => _isTouch ? 2f : 1f;


    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
        _audioSource.clip = _touchSound;
        SetChefEffect(false);
    }

    public void SetData(KitchenBurnerData data)
    {
        _burnerData = data;

    }

    public void SetChefEffect(bool isOn)
    {
        _chefEffect.gameObject.SetActive(isOn);
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

        if (!_burnerData.CookingData.IsDefault() && _isTouch && !_audioSource.isPlaying)
        {
            _audioSource.Play();
        }

        else if ((_burnerData.CookingData.IsDefault() && _audioSource.isPlaying) || (!_isTouch && _audioSource.isPlaying))
        {
            _audioSource.Stop();
        }
        
        if(!_burnerData.CookingData.IsDefault() && (_isTouch || _burnerData.UseStaff != null))
        {
            SetChefEffect(true);
        }
        else
        {
            SetChefEffect(false);
        }
    }
}
