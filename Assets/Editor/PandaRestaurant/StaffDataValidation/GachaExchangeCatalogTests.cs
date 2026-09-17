#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>All synchronous, SDK-free tests. No scene, PlayerPrefs, file save, Editor Play or live clock access.</summary>
public sealed class GachaExchangeCatalogTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<GachaData> _catalog = new List<GachaData>();
    private GachaEconomySettings _settings;
    private FakeClock _clock;
    private UnityEngine.Random.State _gameplayRandom;
    private sealed class FakeClock : IGachaExchangeClock
    {
        public DateTime Utc = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        public bool Ready = true;
        public bool TryGetUtcNow(out DateTime utc) { utc = Utc; return Ready; }
    }
    private sealed class CountingRandom : System.Random
    {
        public int Calls;
        public CountingRandom(int seed) : base(seed) { }
        public override int Next(int maximum) { Calls++; return base.Next(maximum); }
        public override double NextDouble() { Calls++; return base.NextDouble(); }
    }
    [SetUp] public void SetUp()
    {
        _gameplayRandom = UnityEngine.Random.state;
        _settings = ScriptableObject.CreateInstance<GachaEconomySettings>(); _created.Add(_settings);
        _clock = new FakeClock();
        foreach (var staff in Resources.LoadAll<StaffData>("StaffData"))
        { var wrapper = GachaStaffData.Create(staff); _created.Add(wrapper); _catalog.Add(wrapper); }
        for (int i = 0; i < 15; i++)
        {
            var data = ScriptableObject.CreateInstance<GachaItemData>(); _created.Add(data); _catalog.Add(data);
            Field(typeof(BasicData), "_id").SetValue(data, "SHOP-ITEM-" + i.ToString("D2"));
            Field(typeof(BasicData), "_name").SetValue(data, "Offline item " + i);
            Field(typeof(GachaData), "_rank").SetValue(data, (Rank)(i % 5));
            Field(typeof(GachaItemData), "_upgradeType").SetValue(data, i == 14 ? UpgradeType.None : UpgradeType.UPGRADE01);
        }
    }
    [TearDown] public void TearDown()
    {
        foreach (Object value in _created) if (value != null) Object.DestroyImmediate(value);
        _created.Clear(); _catalog.Clear();
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_gameplayRandom), "Shop/test selection must not consume gameplay RNG.");
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private GachaEconomyMemoryStore Store(int itemCounter = 0, int staffCounter = 0, int tickets = 20)
        => new GachaEconomyMemoryStore(new GachaEconomySnapshot("offline:catalog", new StaffAccountSaveData(2,
            Array.Empty<StaffAccountStaffRecord>(), 100000, new GachaEconomySaveData(tickets, tickets, itemCounter, staffCounter)), 20000));
    private GachaEconomyService Service(GachaEconomyMemoryStore store, System.Random random = null,
        Func<GachaMachineKind, IReadOnlyList<GachaData>, bool, GachaData> draw = null, IReadOnlyList<GachaData> catalog = null)
        => new GachaEconomyService(store, catalog ?? _catalog, _settings,
            draw ?? ((machine, pool, guaranteed) => pool.FirstOrDefault(x => x.Rank != Rank.Special) ?? pool[0]), _clock, random ?? new System.Random(1717));
    private static void Initialize(GachaEconomyService service)
    { Assert.That(service.TryEnsureExchangeCatalog(null, out string error), Is.True, error); Assert.That(service.ExchangeCatalogReady, Is.True); }
    private static void Refresh(GachaEconomyService service, GachaExchangeCategory category, string id)
    { Assert.That(service.TryRefreshExchange(category, service.GetExchangeDisplay(category).Version, id, null, out string error), Is.True, error); }
    private static string[] Ids(GachaExchangeDisplaySaveData display) => display.Slots.Select(x => x.ProductId).ToArray();

    [TestCase(0, 10, -1)] [TestCase(89, 99, -1)] [TestCase(90, 0, 9)] [TestCase(98, 8, 1)] [TestCase(99, 9, 0)]
    public void BothMachines_TenBaseThenOneBonus_ExactBoundaryAndUnchangedActualResultStatistics(int start, int after, int guaranteeIndex)
    {
        foreach (var machine in new[] { GachaMachineKind.Staff, GachaMachineKind.Item })
        {
            var service = Service(Store(start, start));
            Assert.That(service.TryDraw(machine, GachaPaymentKind.DiamondsEleven, null, out string error), Is.True, error);
            var tx = service.LastTransaction;
            Assert.That(tx.Results.Count, Is.EqualTo(11)); Assert.That(tx.BaseDrawCount, Is.EqualTo(10)); Assert.That(tx.BonusDrawCount, Is.EqualTo(1));
            Assert.That(tx.Results.Take(10).All(x => !x.IsBonus), Is.True); Assert.That(tx.Results[10].IsBonus, Is.True);
            Assert.That(tx.Results[10].CounterBefore, Is.EqualTo(after)); Assert.That(tx.Results[10].CounterAfter, Is.EqualTo(after));
            Assert.That(tx.Results[10].IsGuaranteedSpecial, Is.False);
            Assert.That(tx.Results.Select((x, i) => x.IsGuaranteedSpecial ? i : -1).Where(x => x >= 0),
                Is.EqualTo(guaranteeIndex < 0 ? Array.Empty<int>() : new[] { guaranteeIndex }));
            Assert.That(tx.After.Account.GachaEconomy.Counter(machine), Is.EqualTo(after));
            Assert.That(tx.After.Account.GachaEconomy.Counter(machine == GachaMachineKind.Staff ? GachaMachineKind.Item : GachaMachineKind.Staff), Is.EqualTo(start));
            Assert.That(tx.After.TotalDrawCount, Is.EqualTo(11)); Assert.That(tx.After.Diamonds, Is.EqualTo(19900));
            var receipt = JObject.Parse(tx.After.Account.GachaEconomy.LastTransactionJson);
            Assert.That((int)receipt["BaseDrawCount"], Is.EqualTo(10)); Assert.That((string)receipt["Results"][10]["DrawRole"], Is.EqualTo("Bonus"));
        }
    }
    [Test] public void BonusNaturalSpecialAtNinetyNine_GrantsNormallyWithoutGuaranteeOrCounterReset()
    {
        int index = 0;
        var service = Service(Store(staffCounter: 89), draw: (machine, pool, guaranteed) =>
            index++ == 10 ? pool.First(x => x.Rank == Rank.Special) : pool.First(x => x.Rank == Rank.Rare));
        Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsEleven, null, out _), Is.True);
        var bonus = service.LastTransaction.Results.Last();
        Assert.That(bonus.Rank, Is.EqualTo(Rank.Special)); Assert.That(bonus.IsNew, Is.True); Assert.That(bonus.IsBonus, Is.True);
        Assert.That(bonus.IsGuaranteedSpecial, Is.False); Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter, Is.EqualTo(99));
        Assert.That(service.Snapshot.Account.Staff.Any(x => x.Id == bonus.Id), Is.True);
    }
    [Test] public void TenBundles_OneHundredCredited_OneGuarantee_OneHundredTenResults()
    {
        var service = Service(Store()); int guarantees = 0;
        for (int i = 0; i < 10; i++)
        { Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsEleven, null, out _), Is.True); guarantees += service.LastTransaction.Results.Count(x => x.IsGuaranteedSpecial); }
        Assert.That(guarantees, Is.EqualTo(1)); Assert.That(service.Snapshot.TotalDrawCount, Is.EqualTo(110));
        Assert.That(service.Snapshot.Account.GachaEconomy.ItemCounter, Is.Zero);
        Assert.That(service.Snapshot.ItemCounts.Values.Sum(), Is.EqualTo(110));
    }
    [TestCase(GachaPaymentKind.DiamondsSingle)] [TestCase(GachaPaymentKind.TicketSingle)]
    public void SingleBaseDrawAtNinetyNine_GuaranteesAndIncludesTickets(GachaPaymentKind payment)
    {
        var service = Service(Store(staffCounter: 99));
        Assert.That(service.TryDraw(GachaMachineKind.Staff, payment, null, out _), Is.True);
        Assert.That(service.LastTransaction.Results.Single().IsGuaranteedSpecial, Is.True);
        Assert.That(service.LastTransaction.BaseDrawCount, Is.EqualTo(1)); Assert.That(service.LastTransaction.BonusDrawCount, Is.Zero);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter, Is.Zero);
    }
    [Test] public void BonusFlagsAndCountsSurviveFixedRetryAndReceiptRestore()
    {
        var store = Store(staffCounter: 98); store.FailNextSave = true; int calls = 0;
        var service = Service(store, draw: (m, p, g) => { calls++; return p[0]; });
        Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsEleven, null, out _), Is.False);
        var transaction = service.LastTransaction;
        Assert.That(service.RetryRejected(null, out string error), Is.True, error);
        Assert.That(service.LastTransaction, Is.SameAs(transaction)); Assert.That(calls, Is.EqualTo(11));
        var restored = GachaEconomyMemoryStore.Restore(store.SavedJson);
        Assert.That(restored.Account.GachaEconomy.StaffCounter, Is.EqualTo(8));
        var receipt = JObject.Parse(restored.Account.GachaEconomy.LastTransactionJson);
        Assert.That((string)receipt["Results"][10]["DrawRole"], Is.EqualTo("Bonus"));
        Assert.That((int)receipt["Results"][10]["CounterAfter"], Is.EqualTo(8));
    }
    [Test] public void LegacySevenFieldEconomy_PreservesCountersAndAddsUninitializedOffers()
    {
        Assert.That(StaffAccountSaveConverter.TrySerialize(Store(98, 89).Capture().Account, out string json, out _), Is.True);
        var root = JObject.Parse(json); ((JObject)root["GachaEconomy"]).Remove("Exchange");
        var read = StaffAccountSaveConverter.Read(root.ToString());
        Assert.That(read.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success));
        Assert.That(read.Data.GachaEconomy.ItemCounter, Is.EqualTo(98)); Assert.That(read.Data.GachaEconomy.StaffCounter, Is.EqualTo(89));
        Assert.That(read.Data.GachaEconomy.Exchange.Staff.IsInitialized, Is.False);
    }
    [Test] public void InitialOffers_AreFreeSixDistinctSortedPersisted_AndNeverGrantOrAdvancePity()
    {
        var store = Store(98, 89); var random = new CountingRandom(17); var service = Service(store, random);
        Initialize(service); int calls = random.Calls;
        Assert.That(service.TryEnsureExchangeCatalog(null, out _), Is.True); Assert.That(random.Calls, Is.EqualTo(calls));
        Assert.That(store.CommitCount, Is.EqualTo(1)); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(3));
        foreach (var category in new[] { GachaExchangeCategory.Staff, GachaExchangeCategory.Items })
        {
            var offers = service.GetDisplayedProducts(category);
            Assert.That(offers.Count, Is.EqualTo(6)); Assert.That(offers.Select(x => x.Id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(offers.Select(x => x.Id), Is.EqualTo(offers.OrderByDescending(x => x.Data.Rank).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id)));
            Assert.That(service.GetExchangeDisplay(category).Version, Is.EqualTo(1));
        }
        Assert.That(service.GetDisplayedProducts(GachaExchangeCategory.Staff).All(x => GachaEconomyService.CanPurchaseStaff(x.Data.Rank)), Is.True);
        Assert.That(service.Snapshot.Account.GachaEconomy.ItemCounter, Is.EqualTo(98)); Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter, Is.EqualTo(89));
        Assert.That(service.Snapshot.Account.Staff, Is.Empty); Assert.That(service.Snapshot.Account.GachaEconomy.Acquired, Is.Empty);
        Assert.That(service.Snapshot.ItemCounts, Is.Empty); Assert.That(service.Snapshot.TotalDrawCount, Is.Zero);
        Assert.That(service.Snapshot.Account.PandaTokens, Is.EqualTo(100000));
    }
    [Test] public void CandidateShortage_DoesNotDuplicateOrAdmitForbiddenStaff()
    {
        var source = _catalog.OfType<GachaStaffData>().Where(x => x.Rank == Rank.Rare).Take(2).Cast<GachaData>()
            .Concat(_catalog.OfType<GachaStaffData>().Where(x => x.Rank == Rank.Special).Take(1))
            .Concat(_catalog.OfType<GachaItemData>().Take(1)).ToArray();
        var service = Service(Store(), catalog: source); Initialize(service);
        Assert.That(service.GetDisplayedProducts(GachaExchangeCategory.Staff).Count, Is.EqualTo(2));
        Assert.That(service.GetDisplayedProducts(GachaExchangeCategory.Items).Count, Is.EqualTo(1));
        Assert.That(service.GetDisplayedProducts(GachaExchangeCategory.Tickets).Count, Is.EqualTo(2));
    }
    [Test] public void SharedThreeRefreshes_StaffTwiceItemOnce_BothExhausted_TicketNeverConsumes()
    {
        var store = Store(); var service = Service(store); Initialize(service);
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Tickets, 0, "ticket", null, out _), Is.False);
        foreach (var category in new[] { GachaExchangeCategory.Staff, GachaExchangeCategory.Staff, GachaExchangeCategory.Items })
            Refresh(service, category, Guid.NewGuid().ToString("N"));
        Assert.That(service.GetRefreshStatus().Remaining, Is.Zero); Assert.That(store.CommitCount, Is.EqualTo(4));
        foreach (var category in new[] { GachaExchangeCategory.Staff, GachaExchangeCategory.Items })
            Assert.That(service.TryRefreshExchange(category, service.GetExchangeDisplay(category).Version, "fourth", null, out _), Is.False);
        Assert.That(store.CommitCount, Is.EqualTo(4)); Assert.That(service.Snapshot.Account.PandaTokens, Is.EqualTo(100000));
    }
    [Test] public void ReconnectAndReopen_PreserveOffersVersionsQuotaAndSavedRequestIdempotence()
    {
        var store = Store(); var service = Service(store); Initialize(service);
        Refresh(service, GachaExchangeCategory.Staff, "refresh-once");
        var savedOffers = Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff));
        var restoredStore = new GachaEconomyMemoryStore(GachaEconomyMemoryStore.Restore(store.SavedJson));
        var restored = Service(restoredStore, new CountingRandom(99)); Initialize(restored);
        Assert.That(restored.GetRefreshStatus().Remaining, Is.EqualTo(2));
        Assert.That(Ids(restored.GetExchangeDisplay(GachaExchangeCategory.Staff)), Is.EqualTo(savedOffers));
        Assert.That(restored.TryRefreshExchange(GachaExchangeCategory.Staff, 1, "refresh-once", null, out _), Is.True);
        Assert.That(restoredStore.SaveAttempts, Is.Zero); Assert.That(restored.GetRefreshStatus().Remaining, Is.EqualTo(2));
        Assert.That(restored.GetExchangeDisplay(GachaExchangeCategory.Staff).Version, Is.EqualTo(2));
    }
    [Test] public void KstMidnightRestoresOnlyQuota_ClockRollbackDoesNotCreateFreeRefreshes()
    {
        _clock.Utc = new DateTime(2026, 9, 17, 14, 59, 59, DateTimeKind.Utc);
        var service = Service(Store()); Initialize(service);
        for (int i = 0; i < 3; i++) Refresh(service, GachaExchangeCategory.Staff, "day1-" + i);
        var ids = Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff));
        _clock.Utc = _clock.Utc.AddSeconds(1);
        Assert.That(service.GetRefreshStatus().DayKey, Is.EqualTo("20260918")); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(3));
        Assert.That(Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff)), Is.EqualTo(ids), "Midnight must not reroll offers.");
        Refresh(service, GachaExchangeCategory.Items, "day2"); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(2));
        _clock.Utc = _clock.Utc.AddSeconds(-1);
        Assert.That(service.GetRefreshStatus().ClockReady, Is.False); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(2));
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Items, service.GetExchangeDisplay(GachaExchangeCategory.Items).Version, "rollback", null, out _), Is.False);
    }
    [Test] public void NoTrustedClock_AllowsFreeInitialOffersButRefusesRefreshWithoutAnySave()
    {
        _clock.Ready = false; var store = Store(); var service = Service(store); Initialize(service);
        Assert.That(service.GetRefreshStatus().ClockReady, Is.False);
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Staff, 1, "untrusted", null, out _), Is.False);
        Assert.That(store.CommitCount, Is.EqualTo(1)); Assert.That(service.Snapshot.Account.GachaEconomy.Exchange.RefreshUsed, Is.Zero);
    }
    [Test] public void RejectedSave_RetryReusesExactOfferIdsAndSharedQuota_WithoutReroll()
    {
        var store = Store(); var random = new CountingRandom(8); var service = Service(store, random); Initialize(service);
        var original = store.SavedJson; store.FailNextSave = true;
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Staff, 1, "fixed-request", null, out _), Is.False);
        var rejected = service.LastTransaction; var ids = Ids(rejected.After.Account.GachaEconomy.Exchange.Staff); int calls = random.Calls;
        Assert.That(store.SavedJson, Is.EqualTo(original)); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(3));
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Staff, 1, "reopened-button-id", null, out _), Is.True);
        Assert.That(service.LastTransaction, Is.SameAs(rejected)); Assert.That(random.Calls, Is.EqualTo(calls));
        Assert.That(Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff)), Is.EqualTo(ids));
        Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(2));
    }
    [Test] public void RejectedInitialSave_ReopeningReusesPlannedBothCatalogs()
    {
        var store = Store(); store.FailNextSave = true; var random = new CountingRandom(4); var service = Service(store, random);
        Assert.That(service.TryEnsureExchangeCatalog(null, out _), Is.False); var transaction = service.LastTransaction; int calls = random.Calls;
        Assert.That(service.ExchangeCatalogReady, Is.False); Initialize(service);
        Assert.That(service.LastTransaction, Is.SameAs(transaction)); Assert.That(random.Calls, Is.EqualTo(calls));
        Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(3));
    }
    [Test] public void PendingRefreshBlocksPurchase_UnknownAndDuplicateCallbacksDoNotConsumeTwice()
    {
        var store = Store(); var service = Service(store); Initialize(service);
        var old = service.GetDisplayedProducts(GachaExchangeCategory.Items).First(); int version = service.GetExchangeDisplay(GachaExchangeCategory.Items).Version;
        int commits = 0; service.Committed += _ => commits++; store.DeferNextSave = true;
        Assert.That(service.TryRefreshExchange(GachaExchangeCategory.Items, version, "pending", null, out _), Is.True);
        Assert.That(service.TryExchange(old.Id, 1, version, null, out _), Is.False);
        Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(3));
        store.CompletePending(GachaStoreResult.Indeterminate); Assert.That(service.IsBusy, Is.True);
        store.CompletePending(GachaStoreResult.Confirmed); store.CompletePending(GachaStoreResult.Confirmed);
        Assert.That(commits, Is.EqualTo(1)); Assert.That(service.GetRefreshStatus().Remaining, Is.EqualTo(2));
        Assert.That(service.TryExchange(old.Id, 1, version, null, out _), Is.False, "Stale displayed version cannot buy a reappearing product.");
    }
    [Test] public void PurchasesUseSavedPriceQuantityAndState_StaffNotRefilled_ItemRepeatPreserved()
    {
        var store = Store(); var service = Service(store); Initialize(service);
        var staff = service.GetDisplayedProducts(GachaExchangeCategory.Staff).First(); var before = Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff));
        Assert.That(service.TryExchange(staff.Id, 1, 1, null, out _), Is.True);
        Assert.That(service.TryExchange(staff.Id, 1, 1, null, out _), Is.False);
        Assert.That(Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff)), Is.EqualTo(before));
        Assert.That(service.GetExchangeDisplay(GachaExchangeCategory.Staff).Slots.First(x => x.ProductId == staff.Id).PurchaseCount, Is.EqualTo(1));
        var item = service.GetDisplayedProducts(GachaExchangeCategory.Items).First(x => x.IsRepeatable); long originalPrice = item.Price;
        _settings.NormalItemPrice = _settings.RareItemPrice = _settings.UniqueItemPrice = _settings.SpecialItemPrice = 9999;
        service = Service(store); long tokens = service.Snapshot.Account.PandaTokens;
        Assert.That(service.TryExchange(item.Id, 2, 1, null, out _), Is.True);
        Assert.That(service.Snapshot.Account.PandaTokens, Is.EqualTo(tokens - originalPrice * 2));
        Assert.That(service.Snapshot.ItemCounts[item.Data.Id], Is.EqualTo(2));
        Assert.That(service.GetExchangeDisplay(GachaExchangeCategory.Items).Slots.First(x => x.ProductId == item.Id).PurchaseCount, Is.EqualTo(2));
    }
    [Test] public void ConditionalDisplayWeightsReuseNativeTables_StaffRareNormalizesToQuarter()
    {
        var service = Service(Store()); var random = new System.Random(1917); int rare = 0;
        for (int i = 0; i < 5000; i++)
            if (GachaExchangeDisplaySelector.Select(service.Products, GachaExchangeCategory.Staff, random, 1)[0].Data.Rank == Rank.Rare) rare++;
        Assert.That(rare / 5000d, Is.EqualTo(.25).Within(.025));
        Assert.That(StaffGachaRandomSelector.GetGradeWeight(Rank.Normal2), Is.EqualTo(60));
        Assert.That(StaffGachaRandomSelector.GetGradeWeight(Rank.Rare), Is.EqualTo(20));
        Assert.That(StaffGachaRandomSelector.GetGradeWeight(Rank.Unique), Is.EqualTo(15));
        Assert.That(StaffGachaRandomSelector.GetGradeWeight(Rank.Special), Is.EqualTo(5));
        Assert.That(Enumerable.Range(0, 5).Select(i => Utility.GetGachaItemRankRange((Rank)i)),
            Is.EqualTo(new[] { .375f, .375f, .125f, .0938f, .0313f }));
    }
    [Test] public void ServerAnchoredClockUsesOnlyMonotonicElapsed_AndRejectsClockWithoutAnchor()
    {
        double monotonic = 10; var clock = new GachaExchangeAnchoredClock(() => monotonic);
        Assert.That(clock.TryGetUtcNow(out _), Is.False);
        DateTime anchor = _clock.Utc; clock.RecordServerUtc(anchor); monotonic = 70;
        Assert.That(clock.TryGetUtcNow(out var now), Is.True); Assert.That(now, Is.EqualTo(anchor.AddMinutes(1)));
        clock.RecordServerUtc(anchor.AddDays(-1)); Assert.That(clock.TryGetUtcNow(out now), Is.True);
        Assert.That(now, Is.EqualTo(anchor.AddMinutes(1)), "An older server response must not reverse the anchored clock.");
        monotonic = 5; Assert.That(clock.TryGetUtcNow(out _), Is.False);
    }
    [TestCase(0f, Rank.Normal1)] [TestCase(.5f, Rank.Normal2)] [TestCase(.8f, Rank.Rare)]
    [TestCase(.9f, Rank.Unique)] [TestCase(.99f, Rank.Special)]
    public void SharedItemDrawSelectorUsesOriginalRankAndUniformWithinRankPolicy(float roll, Rank expected)
    {
        var candidates = _catalog.OfType<GachaItemData>().Cast<GachaData>().ToArray();
        var selected = ItemManager.SelectGachaData(candidates, () => roll, maximum => maximum - 1);
        Assert.That(selected.Rank, Is.EqualTo(expected)); Assert.That(selected, Is.SameAs(candidates.Last(x => x.Rank == expected)));
    }
    [Test] public void SharedItemSelectorKeepsFiveAttemptsThenExistingAvailableRankFallback()
    {
        var candidates = _catalog.OfType<GachaItemData>().Where(x => x.Rank == Rank.Special).Cast<GachaData>().ToArray();
        int rolls = 0, indices = 0;
        var result = ItemManager.SelectGachaData(candidates, () => { rolls++; return 0; }, count => { indices++; return count - 1; });
        Assert.That(rolls, Is.EqualTo(5)); Assert.That(indices, Is.EqualTo(2)); Assert.That(result, Is.SameAs(candidates.Last()));
    }
    [Test] public void RemovedOrUndisplayedOfferAndWrongVersionCannotBePurchased()
    {
        var service = Service(Store()); Initialize(service);
        var displayed = service.GetDisplayedProducts(GachaExchangeCategory.Staff).First();
        Assert.That(service.TryExchange(displayed.Id, 1, 0, null, out _), Is.False);
        var absent = service.Products.First(x => x.Category == GachaExchangeCategory.Staff && !Ids(service.GetExchangeDisplay(GachaExchangeCategory.Staff)).Contains(x.Id));
        Assert.That(service.TryExchange(absent.Id, 1, 1, null, out _), Is.False);
    }
}
#endif
