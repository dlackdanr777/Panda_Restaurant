using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStoreGradeTier { None, C, B, A, S, SS, SSS }

public readonly struct PopupStoreGradeEvaluation
{
    public bool IsValid { get; }
    public int Score { get; }
    public int SuccessfulOrders { get; }
    public PopupStoreGradeTier Grade { get; }
    public int MinimumScore { get; }
    public int NextTargetScore { get; }
    public float Progress { get; }

    internal PopupStoreGradeEvaluation(int score, int successfulOrders, PopupStoreGradeTier grade,
        int minimumScore, int nextTargetScore, float progress)
    {
        IsValid = true;
        Score = score;
        SuccessfulOrders = successfulOrders;
        Grade = grade;
        MinimumScore = minimumScore;
        NextTargetScore = nextTargetScore;
        Progress = progress;
    }
}

/// <summary>Pure thresholds. Synthetic integer boundary scores need not be multiples of actual receipt points.</summary>
public static class PopupStoreGradeCalculator
{
    public static bool TryEvaluate(int score, int successfulOrders,
        out PopupStoreGradeEvaluation evaluation, out string error)
    {
        evaluation = default;
        error = null;
        if (score < 0 || successfulOrders < 0 || (successfulOrders == 0 && score != 0)
            || (successfulOrders > 0 && (score < 100L * successfulOrders || score > 180L * successfulOrders)))
        { error = "Score and successful-order count contradict normal sale bounds."; return false; }
        if (successfulOrders == 0)
        {
            evaluation = new PopupStoreGradeEvaluation(0, 0, PopupStoreGradeTier.None, 0, 700, 0f);
            return true;
        }
        PopupStoreGradeTier grade;
        int lower, next;
        if (score < 700) { grade = PopupStoreGradeTier.C; lower = 0; next = 700; }
        else if (score < 1200) { grade = PopupStoreGradeTier.B; lower = 700; next = 1200; }
        else if (score < 1700) { grade = PopupStoreGradeTier.A; lower = 1200; next = 1700; }
        else if (score < 2100) { grade = PopupStoreGradeTier.S; lower = 1700; next = 2100; }
        else if (score < 2400) { grade = PopupStoreGradeTier.SS; lower = 2100; next = 2400; }
        else { grade = PopupStoreGradeTier.SSS; lower = next = 2400; }
        float progress = grade == PopupStoreGradeTier.SSS ? 1f
            : (float)Math.Max(0d, Math.Min(1d, (double)(score - lower) / (next - lower)));
        evaluation = new PopupStoreGradeEvaluation(score, successfulOrders, grade, lower, next, progress);
        return true;
    }
}

/// <summary>One immutable local close result. Reading it never awards, consumes or finalizes anything.</summary>
public sealed class PopupStoreGradeResult
{
    public string SessionId { get; }
    public int Score { get; }
    public int SuccessfulOrders { get; }
    public PopupStoreGradeTier Grade { get; }
    public bool IsFinalized => true;

    internal PopupStoreGradeResult(string sessionId, PopupStoreGradeEvaluation evaluation)
    {
        SessionId = sessionId;
        Score = evaluation.Score;
        SuccessfulOrders = evaluation.SuccessfulOrders;
        Grade = evaluation.Grade;
    }
}

