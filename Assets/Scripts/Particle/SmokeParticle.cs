using System.Collections;
using UnityEngine;

public class SmokeParticle : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;

    private IEnumerator _ienumerator;

    public void Init()
    {
        _ienumerator = StartHide(_particleSystem.duration);
    }

    public void Play()
    {
        _particleSystem.Emit(1);
    }

    public void Stop()
    {
        _particleSystem.Stop();
    }


    public void SetScale(float size = 1)
    {
        size = Mathf.Clamp(size, 0.1f, 10f);
        transform.localScale = Vector3.one * size;
    }


    private void OnEnable()
    {
        if (_ienumerator != null)
            StartCoroutine(_ienumerator);
    }

    private void OnDisable()
    {
        if (_ienumerator != null)
            StopCoroutine(_ienumerator);
    }

    private IEnumerator StartHide(float duration)
    {
        _particleSystem.Play();
        yield return YieldCache.WaitForSeconds(duration);
        ObjectPoolManager.Instance.DespawnSmokeParticle(this);
    }
}
