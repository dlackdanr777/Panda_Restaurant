using UnityEngine;

public class UITouchImage : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    
    private Coroutine _setAnimationParamsCoroutine;

    private void OnEnable()
    {
        // 활성화될 때 애니메이터 초기화
        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    public void StartTouch()
    {
        gameObject.SetActive(true);

        // 한 프레임 지연 후 파라미터 설정
        if(_setAnimationParamsCoroutine != null)
        {
            StopCoroutine(_setAnimationParamsCoroutine);
        }

        _setAnimationParamsCoroutine = StartCoroutine(SetAnimationParams());
    }
    
    private System.Collections.IEnumerator SetAnimationParams()
    {
        // 한 프레임 기다림
        yield return null;
        
        // 먼저 bool 설정
        _animator.SetBool("Touch", true);
        
        // 약간의 지연 후 트리거 설정
        yield return new WaitForSeconds(0.01f);
        _animator.SetTrigger("Start");
        _setAnimationParamsCoroutine = null;
    }

    public void SetTouch(bool value)
    {
        if (_animator != null)
        {
            _animator.SetBool("Touch", value);
            
            // 값이 즉시 반영되도록 함
            _animator.Update(0f);
        }
    }

    public void Hide()
    {
        if (_animator != null)
        {
            // 숨기기 전에 상태 초기화
            _animator.SetBool("Touch", false);
            _animator.ResetTrigger("Start");
        }
        
        gameObject.SetActive(false);
    }
}
