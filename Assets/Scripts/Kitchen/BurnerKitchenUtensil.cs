using UnityEngine;

public class BurnerKitchenUtensil : KitchenUtensil
{
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private AudioClip _touchSound;
    [SerializeField] private AudioSource _audioSource;


    private KitchenBurnerData _data;

    bool _isTouch = false;

    public float CookSpeedMul => _isTouch ? 2f : 1f;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
    }

    public void SetData(KitchenBurnerData data)
    {
        _data = data;
    }

    private void TouchDownEvent()
    {
        if (_data.CookingData.IsDefault())
            return;

        _isTouch = true;
        if (_touchSound != null)
        {
            _audioSource.clip = _touchSound;
            _audioSource.Play();
        }
        DebugLog.Log("Touch Down on Burner");
    }


    private void TouchUpEvent()
    {
        _isTouch = false;
        _audioSource.Stop();
        DebugLog.Log("Touch Up on Burner");
    }
}
