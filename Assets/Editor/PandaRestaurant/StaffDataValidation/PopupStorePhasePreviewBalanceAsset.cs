#if UNITY_EDITOR
using UnityEngine;

/// <summary>Inspector-authored local preview proposal. Every explicit start takes an independent JSON snapshot.</summary>
public sealed class PopupStorePhasePreviewBalanceAsset : ScriptableObject
{
    [SerializeField, Tooltip("Editor 격리 프리뷰 전용 초안입니다. 변경은 다음 프리뷰 시작부터 적용됩니다. 일반 90초 영업에는 적용하지 않습니다.")]
    private PopupStorePhasePreviewSettings _settings = PopupStorePhasePreviewSettings.CreateProposalV1();

    public PopupStorePhasePreviewSettings Settings => _settings;

    public bool TryFreeze(PopupStoreMaterialCatalog catalog, out string frozenJson,
        out PopupStorePhaseProfileSnapshot profile, out PopupStoreScorePolicySnapshot scorePolicy, out string error)
    {
        frozenJson = null; profile = null; scorePolicy = null; error = null;
        if (_settings == null || _settings.goalMetric != PopupStorePhaseGoalMetric.CumulativeScore)
        { error = "실제 Editor 프리뷰 시작은 누적 점수 설정을 명시해야 합니다. 기존 v0 건수는 변환하지 않습니다."; return false; }
        string json = JsonUtility.ToJson(_settings, true);
        if (!PopupStorePhasePreviewSettings.TryParseSnapshot(json, catalog, out profile, out scorePolicy, out error))
            return false;
        frozenJson = json;
        return true;
    }
}
#endif
