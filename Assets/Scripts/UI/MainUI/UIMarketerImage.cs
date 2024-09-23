using UnityEngine;
using UnityEngine.UI;

public class UIMarketerImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Image _marketerImage;
    [SerializeField] private Image _leftHandImage;
    [SerializeField] private Image _rightHandImage;

    private Sprite _marketerSprite;
    private Sprite _animationSprite;
    private MarketerData _data;

    public void SetData(MarketerData data)
    {
        gameObject.SetActive(true);
        _data = data;
        _marketerSprite = data.UISprite;
        _animationSprite = data.AnimationSprite;

        _marketerImage.sprite = _marketerSprite;
        _leftHandImage.sprite = data.LeftHandSprite;
        _rightHandImage.sprite = data.RightHandSprite;
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void StartAnime()
    {
        if (!gameObject.activeSelf)
            return;

        if (_data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _marketerImage.sprite = _animationSprite;
        _animator.SetTrigger("Touch");
    }

    public void EndAnime()
    {
        if (_data == null)
        {
            gameObject.SetActive(false);
            return;
        }
 
        _marketerImage.sprite = _marketerSprite;
    }
}
