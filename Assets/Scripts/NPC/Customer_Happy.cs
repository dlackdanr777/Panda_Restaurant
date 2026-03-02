using Muks.Tween;
using UnityEngine;

public class Customer_Happy : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private AudioClip _sound;

    private Vector3 _tmpScale;
    private Color _tmpColor;

    private Customer _customer;
    public void Init()
    {
        _tmpColor = _sprite.color;
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _tmpScale = _sprite.transform.localScale;

        gameObject.SetActive(false);
    }
    
        public void SetCustomer(Customer customer)
    {
        _customer = customer;
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _sprite.transform.localScale = _tmpScale;
        gameObject.SetActive(false);
    }

    public void StartAnime()
    {
        gameObject.SetActive(true);
        _sprite.transform.localScale = _tmpScale;
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);

        if (UserInfo.IsFirstTutorialClear)
        {
            EffectType _effectType = SoundManager.Instance.GetHallEffectType(_customer.VisitFloor, RestaurantType.Hall);
            SoundManager.Instance.PlayEffectAudio(_effectType, _sound);
        }
        else
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _sound);
        }

        Vector3 targetScale = _tmpScale + new Vector3(0.1f, 0.1f, 0.1f);
        _sprite.TweenScale(targetScale, 0.5f, Ease.Smoothstep).Loop(LoopType.Yoyo);
        TweenData tween1 = _sprite.TweenAlpha(1, 0.3f, Ease.Smoothstep);
        tween1.OnComplete(() =>
        {
            Tween.Wait(3, () =>
            {
                _sprite.TweenAlpha(0, 0.3f, Ease.Smoothstep)
                .OnComplete(StopAnime);
            });
        });
    }   
    

    public void StopAnime()
    {
        _sprite.TweenStop();
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _sprite.transform.localScale = _tmpScale;
        gameObject.SetActive(false);
    }


}
