using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum GachaExchangeCategory { Tickets, Staff, Items }
public enum GachaTransactionStatus { Prepared, Sending, Completed, Rejected, Indeterminate }
public enum GachaStoreResult { Confirmed, RejectedBeforeCommit, Indeterminate }
public enum GachaEconomyOperation { Draw, Purchase, InitializeDisplay, RefreshDisplay }

public sealed class GachaExchangeProduct
{
    public string Id { get; }
    public GachaExchangeCategory Category { get; }
    public string Name { get; }
    public string Description { get; }
    public Sprite Sprite { get; }
    public long Price { get; }
    public int Quantity { get; }
    public GachaData Data { get; }
    public GachaMachineKind TicketMachine { get; }
    public bool IsRepeatable => Category != GachaExchangeCategory.Staff
        && (Data == null || GachaAcquisitionResult.KindOf(Data) != GachaAcquisitionKind.Recipe);
    internal GachaExchangeProduct(string id, GachaExchangeCategory category, string name, string description,
        Sprite sprite, long price, int quantity, GachaData data = null, GachaMachineKind ticketMachine = GachaMachineKind.Item)
    { Id = id; Category = category; Name = name; Description = description; Sprite = sprite; Price = price;
        Quantity = quantity; Data = data; TicketMachine = ticketMachine; }
}

public sealed class GachaEconomySnapshot
{
    public string AccountId { get; }
    public StaffAccountSaveData Account { get; }
    public int Diamonds { get; }
    public IReadOnlyDictionary<string, int> ItemCounts { get; }
    public IReadOnlyDictionary<string, int> ItemLevels { get; }
    public IReadOnlyDictionary<string, int> RecipeLevels { get; }
    public int TotalDrawCount { get; }
    public GachaEconomySnapshot(string accountId, StaffAccountSaveData account, int diamonds,
        IDictionary<string, int> itemCounts = null, IDictionary<string, int> itemLevels = null,
        IDictionary<string, int> recipeLevels = null, int totalDrawCount = 0)
    {
        AccountId = accountId; Account = account; Diamonds = diamonds; TotalDrawCount = totalDrawCount;
        ItemCounts = Freeze(itemCounts); ItemLevels = Freeze(itemLevels); RecipeLevels = Freeze(recipeLevels);
    }
    private static IReadOnlyDictionary<string, int> Freeze(IDictionary<string, int> value)
        => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(value ?? new Dictionary<string, int>(), StringComparer.Ordinal));
    public bool HasSameState(GachaEconomySnapshot other)
    {
        if (other == null || AccountId != other.AccountId || Diamonds != other.Diamonds || TotalDrawCount != other.TotalDrawCount) return false;
        if (!StaffAccountSaveConverter.TrySerialize(Account, out string a, out _) || !StaffAccountSaveConverter.TrySerialize(other.Account, out string b, out _) || a != b) return false;
        return Same(ItemCounts, other.ItemCounts) && Same(ItemLevels, other.ItemLevels) && Same(RecipeLevels, other.RecipeLevels);
    }
    internal static bool Same(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
        => a.Count == b.Count && a.All(x => b.TryGetValue(x.Key, out int value) && value == x.Value);
}

public sealed class GachaAcquisitionResult
{
    public GachaData Data { get; }
    public string Id { get; }
    public Rank Rank { get; }
    public GachaAcquisitionKind Kind { get; }
    public bool IsNew { get; }
    public bool IsGuaranteedSpecial { get; }
    public int PandaTokenReward { get; }
    public int CounterBefore { get; }
    public int CounterAfter { get; }
    public GachaDrawRole DrawRole { get; }
    public bool IsBonus => DrawRole == GachaDrawRole.Bonus;
    internal GachaAcquisitionResult(GachaData data, bool isNew, bool guaranteed, int reward, int before, int after,
        GachaDrawRole drawRole = GachaDrawRole.Base)
    {
        Data = data; Id = data.Id; Rank = data.Rank; Kind = KindOf(data); IsNew = isNew;
        IsGuaranteedSpecial = guaranteed; PandaTokenReward = reward; CounterBefore = before; CounterAfter = after;
        DrawRole = drawRole;
    }
    public static GachaAcquisitionKind KindOf(GachaData data) => data is GachaStaffData ? GachaAcquisitionKind.Staff
        : data is GachaRecipeData || (data is GachaItemData item && item.UpgradeType == UpgradeType.None)
            ? GachaAcquisitionKind.Recipe : GachaAcquisitionKind.Item;
}

