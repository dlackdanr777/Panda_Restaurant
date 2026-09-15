using Coffee.UIExtensions;
using System.Collections.Generic;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _excludePresentationParticleForDiagnostics;

    internal PresentationParticleState CapturePresentationParticleState()
    {
        ParticleSystem[] particles = _uiParticle.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSimulationState[] simulationStates = new ParticleSimulationState[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            ParticleSystem.MainModule main = particle.main;
            simulationStates[i] = new ParticleSimulationState(
                particle,
                particle.gameObject.activeInHierarchy,
                particle.isPlaying,
                particle.isPaused,
                particle.time,
                main.cullingMode);
        }

        return new PresentationParticleState(
            this,
            _uiParticle,
            _uiParticle.activeSelf,
            simulationStates);
    }

    internal sealed class PresentationParticleState
    {
        private readonly UIMarketerImage _owner;
        private readonly GameObject _root;
        private readonly bool _wasActive;
        private readonly ParticleSimulationState[] _simulationStates;

        internal int ParticleSystemCount => _simulationStates.Length;
        internal bool WasActive => _wasActive;

        internal PresentationParticleState(
            UIMarketerImage owner,
            GameObject root,
            bool wasActive,
            ParticleSimulationState[] simulationStates)
        {
            _owner = owner;
            _root = root;
            _wasActive = wasActive;
            _simulationStates = simulationStates;
        }

        internal string Describe()
        {
            List<string> descriptions = new List<string>(_simulationStates.Length);
            for (int i = 0; i < _simulationStates.Length; i++)
            {
                ParticleSimulationState state = _simulationStates[i];
                descriptions.Add(
                    state.Particle.name
                    + "(active=" + state.Particle.gameObject.activeInHierarchy
                    + ", playing=" + state.WasPlaying
                    + ", paused=" + state.WasPaused
                    + ", time=" + state.Time.ToString("F2")
                    + ", culling=" + state.CullingMode + ")");
            }

            return _root.name
                + "(activeSelf=" + _wasActive
                + ", systems=" + string.Join(", ", descriptions) + ")";
        }

        internal void Exclude()
        {
            _owner._excludePresentationParticleForDiagnostics = true;
            _root.SetActive(false);
        }

        internal void Restore()
        {
            _owner._excludePresentationParticleForDiagnostics = false;
            _root.SetActive(_wasActive);
            if (!_wasActive)
                return;

            for (int i = 0; i < _simulationStates.Length; i++)
            {
                ParticleSimulationState state = _simulationStates[i];
                if (state.Particle == null)
                    continue;

                if (!state.WasActiveInHierarchy || !state.Particle.gameObject.activeInHierarchy)
                    continue;

                state.Particle.Simulate(state.Time, true, true, true);
                if (state.WasPaused)
                    state.Particle.Pause(true);
                else if (state.WasPlaying)
                    state.Particle.Play(true);
                else
                    state.Particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    internal readonly struct ParticleSimulationState
    {
        internal readonly ParticleSystem Particle;
        internal readonly bool WasActiveInHierarchy;
        internal readonly bool WasPlaying;
        internal readonly bool WasPaused;
        internal readonly float Time;
        internal readonly ParticleSystemCullingMode CullingMode;

        internal ParticleSimulationState(
            ParticleSystem particle,
            bool wasActiveInHierarchy,
            bool wasPlaying,
            bool wasPaused,
            float time,
            ParticleSystemCullingMode cullingMode)
        {
            Particle = particle;
            WasActiveInHierarchy = wasActiveInHierarchy;
            WasPlaying = wasPlaying;
            WasPaused = wasPaused;
            Time = time;
            CullingMode = cullingMode;
        }
    }
#endif

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
            SetPresentationParticleActive(true);
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

        }
        else
        {
            _marketerSprite = skinData.Sprite;
            _animationSprite = skinData.AnimationSprite;
            _marketerImage.sprite = _marketerSprite;
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

        }

        _particleSystem.gameObject.SetActive(true);
        SetPresentationParticleActive(true);
    }

    private void SetPresentationParticleActive(bool active)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_excludePresentationParticleForDiagnostics)
        {
            _uiParticle.SetActive(false);
            return;
        }
#endif
        _uiParticle.SetActive(active);
    }
}
