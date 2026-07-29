using UnityEngine;
using Unity.Services.LevelPlay;

/// <summary>
/// LevelPlay SDK 초기화 전담.
/// AdManager보다 늦게 실행(DefaultExecutionOrder -50)되어
/// AdManager.Awake()가 먼저 OnInitSuccess를 구독한 뒤 Init이 호출됨을 보장.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LevelPlayBoot : MonoBehaviour
{
    private const string AdsBuildStamp = "ADS_FIX_20260729_V3";

    [SerializeField] private string appKey = "YOUR_LEVELPLAY_APP_KEY";

    [Header("모드 설정")]
    [Tooltip("true: Integration Test Suite 실행  |  false: 실제 광고")]
    [SerializeField] private bool enableTestSuite = false;

    private void Awake()
    {
        Debug.LogError($"[ADS_BOOT] {AdsBuildStamp} LevelPlayBoot.Awake");

        // ── App Key 검증 ──
        if (string.IsNullOrEmpty(appKey) || appKey == "YOUR_LEVELPLAY_APP_KEY")
        {
            Debug.LogError("[LevelPlayBoot] appKey가 설정되지 않았습니다! 인스펙터에서 실제 App Key를 입력하세요.");
            return;
        }

        // 중요: LevelPlay.Init() 이전에 AdManager를 반드시 생성하여
        // OnInitSuccess 구독이 Init 호출 전에 등록됨을 보장합니다.
        AdManager.Instance.SetTestSuiteMode(enableTestSuite);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LevelPlay.SetAdaptersDebug(true);
        LevelPlay.ValidateIntegration();
#endif

        // ── Test Suite 모드 ──
        if (enableTestSuite)
        {
            Debug.LogWarning("[LevelPlayBoot] Integration Test Suite 모드 활성화 — 일반 광고 로드 비활성화");
            LevelPlay.SetMetaData("is_test_suite", "enable");
        }
        else
        {
            Debug.Log("[LevelPlayBoot] 프로덕션 모드 — 실제 광고");
        }

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed  += OnInitFailed;

        Debug.LogError($"[ADS_BOOT_V3] Init 호출 직전, AppKey={appKey}");
        LevelPlay.Init(appKey);
        Debug.LogError("[ADS_BOOT_V3] LevelPlay.Init 호출 완료");
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed  -= OnInitFailed;
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[LevelPlayBoot] SDK 초기화 성공");

        if (enableTestSuite)
        {
            Debug.Log("[LevelPlayBoot] Test Suite 실행");
            LevelPlay.LaunchTestSuite();
        }
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[LevelPlayBoot] SDK 초기화 실패: [{error.ErrorCode}] {error.ErrorMessage}");
    }
}