public sealed class GachaEconomyTransaction
{
    public string Id { get; }
    public GachaMachineKind Machine { get; }
    public bool IsDraw { get; }
    public GachaEconomyOperation Operation { get; }
    public bool IsCatalogChange => Operation == GachaEconomyOperation.InitializeDisplay || Operation == GachaEconomyOperation.RefreshDisplay;
    public GachaPaymentKind Payment { get; }
    public string ProductId { get; }
    public GachaEconomySnapshot Before { get; }
    public GachaEconomySnapshot After { get; }
    public IReadOnlyList<GachaAcquisitionResult> Results { get; }
    public GachaTransactionStatus Status { get; internal set; }
    public string Error { get; internal set; }
    public bool IsCompleted => Status == GachaTransactionStatus.Completed;
    public int BaseDrawCount => IsDraw ? Results.Count(x => !x.IsBonus) : 0;
    public int BonusDrawCount => IsDraw ? Results.Count(x => x.IsBonus) : 0;
    internal GachaEconomyTransaction(string id, GachaMachineKind machine, bool isDraw, GachaPaymentKind payment,
        string productId, GachaEconomySnapshot before, GachaEconomySnapshot after, List<GachaAcquisitionResult> results,
        GachaEconomyOperation? operation = null)
    {
        Id = id; Machine = machine; IsDraw = isDraw; Payment = payment; ProductId = productId; Before = before; After = after;
        Results = results.AsReadOnly(); Status = GachaTransactionStatus.Prepared;
        Operation = operation ?? (isDraw ? GachaEconomyOperation.Draw : GachaEconomyOperation.Purchase);
    }
}

/// <summary>Store must atomically persist and install the complete fixed transaction before reporting Confirmed.</summary>
public interface IGachaEconomyStore
{
    GachaEconomySnapshot Capture();
    bool CanStart(out string error);
    void Save(GachaEconomyTransaction transaction, Action<GachaStoreResult, string> completed);
}

/// <summary>Optional live progression policy, checked before selecting a paid result and again before sending.</summary>
public interface IGachaEconomyDrawAdmission
{
    bool CanDraw(GachaMachineKind machine, GachaPaymentKind payment, out string error);
}

