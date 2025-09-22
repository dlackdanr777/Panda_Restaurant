using System.Collections;
using UnityEngine;

public class UITouchImage : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    private void OnEnable()
    {
        StopAllCoroutines();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void StartTouch()
    {
        gameObject.SetActive(false);
        gameObject.SetActive(true);

        _animator.SetBool("Touch", true);
    }


    public void SetTouch(bool value)
    {
        if(!gameObject.activeSelf)
        {
            return;
        }

        if (_animator != null)
        {
            _animator.SetBool("Touch", value);
        }
    }

    public void Hide()
    {
        if (_animator != null)
        {
            // 숨기기 전에 상태 초기화
            _animator.SetBool("Touch", false);
        }

        gameObject.SetActive(false);
    }


    public void EndTouch()
    {
        StartCoroutine(EndTouchRoutine());
    }


    private IEnumerator EndTouchRoutine()
    {
        yield return new WaitForSeconds(0.33f);
        Hide();
    }
}
