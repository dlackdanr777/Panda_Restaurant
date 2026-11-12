using System;
using System.Linq;
using UnityEngine;

/// <summary>Explicit opt-in for the existing approved phase game. Ordinary 90-second play and review settings are separate.</summary>
[CreateAssetMenu(menuName = "Panda Restaurant/Popup Store/Account Phase Configuration")]
public sealed class PopupStoreAccountPhaseConfiguration : ScriptableObject
{
    [SerializeField, Tooltip("Actual account mode remains disabled until test-account server validation is approved.")]
    private bool _enableAccountMode;
    [Header("Account daily admission limit")]
    [SerializeField, Range(1, Muks.BackEnd.PopupStoreGameDataRecord.MaximumDailyLimit)]
    [Tooltip("Per account, per server KST-noon day. Internal QA: 200. Before release: choose 1 or 3. Change in Edit Mode and restart Play. This does not disable real rewards.")]
    private int _dailyBusinessLimit = 1;
    [SerializeField] private GameObject _tutorialPrefab;
    [SerializeField] private GameObject _rewardPrefab;
    [SerializeField] private PopupStoreRewardTable _rewardTable;
    [SerializeField] private string _profileId;
    [SerializeField] private int _profileVersion;
    [SerializeField] private int _threeMaterialPoints;
    [SerializeField] private int _fourMaterialPoints;
    [SerializeField] private int _fiveMaterialPoints;
    [SerializeField] private double _clearNoticeSeconds;
    [SerializeField] private double _countdownStepSeconds;
    [SerializeField] private Stage[] _stages;
    [Serializable] private sealed class Stage
    {
        public double Seconds;
        public int TargetScore;
        public int PlushCount;
        public double PatienceSeconds;
        public double EntryIntervalSeconds;
    }
    public bool EnableAccountMode => _enableAccountMode;
    public int DailyBusinessLimit => _dailyBusinessLimit;
    public GameObject TutorialPrefab => _tutorialPrefab;
    public GameObject RewardPrefab => _rewardPrefab;
    public PopupStoreRewardTable RewardTable => _rewardTable;

    public bool TryFreeze(PopupStoreMaterialCatalog catalog, out PopupStorePhaseProfileSnapshot profile,
        out PopupStoreScorePolicySnapshot scoring, out string error)
    {
        profile = null; scoring = null; error = null;
        if (_dailyBusinessLimit < 1 || _dailyBusinessLimit > Muks.BackEnd.PopupStoreGameDataRecord.MaximumDailyLimit)
        { error = "일일 이용 한도는 1~200 범위여야 합니다."; return false; }
        if (_tutorialPrefab == null || _tutorialPrefab.GetComponent<PopupStorePhasePreviewView>() == null
            || _rewardPrefab == null || _rewardPrefab.GetComponent<PopupStorePhaseRewardPresentation>() == null
            || _rewardTable == null || !_rewardTable.TryCreateSnapshot(out var rewards, out error)
            || rewards.Schema != PopupStoreRewardSchema.R1Permille || rewards.PolicyId != PopupStoreRewardTable.R1PolicyId)
        { error = error ?? "The approved tutorial, reward presentation and R1 assets are required."; return false; }
        if (catalog == null || !catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out error)
            || _stages == null || _stages.Length != 6)
        { error = error ?? "The approved material catalog and six phases are required."; return false; }
        if (!PopupStoreScorePolicySnapshot.TryCreate(_threeMaterialPoints, _fourMaterialPoints,
            _fiveMaterialPoints, out scoring, out error)) return false;
        var definitions = new PopupStorePhaseDefinition[6];
        for (int i = 0; i < definitions.Length; i++)
        {
            var stage = _stages[i];
            if (stage == null || stage.PlushCount < 1 || stage.PlushCount > plushes.Length)
            { error = "A phase material limit is invalid."; return false; }
            definitions[i] = new PopupStorePhaseDefinition { PhaseNumber = i + 1,
                Grade = (PopupStoreGradeTier)(i + 1), TimeLimitSeconds = stage.Seconds,
                TargetScore = stage.TargetScore, AllowedPlushIds = plushes.Take(stage.PlushCount).Select(x => x.Id).ToArray(),
                CustomerPatienceSeconds = stage.PatienceSeconds, EntryIntervalSeconds = stage.EntryIntervalSeconds,
                StageClearNoticeSeconds = _clearNoticeSeconds, CountdownStepSeconds = _countdownStepSeconds };
        }
        return new PopupStorePhaseProfile { ProfileId = _profileId, Version = _profileVersion,
            GoalMetric = PopupStorePhaseGoalMetric.CumulativeScore, Phases = definitions }
            .TryCreateSnapshot(catalog, out profile, out error);
    }
}
