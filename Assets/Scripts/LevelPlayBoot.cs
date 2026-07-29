using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>
/// LevelPlay 초기화, 실패 재시도, 무응답 감시를 담당합니다.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class LevelPlayBoot : MonoBehaviour
{
    public static LevelPlayBoot Instance { get; private set; }

    private const string BuildStamp = "ADS_FIX_20260729_V4";

    [Header("LevelPlay")]
    [SerializeField] private string appKey = "249b664bd";

    [Header("Mode")]
    [SerializeField] private bool enableTestSuite = false;

    [Header("Retry")]
    [SerializeField, Min(1f)] private float retryDelaySeconds   = 5f;
    [SerializeField, Min(5f)] private float initWatchdogSeconds = 15f;

    private bool      _initialized;
    private bool      _initializing;
    private int       _initAttempt;
    private Coroutine _retryCoroutine;
    private Coroutine _watchdogCoroutine;

    public bool IsInitialized  => _initialized;
    public bool IsInitializing => _initializing;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.LogError($"[ADS_BOOT] {BuildStamp} LevelPlayBoot.Awake");

        if (string.IsNullOrWhiteSpace(appKey) || appKey == "YOUR_LEVELPLAY_APP_KEY")
        {
            Debug.LogError("[LevelPlayBoot] 유효한 App Key가 없습니다.");
            enabled = false;
            return;
        }

        // 반드시 Init 이전에 AdManager 생성 → OnInitSuccess 구독 보장
        AdManager.Instance.SetTestSuiteMode(enableTestSuite);

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed  += OnInitFailed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LevelPlay.SetAdaptersDebug(true);
        LevelPlay.ValidateIntegration();
#endif

        if (enableTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");
    }

    private IEnumerator Start()
    {
        // Android Activity와 네트워크 초기화를 위해 한 프레임 대기
        yield return null;
        EnsureInitialized();
    }

    /// <summary>
    /// 초기화되지 않은 상태라면 SDK 초기화를 요청합니다.
    /// 광고 버튼에서도 호출할 수 있습니다.
    /// </summary>
    public void EnsureInitialized()
    {
        if (_initialized || _initializing) return;

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("[LevelPlayBoot] 네트워크 없음 — 초기화 재시도 예약");
            ScheduleRetry();
            return;
        }

        BeginInitialize();
    }

    private void BeginInitialize()
    {
        if (_initialized || _initializing) return;

        _initializing = true;
        _initAttempt++;
        int attempt = _initAttempt;

        Debug.LogError($"[LevelPlayBoot] LevelPlay.Init 시작 Attempt={attempt}, AppKey={appKey}");

        try
        {
            LevelPlay.Init(appKey);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelPlayBoot] LevelPlay.Init 예외: {e}");
            _initializing = false;
            ScheduleRetry();
            return;
        }

        StopWatchdog();
        _watchdogCoroutine = StartCoroutine(InitWatchdog(attempt));
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.LogError($"[LevelPlayBoot] SDK 초기화 성공 Attempt={_initAttempt}");
        _initialized  = true;
        _initializing = false;
        StopWatchdog();
        StopRetry();

        if (enableTestSuite)
        {
            Debug.Log("[LevelPlayBoot] Integration Test Suite 실행");
            LevelPlay.LaunchTestSuite();
        }
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[LevelPlayBoot] SDK 초기화 실패 Attempt={_initAttempt}, Code={error.ErrorCode}, Message={error.ErrorMessage}");
        _initialized  = false;
        _initializing = false;
        StopWatchdog();
        ScheduleRetry();
    }

    private IEnumerator InitWatchdog(int attempt)
    {
        yield return new WaitForSecondsRealtime(initWatchdogSeconds);
        _watchdogCoroutine = null;

        if (_initialized || !_initializing || attempt != _initAttempt) yield break;

        Debug.LogError($"[LevelPlayBoot] 초기화 콜백 무응답 Attempt={attempt}, {initWatchdogSeconds}초 경과");
        _initializing = false;
        ScheduleRetry();
    }

    private void ScheduleRetry()
    {
        if (_initialized || _retryCoroutine != null) return;
        _retryCoroutine = StartCoroutine(RetryInitialize());
    }

    private IEnumerator RetryInitialize()
    {
        Debug.LogWarning($"[LevelPlayBoot] {retryDelaySeconds}초 후 SDK 초기화 재시도");
        yield return new WaitForSecondsRealtime(retryDelaySeconds);
        _retryCoroutine = null;
        EnsureInitialized();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !_initialized)
            EnsureInitialized();
    }

    private void StopWatchdog()
    {
        if (_watchdogCoroutine == null) return;
        StopCoroutine(_watchdogCoroutine);
        _watchdogCoroutine = null;
    }

    private void StopRetry()
    {
        if (_retryCoroutine == null) return;
        StopCoroutine(_retryCoroutine);
        _retryCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        StopWatchdog();
        StopRetry();
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed  -= OnInitFailed;
        Instance = null;
    }
}
