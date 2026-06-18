using UnityEngine;
using Unity.Services.LevelPlay;

public class LevelPlayBoot : MonoBehaviour
{
    [SerializeField] private string appKey = "YOUR_LEVELPLAY_APP_KEY";
    
    [Header("Test Mode 설정")]
    [Tooltip("테스트 광고를 사용합니다. 빌드 전 반드시 확인하세요!")]
    [SerializeField] private bool enableTestMode = true;

    private void Awake()
    {
        Debug.Log("[LevelPlayBoot] === SDK 초기화 시작 ===");
        
        // ★★★ Test Mode 설정 ★★★
        if (enableTestMode)
        {
            Debug.LogWarning("[LevelPlayBoot] ⚠⚠⚠ TEST MODE = ON ⚠⚠⚠");
            Debug.LogWarning("[LevelPlayBoot] 테스트 광고가 표시됩니다.");
            Debug.LogWarning("[LevelPlayBoot] 릴리즈 빌드 전 반드시 OFF로 변경하세요!");
            
            // LevelPlay Test Mode 활성화
            LevelPlay.SetMetaData("is_test_suite", "enable");
        }
        else
        {
            Debug.Log("[LevelPlayBoot] ✓ TEST MODE = OFF (프로덕션 모드)");
            Debug.Log("[LevelPlayBoot] 실제 광고가 표시됩니다.");
        }
        
        // 1) 디버그 로그
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LevelPlay.SetAdaptersDebug(true);
        Debug.Log("[LevelPlayBoot] SetAdaptersDebug(true) 설정 완료");
#endif
        
        // 2) 연동 검증
        LevelPlay.ValidateIntegration();
        Debug.Log("[LevelPlayBoot] ValidateIntegration() 호출 완료");

        // 3) 초기화 + 결과 이벤트
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        
        Debug.Log("[LevelPlayBoot] 초기화 이벤트 등록 완료");

        // 4) SDK 초기화
        LevelPlay.Init(appKey);
        Debug.Log("[LevelPlayBoot] LevelPlay.Init() 호출 완료");
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[LevelPlayBoot] ★★★ initialization success ★★★");
        // 어댑터 로딩 상태 확인을 위한 로그
        Debug.Log("[LevelPlayBoot] 광고 네트워크 어댑터 초기화 대기 중...");
        Debug.Log("[LevelPlayBoot] 로그에서 다음 항목들을 확인하세요:");
        Debug.Log("[LevelPlayBoot] - adapter loaded (ironSource)");
        Debug.Log("[LevelPlayBoot] - adapter loaded (UnityAds)");
        Debug.Log("[LevelPlayBoot] - adapter loaded (AdMob)");
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[LevelPlayBoot] ✖✖✖ initialization failed ✖✖✖: {error.ErrorMessage}");
        Debug.LogError($"[LevelPlayBoot] Error Code: {error.ErrorCode}");
    }
}