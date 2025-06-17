using System;
using UnityEngine;

public class TouchEffect : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private RectTransform _effectTransform;

    private Action<TouchEffect> _onEndHandler;

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
}
