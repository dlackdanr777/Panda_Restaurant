using UnityEngine;
using UnityEngine.UI;

public class UIMarketerImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Image _marketerImage;
    [SerializeField] private Image _leftHandImage;
    [SerializeField] private Image _rightHandImage;

    private Sprite _marketerSprite;
    private Sprite _animationSprite;
    private MarketerData _data;
    private int _particleCount;

    public void Init()
    {
        OnChangeMarketerEvent();
        UserInfo.OnChangeStaffHandler += OnChangeMarketerEvent;
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
        _particleSystem.Emit(_particleCount);
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

    private void OnChangeMarketerEvent()
    {
        MarketerData equipData = (MarketerData)UserInfo.GetEquipStaff(StaffType.Marketer);
        if (equipData == _data)
            return;

        _data = equipData;
        if(_data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        SetData(_data);
    }

    private void SetData(MarketerData data)
    {
        gameObject.SetActive(true);
        _data = data;
        _marketerSprite = data.UISprite;
        _animationSprite = data.AnimationSprite;

        _marketerImage.sprite = _marketerSprite;
        _leftHandImage.sprite = data.LeftHandSprite;
        _rightHandImage.sprite = data.RightHandSprite;
        _particleCount = data.ParticleCount;

        for (int i = 0, cnt = _particleSystem.textureSheetAnimation.spriteCount; i < cnt; ++i)
        {
            _particleSystem.textureSheetAnimation.RemoveSprite(0);
        }

        for(int i = 0, cnt = data.ParticleSprites.Length; i < cnt; ++i)
        {
            _particleSystem.textureSheetAnimation.AddSprite(data.ParticleSprites[i]);
        }
    }

}
