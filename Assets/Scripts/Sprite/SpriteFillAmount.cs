using System.Collections;
using UnityEngine;

public class SpriteFillAmount : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Range(0f, 1f)][SerializeField] private float _value;

    private Vector2 _tmpSize;
    private Coroutine _coroutine;

    private void Awake()
    {
        _spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        _tmpSize = _spriteRenderer.sprite.bounds.size;
        _spriteRenderer.size = _tmpSize;
        SetFillAmount(_value);
    }

    private void OnDisable()
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);
    }


    public void SetFillAmount(float amount)
    {
        _value = Mathf.Clamp(amount, 0f, 1f);
        _spriteRenderer.size = new Vector2(_tmpSize.x * _value, _tmpSize.y);
    }


    public void TweenFillAmount(float amount, float time)
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(TweenFillAmountRoutine(amount, time));
    }

    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }


    private IEnumerator TweenFillAmountRoutine(float amount, float time)
    {
        amount = Mathf.Clamp(amount, 0, 1);
        float currentAmount = _value;
        float currenttime = 0;

        while(currenttime < time)
        {
            currenttime += 0.02f;
            _value = Mathf.Lerp(currentAmount, amount, currenttime / time);
            _spriteRenderer.size = new Vector2(_tmpSize.x * _value, _tmpSize.y);
            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _value = amount;
        _spriteRenderer.size = new Vector2(_tmpSize.x * _value, _tmpSize.y);
    }
}
