using System;

/// <summary>Main-thread, non-notifying cost commit used by the protected staff purchase operation.</summary>
public interface IStaffPurchaseWallet
{
    int Diamonds { get; }
    bool TryReserve(object owner, int cost, int expectedBalance, out string error);
    bool ValidateReservation(object owner, out string error);
    bool ValidateReservation(object owner, int expectedCost, int expectedInitialBalance, out string error);
    bool TryCommitReservedCost(object owner, out string error);
    void ReleaseBeforeSend(object owner);
    void NotifyCommittedCost();
}

/// <summary>
/// Reserves a purchase cost without spending it. Rewards still enter the current balance;
/// competing consumption is refused until the owning operation explicitly releases protection.
/// Read/write callbacks are synchronous memory access only, with no events or persistence.
/// This is a process-local fence, not a durable transaction or a server balance reservation.
/// </summary>
public sealed class StaffPurchaseDiamondWallet : IStaffPurchaseWallet
{
    private readonly Func<int> _read;
    private readonly Action<int> _write;
    private readonly Action _notify;
    private readonly Func<long> _readGeneration;
    private object _owner;
    private int _initialBalance;
    private int _cost;
    private long _rewardDelta;
    private long _generation;
    private bool _committed;
    private bool _notified;
    private int _notificationDepth;

    public StaffPurchaseDiamondWallet(Func<int> read, Action<int> nonNotifyingWrite,
        Action notify, Func<long> sourceGeneration = null)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = nonNotifyingWrite ?? throw new ArgumentNullException(nameof(nonNotifyingWrite));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _readGeneration = sourceGeneration ?? (() => 0L);
    }

    public int Diamonds => _read();
    public bool HasReservation => _owner != null;
    public string LastNotificationError { get; private set; }

    public bool TryReserve(object owner, int cost, int expectedBalance, out string error)
    {
        error = null;
        long generation = _readGeneration();
        int current = Diamonds;
        if (owner == null || _owner != null || _notificationDepth != 0 || cost <= 0 || expectedBalance < 0
            || current != expectedBalance || current < cost || generation != _readGeneration())
        {
            error = "다이아 사용 준비 상태를 확인할 수 없습니다.";
            return false;
        }
        _owner = owner;
        _initialBalance = current;
        _cost = cost;
        _rewardDelta = 0;
        _generation = generation;
        _committed = false;
        _notified = false;
        return true;
    }

    public bool ValidateReservation(object owner, out string error)
    {
        return TryReadReservation(owner, out _, out error);
    }

    public bool ValidateReservation(object owner, int expectedCost, int expectedInitialBalance, out string error)
    {
        if (_cost != expectedCost || _initialBalance != expectedInitialBalance)
        {
            error = "현재 요청에 고정된 다이아 비용 또는 최초 잔액과 다릅니다.";
            return false;
        }
        return TryReadReservation(owner, out _, out error);
    }

    private bool TryReadReservation(object owner, out int current, out string error)
    {
        error = null;
        long generation = _generation;
        int initial = _initialBalance, cost = _cost;
        long rewards = _rewardDelta;
        bool committed = _committed;
        long beforeRead = _readGeneration();
        current = Diamonds;
        long afterRead = _readGeneration();
        long expected = (long)initial + rewards - (committed ? cost : 0);
        if (owner == null || !ReferenceEquals(_owner, owner) || generation != beforeRead
            || generation != afterRead || generation != _generation || initial != _initialBalance
            || cost != _cost || rewards != _rewardDelta || committed != _committed
            || expected < 0 || expected > int.MaxValue || current != expected
            || (!committed && expected < cost))
        {
            error = "다이아 사용 준비 이후 잔액 또는 계정 자료가 변경되었습니다.";
            return false;
        }
        return true;
    }

    public bool TryCommitReservedCost(object owner, out string error)
    {
        if (!TryReadReservation(owner, out int current, out error)) return false;
        if (_committed)
        {
            error = "이미 반영된 다이아 사용입니다.";
            return false;
        }
        // Never assign the old purchase plan's DiamondsAfter: rewards received while waiting survive.
        int updated = checked(current - _cost);
        _write(updated);
        _committed = true;
        return true;
    }

    // The coordinator calls this only for pre-send rejection or after the core commit completes.
    // An unanswered/sent operation must keep its reservation; display closing never calls this.
    public void ReleaseBeforeSend(object owner)
    {
        if (owner != null && ReferenceEquals(_owner, owner)) _owner = null;
    }

    public void NotifyCommittedCost()
    {
        if (!_committed || _notified) return;
        _notified = true;
        NotifyCommittedChange();
    }

    public bool CanSpend(int cost)
    {
        int current = Diamonds;
        // This is also a display query inside balance notifications. Only execution is fenced
        // during an observer callback, so a refreshed button does not stay falsely unaffordable.
        return cost >= 0 && current >= 0 && !HasReservation && current >= cost;
    }

    public bool TrySpend(int cost, out string error)
    {
        error = null;
        long generation = _readGeneration();
        int current = Diamonds;
        if (cost < 0 || current < 0 || HasReservation || _notificationDepth != 0
            || current < cost || generation != _readGeneration())
        {
            error = HasReservation ? "직원 뽑기 결과를 확인 중입니다. 잠시 후 다시 시도해 주세요."
                : _notificationDepth != 0 ? "다이아 사용을 처리 중입니다. 잠시 후 다시 시도해 주세요."
                : "다이아가 부족하거나 사용량이 올바르지 않습니다.";
            return false;
        }
        _write(checked(current - cost));
        NotifyCommittedChange();
        return true;
    }

    public bool TryAddReward(int value, out string error)
    {
        error = null;
        long generation = _readGeneration();
        int current = Diamonds;
        long updated = (long)current + value;
        if (value < 0 || current < 0 || updated > int.MaxValue || generation != _readGeneration())
        {
            error = "다이아 보상 값이 허용 범위를 벗어났습니다.";
            return false;
        }
        // Validate arithmetic before either the account balance or reservation evidence is changed.
        long nextRewardDelta = _owner == null ? _rewardDelta : checked(_rewardDelta + value);
        _write((int)updated);
        _rewardDelta = nextRewardDelta;
        NotifyCommittedChange();
        return true;
    }

    private void NotifyCommittedChange()
    {
        LastNotificationError = null;
        // A consumer has not granted its product yet when TrySpend publishes this change.
        // Do not allow a listener to start another consumption inside that half-finished call.
        // Positive rewards remain accepted; their nested notifications keep the same fence.
        _notificationDepth++;
        try { _notify(); }
        catch (Exception exception)
        {
            // The arithmetic already committed. A display/listener error must not turn that into
            // a rejected spend (free product) or a failed reward that callers might grant again.
            LastNotificationError = exception.Message;
        }
        finally { _notificationDepth--; }
    }
}
