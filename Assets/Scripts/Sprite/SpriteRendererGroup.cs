using Muks.Tween;
using UnityEngine;


public class SpriteRendererGroup : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _spriteRenderers;

    private void OnDisable()
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].TweenStop();
        }
    }

    public void SetAlpha(float alpha)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].TweenStop();
            Color tmpColor = _spriteRenderers[i].color;
            _spriteRenderers[i].color = new Color(tmpColor.r, tmpColor.g, tmpColor.b, alpha);
        }
    }

    public void TweenSetAlpha(float alpha, float duration, Ease ease = Ease.Constant)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].TweenStop();
            _spriteRenderers[i].TweenAlpha(alpha, duration, ease);
        }
    }


    public void SetColor(Color color)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].TweenStop();
            _spriteRenderers[i].color = color;
        }
    }

    public void SetAcive(bool acive)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].gameObject.SetActive(acive);
        }
    }
}
