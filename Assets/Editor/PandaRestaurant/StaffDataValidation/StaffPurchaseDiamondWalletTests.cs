#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;

public sealed class StaffPurchaseDiamondWalletTests
{
    [Test]
    public void DiamondReservation_PreservesIncomingRewardCommitsCurrentBalanceAndNotifiesOnce()
    {
        int diamonds = 110, notifications = 0;
        long generation = 1;
        bool commonInstalled = false, commonObservedAtCostNotification = false;
        var random = UnityEngine.Random.state;
        var wallet = new StaffPurchaseDiamondWallet(() => diamonds, value => diamonds = value,
            () => { notifications++; if (diamonds == 15) commonObservedAtCostNotification = commonInstalled; }, () => generation);
        object owner = new object();
        Assert.That(wallet.TryReserve(owner, 100, 110, out var error), Is.True, error);
        Assert.That(diamonds, Is.EqualTo(110));
        Assert.That(notifications, Is.Zero);
        Assert.That(wallet.TryReserve(new object(), 10, 110, out _), Is.False);
        Assert.That(wallet.TrySpend(10, out _), Is.False);
        Assert.That(wallet.TryAddReward(5, out error), Is.True, error);
        Assert.That(diamonds, Is.EqualTo(115));
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(wallet.ValidateReservation(owner, out error), Is.True, error);
        Assert.That(wallet.TryCommitReservedCost(owner, out error), Is.True, error);
        Assert.That(diamonds, Is.EqualTo(15), "Do not assign the plan's old DiamondsAfter = 10");
        Assert.That(notifications, Is.EqualTo(1), "Core commit must not publish the half-applied purchase");
        Assert.That(wallet.TryCommitReservedCost(owner, out _), Is.False);
        Assert.That(wallet.TrySpend(1, out _), Is.False, "Core state commit has not released the fence");
        commonInstalled = true;
        wallet.NotifyCommittedCost();
        wallet.NotifyCommittedCost();
        Assert.That(notifications, Is.EqualTo(2));
        Assert.That(commonObservedAtCostNotification, Is.True);
        wallet.ReleaseBeforeSend(new object());
        Assert.That(wallet.HasReservation, Is.True);
        wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.TrySpend(5, out error), Is.True, error);
        Assert.That(diamonds, Is.EqualTo(10));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(random)));
    }

    [Test]
    public void DiamondReservation_RejectsInvalidInputsOverflowAndUntrackedSourceChangesWithoutPartialCommit()
    {
        int diamonds = 110, writes = 0;
        long generation = 7;
        bool changeGenerationOnRead = false;
        var wallet = new StaffPurchaseDiamondWallet(
            () => { if (changeGenerationOnRead) { generation++; changeGenerationOnRead = false; } return diamonds; },
            value => { writes++; diamonds = value; }, () => { }, () => generation);
        object owner = new object();
        Assert.That(wallet.TryReserve(null, 10, 110, out _), Is.False);
        Assert.That(wallet.TryReserve(owner, 0, 110, out _), Is.False);
        Assert.That(wallet.TryReserve(owner, 111, 110, out _), Is.False);
        Assert.That(wallet.TryReserve(owner, 100, 109, out _), Is.False);
        Assert.That(wallet.TrySpend(-1, out _), Is.False);
        Assert.That(wallet.TryAddReward(-1, out _), Is.False);
        Assert.That(wallet.TryAddReward(int.MaxValue, out _), Is.False);
        Assert.That(writes, Is.Zero);
        Assert.That(wallet.TryReserve(owner, 100, 110, out _), Is.True);
        Assert.That(wallet.ValidateReservation(owner, 100, 110, out _), Is.True);
        Assert.That(wallet.ValidateReservation(owner, 10, 110, out _), Is.False);
        Assert.That(wallet.ValidateReservation(owner, 100, 109, out _), Is.False);
        wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.TryReserve(owner, 10, 110, out _), Is.True);
        Assert.That(wallet.ValidateReservation(owner, out _), Is.True, "Owner alone does not bind the purchase price");
        Assert.That(wallet.ValidateReservation(owner, 100, 110, out _), Is.False,
            "A replacement reservation cannot authorize the original fixed plan with a different cost");
        wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.TryReserve(owner, 100, 110, out _), Is.True);
        Assert.That(wallet.TryAddReward(int.MaxValue, out _), Is.False);
        Assert.That(wallet.ValidateReservation(owner, out _), Is.True, "Rejected overflow does not change evidence");
        diamonds = 111; // An unsupported assignment, not an accepted reward.
        Assert.That(wallet.TryCommitReservedCost(owner, out _), Is.False);
        Assert.That(diamonds, Is.EqualTo(111));
        diamonds = 110;
        changeGenerationOnRead = true;
        Assert.That(wallet.TryCommitReservedCost(owner, out _), Is.False,
            "A generation change during balance access must be checked after the accessor returns");
        Assert.That(writes, Is.Zero);
        Assert.That(wallet.HasReservation, Is.True, "Invalidated requests are not silently cancelled");
        wallet.ReleaseBeforeSend(owner);
        diamonds = int.MaxValue;
        Assert.That(wallet.TryReserve(owner, 10, int.MaxValue, out _), Is.True);
        Assert.That(wallet.TryCommitReservedCost(owner, out _), Is.True);
        Assert.That(diamonds, Is.EqualTo(int.MaxValue - 10));
    }

    [Test]
    public void DiamondNotifications_CannotTurnCommittedSpendOrRewardIntoFailureAndReentryCannotDoubleSpend()
    {
        int diamonds = 110, notifications = 0, productGrants = 0;
        bool reentrantSpendAccepted = false, reentrantReservationAccepted = false;
        bool grantNestedReward = false;
        bool observedAffordability = false, nestedRewardAccepted = false;
        StaffPurchaseDiamondWallet wallet = null;
        object owner = new object();
        wallet = new StaffPurchaseDiamondWallet(() => diamonds, value => diamonds = value, () =>
        {
            notifications++;
            reentrantSpendAccepted |= wallet.TrySpend(1, out _);
            reentrantReservationAccepted |= wallet.TryReserve(new object(), 10, diamonds, out _);
            if (!wallet.HasReservation) observedAffordability |= wallet.CanSpend(1);
            if (grantNestedReward)
            {
                grantNestedReward = false;
                nestedRewardAccepted = wallet.TryAddReward(5, out _);
            }
            if (wallet.HasReservation)
            {
                wallet.NotifyCommittedCost(); // Already marked before observer reentry.
            }
            throw new InvalidOperationException("isolated test observer failure");
        });
        Assert.That(wallet.TryReserve(owner, 100, 110, out _), Is.True);
        if (wallet.TrySpend(10, out _)) productGrants++;
        Assert.That(productGrants, Is.Zero, "The actual consumer contract must gate product/animation on true");
        Assert.That(wallet.TryAddReward(5, out _), Is.True);
        Assert.That(diamonds, Is.EqualTo(115));
        Assert.That(wallet.LastNotificationError, Does.Contain("test observer failure"));
        Assert.That(wallet.TryCommitReservedCost(owner, out _), Is.True);
        wallet.NotifyCommittedCost();
        wallet.NotifyCommittedCost();
        Assert.That(diamonds, Is.EqualTo(15));
        Assert.That(notifications, Is.EqualTo(2));
        Assert.That(reentrantSpendAccepted, Is.False);
        Assert.That(reentrantReservationAccepted, Is.False);
        wallet.ReleaseBeforeSend(owner);
        if (wallet.TrySpend(5, out _)) productGrants++;
        Assert.That(productGrants, Is.EqualTo(1), "Observer failure must not masquerade as a rejected deduction");
        Assert.That(diamonds, Is.EqualTo(10));
        Assert.That(wallet.LastNotificationError, Does.Contain("test observer failure"));
        Assert.That(reentrantSpendAccepted, Is.False);
        Assert.That(reentrantReservationAccepted, Is.False);
        grantNestedReward = true;
        Assert.That(wallet.TrySpend(5, out _), Is.True);
        Assert.That(nestedRewardAccepted, Is.True, "A real positive reward must not be swallowed by the fence");
        Assert.That(diamonds, Is.EqualTo(10), "One spend and an independent nested +5 reward both survive");
        Assert.That(reentrantSpendAccepted, Is.False);
        Assert.That(reentrantReservationAccepted, Is.False);
        Assert.That(observedAffordability, Is.True, "Balance observers still see correct display affordability");
        Assert.That(wallet.CanSpend(10), Is.True, "Exception paths release only the notification fence");
    }
}
#endif
