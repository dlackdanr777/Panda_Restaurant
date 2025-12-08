using Coffee.UIExtensions;
using UnityEngine;
using UnityEngine.UI;

public class UIMarketerImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CameraController _camera;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private Animator _marketerAnimator;
    [SerializeField] private Animator _emptyAnimator;
    [SerializeField] private GameObject _uiParticle;
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Image _marketerImage;
    [SerializeField] private Image _leftHandImage;
    [SerializeField] private Image _rightHandImage;
    [SerializeField] private GameObject _emptyObject;

    [SerializeField] private Sprite[] _emptySprites;

    [SerializeField] private Image _marketerSkillEffect;
    public Image MarketerSkillEffect => _marketerSkillEffect;

    private Sprite _marketerSprite;
    private Sprite _animationSprite;
    private MarketerData _data;
    private int _particleCount;
    private ERestaurantFloorType _currentFloor;
    private MarketerLightStickData _lightStickData;

    private void OnEnable()
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, EquipStaffType.Marketer);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= OnChangeMarketerEvent;
        UserInfo.OnChangeStaffSkinHandler -= OnChangeMarketerSkinEvent;
    }

    public void Init()
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, EquipStaffType.Marketer);
        UserInfo.OnChangeStaffHandler += OnChangeMarketerEvent;
        _camera.OnEndMoveCameraHandler += OnChangeFloorEvent;
        UserInfo.OnChangeStaffSkinHandler += OnChangeMarketerSkinEvent;
    }

    public void StartAnime()
    {
        if (_data == null)
        {
            _emptyObject.SetActive(true);
            _particleSystem.Emit(_particleCount);
            _emptyAnimator.SetTrigger("Touch");
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        _emptyObject.SetActive(false);
        _marketerImage.sprite = _animationSprite;
        _marketerAnimator.SetTrigger("Touch");
        _particleSystem.Emit(_particleCount);
        SetAnimeHandPosition();
    }

    public void EndAnime()
    {
        if (_data == null)
        {
            gameObject.SetActive(false);
            _emptyObject.SetActive(true);
            return;
        }

        _marketerImage.sprite = _marketerSprite;
        SetIdleHandPosition();
    }


    private void OnChangeFloorEvent(ERestaurantFloorType floor, RestaurantType type)
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, EquipStaffType.Marketer);
    }


    private void OnChangeMarketerEvent(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (floorType != _currentFloor)
            return;

        if (type != EquipStaffType.Marketer)
            return;

        StaffData equipData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, floorType, type);
        if (equipData == null)
        {
            gameObject.SetActive(false);
            _emptyObject.SetActive(true);
            _data = null;

            _particleSystem.gameObject.SetActive(false);
            _uiParticle.gameObject.SetActive(false);
            for (int i = _particleSystem.textureSheetAnimation.spriteCount - 1; i >= 0; i--)
            {
                _particleSystem.textureSheetAnimation.RemoveSprite(i);
            }

            for (int i = 0, cnt = _emptySprites.Length; i < cnt; ++i)
            {
                _particleSystem.textureSheetAnimation.AddSprite(_emptySprites[i]);
            }
            _particleSystem.gameObject.SetActive(true);
            _uiParticle.SetActive(true);
            return;
        }

        _data = (MarketerData)equipData;
        gameObject.SetActive(true);
        SetData(_data);
    }
    
    private void OnChangeMarketerSkinEvent()
    {
        if (_data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        SetData(_data);
    }

    private void SetData(MarketerData data)
    {
        if (data == null)
        {
            gameObject.SetActive(false);
            _emptyObject.SetActive(true);
            _data = null;
            return;
        }

        gameObject.SetActive(true);
        _emptyObject.SetActive(false);
        _data = data;

        MarketerSkinData skinData = (MarketerSkinData)UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, _data);
        if (skinData == null)
        {
            _marketerSprite = data.UISprite;
            _animationSprite = data.AnimationSprite;

            _marketerImage.sprite = _marketerSprite;
            _leftHandImage.sprite = data.LeftHandSprite;
            _rightHandImage.sprite = data.RightHandSprite;

            _particleCount = data.ParticleCount;
            _particleSystem.gameObject.SetActive(false);
            _uiParticle.gameObject.SetActive(false);
            for (int i = _particleSystem.textureSheetAnimation.spriteCount - 1; i >= 0; i--)
            {
                _particleSystem.textureSheetAnimation.RemoveSprite(i);
            }

            for (int i = 0, cnt = data.ParticleSprites.Length; i < cnt; ++i)
            {
                _particleSystem.textureSheetAnimation.AddSprite(data.ParticleSprites[i]);
            }

            _lightStickData = StaffDataManager.Instance.GetMarketerLightStickData(_data.Id);
            SetIdleHandPosition();
        }
        else
        {
            _marketerSprite = skinData.Sprite;
            _animationSprite = skinData.AnimationSprite;

            _marketerImage.sprite = _marketerSprite;
            _leftHandImage.sprite = skinData.LeftHandSprite;
            _rightHandImage.sprite = skinData.RightHandSprite;

            _particleCount = 10;
            _particleSystem.gameObject.SetActive(false);
            _uiParticle.gameObject.SetActive(false);

            for (int i = _particleSystem.textureSheetAnimation.spriteCount - 1; i >= 0; i--)
            {
                _particleSystem.textureSheetAnimation.RemoveSprite(i);
            }

            for (int i = 0, cnt = skinData.ParticleSprites.Length; i < cnt; ++i)
            {
                _particleSystem.textureSheetAnimation.AddSprite(skinData.ParticleSprites[i]);
            }

            _lightStickData = StaffDataManager.Instance.GetMarketerLightStickData(skinData.Id);
            SetIdleHandPosition();
        }

        _particleSystem.gameObject.SetActive(true);
        _uiParticle.SetActive(true);
    }


    private void SetIdleHandPosition()
    {
        if (_lightStickData == null)
        {
            _leftHandImage.transform.localScale = Vector3.one;
            _rightHandImage.transform.localScale = Vector3.one;
            _leftHandImage.rectTransform.anchoredPosition = Vector2.zero;
            _rightHandImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        _leftHandImage.transform.localScale = Vector3.one * _lightStickData.Size;
        _rightHandImage.transform.localScale = Vector3.one * _lightStickData.Size;
        _leftHandImage.rectTransform.anchoredPosition = _lightStickData.LeftActionHandOffset;
        _rightHandImage.rectTransform.anchoredPosition = _lightStickData.RightIdleHandOffset;
    }
    
    private void SetAnimeHandPosition()
    {
        if (_lightStickData == null)
        {
            _leftHandImage.transform.localScale = Vector3.one;
            _rightHandImage.transform.localScale = Vector3.one;
            _leftHandImage.rectTransform.anchoredPosition = Vector2.zero;
            _rightHandImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        _leftHandImage.transform.localScale = Vector3.one * _lightStickData.Size;
        _rightHandImage.transform.localScale = Vector3.one * _lightStickData.Size;
        _leftHandImage.rectTransform.anchoredPosition = _lightStickData.LeftIdleHandOffset;
        _rightHandImage.rectTransform.anchoredPosition = _lightStickData.RightActionHandOffset;
    }

}
