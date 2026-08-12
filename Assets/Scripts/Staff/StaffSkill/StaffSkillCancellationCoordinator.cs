using System;

public static class StaffSkillCancellationCoordinator
{
    public static bool TryCancel(
        StaffSkillRuntimeContext runtimeContext,
        StaffSkillEffectRegistry effectRegistry,
        StaffSkillSourceToken sourceToken,
        Action deactivateAction,
        out Exception deactivateException)
    {
        if (runtimeContext == null)
        {
            throw new ArgumentNullException(nameof(runtimeContext));
        }

        if (effectRegistry == null)
        {
            throw new ArgumentNullException(nameof(effectRegistry));
        }

        if (!sourceToken.IsValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceToken),
                sourceToken,
                "Source token must be valid.");
        }

        deactivateException = null;
        if (!runtimeContext.IsCurrentToken(sourceToken)
            || !runtimeContext.TryBeginCancellation(sourceToken))
        {
            effectRegistry.RemoveAllForSource(sourceToken);
            return false;
        }

        try
        {
            deactivateAction?.Invoke();
        }
        catch (Exception exception)
        {
            deactivateException = exception;
        }
        finally
        {
            effectRegistry.RemoveAllForSource(sourceToken);
            runtimeContext.MarkDeactivationCompleted(sourceToken);
            runtimeContext.CompleteCancellation(sourceToken);
        }

        return true;
    }
}
