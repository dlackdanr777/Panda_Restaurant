using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed partial class GachaEconomyService
{
    private readonly IGachaExchangeClock _exchangeClock;
    private readonly System.Random _displayRandom;
    public bool ExchangeCatalogReady
    {
        get { var state = Snapshot?.Account?.GachaEconomy.Exchange; return state != null && state.Staff.IsInitialized && state.Items.IsInitialized; }
    }
    public GachaExchangeDisplaySaveData GetExchangeDisplay(GachaExchangeCategory category)
        => Snapshot?.Account?.GachaEconomy.Exchange.Display(category);

    public IReadOnlyList<GachaExchangeProduct> GetDisplayedProducts(GachaExchangeCategory category)
    {
        if (category == GachaExchangeCategory.Tickets) return Products.Where(x => x.Category == category).ToArray();
        var display = GetExchangeDisplay(category);
        if (display == null || !display.IsInitialized) return Array.Empty<GachaExchangeProduct>();
        return display.Slots.Select(slot => ResolveSavedProduct(category, slot)).Where(x => x != null).ToArray();
    }
    private GachaExchangeProduct ResolveSavedProduct(GachaExchangeCategory category, GachaExchangeSlotSaveData slot)
    {
        var original = Products.FirstOrDefault(x => x.Id == slot.ProductId && x.Category == category);
        if (original == null || (category == GachaExchangeCategory.Staff && !CanPurchaseStaff(original.Data.Rank))) return null;
        return new GachaExchangeProduct(original.Id, original.Category, original.Name, original.Description, original.Sprite,
            slot.Price, slot.Quantity, original.Data, original.TicketMachine);
    }
    private GachaExchangeProduct ResolveOffer(GachaEconomySnapshot source, string productId, int expectedVersion, out string error)
    {
        error = null;
        var original = Products.FirstOrDefault(x => x.Id == productId);
        if (original == null) { error = "현재 판매하지 않는 상품입니다."; return null; }
        if (original.Category == GachaExchangeCategory.Tickets) return original;
        var display = source?.Account?.GachaEconomy.Exchange.Display(original.Category);
        if (display == null || !display.IsInitialized) { error = "상품 진열 저장을 기다려 주세요."; return null; }
        if (display.Version != expectedVersion) { error = "상품 진열이 변경되었습니다. 다시 선택해 주세요."; return null; }
        var slot = display.Slots.FirstOrDefault(x => x.ProductId == productId);
        if (slot == null) { error = "현재 진열에 없는 상품입니다."; return null; }
        var product = ResolveSavedProduct(original.Category, slot);
        if (product == null) error = "현재 판매하지 않는 상품입니다.";
        return product;
    }
    private int OfferVersion(string productId)
    {
        var product = Products.FirstOrDefault(x => x.Id == productId);
        return product == null || product.Category == GachaExchangeCategory.Tickets ? 0 : GetExchangeDisplay(product.Category)?.Version ?? 0;
    }
    public bool TryEnsureExchangeCatalog(Action<GachaEconomyTransaction> completed, out string error)
    {
        error = null;
        if (ExchangeCatalogReady) { Notify(() => completed?.Invoke(null)); return true; }
        if (LastTransaction?.Operation == GachaEconomyOperation.InitializeDisplay && LastTransaction.Status == GachaTransactionStatus.Rejected)
            return RetryRejected(completed, out error);
        return StartCatalog(false, GachaExchangeCategory.Staff, 0, "", completed, out error);
    }
    public GachaExchangeRefreshStatus GetRefreshStatus()
    {
        var saved = Snapshot?.Account?.GachaEconomy.Exchange;
        if (saved == null || _exchangeClock == null || !_exchangeClock.TryGetUtcNow(out DateTime utc) || utc.Kind != DateTimeKind.Utc)
            return new GachaExchangeRefreshStatus(0, false, saved?.RefreshDateKey ?? "", "서버 시간 확인 후 새로고침할 수 있습니다.");
        string day;
        try { day = utc.AddHours(9).ToString("yyyyMMdd", CultureInfo.InvariantCulture); }
        catch (ArgumentOutOfRangeException) { return new GachaExchangeRefreshStatus(0, false, saved.RefreshDateKey, "서버 시간 범위를 확인해 주세요."); }
        if (string.CompareOrdinal(day, saved.RefreshDateKey) < 0)
            return new GachaExchangeRefreshStatus(Math.Max(0, 3 - saved.RefreshUsed), false, saved.RefreshDateKey, "저장된 날짜보다 이전 시간입니다. 서버 시간을 확인해 주세요.");
        int remaining = day == saved.RefreshDateKey ? Math.Max(0, 3 - saved.RefreshUsed) : 3;
        return new GachaExchangeRefreshStatus(remaining, true, day,
            remaining == 0 ? "오늘 새로고침 완료 · KST 자정 초기화" : "직원·아이템 합산 하루 3회 · KST 자정 초기화");
    }
    public bool CanRefreshExchange(GachaExchangeCategory category, int expectedVersion, out string error)
    {
        error = null;
        if (category != GachaExchangeCategory.Staff && category != GachaExchangeCategory.Items)
        { error = "뽑기권은 고정 상품입니다."; return false; }
        if (IsBusy || !_store.CanStart(out error)) { error = error ?? "이전 거래 확인을 기다려 주세요."; return false; }
        var display = GetExchangeDisplay(category);
        if (!ExchangeCatalogReady || display == null || display.Version != expectedVersion)
        { error = "상품 진열 저장 또는 최신 진열 확인을 기다려 주세요."; return false; }
        var status = GetRefreshStatus();
        if (!status.ClockReady || status.Remaining < 1) { error = status.Message; return false; }
        return true;
    }
    public bool TryRefreshExchange(GachaExchangeCategory category, int expectedVersion, string requestId,
        Action<GachaEconomyTransaction> completed, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) { error = "새로고침 요청 ID가 올바르지 않습니다."; return false; }
        var saved = Snapshot?.Account?.GachaEconomy.Exchange;
        if (saved != null && saved.LastRefreshRequestId == requestId && !IsBusy)
        {
            try
            {
                var receipt = JObject.Parse(saved.LastRefreshReceiptJson);
                if ((string)receipt["Category"] != category.ToString() || (int)receipt["VersionBefore"] != expectedVersion)
                { error = "이 요청 ID는 다른 새로고침에 사용되었습니다."; return false; }
                var snapshot = Snapshot;
                var replay = new GachaEconomyTransaction((string)receipt["Id"], GachaMachineKind.Item, false,
                    GachaPaymentKind.TicketSingle, "refresh:" + category, snapshot, snapshot,
                    new List<GachaAcquisitionResult>(), GachaEconomyOperation.RefreshDisplay) { Status = GachaTransactionStatus.Completed };
                Notify(() => completed?.Invoke(replay)); return true;
            }
            catch { error = "새로고침 확정 기록을 확인할 수 없습니다."; return false; }
        }
        // Reopening a failed request cannot turn save rejection into a free new roll.
        var rejected = LastTransaction;
        if (rejected?.Operation == GachaEconomyOperation.RefreshDisplay && rejected.Status == GachaTransactionStatus.Rejected &&
            rejected.ProductId == "refresh:" + category && rejected.Before.Account.GachaEconomy.Exchange.Display(category)?.Version == expectedVersion)
            return RetryRejected(completed, out error);
        if (!CanRefreshExchange(category, expectedVersion, out error)) return false;
        return StartCatalog(true, category, expectedVersion, requestId, completed, out error);
    }
    private bool StartCatalog(bool refresh, GachaExchangeCategory category, int expectedVersion, string requestId,
        Action<GachaEconomyTransaction> completed, out string error)
    {
        error = null;
        if (IsBusy || !_store.CanStart(out error)) { error = error ?? "이전 거래 확인을 기다려 주세요."; return false; }
        _preparing = true;
        try
        {
            var before = Snapshot;
            if (before == null || !StaffAccountSaveConverter.Validate(before.Account, out error)) return false;
            var old = before.Account.GachaEconomy;
            var catalog = old.Exchange;
            var status = GetRefreshStatus();
            string day = status.ClockReady ? status.DayKey : catalog.RefreshDateKey;
            int used = status.ClockReady && day != catalog.RefreshDateKey ? 0 : catalog.RefreshUsed;
            var staff = catalog.Staff; var items = catalog.Items;
            if (refresh)
            {
                if (!status.ClockReady || status.Remaining < 1 || catalog.Display(category)?.Version != expectedVersion)
                    throw new InvalidOperationException(status.Message ?? "상품 진열이 변경되었습니다.");
                if (category == GachaExchangeCategory.Staff) staff = ChooseDisplay(category, checked(staff.Version + 1));
                else if (category == GachaExchangeCategory.Items) items = ChooseDisplay(category, checked(items.Version + 1));
                else throw new InvalidOperationException("뽑기권은 고정 상품입니다.");
                used++;
            }
            else
            {
                if (!staff.IsInitialized) staff = ChooseDisplay(GachaExchangeCategory.Staff, 1);
                if (!items.IsInitialized) items = ChooseDisplay(GachaExchangeCategory.Items, 1);
            }
            string id = "economy:" + Guid.NewGuid().ToString("N");
            var receipt = new JObject { ["Id"] = id, ["Operation"] = refresh ? "RefreshDisplay" : "InitializeDisplay",
                ["Category"] = category.ToString(), ["RequestId"] = requestId, ["VersionBefore"] = expectedVersion,
                ["VersionAfter"] = category == GachaExchangeCategory.Staff ? staff.Version : items.Version,
                ["DayKey"] = day, ["RefreshUsed"] = used };
            string receiptJson = receipt.ToString(Formatting.None);
            var nextCatalog = new GachaExchangeCatalogSaveData(staff, items, day, used,
                refresh ? requestId : catalog.LastRefreshRequestId, refresh ? receiptJson : catalog.LastRefreshReceiptJson);
            var economy = new GachaEconomySaveData(old.ItemTickets, old.StaffTickets, old.ItemCounter, old.StaffCounter,
                old.Acquired, id, receiptJson, nextCatalog);
            var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, before.Account.Staff, before.Account.PandaTokens, economy);
            var after = new GachaEconomySnapshot(before.AccountId, account, before.Diamonds, before.ItemCounts.ToDictionary(x => x.Key, x => x.Value),
                before.ItemLevels.ToDictionary(x => x.Key, x => x.Value), before.RecipeLevels.ToDictionary(x => x.Key, x => x.Value), before.TotalDrawCount);
            var transaction = new GachaEconomyTransaction(id, GachaMachineKind.Item, false, GachaPaymentKind.TicketSingle,
                refresh ? "refresh:" + category : "initialize:exchange", before, after, new List<GachaAcquisitionResult>(),
                refresh ? GachaEconomyOperation.RefreshDisplay : GachaEconomyOperation.InitializeDisplay);
            LastTransaction = transaction; Send(transaction, completed); error = transaction.Error;
            return transaction.Status != GachaTransactionStatus.Rejected;
        }
        catch (Exception exception) { error = exception.Message; return false; }
        finally { _preparing = false; Notify(Changed); }
    }
    private GachaExchangeDisplaySaveData ChooseDisplay(GachaExchangeCategory category, int version)
        => new GachaExchangeDisplaySaveData(version, GachaExchangeDisplaySelector.Select(Products, category, _displayRandom)
            .Select(x => new GachaExchangeSlotSaveData(x.Id, x.Price, x.Quantity)));
}
