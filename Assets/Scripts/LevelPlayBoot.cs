using UnityEngine;
using Unity.Services.LevelPlay;

public class LevelPlayBoot : MonoBehaviour
{
    [SerializeField] private string appKey = "YOUR_LEVELPLAY_APP_KEY";

    private void Awake()
    {
        // 1) 디버그 로그
        LevelPlay.SetAdaptersDebug(true);                 // (예전: IronSource.Agent.setAdaptersDebug)
        
        // 2) 연동 검증
        LevelPlay.ValidateIntegration();                  // (예전: IronSource.Agent.validateIntegration)

        // 3) 초기화 + 결과 이벤트
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[LevelPlay] Init Success");
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[LevelPlay] Init Failed: {error}");
    }
}