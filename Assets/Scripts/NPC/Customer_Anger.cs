using Muks.Tween;
using UnityEngine;

public class Customer_Anger : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private AudioClip _sound;

    private Vector3 _tmpScale;
    private Color _tmpColor;


    public void Init()
    {
        _tmpColor = _sprite.color;
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _tmpScale = _sprite.transform.localScale;

        gameObject.SetActive(false);
    }

    public void StartAnime()
    {
        gameObject.SetActive(true);
        _sprite.transform.localScale = _tmpScale;
        _sprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _sound);
        Vector3 targetScale = _tmpScale + new Vector3(0.02f, 0.02f, 0.02f);
        _sprite.TweenScale(targetScale, 0.25f, Ease.Smoothstep).Loop(LoopType.Yoyo);
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
