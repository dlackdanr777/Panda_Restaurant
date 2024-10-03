using System.Collections;
using UnityEngine;

public class SmokeParticle : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;

    private IEnumerator _ienumerator;
    private Coroutine _coroutine;


    public void Init()
    {
        _ienumerator = StartHide(_particleSystem.duration);
    }

    public void Play()
    {
        _particleSystem.Emit(1);
    }

    private void OnEnable()
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        if (_ienumerator != null)
            StartCoroutine(_ienumerator);
    }

    private void OnDisable()
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        ObjectPoolManager.Instance.DespawnSmokeParticle(this);
    }

    private IEnumerator StartHide(float duration)
    {
        _particleSystem.Play();
        yield return YieldCache.WaitForSeconds(duration);
        ObjectPoolManager.Instance.DespawnSmokeParticle(this);
    }
}