/// <summary>Same authoritative policy is used by runtime and offline demo. No payment/grant occurs during planning.</summary>
public sealed partial class GachaEconomyService
{
    private readonly IGachaEconomyStore _store;
    private readonly IReadOnlyList<GachaData> _catalog;
    private readonly Func<GachaMachineKind, IReadOnlyList<GachaData>, bool, GachaData> _draw;
    private readonly GachaEconomySettings _settings;
    private bool _preparing;
    public GachaEconomySnapshot Snapshot => _store.Capture();
    public IReadOnlyList<GachaExchangeProduct> Products { get; }
    public GachaEconomyTransaction LastTransaction { get; private set; }
    public bool IsBusy
    {
        get
        {
            if (_preparing || LastTransaction?.Status == GachaTransactionStatus.Sending || LastTransaction?.Status == GachaTransactionStatus.Indeterminate) return true;
            try { return !_store.CanStart(out _); }
            catch { return true; }
        }
    }
    public event Action Changed;
    public event Action<GachaEconomyTransaction> Committed;
    public GachaEconomyService(IGachaEconomyStore store, IReadOnlyList<GachaData> catalog, GachaEconomySettings settings,
        Func<GachaMachineKind, IReadOnlyList<GachaData>, bool, GachaData> draw = null,
        IGachaExchangeClock clock = null, System.Random displayRandom = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = Array.AsReadOnly((catalog ?? throw new ArgumentNullException(nameof(catalog))).ToArray());
        _settings = settings ?? throw new ArgumentNullException(nameof(settings)); _draw = draw ?? DrawDefault;
        Products = BuildProducts().AsReadOnly();
        _exchangeClock = clock; _displayRandom = displayRandom ?? new System.Random();
    }
    public static bool CanPurchaseStaff(Rank rank) => rank == Rank.Normal1 || rank == Rank.Normal2 || rank == Rank.Rare;
    public static int StaffPrice(Rank rank) => CanPurchaseStaff(rank) ? (rank == Rank.Rare ? 100 : 40) : 0;
    private List<GachaExchangeProduct> BuildProducts()
    {
        var list = new List<GachaExchangeProduct>
        {
            new GachaExchangeProduct("ticket:item", GachaExchangeCategory.Tickets, "아이템 뽑기권", "1장당 아이템 추첨 1회 · 보장 횟수 포함", _settings.ItemTicketSprite, _settings.ItemTicketPrice, _settings.TicketBundleQuantity),
            new GachaExchangeProduct("ticket:staff", GachaExchangeCategory.Tickets, "직원 뽑기권", "1장당 직원 추첨 1회 · 보장 횟수 포함", _settings.StaffTicketSprite, _settings.StaffTicketPrice, _settings.TicketBundleQuantity, ticketMachine:GachaMachineKind.Staff)
        };
        foreach (var data in _catalog)
        {
            if (data == null) continue;
            if (data is GachaStaffData && CanPurchaseStaff(data.Rank))
                list.Add(new GachaExchangeProduct("staff:" + data.Id, GachaExchangeCategory.Staff, data.Name, data.Description, data.ThumbnailSprite ?? data.Sprite, StaffPrice(data.Rank), 1, data));
            else if (data is GachaItemData)
                list.Add(new GachaExchangeProduct("item:" + data.Id, GachaExchangeCategory.Items, data.Name, data.Description, data.ThumbnailSprite ?? data.Sprite, _settings.ItemPrice(data.Rank), 1, data));
        }
        return list;
    }
    public bool CanExchange(string productId, int quantity, out string error)
        => CanExchange(productId, quantity, OfferVersion(productId), out error);
    public bool CanExchange(string productId, int quantity, int expectedDisplayVersion, out string error)
    {
        error = null;
        if (IsBusy || !_store.CanStart(out error)) { error = error ?? "거래 결과를 확인 중입니다."; return false; }
        var source = Snapshot;
        var product = ResolveOffer(source, productId, expectedDisplayVersion, out error);
        if (product == null) return false;
        if (product == null || quantity <= 0 || quantity > 100 || product.Price <= 0 || product.Quantity <= 0 || product.Quantity > 1000)
        { error = "올바르지 않은 상품 또는 수량입니다."; return false; }
        if (source == null || source.Account == null) { error = "계정 복원을 기다려 주세요."; return false; }
        if (!product.IsRepeatable && quantity != 1)
        { error = "한 번 해금하는 상품은 1개만 교환할 수 있습니다."; return false; }
        if (product.Category == GachaExchangeCategory.Staff && !CanPurchaseStaff(product.Data.Rank))
        { error = "이 직원은 뽑기로 획득할 수 있습니다."; return false; }
        if (!product.IsRepeatable && IsOwnedUnlock(source, product))
        { error = product.Category == GachaExchangeCategory.Staff ? "이미 보유한 직원입니다." : "이미 해금한 레시피입니다."; return false; }
        try { if (source.Account.PandaTokens < checked(product.Price * quantity)) { error = "판다토큰이 부족합니다."; return false; } }
        catch (OverflowException) { error = "교환 금액 범위를 초과했습니다."; return false; }
        return true;
    }
    public bool TryExchange(string productId, int quantity, Action<GachaEconomyTransaction> completed, out string error)
        => TryExchange(productId, quantity, OfferVersion(productId), completed, out error);
    public bool TryExchange(string productId, int quantity, int expectedDisplayVersion, Action<GachaEconomyTransaction> completed, out string error)
    {
        if (!CanExchange(productId, quantity, expectedDisplayVersion, out error)) return false;
        return Start(false, GachaMachineKind.Item, GachaPaymentKind.TicketSingle, productId, quantity, completed, out error);
    }
    public bool CanDraw(GachaMachineKind machine, GachaPaymentKind payment, out string error)
    {
        error = null;
        if (IsBusy || !_store.CanStart(out error)) { error = error ?? "이전 거래 확인을 기다려 주세요."; return false; }
        if (!Enum.IsDefined(typeof(GachaMachineKind), machine) || !Enum.IsDefined(typeof(GachaPaymentKind), payment))
        { error = "올바르지 않은 머신 또는 결제 방식입니다."; return false; }
        if (_store is IGachaEconomyDrawAdmission admission && !admission.CanDraw(machine, payment, out error)) return false;
        var source = Snapshot;
        if (source == null || !StaffAccountSaveConverter.Validate(source.Account, out error))
        { error = error ?? "계정 복원을 기다려 주세요."; return false; }
        if (payment == GachaPaymentKind.TicketSingle && source.Account.GachaEconomy.Tickets(machine) < 1)
        { error = "해당 머신의 뽑기권이 부족합니다."; return false; }
        int cost = payment == GachaPaymentKind.DiamondsEleven ? 100 : payment == GachaPaymentKind.DiamondsSingle ? 10 : 0;
        if (source.Diamonds < cost) { error = "다이아가 부족합니다."; return false; }
        return true;
    }
    private static bool IsOwnedUnlock(GachaEconomySnapshot source, GachaExchangeProduct product)
    {
        string id = product.Data.Id;
        if (product.Category == GachaExchangeCategory.Staff) return source.Account.Staff.Any(staff => staff.Id == id);
        return source.ItemCounts.ContainsKey(id) || source.ItemLevels.ContainsKey(id) || source.RecipeLevels.ContainsKey(id)
            || source.Account.GachaEconomy.Acquired.Contains(GachaEconomySaveData.Key(GachaAcquisitionKind.Recipe, id));
    }
    public bool TryDraw(GachaMachineKind machine, GachaPaymentKind payment, Action<GachaEconomyTransaction> completed, out string error)
    {
        if (!CanDraw(machine, payment, out error)) return false;
        return Start(true, machine, payment, null, 1, completed, out error);
    }
    private bool Start(bool isDraw, GachaMachineKind machine, GachaPaymentKind payment, string productId, int quantity,
        Action<GachaEconomyTransaction> completed, out string error)
    {
        error = null;
        if (IsBusy || !_store.CanStart(out error)) { error = error ?? "이전 거래 확인을 기다려 주세요."; return false; }
        _preparing = true;
        try
        {
            var before = Snapshot;
            if (before == null || !StaffAccountSaveConverter.Validate(before.Account, out error)) return false;
            if (!Enum.IsDefined(typeof(GachaMachineKind), machine) || !Enum.IsDefined(typeof(GachaPaymentKind), payment))
            { error = "올바르지 않은 머신 또는 결제 방식입니다."; return false; }
            var tx = Plan(before, isDraw, machine, payment, productId, quantity);
            LastTransaction = tx;
            Send(tx, completed);
            error = tx.Error;
            return tx.Status != GachaTransactionStatus.Rejected;
        }
        catch (Exception ex) { error = ex is OverflowException ? "재화 또는 아이템 수량 범위를 초과했습니다." : ex.Message; return false; }
        finally { _preparing = false; Notify(Changed); }
    }
    public bool RetryRejected(Action<GachaEconomyTransaction> completed, out string error)
    {
        error = null; var tx = LastTransaction;
        if (IsBusy || tx == null || tx.Status != GachaTransactionStatus.Rejected || !_store.CanStart(out error)
            || !tx.Before.HasSameState(Snapshot)) { error = error ?? "같은 미반영 거래만 재시도할 수 있습니다."; return false; }
        if (tx.IsDraw && !CanDraw(tx.Machine, tx.Payment, out error)) return false;
        Send(tx, completed); return tx.Status != GachaTransactionStatus.Rejected;
    }
    private void Send(GachaEconomyTransaction tx, Action<GachaEconomyTransaction> completed)
    {
        tx.Status = GachaTransactionStatus.Sending;
        bool settled = false;
        try
        {
            _store.Save(tx, (result, error) =>
            {
                if (settled || tx.IsCompleted) return;
                // An indeterminate response may later be followed by the ORIGINAL confirmed callback.
                if (result != GachaStoreResult.Indeterminate) settled = true;
                tx.Error = error;
                tx.Status = result == GachaStoreResult.Confirmed ? GachaTransactionStatus.Completed
                    : result == GachaStoreResult.Indeterminate ? GachaTransactionStatus.Indeterminate : GachaTransactionStatus.Rejected;
                Notify(Changed);
                if (tx.IsCompleted)
                {
                    var observers = Committed;
                    if (observers != null)
                        foreach (Action<GachaEconomyTransaction> observer in observers.GetInvocationList())
                            if (CanPublish(tx)) Notify(() => observer(tx));
                    if (CanPublish(tx)) Notify(() => completed?.Invoke(tx));
                }
            });
        }
        catch (Exception ex) { tx.Status = GachaTransactionStatus.Indeterminate; tx.Error = ex.Message; Notify(Changed); }
    }
    private bool CanPublish(GachaEconomyTransaction transaction)
    {
        try { return Snapshot?.AccountId == transaction.After.AccountId; }
        catch { return false; }
    }
    private GachaEconomyTransaction Plan(GachaEconomySnapshot before, bool isDraw, GachaMachineKind machine,
        GachaPaymentKind payment, string productId, int quantity)
    {
        string id = "economy:" + Guid.NewGuid().ToString("N");
        if (before.Diamonds < 0 || before.TotalDrawCount < 0 || before.ItemCounts.Any(x => x.Value < 0)
            || before.ItemLevels.Any(x => x.Value < 1) || before.RecipeLevels.Any(x => x.Value < 1))
            throw new InvalidOperationException("저장된 재화 또는 보유 수량이 올바르지 않습니다.");
        var state = before.Account.GachaEconomy;
        int itemTickets = state.ItemTickets, staffTickets = state.StaffTickets;
        int itemCounter = state.ItemCounter, staffCounter = state.StaffCounter;
        int diamonds = before.Diamonds, total = before.TotalDrawCount;
        long tokens = before.Account.PandaTokens;
        var staff = before.Account.Staff.ToList();
        var counts = before.ItemCounts.ToDictionary(x => x.Key, x => x.Value);
        var levels = before.ItemLevels.ToDictionary(x => x.Key, x => x.Value);
        var recipes = before.RecipeLevels.ToDictionary(x => x.Key, x => x.Value);
        var history = new HashSet<string>(state.Acquired, StringComparer.Ordinal);
        foreach (var owned in staff) history.Add(GachaEconomySaveData.Key(GachaAcquisitionKind.Staff, owned.Id));
        foreach (var owned in counts.Keys.Concat(levels.Keys))
        {
            var registered = _catalog.FirstOrDefault(x => x is GachaItemData && x.Id == owned);
            history.Add(GachaEconomySaveData.Key(registered == null ? GachaAcquisitionKind.Item : GachaAcquisitionResult.KindOf(registered), owned));
        }
        foreach (var owned in recipes.Keys) history.Add(GachaEconomySaveData.Key(GachaAcquisitionKind.Recipe, owned));
        var results = new List<GachaAcquisitionResult>();
        void Acquire(GachaData data, bool guaranteed, int previous, int next, GachaDrawRole role = GachaDrawRole.Base)
        {
            var kind = GachaAcquisitionResult.KindOf(data);
            bool isNew = history.Add(GachaEconomySaveData.Key(kind, data.Id));
            int reward = 0;
            if (kind == GachaAcquisitionKind.Staff)
            {
                bool owned = staff.Any(x => x.Id == data.Id);
                if (!owned) staff.Add(new StaffAccountStaffRecord(data.Id, 1));
                else if (isDraw) { reward = DuplicateReward(data.Rank); tokens = checked(tokens + reward); }
                else throw new InvalidOperationException("이미 보유한 직원입니다.");
            }
            else if (data is GachaRecipeData)
            {
                if (!recipes.ContainsKey(data.Id)) recipes.Add(data.Id, 1);
            }
            else
            {
                counts.TryGetValue(data.Id, out int count); counts[data.Id] = checked(count + 1);
                if (!levels.ContainsKey(data.Id)) levels.Add(data.Id, 1);
            }
            results.Add(new GachaAcquisitionResult(data, isNew, guaranteed, reward, previous, next, role));
        }
        if (isDraw)
        {
            var drawPlan = GachaDrawPlan.ForPayment(payment);
            int count = drawPlan.ResultCount;
            int cost = drawPlan.DiamondCost;
            if (diamonds < cost) throw new InvalidOperationException("다이아가 부족합니다.");
            int counter = state.Counter(machine);
            var candidates = _catalog.Where(x => x != null && (machine == GachaMachineKind.Staff ? x is GachaStaffData : x is GachaItemData || x is GachaRecipeData)).ToArray();
            if (candidates.Length == 0 || candidates.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Rank < Rank.Normal1 || x.Rank >= Rank.Length)
                || candidates.Select(x => GachaEconomySaveData.Key(GachaAcquisitionResult.KindOf(x), x.Id)).Distinct().Count() != candidates.Length)
                throw new InvalidOperationException("머신의 추첨 원본 자료가 올바르지 않습니다.");
            if (counter + drawPlan.BaseCount >= 100 && !candidates.Any(x => x.Rank == Rank.Special))
                throw new InvalidOperationException("Special 보장 후보가 없어 결제를 중단했습니다.");
            if (payment == GachaPaymentKind.TicketSingle)
            {
                if (state.Tickets(machine) < 1) throw new InvalidOperationException("해당 머신의 뽑기권이 부족합니다.");
                if (machine == GachaMachineKind.Staff) staffTickets--; else itemTickets--;
            }
            diamonds = checked(diamonds - cost); total = checked(total + count);
            for (int i = 0; i < count; i++)
            {
                bool bonus = drawPlan.Roles[i] == GachaDrawRole.Bonus;
                bool guarantee = !bonus && counter == 99;
                IReadOnlyList<GachaData> eligible = guarantee ? candidates.Where(x => x.Rank == Rank.Special).ToArray() : candidates;
                var selected = _draw(machine, eligible, guarantee);
                if (selected == null || !eligible.Any(x => ReferenceEquals(x, selected)) || (guarantee && selected.Rank != Rank.Special))
                    throw new InvalidOperationException("등록 후보 밖의 추첨 결과입니다.");
                int next = bonus ? counter : guarantee ? 0 : counter + 1;
                Acquire(selected, guarantee, counter, next, drawPlan.Roles[i]); counter = next;
            }
            if (machine == GachaMachineKind.Staff) staffCounter = counter; else itemCounter = counter;
        }
        else
        {
            var definition = Products.FirstOrDefault(x => x.Id == productId);
            var product = definition == null ? null : ResolveOffer(before, productId,
                state.Exchange.Display(definition.Category)?.Version ?? 0, out _);
            if (product == null || quantity <= 0 || quantity > 100 || product.Price <= 0 || product.Quantity <= 0 || product.Quantity > 1000)
                throw new InvalidOperationException("올바르지 않은 상품 또는 수량입니다.");
            if (!product.IsRepeatable && (quantity != 1 || IsOwnedUnlock(before, product)))
                throw new InvalidOperationException("이미 해금했거나 한 번 해금하는 상품의 수량이 올바르지 않습니다.");
            if (product.Category == GachaExchangeCategory.Staff && !CanPurchaseStaff(product.Data.Rank))
                throw new InvalidOperationException("이 직원은 뽑기로 획득할 수 있습니다.");
            long cost = checked(product.Price * quantity);
            if (tokens < cost) throw new InvalidOperationException("판다토큰이 부족합니다.");
            tokens -= cost;
            int amount = checked(product.Quantity * quantity);
            if (product.Category == GachaExchangeCategory.Tickets)
            {
                if (product.TicketMachine == GachaMachineKind.Staff) staffTickets = checked(staffTickets + amount);
                else itemTickets = checked(itemTickets + amount);
            }
            else for (int i = 0; i < amount; i++) Acquire(product.Data, false, state.Counter(machine), state.Counter(machine));
        }
        var receipt = new JObject { ["Id"] = id, ["State"] = "Committed", ["Machine"] = machine.ToString(), ["IsDraw"] = isDraw,
            ["BaseDrawCount"] = isDraw ? results.Count(r => !r.IsBonus) : 0, ["BonusDrawCount"] = isDraw ? results.Count(r => r.IsBonus) : 0,
            ["Payment"] = payment.ToString(), ["ProductId"] = productId, ["DiamondsBefore"] = before.Diamonds,
            ["DiamondsAfter"] = diamonds, ["TokensBefore"] = before.Account.PandaTokens, ["TokensAfter"] = tokens,
            ["ItemTicketsBefore"] = state.ItemTickets, ["ItemTicketsAfter"] = itemTickets,
            ["StaffTicketsBefore"] = state.StaffTickets, ["StaffTicketsAfter"] = staffTickets,
            ["ItemCounterBefore"] = state.ItemCounter, ["ItemCounterAfter"] = itemCounter,
            ["StaffCounterBefore"] = state.StaffCounter, ["StaffCounterAfter"] = staffCounter,
            ["Results"] = new JArray(results.Select(r => new JObject { ["Id"] = r.Id, ["Kind"] = r.Kind.ToString(), ["Rank"] = (int)r.Rank,
                ["IsNew"] = r.IsNew, ["Guaranteed"] = r.IsGuaranteedSpecial, ["DrawRole"] = r.DrawRole.ToString(), ["PandaTokenReward"] = r.PandaTokenReward,
                ["CounterBefore"] = r.CounterBefore, ["CounterAfter"] = r.CounterAfter })) };
        var exchange = state.Exchange;
        if (!isDraw)
        {
            var product = Products.First(x => x.Id == productId);
            if (product.Category != GachaExchangeCategory.Tickets) exchange = exchange.RecordPurchase(product.Category, productId, quantity);
        }
        var economy = new GachaEconomySaveData(itemTickets, staffTickets, itemCounter, staffCounter, history.OrderBy(x => x), id, receipt.ToString(Formatting.None), exchange);
        var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, staff, tokens, economy);
        var after = new GachaEconomySnapshot(before.AccountId, account, diamonds, counts, levels, recipes, total);
        return new GachaEconomyTransaction(id, machine, isDraw, payment, productId, before, after, results);
    }
    public static int DuplicateReward(Rank rank)
    {
        switch (rank) { case Rank.Normal1: case Rank.Normal2: return 5; case Rank.Rare: return 10;
            case Rank.Unique: return 20; case Rank.Special: return 50; default: throw new InvalidOperationException("유효하지 않은 등급입니다."); }
    }
    private static GachaData DrawDefault(GachaMachineKind machine, IReadOnlyList<GachaData> candidates, bool guaranteed)
        => guaranteed ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : machine == GachaMachineKind.Staff ? StaffGachaRandomSelector.Select(candidates)
            : ItemManager.Instance.GetRandomGachaData(candidates.ToList());
    public static GachaData SelectWithRandom(GachaMachineKind machine, IReadOnlyList<GachaData> candidates, bool guaranteed, System.Random random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (guaranteed) return candidates[random.Next(candidates.Count)];
        return machine == GachaMachineKind.Staff
            ? StaffGachaRandomSelector.Select(candidates, random.Next, random.Next)
            : ItemManager.SelectGachaData(candidates, () => (float)random.NextDouble(), random.Next);
    }
    private static void Notify(Action action) { try { action?.Invoke(); } catch (Exception ex) { Debug.LogWarning("Gacha listener: " + ex.Message); } }
}

/// <summary>Optional explicit FoodData result. Existing recipe gacha rows remain their original GachaItemData inventory.</summary>
public sealed class GachaRecipeData : GachaData
{
    public FoodData Recipe { get; private set; }
    public static GachaRecipeData Create(FoodData food)
    {
        if (food == null) throw new ArgumentNullException(nameof(food));
        var result = CreateInstance<GachaRecipeData>(); result.Recipe = food;
        result._id = food.Id; result._name = food.Name; result._description = food.Description;
        result._rank = food.Rank; result._sprite = food.Sprite; result._thumbnailSprite = food.ThumbnailSprite;
        result.hideFlags = HideFlags.HideAndDontSave; return result;
    }
}
