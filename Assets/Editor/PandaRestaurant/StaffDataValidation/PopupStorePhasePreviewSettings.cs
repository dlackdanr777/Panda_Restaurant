#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Editor preview configuration DTO. Legacy v0 counts remain separate from cumulative-score v1.</summary>
[Serializable]
public sealed class PopupStorePhasePreviewSettings
{
    public const string SettingsPath = "Temp/PopupStorePhasePreview/settings-v0.json";
    public const string BalanceAssetPath = "Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStorePhasePreviewBalance.asset";
    public string profileId = "EDITOR_PHASE_PREVIEW_PROPOSAL_V0";
    public int version = 1;
    public PopupStorePhaseGoalMetric goalMetric;
    public double clearNoticeSeconds = 1;
    public double countdownStepSeconds = 1;
    public PointPolicy points;
    public Stage[] stages;

    [Serializable]
    public sealed class PointPolicy
    {
        public int threeMaterials;
        public int fourMaterials;
        public int fiveMaterials;
    }

    [Serializable]
    public sealed class Stage
    {
        public double seconds;
        public int target;
        public int targetScore;
        public int plushCount;
        public double onePlushPatienceSeconds;
        public double entryIntervalSeconds;
    }

    public static PopupStorePhasePreviewSettings CreateProposalV0()
        => new PopupStorePhasePreviewSettings { stages = new[] {
            Row(22, 1, 5, 22, 3.2), Row(27, 2, 8, 21, 3.0), Row(30, 2, 12, 20, 2.8),
            Row(34, 3, 16, 19, 2.6), Row(38, 3, 24, 18, 2.4), Row(42, 4, 32, 17, 2.2)
        } };

    public static PopupStorePhasePreviewSettings CreateProposalV1()
        => new PopupStorePhasePreviewSettings {
            profileId = "EDITOR_PHASE_PREVIEW_CUMULATIVE_V1", version = 2,
            goalMetric = PopupStorePhaseGoalMetric.CumulativeScore,
            points = new PointPolicy { threeMaterials = 100, fourMaterials = 150, fiveMaterials = 220 },
            stages = new[] {
                ScoreRow(22, 200, 5, 22, 3.2), ScoreRow(27, 650, 8, 20, 3.0),
                ScoreRow(30, 1200, 12, 18, 2.8), ScoreRow(34, 1900, 16, 16, 2.6),
                ScoreRow(38, 2800, 24, 14, 2.4), ScoreRow(42, 3900, 32, 12, 2.2)
            }
        };

    private static Stage ScoreRow(double seconds, int targetScore, int plushes, double patience, double interval)
        => new Stage { seconds = seconds, targetScore = targetScore, plushCount = plushes,
            onePlushPatienceSeconds = patience, entryIntervalSeconds = interval };

    private static Stage Row(double seconds, int target, int plushes, double patience, double interval)
        => new Stage { seconds = seconds, target = target, plushCount = plushes,
            onePlushPatienceSeconds = patience, entryIntervalSeconds = interval };

    public bool TryCreateSnapshot(PopupStoreMaterialCatalog catalog,
        out PopupStorePhaseProfileSnapshot snapshot, out string error)
        => TryCreateSnapshot(catalog, out snapshot, out _, out error);

