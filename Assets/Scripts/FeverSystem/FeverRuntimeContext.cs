using System;
using System.Globalization;
using System.Threading;

public readonly struct FeverRuntimeToken : IEquatable<FeverRuntimeToken>
{
    public long ContextId { get; }
    public long ActivationSequence { get; }
    public bool IsValid => ContextId > 0 && ActivationSequence > 0;

    public FeverRuntimeToken(long contextId, long activationSequence)
    {
        if (contextId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextId),
                contextId,
                "Context ID must be greater than zero.");
        }

        if (activationSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activationSequence),
                activationSequence,
                "Activation sequence must be greater than zero.");
        }

        ContextId = contextId;
        ActivationSequence = activationSequence;
    }

    public bool Equals(FeverRuntimeToken other)
    {
        return ContextId == other.ContextId
               && ActivationSequence == other.ActivationSequence;
    }

    public override bool Equals(object obj)
    {
        return obj is FeverRuntimeToken other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (ContextId.GetHashCode() * 397) ^ ActivationSequence.GetHashCode();
        }
    }

    public static bool operator ==(FeverRuntimeToken left, FeverRuntimeToken right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FeverRuntimeToken left, FeverRuntimeToken right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return ContextId.ToString(CultureInfo.InvariantCulture)
               + ":"
               + ActivationSequence.ToString(CultureInfo.InvariantCulture);
    }
}

public readonly struct FeverRuntimeAdvanceResult
{
    public bool IsCurrentActivation { get; }
    public bool DurationCompleted { get; }
    public int AutoCallOpportunityCount { get; }
    public float ConsumedDeltaSeconds { get; }
    public float RemainingRatio { get; }
    public float AutoCallRemainderSeconds { get; }

    public FeverRuntimeAdvanceResult(
        bool isCurrentActivation,
        bool durationCompleted,
        int autoCallOpportunityCount,
        float consumedDeltaSeconds,
        float remainingRatio,
        float autoCallRemainderSeconds)
    {
        IsCurrentActivation = isCurrentActivation;
        DurationCompleted = durationCompleted;
        AutoCallOpportunityCount = autoCallOpportunityCount;
        ConsumedDeltaSeconds = consumedDeltaSeconds;
        RemainingRatio = remainingRatio;
        AutoCallRemainderSeconds = autoCallRemainderSeconds;
    }
}

public sealed class FeverRuntimeContext
{
    // FEVER_POLICY_2026_08_19_V2
    private const float ActiveMultiplier = 2f;
    private const double AutoCallIntervalSeconds = 0.5d;

    private static long _nextContextId;

    private double _durationSeconds;
    private double _elapsedSeconds;
    private double _autoCallAccumulatorSeconds;

    public long ContextId { get; }
    public long ActivationSequence { get; private set; }
    public FeverRuntimeToken CurrentToken { get; private set; }
    public bool IsActive { get; private set; }
    public float DurationSeconds => (float)_durationSeconds;
    public float ElapsedSeconds => (float)_elapsedSeconds;
    public float RemainingRatio => IsActive ? CalculateRemainingRatio() : 0f;
    public float AutoCallRemainderSeconds => (float)_autoCallAccumulatorSeconds;

    public float FoodPriceMultiplier => GetActiveMultiplier();
    public float NormalCustomerMoveMultiplier => GetActiveMultiplier();
    public float StaffMoveMultiplier => GetActiveMultiplier();
    public float ManagerGuideMultiplier => GetActiveMultiplier();
    public float MarketerCallMultiplier => GetActiveMultiplier();
    public float CookingMultiplier => GetActiveMultiplier();

    public FeverRuntimeContext()
    {
        long contextId = Interlocked.Increment(ref _nextContextId);
        if (contextId <= 0)
        {
            throw new InvalidOperationException("Fever runtime context ID range has been exhausted.");
        }

        ContextId = contextId;
    }

    public bool TryActivate(float durationSeconds, out FeverRuntimeToken token)
    {
        token = default;
        if (!IsFinitePositive(durationSeconds) || IsActive)
        {
            return false;
        }

        long nextSequence = checked(ActivationSequence + 1);
        FeverRuntimeToken nextToken = new FeverRuntimeToken(ContextId, nextSequence);

        ActivationSequence = nextSequence;
        CurrentToken = nextToken;
        IsActive = true;
        _durationSeconds = durationSeconds;
        _elapsedSeconds = 0d;
        _autoCallAccumulatorSeconds = 0d;
        token = nextToken;
        return true;
    }

    public bool IsCurrentToken(FeverRuntimeToken token)
    {
        return IsActive && token.IsValid && CurrentToken == token;
    }

    public FeverRuntimeAdvanceResult Advance(
        FeverRuntimeToken token,
        float deltaSeconds)
    {
        ValidateFiniteNonNegative(deltaSeconds, nameof(deltaSeconds));

        if (!IsCurrentToken(token))
        {
            return default;
        }

        double remainingSeconds = Math.Max(0d, _durationSeconds - _elapsedSeconds);
        double consumedDeltaSeconds = Math.Min((double)deltaSeconds, remainingSeconds);
        double nextElapsedSeconds = _elapsedSeconds + consumedDeltaSeconds;
        double nextAccumulatorSeconds = _autoCallAccumulatorSeconds + consumedDeltaSeconds;
        double opportunityCountValue = Math.Floor(
            nextAccumulatorSeconds / AutoCallIntervalSeconds);
        if (opportunityCountValue > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Auto-call opportunity count exceeds the supported range.");
        }

        int opportunityCount = (int)opportunityCountValue;
        double nextRemainderSeconds =
            nextAccumulatorSeconds - opportunityCount * AutoCallIntervalSeconds;
        if (nextRemainderSeconds < 0.0000001d)
        {
            nextRemainderSeconds = 0d;
        }

        _elapsedSeconds = nextElapsedSeconds;
        _autoCallAccumulatorSeconds = nextRemainderSeconds;

        bool durationCompleted = _elapsedSeconds >= _durationSeconds;
        return new FeverRuntimeAdvanceResult(
            true,
            durationCompleted,
            opportunityCount,
            (float)consumedDeltaSeconds,
            CalculateRemainingRatio(),
            (float)_autoCallAccumulatorSeconds);
    }

    public bool Deactivate(FeverRuntimeToken token)
    {
        if (!IsCurrentToken(token))
        {
            return false;
        }

        ClearActiveState();
        return true;
    }

    public void Reset()
    {
        ClearActiveState();
    }

    private float GetActiveMultiplier()
    {
        return IsActive ? ActiveMultiplier : 1f;
    }

    private float CalculateRemainingRatio()
    {
        if (_durationSeconds <= 0d)
        {
            return 0f;
        }

        double ratio = (_durationSeconds - _elapsedSeconds) / _durationSeconds;
        return (float)Math.Max(0d, Math.Min(1d, ratio));
    }

    private void ClearActiveState()
    {
        CurrentToken = default;
        IsActive = false;
        _durationSeconds = 0d;
        _elapsedSeconds = 0d;
        _autoCallAccumulatorSeconds = 0d;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static void ValidateFiniteNonNegative(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and non-negative.");
        }
    }
}
