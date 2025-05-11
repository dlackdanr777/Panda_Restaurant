using System;
using UnityEngine;

public abstract class MiniGameSystem : MonoBehaviour
{
    public abstract void Init(UIMiniGameController uiMiniGameController);
    public abstract void Show(FoodData foodData, Action onComplete);
    public abstract void Hide();
}

