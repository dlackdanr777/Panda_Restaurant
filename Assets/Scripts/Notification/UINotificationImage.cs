using Muks.Tween;
using UnityEngine;

public class UINotificationImage : MonoBehaviour
{
    private Quaternion _tmpRotation;
    private Vector3 _tmpScale;

    private void Awake()
    {
        _tmpRotation = transform.rotation;
        _tmpScale = transform.localScale;
    }


    private void OnEnable()
    {
        PlayAnime();
    }


    private void PlayAnime()
    {
        transform.TweenStop();
        transform.rotation = _tmpRotation;
        transform.localScale = _tmpScale;

        Vector3 targetRot = new Vector3(0, 0, -15f);
        Vector3 targetScale = _tmpScale * 1.1f;

        transform.TweenScale(targetScale, 0.25f, Ease.Smoothstep);
        transform.TweenRotate(targetRot, 0.4f, Ease.Smoothstep).OnComplete(() =>
        {
            transform.TweenRotate(-targetRot, 0.5f, Ease.Smoothstep).OnComplete(() =>
            {
                transform.TweenRotate(targetRot, 0.32f, Ease.Smoothstep).OnComplete(() =>
                {
                    transform.TweenScale(_tmpScale, 0.25f, Ease.Smoothstep);
                    transform.TweenRotate(_tmpRotation.eulerAngles, 0.25f, Ease.Smoothstep).OnComplete(() =>
                    {
                        transform.TweenScale(_tmpScale, 1f).OnComplete(() =>
                        {
                            PlayAnime();
                        });
                    });
                });
            });
        });

    }
}
