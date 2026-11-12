using System;
using System.Globalization;

public enum PopupStoreBusinessMode { Rewarded, Practice }
public enum PopupStorePracticeClockFailure { Reject, AllowWithoutDayKey }
public enum PopupStoreBusinessEndReason { Completed, DirectExit }

/// <summary>Configurable v1.3 TEST policy, not a release policy or an account setting.
/// Start admission is independent of the R0 reward table and any payout transaction.</summary>
public sealed class PopupStoreBusinessAccessPolicy
{
    public string Version { get; }
    public int RewardedDailyLimit { get; }
    public PopupStorePracticeClockFailure PracticeClockFailure { get; }
    public bool IsTestPolicy => true;

    public PopupStoreBusinessAccessPolicy(string version = "v1.3-test", int rewardedDailyLimit = 2,
        PopupStorePracticeClockFailure practiceClockFailure = PopupStorePracticeClockFailure.Reject)
    {
        if (string.IsNullOrWhiteSpace(version) || rewardedDailyLimit < 0
            || (practiceClockFailure != PopupStorePracticeClockFailure.Reject
                && practiceClockFailure != PopupStorePracticeClockFailure.AllowWithoutDayKey))
            throw new ArgumentException("Invalid test access policy.");
        Version = version; RewardedDailyLimit = rewardedDailyLimit; PracticeClockFailure = practiceClockFailure;
    }

    /// <summary>Only a candidate filter. The authority must first look up the original stored start
    /// and verify the outcome. A client-created record or sales count is never payout authorization.
    /// No random draw, grant creation or quota consumption happens here.</summary>
    public static bool CanConsiderReward(PopupStoreBusinessStartRecord record, string authenticatedAccountId,
        int successfulOrders, PopupStoreBusinessEndReason endReason)
        => record != null && record.Matches(record.Request)
            && string.Equals(record.AccountId, authenticatedAccountId, StringComparison.Ordinal)
            && record.Mode == PopupStoreBusinessMode.Rewarded
            && endReason == PopupStoreBusinessEndReason.Completed && successfulOrders > 0;
}

/// <summary>Stable client request identity, saved by a future host before sending. The expected
/// business ID is only a recovery assertion; new business IDs are assigned by the authority.</summary>
public sealed class PopupStoreBusinessStartRequest
{
    public string AccountId { get; }
    public string StartRequestId { get; }
    public PopupStoreBusinessMode Mode { get; }
    public string PolicyVersion { get; }
    public string ExpectedBusinessSessionId { get; }

    public PopupStoreBusinessStartRequest(string accountId, string startRequestId, PopupStoreBusinessMode mode,
        string policyVersion, string expectedBusinessSessionId = null)
    {
        AccountId = accountId; StartRequestId = startRequestId; Mode = mode;
        PolicyVersion = policyVersion; ExpectedBusinessSessionId = expectedBusinessSessionId;
    }

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(StartRequestId)
            || string.IsNullOrWhiteSpace(PolicyVersion)
            || (ExpectedBusinessSessionId != null && string.IsNullOrWhiteSpace(ExpectedBusinessSessionId)))
            error = "Account, stable start request and policy identifiers are required.";
        else if (Mode != PopupStoreBusinessMode.Rewarded && Mode != PopupStoreBusinessMode.Practice)
            error = "Unknown business mode.";
        return error == null;
    }

    public bool Matches(PopupStoreBusinessStartRequest other) => other != null
        && SameConditions(other)
        && string.Equals(ExpectedBusinessSessionId, other.ExpectedBusinessSessionId, StringComparison.Ordinal);

    public bool SameConditions(PopupStoreBusinessStartRequest other) => other != null
        && string.Equals(AccountId, other.AccountId, StringComparison.Ordinal)
        && string.Equals(StartRequestId, other.StartRequestId, StringComparison.Ordinal)
        && string.Equals(PolicyVersion, other.PolicyVersion, StringComparison.Ordinal) && Mode == other.Mode;

    public static string CreateRequestKey(string accountId, string startRequestId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(startRequestId)) return null;
        return accountId.Length.ToString(CultureInfo.InvariantCulture) + ":" + accountId + ":"
            + startRequestId.Length.ToString(CultureInfo.InvariantCulture) + ":" + startRequestId;
    }
}

