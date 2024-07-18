using Muks.Tween;
using System;
using UnityEngine;

public class GarbageAnimation : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer[] _coinImages;
    [SerializeField] private SpriteRenderer[] _garbageImages;

    [Space]
    [Header("Animation Options")]
    [SerializeField] private float _cleanDuration;
    [SerializeField] private float _delay;
    [SerializeField] private TweenMode _cleanTweenMode;

    public void Init()
    {
        gameObject.SetActive(false);
    }

    public void StartAnime()
    {
        gameObject.SetActive(true);
        _animator.enabled = true;

        for (int i = 0, cnt = _coinImages.Length; i < cnt; i++)
        {
            _coinImages[i].TweenStop();
            _coinImages[i].gameObject.SetActive(true);
        }

        for (int i = 0, cnt = _garbageImages.Length; i < cnt; i++)
        {
            _garbageImages[i].TweenStop();
            _garbageImages[i].color= Color.white;
        }
    }

    public void StartCleanAnime(Vector3 coinTargetPos, Action onCompleted = null)
    {
        _animator.enabled = false;

        float coinDuration = _cleanDuration;
        float gabageDuration = _cleanDuration;
        for (int i = 0, cnt = _coinImages.Length; i < cnt; i++)
        {
            int index = i;
            _coinImages[i].TweenMove(coinTargetPos, coinDuration, _cleanTweenMode).OnComplete(() => _coinImages[index].gameObject.SetActive(false));
            coinDuration += _delay;
        }

        for(int i = 0, cnt = _garbageImages.Length; i < cnt; i++)
        {
            Vector3 pos = _garbageImages[i].transform.position;
            _garbageImages[i].TweenMove(new Vector3(pos.x, pos.y + 10, pos.z), gabageDuration, _cleanTweenMode);
            _garbageImages[i].TweenAlpha(0, gabageDuration, _cleanTweenMode);
            gabageDuration += _delay;
        }

        Tween.Wait(coinDuration < gabageDuration ? gabageDuration : coinDuration, () =>
        {
            gameObject.SetActive(false);
            onCompleted?.Invoke();
        });
    }
}
