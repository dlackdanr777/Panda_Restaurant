using System;
using System.Threading;

public sealed class StaffSkillRuntimeContext
{
    private static long _nextRuntimeContextId;

    public long RuntimeContextId { get; }
    public long ActivationSequence { get; private set; }
    public StaffSkillSourceToken CurrentActivationToken { get; private set; }
    public string ActiveSkillDebugId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsCancelling { get; private set; }
    public bool DeactivationCompleted { get; private set; }
    public float PersonalMoveBonusPercent { get; private set; }
    public float CustomerCallElapsedTime { get; private set; }
    public float AssignedCookingBonusPercent { get; private set; }

    public StaffSkillRuntimeContext()
    {
        long runtimeContextId = Interlocked.Increment(ref _nextRuntimeContextId);
        if (runtimeContextId <= 0)
        {
            throw new InvalidOperationException("Runtime context ID range has been exhausted.");
        }

        RuntimeContextId = runtimeContextId;
        ActiveSkillDebugId = string.Empty;
    }

    public StaffSkillSourceToken BeginActivation(string skillDebugId)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("A staff skill activation is already active.");
        }

        long nextSequence = checked(ActivationSequence + 1);
        StaffSkillSourceToken token = new StaffSkillSourceToken(RuntimeContextId, nextSequence);

        ActivationSequence = nextSequence;
        CurrentActivationToken = token;
        ActiveSkillDebugId = skillDebugId ?? string.Empty;
        IsActive = true;
        IsCancelling = false;
        DeactivationCompleted = false;
        ClearTemporaryValues();
        return token;
    }

    public bool IsCurrentToken(StaffSkillSourceToken token)
    {
        return IsActive && token.IsValid && CurrentActivationToken == token;
    }

    public bool TryBeginCancellation(StaffSkillSourceToken token)
    {
        if (!IsCurrentToken(token) || IsCancelling)
        {
            return false;
        }

        IsCancelling = true;
        return true;
    }

    public void MarkDeactivationCompleted(StaffSkillSourceToken token)
    {
        if (!IsCurrentToken(token) || !IsCancelling)
        {
            return;
        }

        DeactivationCompleted = true;
    }

    public void CompleteCancellation(StaffSkillSourceToken token)
    {
        if (!IsCurrentToken(token))
        {
            return;
        }

        ClearActiveState();
    }

    public void ResetLocalState()
    {
        ClearActiveState();
    }

    public void SetPersonalMoveBonusPercent(
        StaffSkillSourceToken token,
        float percent)
    {
        ValidatePercent(percent, nameof(percent));
        if (!IsCurrentToken(token))
        {
            return;
        }

        PersonalMoveBonusPercent = percent;
    }

    public void SetAssignedCookingBonusPercent(
        StaffSkillSourceToken token,
        float percent)
    {
        ValidatePercent(percent, nameof(percent));
        if (!IsCurrentToken(token))
        {
            return;
        }

        AssignedCookingBonusPercent = percent;
    }

    public int AdvanceCustomerCallTimer(
        StaffSkillSourceToken token,
        float deltaSeconds,
        float intervalSeconds)
    {
        ValidateFiniteNonNegative(deltaSeconds, nameof(deltaSeconds));
        ValidateFinitePositive(intervalSeconds, nameof(intervalSeconds));

        if (!IsCurrentToken(token))
        {
            return 0;
        }

        double totalSeconds = CustomerCallElapsedTime + (double)deltaSeconds;
        double completedIntervalsValue = Math.Floor(totalSeconds / intervalSeconds);
        if (completedIntervalsValue > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Completed interval count exceeds the supported range.");
        }

        int completedIntervals = (int)completedIntervalsValue;
        double remainder = totalSeconds - completedIntervals * (double)intervalSeconds;
        CustomerCallElapsedTime = (float)Math.Max(0d, remainder);
        return completedIntervals;
    }

    private void ClearActiveState()
    {
        CurrentActivationToken = default;
        ActiveSkillDebugId = string.Empty;
        IsActive = false;
        IsCancelling = false;
        DeactivationCompleted = false;
        ClearTemporaryValues();
    }

    private void ClearTemporaryValues()
    {
        PersonalMoveBonusPercent = 0f;
        CustomerCallElapsedTime = 0f;
        AssignedCookingBonusPercent = 0f;
    }

    private static void ValidatePercent(float value, string parameterName)
    {
        ValidateFiniteNonNegative(value, parameterName);
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

    private static void ValidateFinitePositive(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and greater than zero.");
        }
    }
}
