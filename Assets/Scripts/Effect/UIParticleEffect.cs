using Unity.VisualScripting;
using UnityEngine;
using System;
using System.Collections;

public class UIParticleEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private UIEffectType _type;
    public UIEffectType Type => _type;

    private IEnumerator _ienumerator;
    private Coroutine _coroutine;

    [Obsolete]
    public void Init(UIEffectType type)
    {
        _ienumerator = StartHide(_particleSystem.duration);
        _type = type;
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
        if(_coroutine != null)
            StopCoroutine(_coroutine);

        ObjectPoolManager.Instance.DespawnUIEffect(_type, this);
    }

    private IEnumerator StartHide(float duration)
    {
        _particleSystem.Play();
        yield return YieldCache.WaitForSeconds(duration);
        gameObject.SetActive(false);
        ObjectPoolManager.Instance.DespawnUIEffect(_type, this);
    }


    
}
