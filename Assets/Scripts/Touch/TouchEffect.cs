using System;
using System.Collections;
using UnityEngine;

public class TouchEffect : MonoBehaviour
{
    private const float HIDE_TIME = 1f;

    [SerializeField] private Animator _animator;
    [SerializeField] private RectTransform _effectTransform;

    private Action<TouchEffect> _onEndHandler;
    private Coroutine _hideCoroutine;

    public void Init(Action<TouchEffect> onHideHandler)
    {
        _onEndHandler = onHideHandler;
        LoadingSceneManager.OnLoadSceneHandler += OnLoadingScene;
    }

    public void StartEffect(Vector2 pos)
    {
        if (_effectTransform == null || _animator == null)
        {
            Debug.LogWarning("Effect Transform or Animator is not set.");
            return;
        }
        _effectTransform.SetAsFirstSibling();
        _effectTransform.transform.position = pos;

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
        _hideCoroutine = StartCoroutine(HideRoutine());
    }

    public void EndEffect()
    {
        gameObject.SetActive(false);
        _onEndHandler?.Invoke(this);
    }


    private void OnLoadingScene()
    {
        _onEndHandler?.Invoke(this);
    }

    private void OnDisable()
    {
        if(_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
    }

    private IEnumerator HideRoutine()
    {
        yield return new WaitForSeconds(HIDE_TIME);
        EndEffect();
        _hideCoroutine = null;
    }
}
