using System;

/// <summary>An immutable client checkpoint for simulation, NOT a grant or proof of persistence.
/// No account file/PlayerPrefs/SDK is written. A future durable adapter must store the identity
/// before dispatch and scope it to the authenticated account.</summary>
public sealed class PopupStoreRewardRecoveryRecord
{
    public PopupStoreRewardRequest Request { get; }
    public PopupStoreRewardTransactionState ObservedState { get; }
    internal PopupStoreRewardRecoveryRecord(PopupStoreRewardRequest request, PopupStoreRewardTransactionState state)
    { Request = request; ObservedState = state; }
}

public static class PopupStoreRewardRecovery
{
    public static PopupStoreRewardRecoveryRecord Capture(PopupStoreRewardTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));
        return new PopupStoreRewardRecoveryRecord(transaction.Request, transaction.State);
    }

    /// <summary>Even a checkpoint marked Granted or Prepared must reconcile with the authority.
    /// ObservedState is diagnostic only; it cannot authorize payment or restore a fake success.</summary>
    public static PopupStoreRewardTransaction Restore(PopupStoreRewardRecoveryRecord record,
        IPopupStoreRewardPersistence repository, Func<PopupStoreRewardAccountSession> readSession,
        Action<PopupStoreRewardGrant> completeLocally = null)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return new PopupStoreRewardTransaction(record.Request, repository, readSession, completeLocally, true);
    }
}
