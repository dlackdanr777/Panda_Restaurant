using UnityEngine;

public class BurnerKitchenUtensil : KitchenUtensil
{
    [SerializeField] private SpriteTouchEvent _touchEvent;

    bool _isTouch = false;

    public float CookSpeedMul => _isTouch ? 1.1f : 1f;

    public override void Init(ERestaurantFloorType floor)
    {
        base.Init(floor);
        _touchEvent.AddDownEvent(TouchDownEvent);
        _touchEvent.AddUpEvent(TouchUpEvent);
    }

    private void TouchDownEvent()
    {
        _isTouch = true;
        DebugLog.Log("Touch Down on Burner");
    }


    private void TouchUpEvent()
    {
        _isTouch = false;
        DebugLog.Log("Touch Up on Burner");
    }
}
