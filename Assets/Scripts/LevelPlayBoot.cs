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
    [SerializeField] private string appKey = "YOUR_LEVELPLAY_APP_KEY";

    [Header("모드 설정")]
    [Tooltip("true: Integration Test Suite 실행  |  false: 실제 광고")]
    [SerializeField] private bool enableTestSuite = false;

    private void Awake()
    {
        // ── App Key 검증 ──
        if (string.IsNullOrEmpty(appKey) || appKey == "YOUR_LEVELPLAY_APP_KEY")
        {
            Debug.LogError("[LevelPlayBoot] appKey가 설정되지 않았습니다! 인스펙터에서 실제 App Key를 입력하세요.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LevelPlay.SetAdaptersDebug(true);
        LevelPlay.ValidateIntegration();
#endif

        // ── Test Suite 모드 ──
        if (enableTestSuite)
        {
            Debug.LogWarning("[LevelPlayBoot] Integration Test Suite 모드 활성화 — 일반 광고 로드 비활성화");
            LevelPlay.SetMetaData("is_test_suite", "enable");
            // AdManager가 SDK 초기화 완료 후 광고를 로드하지 않도록 플래그 설정
            AdManager.Instance.SetTestSuiteMode(true);
        }
        else
        {
            Debug.Log("[LevelPlayBoot] 프로덕션 모드 — 실제 광고");
        }

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed  += OnInitFailed;

        LevelPlay.Init(appKey);
        Debug.Log($"[LevelPlayBoot] LevelPlay.Init() 호출 완료");
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