    public bool TryCreateSnapshot(PopupStoreMaterialCatalog catalog,
        out PopupStorePhaseProfileSnapshot snapshot, out PopupStoreScorePolicySnapshot scorePolicy, out string error)
    {
        snapshot = null; scorePolicy = null;
        PopupStoreScorePolicySnapshot validatedPolicy;
        if (goalMetric == PopupStorePhaseGoalMetric.SuccessfulOrders)
        {
            // Unity can materialize a missing inline serializable class with zero fields on JSON round-trip.
            // Legacy count proposals always retain their original ordinary policy; they are never migrated.
            validatedPolicy = PopupStoreScorePolicySnapshot.Ordinary;
        }
        else if (points == null)
        {
            error = "누적 점수 프리뷰는 3/4/5개 재료의 점수 정책을 명시해야 합니다."; return false;
        }
        else if (!PopupStoreScorePolicySnapshot.TryCreate(points.threeMaterials, points.fourMaterials,
            points.fiveMaterials, out validatedPolicy, out error)) return false;
        if (catalog == null) { error = "승인된 38종 재료 카탈로그가 없습니다."; return false; }
        if (!catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out error)) return false;
        if (stages == null || stages.Length != 6)
        { error = "프리뷰 설정은 C/B/A/S/SS/SSS 여섯 단계를 명시해야 합니다."; return false; }
        var definitions = new PopupStorePhaseDefinition[6];
        for (int i = 0; i < stages.Length; i++)
        {
            var stage = stages[i];
            if (stage == null || stage.plushCount < 1 || stage.plushCount > plushes.Length)
            { error = "단계 " + (i + 1) + "의 활성 인형 수가 유효하지 않습니다."; return false; }
            definitions[i] = new PopupStorePhaseDefinition {
                PhaseNumber = i + 1, Grade = (PopupStoreGradeTier)(i + 1),
                TimeLimitSeconds = stage.seconds, TargetSuccessfulOrders = stage.target, TargetScore = stage.targetScore,
                AllowedPlushIds = plushes.Take(stage.plushCount).Select(item => item.Id).ToArray(),
                CustomerPatienceSeconds = stage.onePlushPatienceSeconds,
                EntryIntervalSeconds = stage.entryIntervalSeconds,
                StageClearNoticeSeconds = clearNoticeSeconds, CountdownStepSeconds = countdownStepSeconds
            };
        }
        if (!new PopupStorePhaseProfile { ProfileId = profileId, Version = version, GoalMetric = goalMetric, Phases = definitions }
            .TryCreateSnapshot(catalog, out snapshot, out error)) return false;
        scorePolicy = validatedPolicy;
        return true;
    }

    /// <summary>Explicit menu start freezes the authored Editor asset. It never reads, migrates or writes legacy Temp v0.</summary>
    internal static bool TryLoadForExplicitStart(PopupStoreMaterialCatalog catalog,
        out string frozenJson, out PopupStorePhaseProfileSnapshot snapshot, out string error)
        => TryLoadForExplicitStart(catalog, out frozenJson, out snapshot, out _, out error);

    internal static bool TryLoadForExplicitStart(PopupStoreMaterialCatalog catalog,
        out string frozenJson, out PopupStorePhaseProfileSnapshot snapshot,
        out PopupStoreScorePolicySnapshot scorePolicy, out string error)
    {
        frozenJson = null; snapshot = null; scorePolicy = null; error = null;
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<PopupStorePhasePreviewBalanceAsset>(BalanceAssetPath);
            if (asset == null) { error = "단계 프리뷰의 Editor 밸런스 에셋이 없습니다: " + BalanceAssetPath; return false; }
            return asset.TryFreeze(catalog, out frozenJson, out snapshot, out scorePolicy, out error);
        }
        catch (Exception exception)
        { error = "프리뷰 설정을 읽지 못했습니다: " + exception.GetType().Name; return false; }
    }

    internal static bool TryParseSnapshot(string json, PopupStoreMaterialCatalog catalog,
        out PopupStorePhaseProfileSnapshot snapshot, out string error)
        => TryParseSnapshot(json, catalog, out snapshot, out _, out error);

    internal static bool TryParseSnapshot(string json, PopupStoreMaterialCatalog catalog,
        out PopupStorePhaseProfileSnapshot snapshot, out PopupStoreScorePolicySnapshot scorePolicy, out string error)
    {
        snapshot = null; scorePolicy = null; error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) { error = "프리뷰 설정이 비어 있습니다."; return false; }
            var settings = JsonUtility.FromJson<PopupStorePhasePreviewSettings>(json);
            if (settings == null) { error = "프리뷰 설정 형식이 유효하지 않습니다."; return false; }
            return settings.TryCreateSnapshot(catalog, out snapshot, out scorePolicy, out error);
        }
        catch (Exception exception)
        { error = "프리뷰 설정 형식 오류: " + exception.GetType().Name; return false; }
    }
}
#endif
