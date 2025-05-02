using UnityEngine;

public abstract class MiniGameSystem : MonoBehaviour
{
    public abstract void Init(UIMiniGameController uiMiniGameController);
    public abstract void Show(FoodData foodData);
    public abstract void Hide();
}

