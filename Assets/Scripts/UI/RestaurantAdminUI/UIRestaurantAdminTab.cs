using Muks.Tween;
using UnityEngine;

public abstract class UIRestaurantAdminTab : MonoBehaviour
{
    [Header("Tab Background")]
    [SerializeField] protected UnityEngine.UI.Image _tabBackground;
    [SerializeField] protected Sprite _normalBackgroundSprite;
    [SerializeField] protected Sprite _vipBackgroundSprite;

    public abstract void Init();

    public abstract void UpdateUI();

    public abstract void SetAttention();

    public abstract void SetNotAttention();
    
    public abstract void ChangeFloorType(ERestaurantFloorType floorType);
    
    protected void UpdateTabBackground(ERestaurantFloorType floorType)
    {
        if (_tabBackground == null) return;
        
        bool isVIPRoom = floorType == ERestaurantFloorType.Floor2;
        Sprite targetSprite = isVIPRoom ? _vipBackgroundSprite : _normalBackgroundSprite;
        
        if (targetSprite != null)
        {
            _tabBackground.sprite = targetSprite;
            
            // 알파 페이드 애니메이션
            _tabBackground.TweenAlpha(0.7f, 0.15f)
                .OnComplete(() => _tabBackground.TweenAlpha(1f, 0.15f));
        }
    }
}
