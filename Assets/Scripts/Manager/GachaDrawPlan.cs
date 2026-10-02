using System;
using System.Collections.Generic;
using System.Linq;

public enum GachaDrawRole { Base, Bonus }

/// <summary>The purchased product defines draw roles; result-array length never defines pity credit.</summary>
public sealed class GachaDrawPlan
{
    public IReadOnlyList<GachaDrawRole> Roles { get; }
    public int BaseCount { get; }
    public int BonusCount { get; }
    public int ResultCount => Roles.Count;
    public int DiamondCost { get; }
    public int TicketCost { get; }
    private GachaDrawPlan(int basic, int bonus, int cost, int tickets = 0)
    {
        BaseCount = basic; BonusCount = bonus; DiamondCost = cost; TicketCost = tickets;
        Roles = Array.AsReadOnly(Enumerable.Repeat(GachaDrawRole.Base, basic)
            .Concat(Enumerable.Repeat(GachaDrawRole.Bonus, bonus)).ToArray());
    }
    public static GachaDrawPlan ForPayment(GachaPaymentKind payment)
    {
        switch (payment)
        {
            case GachaPaymentKind.DiamondsSingle: return new GachaDrawPlan(1, 0, 10);
            case GachaPaymentKind.DiamondsEleven: return new GachaDrawPlan(10, 1, 100);
            case GachaPaymentKind.TicketSingle: return new GachaDrawPlan(1, 0, 0, 1);
            case GachaPaymentKind.TicketEleven: return new GachaDrawPlan(10, 1, 0, 10);
            default: throw new ArgumentOutOfRangeException(nameof(payment));
        }
    }
}
