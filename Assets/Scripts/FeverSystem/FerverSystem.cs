using System;
using System.Collections;
using UnityEngine;

public class FeverSystem : MonoBehaviour
{
    public event Action OnStartFeverHandler;
    public event Action OnEndFeverHandler;

    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIFever _uiFever;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private FeverTutorial _feverTutorial;

    private GameManager _gameManager;
    private FeverRuntimeContext _feverRuntimeContext;
    private FeverRuntimeToken _activeFeverToken;
    private bool _isStoppingFever;
    private bool _isApplicationQuitting;
    private bool _hasNotifiedGameplayStart;

    public bool IsFeverStart =>
        _feverRuntimeContext != null
        && _feverRuntimeContext.IsCurrentToken(_activeFeverToken);

    private static int _currentMaxFeverGauge = 500;
    public static int CurrentMaxFeverGauge => _currentMaxFeverGauge;
    private int[] _maxFeverGauges = new int[]{550, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1000, 1000, 1000, 1000, 1000, 1000};

    public static float FeverGauge => UserInfo.GetFeverGauge(UserInfo.CurrentStage);
    public void SetFeverGauge(float value) { UserInfo.SetFeverGauge(UserInfo.CurrentStage, value); }
    private Coroutine _feverRoutine = null;

    public void AddFeverGauge(float addMul = 1)
    {
        if (IsFeverStart || UserInfo.IsTutorialStart)
            return;

        // 이미 현재 맥스 이상이면 수정하지 않음 (로드된 게이지가 덮어쓰이는 것 방지)
        if (FeverGauge >= _currentMaxFeverGauge)
            return;

        float newGauge = Mathf.Clamp(FeverGauge + ConstValue.ADD_FEVER_GAUGE * addMul, 0, _currentMaxFeverGauge);
        UserInfo.SetFeverGauge(UserInfo.CurrentStage, newGauge);
        DebugLog.Log($"Fever Gauge : {FeverGauge} / {_currentMaxFeverGauge}");
        _uiFever.OnChangeGauge();
    }

    public void StartTutorial()
    {
        if (FeverGauge >= _currentMaxFeverGauge)
        {
            _feverTutorial.StartTutorial();
        }
    }


    public void FeverStart()
    {
        if (_isStoppingFever || IsFeverStart)
            return;

        if (UserInfo.IsFeverTutorialClear == false)
        {
            _feverTutorial.StartTutorial();
            return;
        }

        if (_gameManager == null || _feverRuntimeContext == null)
        {
            DebugLog.LogError("[Fever] Cached runtime dependencies are unavailable.");
            return;
        }

        if (_feverRoutine != null)
        {
            Coroutine staleRoutine = _feverRoutine;
            _feverRoutine = null;
            StopCoroutine(staleRoutine);
        }

        float durationSeconds = ConstValue.PEVER_TIME + _gameManager.AddFerverTime;
        if (!_feverRuntimeContext.TryActivate(durationSeconds, out FeverRuntimeToken token))
        {
            DebugLog.LogError("[Fever] Runtime activation failed. Duration: " + durationSeconds);
            return;
        }

        _activeFeverToken = token;
        _hasNotifiedGameplayStart = false;

        try
        {
            Coroutine startedRoutine = StartCoroutine(
                StartFeverRoutine(_feverRuntimeContext, token));
            if (_feverRuntimeContext.IsCurrentToken(token)
                && _activeFeverToken == token)
            {
                _feverRoutine = startedRoutine;
            }
            else if (startedRoutine != null)
            {
                StopCoroutine(startedRoutine);
            }
        }
        catch (Exception exception)
        {
            StopFeverRuntime(token, _hasNotifiedGameplayStart, false);
            DebugLog.LogError("[Fever] Coroutine start failed.\n" + exception);
        }
    }

    private void Awake()
    {
        _gameManager = GameManager.Instance;
        _feverRuntimeContext = _gameManager.FeverRuntimeContext;
        _activeFeverToken = default;
        _isStoppingFever = false;
        _isApplicationQuitting = false;
        _hasNotifiedGameplayStart = false;
        UserInfo.OnChangeFurnitureHandler += OnEquipFurnitureEvent;
        LoadingSceneManager.OnLoadSceneHandler += OnChangeSceneEvent;
        OnEquipFurnitureEvent(ERestaurantFloorType.Floor1, FurnitureType.Table1); // MaxFeverGauge 먼저 설정
        _uiFever.Init(this); // 올바른 MaxFeverGauge 반영 후 Init
    }

    private void Start()
    {
    }


    private void OnEnable()
    {
        _uiFever.OnChangeGauge();
    }

    private void OnDisable()
    {
        StopFeverRuntime(
            _activeFeverToken,
            !_isApplicationQuitting,
            true);
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
        StopFeverRuntime(_activeFeverToken, false, true);
    }

    private void OnDestroy()
    {
        StopFeverRuntime(
            _activeFeverToken,
            !_isApplicationQuitting,
            true);
        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
        UserInfo.OnChangeFurnitureHandler -= OnEquipFurnitureEvent;
    }

    private void OnChangeSceneEvent()
    {
        StopFeverRuntime(_activeFeverToken, true, true);
    }


