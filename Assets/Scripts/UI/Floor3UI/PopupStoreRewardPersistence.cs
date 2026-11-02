using System;
using System.Globalization;

/// <summary>An opaque session supplied by a repository adapter, NOT proof of real authentication.
/// Reference identity fences A -> B -> A callbacks, including relogin to the same account.</summary>
public sealed class PopupStoreRewardAccountSession
{
    public string AccountId { get; }
    public PopupStoreRewardAccountSession(string accountId) { AccountId = accountId; }
}

/// <summary>Immutable expected decision. The repository must compare ALL fields with its own
/// previously authorized business record; this request cannot authorize an amount or create a business.</summary>
public sealed class PopupStoreRewardRequest
{
    public const string Purpose = "PopupStoreReward";
    public string AccountId { get; }
    public string BusinessSessionId { get; }
    public string DecisionId { get; }
    public string GrantKey { get; }
    public string PolicyVersion { get; }
    public PopupStoreGradeTier Grade { get; }
    public PopupStoreRewardKind Kind { get; }
    public int Diamonds { get; }

    public PopupStoreRewardRequest(string accountId, string businessSessionId, string decisionId,
        PopupStoreGradeTier grade, PopupStoreRewardKind kind, int diamonds, string policyVersion,
        string grantKey = null)
    {
        AccountId = accountId; BusinessSessionId = businessSessionId; DecisionId = decisionId;
        Grade = grade; Kind = kind; Diamonds = diamonds; PolicyVersion = policyVersion;
        GrantKey = grantKey ?? CreateGrantKey(accountId, businessSessionId);
    }

    // Lengths are ordinal .NET UTF-16 code-unit lengths; no normalization, culture or hash collisions.
    // Policy/table versions and DecisionId deliberately do not create a second entitlement.
    public static string CreateGrantKey(string accountId, string businessSessionId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(businessSessionId)) return null;
        return Purpose + ":" + accountId.Length.ToString(CultureInfo.InvariantCulture) + ":" + accountId
            + ":" + businessSessionId.Length.ToString(CultureInfo.InvariantCulture) + ":" + businessSessionId;
    }

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(BusinessSessionId)
            || string.IsNullOrWhiteSpace(DecisionId) || string.IsNullOrWhiteSpace(PolicyVersion))
            error = "Account, authorized business, decision and policy identifiers are required.";
        else if (!string.Equals(GrantKey, CreateGrantKey(AccountId, BusinessSessionId), StringComparison.Ordinal))
            error = "The grant key must be canonical for this account, business and reward purpose.";
        else if (Grade < PopupStoreGradeTier.None || Grade > PopupStoreGradeTier.SSS
            || Kind < PopupStoreRewardKind.Basic || Kind > PopupStoreRewardKind.Jackpot || Diamonds < 0
            || (Grade == PopupStoreGradeTier.None && Diamonds != 0))
            error = "The expected reward values are invalid.";
        return error == null;
    }

    public bool Matches(PopupStoreRewardRequest other) => other != null
        && string.Equals(AccountId, other.AccountId, StringComparison.Ordinal)
        && string.Equals(BusinessSessionId, other.BusinessSessionId, StringComparison.Ordinal)
        && string.Equals(DecisionId, other.DecisionId, StringComparison.Ordinal)
        && string.Equals(GrantKey, other.GrantKey, StringComparison.Ordinal)
        && string.Equals(PolicyVersion, other.PolicyVersion, StringComparison.Ordinal)
        && Grade == other.Grade && Kind == other.Kind && Diamonds == other.Diamonds;
}

/// <summary>Repository-confirmed immutable outcome. BalanceAfter is the balance at this commit,
/// not necessarily the account's latest balance. Never assign it to UserInfo on replay.</summary>
public sealed class PopupStoreRewardGrant
{
    public PopupStoreRewardRequest Decision { get; }
    public string AccountId => Decision.AccountId;
    public string BusinessSessionId => Decision.BusinessSessionId;
    public string DecisionId => Decision.DecisionId;
    public string GrantKey => Decision.GrantKey;
    public string GrantId { get; }
    public int Diamonds => Decision.Diamonds;
    public int BalanceAfter { get; }
    public long BalanceVersion { get; }

    public PopupStoreRewardGrant(PopupStoreRewardRequest decision, string grantId, int balanceAfter, long balanceVersion)
    { Decision = decision; GrantId = grantId; BalanceAfter = balanceAfter; BalanceVersion = balanceVersion; }

    public bool Matches(PopupStoreRewardRequest request) => request != null && request.TryValidate(out _)
        && request.Grade != PopupStoreGradeTier.None && Decision != null && Decision.Matches(request)
        && !string.IsNullOrWhiteSpace(GrantId) && BalanceVersion > 0 && BalanceAfter >= request.Diamonds;
}

public enum PopupStoreRewardResponseKind
{
    Granted, RejectedBeforeSend, Indeterminate, Conflict, NoReward,
    NotFound, NotAppliedConfirmed
}

/// <summary>Echoed immutable request correlates replies. NotFound is NEVER no-write proof.
/// NotAppliedConfirmed requires an authoritative fence: all previous submissions for this key
/// have settled without a grant and cannot commit later. Only a query may return that proof.</summary>
public sealed class PopupStoreRewardResponse
{
    public PopupStoreRewardRequest Request { get; }
    public PopupStoreRewardResponseKind Kind { get; }
    public PopupStoreRewardGrant Grant { get; }
    public string Error { get; }
    public PopupStoreRewardResponse(PopupStoreRewardRequest request, PopupStoreRewardResponseKind kind,
        PopupStoreRewardGrant grant = null, string error = null)
    { Request = request; Kind = kind; Grant = grant; Error = error; }
}

/// <summary>No production implementation is supplied. A conforming authority authenticates the
/// session, owns the business/decision, and commits a unique canonical grant AND balance/version
/// atomically. Same key+same decision replays the original grant; conflicts never overwrite it.
/// No Unity, Backend, account storage or local file persistence is performed by this contract.
/// Callbacks for each transaction must be serialized by its adapter (e.g. the Editor/main thread).
/// Distinct clients may invoke the shared authority concurrently.</summary>
public interface IPopupStoreRewardPersistence
{
    void Submit(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
        Action<PopupStoreRewardResponse> completed);
    void Query(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
        Action<PopupStoreRewardResponse> completed);
}