/// <summary>Observes this popup's existing score and clock; it owns neither transactions nor time.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreGradeSession : MonoBehaviour
{
    [SerializeField] private PopupStoreScore _score;
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private TMP_Text _gradeText;
    [SerializeField] private Image _progressImage;
    [SerializeField] private Image _progressFrame;
    [SerializeField] private bool _showHud = true;
    [SerializeField] private bool _showProgress = true;
    [SerializeField] private string _liveFormat = "등급 {0}";
    [SerializeField] private string _finalFormat = "최종 등급 {0}";
    [SerializeField] private string _noneText = "—";
    [SerializeField] private string _invalidText = "등급 확인 불가";

    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private PopupStoreScore _ownerScore, _boundScore;
    private PopupStoreBusinessClock _ownerClock, _boundClock;
    private PopupStoreGradeEvaluation _current = new PopupStoreGradeEvaluation(0, 0, PopupStoreGradeTier.None, 0, 700, 0f);
    private bool _refreshing;
    private string _lastDataError, _lastHudError;

    public string SessionId => _sessionId;
    public PopupStoreGradeEvaluation Current => _current;
    public PopupStoreGradeResult FinalResult { get; private set; }
    public bool IsFinalized => FinalResult != null;
    public int FinalizationCount { get; private set; }
    public event Action<PopupStoreGradeResult> Finalized;

    public bool Refresh()
    {
        if (_refreshing) return _current.IsValid;
        if (IsFinalized) { RefreshHud(); return true; }
        _refreshing = true;
        try
        {
            if (!BindSources(out string error)) return Reject(error);
            // Set the guard BEFORE recovering scores. Several missing receipts may each publish Changed.
            if (!_ownerScore.Refresh()) return Reject("The local score source could not be refreshed.");
            if (!_ownerScore.TryReadCompleteSnapshot(out int score, out int count, out error)) return Reject(error);
            if (!PopupStoreGradeCalculator.TryEvaluate(score, count, out var evaluation, out error)) return Reject(error);
            _lastDataError = null;
            _current = evaluation;
            if (_ownerClock.State == PopupStoreBusinessState.Ended)
            {
                // Freeze the complete result before any HUD, log or external completion observer runs.
                FinalResult = new PopupStoreGradeResult(_sessionId, evaluation);
                FinalizationCount++;
                RefreshHud();
#if UNITY_EDITOR
                Debug.Log("POPUP_STORE_GRADE_FINAL session=" + _sessionId + " score=" + FinalResult.Score
                    + " successful=" + FinalResult.SuccessfulOrders + " grade=" + FinalResult.Grade
                    + " finalized=" + FinalizationCount, this);
#endif
                var handlers = Finalized;
                if (handlers != null)
                    foreach (Action<PopupStoreGradeResult> handler in handlers.GetInvocationList())
                        try { handler(FinalResult); }
                        catch (Exception exception) { Debug.LogException(exception, this); }
            }
            else RefreshHud();
            return true;
        }
        finally { _refreshing = false; }
    }

    public void RefreshHud()
    {
        try
        {
            var popup = GetComponentInParent<UIPopupStore>(true);
            if (popup == null || _gradeText == null || _progressImage == null || _progressFrame == null
                || _gradeText.GetComponentInParent<UIPopupStore>(true) != popup
                || _progressImage.GetComponentInParent<UIPopupStore>(true) != popup
                || _progressFrame.GetComponentInParent<UIPopupStore>(true) != popup)
                throw new InvalidOperationException("Grade TMP and progress images must belong to this popup.");
            string label = _current.Grade == PopupStoreGradeTier.None ? _noneText : _current.Grade.ToString();
            string text = _current.IsValid
                ? string.Format(CultureInfo.InvariantCulture, IsFinalized ? _finalFormat : _liveFormat, label)
                : _invalidText;
            _gradeText.enabled = _showHud;
            if (_gradeText.text != text) _gradeText.text = text;
            _progressFrame.enabled = _showHud && _showProgress;
            _progressImage.enabled = _showHud && _showProgress;
            _progressImage.fillAmount = _current.IsValid ? _current.Progress : 0f;
            _lastHudError = null;
        }
        catch (Exception exception)
        {
            if (_lastHudError != exception.Message)
                Debug.LogError("PopupStoreGradeSession: HUD update failed; result data is preserved. " + exception.Message, this);
            _lastHudError = exception.Message;
        }
    }

    private bool BindSources(out string error)
    {
        error = null;
        var popup = GetComponentInParent<UIPopupStore>(true);
        if (popup == null || _score == null || _clock == null
            || _score.GetComponentInParent<UIPopupStore>(true) != popup || _clock.gameObject != popup.gameObject)
            error = "Score and business clock must belong to this popup.";
        else if ((_ownerScore != null && _ownerScore != _score) || (_ownerClock != null && _ownerClock != _clock))
            error = "A grade session cannot be rebound to another score or clock.";
        if (error != null) { Unbind(); return false; }
        if (_ownerScore == null) _ownerScore = _score;
        if (_ownerClock == null) _ownerClock = _clock;
        if (_boundScore != _ownerScore || _boundClock != _ownerClock)
        {
            Unbind();
            _boundScore = _ownerScore;
            _boundClock = _ownerClock;
            _boundScore.Changed += OnScoreChanged;
            _boundClock.Ended += OnEnded;
        }
        return true;
    }

    private bool Reject(string error)
    {
        _current = default;
        if (_lastDataError != error) Debug.LogError("PopupStoreGradeSession: result not finalized. " + error, this);
        _lastDataError = error;
        RefreshHud();
        return false;
    }

    private void OnScoreChanged() => Refresh();
    private void OnEnded() => Refresh();
    private void Unbind()
    {
        if (_boundScore != null) _boundScore.Changed -= OnScoreChanged;
        if (_boundClock != null) _boundClock.Ended -= OnEnded;
        _boundScore = null;
        _boundClock = null;
    }
    private void OnEnable() => Refresh();
    // Hidden HUDs must still observe a terminal clock notification during deactivation.
    private void OnDisable() { }
    private void OnDestroy() => Unbind();
}
