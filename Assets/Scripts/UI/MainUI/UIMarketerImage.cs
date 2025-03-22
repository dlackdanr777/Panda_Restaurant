using Coffee.UIExtensions;
using UnityEngine;
using UnityEngine.UI;

public class UIMarketerImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CameraController _camera;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private Animator _animator;
    [SerializeField] private UIParticle _uiParticle;
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Image _marketerImage;
    [SerializeField] private Image _leftHandImage;
    [SerializeField] private Image _rightHandImage;


    private Sprite _marketerSprite;
    private Sprite _animationSprite;
    private MarketerData _data;
    private int _particleCount;
    private ERestaurantFloorType _currentFloor;

    private void OnEnable()
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, StaffType.Marketer);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= OnChangeMarketerEvent;
    }

    public void Init()
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, StaffType.Marketer);
        UserInfo.OnChangeStaffHandler += OnChangeMarketerEvent;
        _camera.OnEndMoveCameraHandler += OnChangeFloorEvent;
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


    private void OnChangeFloorEvent(ERestaurantFloorType floor, CameraController.RestaurantType type)
    {
        _currentFloor = _mainScene.CurrentFloor;
        OnChangeMarketerEvent(_currentFloor, StaffType.Marketer);
    }


    private void OnChangeMarketerEvent(ERestaurantFloorType floorType, StaffType type)
    {
        if (floorType != _currentFloor)
            return;

        if (type != StaffType.Marketer)
            return;

        StaffData equipData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, floorType, type);
        if (equipData == null)
        {
            gameObject.SetActive(false);
            _data = null;
            return;
        }

        if (equipData == _data)
            return;

        _data = (MarketerData)equipData;
        gameObject.SetActive(true);
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
        _particleSystem.gameObject.SetActive(false);
        _uiParticle.enabled = false;
        for (int i = _particleSystem.textureSheetAnimation.spriteCount - 1; i >= 0; i--)
        {
            _particleSystem.textureSheetAnimation.RemoveSprite(i);
        }

        for (int i = 0, cnt = data.ParticleSprites.Length; i < cnt; ++i)
        {
            _particleSystem.textureSheetAnimation.AddSprite(data.ParticleSprites[i]);
        }

        _particleSystem.gameObject.SetActive(true);
        _uiParticle.enabled = true;
    }

}
