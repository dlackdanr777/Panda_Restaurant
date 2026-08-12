using System;
using System.Globalization;

public readonly struct StaffSkillSourceToken : IEquatable<StaffSkillSourceToken>
{
    public long RuntimeContextId { get; }
    public long ActivationSequence { get; }
    public bool IsValid => RuntimeContextId > 0 && ActivationSequence > 0;

    public StaffSkillSourceToken(long runtimeContextId, long activationSequence)
    {
        if (runtimeContextId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeContextId),
                runtimeContextId,
                "Runtime context ID must be greater than zero.");
        }

        if (activationSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activationSequence),
                activationSequence,
                "Activation sequence must be greater than zero.");
        }

        RuntimeContextId = runtimeContextId;
        ActivationSequence = activationSequence;
    }

    public bool Equals(StaffSkillSourceToken other)
    {
        return RuntimeContextId == other.RuntimeContextId
               && ActivationSequence == other.ActivationSequence;
    }

    public override bool Equals(object obj)
    {
        return obj is StaffSkillSourceToken other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (RuntimeContextId.GetHashCode() * 397) ^ ActivationSequence.GetHashCode();
        }
    }

    public static bool operator ==(StaffSkillSourceToken left, StaffSkillSourceToken right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StaffSkillSourceToken left, StaffSkillSourceToken right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return RuntimeContextId.ToString(CultureInfo.InvariantCulture)
               + ":"
               + ActivationSequence.ToString(CultureInfo.InvariantCulture);
    }
}