/// <summary>Immutable authority start receipt. Usage is historical at commit, not the latest quota.
/// A missing day is allowed only for explicitly permitted, time-unverified Practice tests.</summary>
public sealed class PopupStoreBusinessStartRecord
{
    public PopupStoreBusinessStartRequest Request { get; }
    public string AccountId => Request.AccountId;
    public string StartRequestId => Request.StartRequestId;
    public PopupStoreBusinessMode Mode => Request.Mode;
    public string PolicyVersion => Request.PolicyVersion;
    public string BusinessSessionId { get; }
    public string StartDayKey { get; }
    public DateTimeOffset? StartedAtUtc { get; }
    public int RewardedUsesAfterStart { get; }
    public PopupStoreBusinessStartState StartStatus => PopupStoreBusinessStartState.Started;

    public PopupStoreBusinessStartRecord(PopupStoreBusinessStartRequest request, string businessSessionId,
        string startDayKey, DateTimeOffset? startedAtUtc, int rewardedUsesAfterStart)
    {
        Request = request; BusinessSessionId = businessSessionId; StartDayKey = startDayKey;
        StartedAtUtc = startedAtUtc; RewardedUsesAfterStart = rewardedUsesAfterStart;
    }

    public bool Matches(PopupStoreBusinessStartRequest request)
    {
        if (request == null || !request.TryValidate(out _) || Request == null || !Request.TryValidate(out _)
            || !Request.SameConditions(request) || string.IsNullOrWhiteSpace(BusinessSessionId)
            || RewardedUsesAfterStart < 0
            || (request.ExpectedBusinessSessionId != null
                && !string.Equals(request.ExpectedBusinessSessionId, BusinessSessionId, StringComparison.Ordinal))
            || (Request.ExpectedBusinessSessionId != null
                && !string.Equals(Request.ExpectedBusinessSessionId, BusinessSessionId, StringComparison.Ordinal))) return false;
        if (!StartedAtUtc.HasValue)
            return Mode == PopupStoreBusinessMode.Practice && StartDayKey == null;
        return StartedAtUtc.Value.Offset == TimeSpan.Zero
            && PopupStoreDailyUsage.TryGetDayKey(StartedAtUtc.Value, out string day)
            && string.Equals(day, StartDayKey, StringComparison.Ordinal)
            && (Mode != PopupStoreBusinessMode.Rewarded || RewardedUsesAfterStart > 0);
    }
}

public enum PopupStoreBusinessStartResponseKind
{ Started, RejectedBeforeSend, Indeterminate, Conflict, NotFound, NotAppliedConfirmed }

public sealed class PopupStoreBusinessStartResponse
{
    public PopupStoreBusinessStartRequest Request { get; }
    public PopupStoreBusinessStartResponseKind Kind { get; }
    public PopupStoreBusinessStartRecord Record { get; }
    public string Error { get; }
    public PopupStoreBusinessStartResponse(PopupStoreBusinessStartRequest request,
        PopupStoreBusinessStartResponseKind kind, PopupStoreBusinessStartRecord record = null, string error = null)
    { Request = request; Kind = kind; Record = record; Error = error; }
}

/// <summary>No production implementation. Authenticate first; replay existing account/request
/// receipts BEFORE consulting time/current policy. New record and daily usage commit atomically.
/// NotFound is not proof of no write. Query-only NotAppliedConfirmed must fence every older attempt
/// against a late commit. Callbacks per executor must be serialized; separate clients may race.</summary>
public interface IPopupStoreBusinessStartPersistence
{
    void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
        Action<PopupStoreBusinessStartResponse> completed);
    void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
        Action<PopupStoreBusinessStartResponse> completed);
}