    private void OnEquipFurnitureEvent(ERestaurantFloorType floor, FurnitureType type)
    {
        int equipTableCount = 0;
        for(int i = 0, cnt = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage); i <= cnt; ++i)
        {
            ERestaurantFloorType floorType = (ERestaurantFloorType)i;
            for(int j = 0, cntJ = (int)FurnitureType.Table5; j <= cntJ; ++j)
            {
                FurnitureType furnitureType = (FurnitureType)j;
                if (UserInfo.IsEquipFurniture(UserInfo.CurrentStage, floorType, furnitureType))
                {
                    equipTableCount++;
                }
            }
        }
        _currentMaxFeverGauge = Mathf.Clamp(_maxFeverGauges[equipTableCount], _maxFeverGauges[0], ConstValue.MAX_PEVER_GAUGE);
        _uiFever.OnChangeGauge();
    }


    private IEnumerator StartFeverRoutine(
        FeverRuntimeContext context,
        FeverRuntimeToken activeToken)
    {
        try
        {
            if (!context.IsCurrentToken(activeToken))
            {
                yield break;
            }
            SetFeverGauge(0); // 피버 시작 즉시 0으로 저장 (저장 도중 중단 시 MAX로 남는 문제 방지)
            _hasNotifiedGameplayStart = true;
            OnStartFeverHandler?.Invoke();

            if (!context.IsCurrentToken(activeToken))
            {
                yield break;
            }

            _mainScene.PlayMainMusic();

            if (!context.IsCurrentToken(activeToken))
            {
                yield break;
            }

            SetLegacyFeverSpeed(true);
            while (context.IsCurrentToken(activeToken))
            {
                yield return null;

                float deltaSeconds = Time.deltaTime;
                FeverRuntimeAdvanceResult result =
                    context.Advance(activeToken, deltaSeconds);
                if (!result.IsCurrentActivation)
                {
                    yield break;
                }

                for (int i = 0; i < result.AutoCallOpportunityCount; ++i)
                {
                    if (!context.IsCurrentToken(activeToken))
                    {
                        yield break;
                    }

                    if (!CustomerController.IsMaxCount)
                    {
                        _customerController.AddTabCount();
                    }

                    if (!context.IsCurrentToken(activeToken))
                    {
                        yield break;
                    }
                }

                _uiFever.OnChangeGaugeNoAnime(result.RemainingRatio);
                if (result.DurationCompleted)
                {
                    yield break;
                }
            }
        }
        finally
        {
            StopFeverRuntime(
                activeToken,
                _hasNotifiedGameplayStart && !_isApplicationQuitting,
                false);
        }
    }

    private void StopFeverRuntime(
        FeverRuntimeToken token,
        bool notifyGameplayEnd,
        bool stopRunningCoroutine)
    {
        if (!token.IsValid
            || _activeFeverToken != token
            || _isStoppingFever)
        {
            return;
        }

        if (_feverRuntimeContext != null
            && _feverRuntimeContext.IsActive
            && !_feverRuntimeContext.IsCurrentToken(token))
        {
            Coroutine staleCoroutine = _feverRoutine;
            _feverRoutine = null;
            _hasNotifiedGameplayStart = false;
            _activeFeverToken = default;
            if (stopRunningCoroutine && staleCoroutine != null)
            {
                StopCoroutine(staleCoroutine);
            }

            return;
        }

        _isStoppingFever = true;
        Coroutine runningCoroutine = _feverRoutine;
        bool shouldNotifyGameplayEnd =
            notifyGameplayEnd && _hasNotifiedGameplayStart;

        _feverRoutine = null;
        _hasNotifiedGameplayStart = false;
        _activeFeverToken = default;

        try
        {
            try
            {
                _feverRuntimeContext?.Deactivate(token);
            }
            catch (Exception exception)
            {
                DebugLog.LogError("[Fever] Runtime context cleanup failed.\n" + exception);
            }

            try
            {
                SetLegacyFeverSpeed(false);
            }
            catch (Exception exception)
            {
                DebugLog.LogError("[Fever] Legacy speed cleanup failed.\n" + exception);
            }

            if (stopRunningCoroutine && runningCoroutine != null)
            {
                try
                {
                    StopCoroutine(runningCoroutine);
                }
                catch (Exception exception)
                {
                    DebugLog.LogError("[Fever] Coroutine stop failed.\n" + exception);
                }
            }

            if (shouldNotifyGameplayEnd && !_isApplicationQuitting)
            {
                try
                {
                    _mainScene.PlayMainMusic();
                }
                catch (Exception exception)
                {
                    DebugLog.LogError("[Fever] Music restore failed.\n" + exception);
                }

                try
                {
                    OnEndFeverHandler?.Invoke();
                }
                catch (Exception exception)
                {
                    DebugLog.LogError("[Fever] End event failed.\n" + exception);
                }
            }
        }
        finally
        {
            _isStoppingFever = false;
        }
    }

    private void SetLegacyFeverSpeed(bool isActive)
    {
        // FEVER_LEGACY_SPEED_BRIDGE_REMOVE_IN_FEVER_01_C
        if (_gameManager == null)
        {
            return;
        }

        if (isActive)
        {
            _gameManager.SetGameSpeed(1.5f);
        }
        else
        {
            _gameManager.SetGameSpeed(0f);
        }
    }
}
