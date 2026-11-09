#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Separate explicit planner path; the existing ninety-second test menu remains unchanged.</summary>
public static class PopupStorePhasePreviewMenu
{
    internal const string StartPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 시작";
    internal const string ResetPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 초기화";
    internal const string StatusPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 상태 확인";

    [MenuItem(StartPath)]
    private static void StartPreview()
        => PopupStoreVerificationRunner.ExecutePhasePreviewCommand("phase-preview-start");

    [MenuItem(ResetPath)]
    private static void ResetPreview()
        => PopupStoreVerificationRunner.ExecutePhasePreviewCommand("phase-preview-reset");

    [MenuItem(StatusPath)]
    private static void StatusPreview()
        => PopupStoreVerificationRunner.ExecutePhasePreviewCommand("phase-preview-status");
}
#endif
