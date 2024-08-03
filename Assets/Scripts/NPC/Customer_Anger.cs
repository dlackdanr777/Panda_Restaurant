using Muks.Tween;
using UnityEngine;

public class Customer_Anger : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _angerSprite;


    private Vector3 _tmpScale;
    private Color _tmpColor;


    public void Init()
    {
        _tmpColor = _angerSprite.color;
        _angerSprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _tmpScale = _angerSprite.transform.localScale;

        gameObject.SetActive(false);
    }

    public void StartAnime()
    {
        gameObject.SetActive(true);
        _angerSprite.transform.localScale = _tmpScale;
        _angerSprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);

        Vector3 targetScale = _tmpScale + new Vector3(0.02f, 0.02f, 0.02f);
        _angerSprite.TweenScale(targetScale, 0.25f, Ease.Smoothstep).Loop(LoopType.Yoyo);
        TweenData tween1 = _angerSprite.TweenAlpha(1, 0.3f, Ease.Smoothstep);
        tween1.OnComplete(() =>
        {
            Tween.Wait(3, () => 
            {
                _angerSprite.TweenAlpha(0, 0.3f, Ease.Smoothstep)
                .OnComplete(StopAnime); 
            });
        });
    }   
    

    public void StopAnime()
    {
        _angerSprite.TweenStop();
        _angerSprite.color = new Color(_tmpColor.r, _tmpColor.g, _tmpColor.b, 0);
        _angerSprite.transform.localScale = _tmpScale;
        gameObject.SetActive(false);
    }


}
