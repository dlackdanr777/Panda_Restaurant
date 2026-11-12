#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Migration planner/host-port tests only. No SDK, real account, UserInfo, or remote atomicity claim.</summary>
public sealed class PopupStoreWalletCutoverTests
{
    [Test]
    public void DefaultHostCannotFreezeOrEnableEitherWallet()
    {
        var model = new WalletCutoverCoordinator();
        Assert.That(model.TryFreeze(A, "wallet", "migration", "fence", out var plan, out _), Is.False);
        Assert.That(plan, Is.Null);
        Assert.That(model.CanUseDatabaseWallet(A, null, "wallet", "migration", out _), Is.False);
    }

    [TestCase(WalletMigrationRoute.Autosave)]
    [TestCase(WalletMigrationRoute.AccountLoad)]
    [TestCase(WalletMigrationRoute.Gacha)]
    [TestCase(WalletMigrationRoute.StaffGacha)]
    [TestCase(WalletMigrationRoute.ShopAndUpgrade)]
    [TestCase(WalletMigrationRoute.Attendance)]
    [TestCase(WalletMigrationRoute.Mail)]
    [TestCase(WalletMigrationRoute.Payment)]
    [TestCase(WalletMigrationRoute.Advertisement)]
    [TestCase(WalletMigrationRoute.Challenge)]
    [TestCase(WalletMigrationRoute.MiniGame)]
    [TestCase(WalletMigrationRoute.TutorialAndDebug)]
    public void AnyUnmigratedRouteBlocksCutoverBeforeReadingLegacyBalance(WalletMigrationRoute missing)
    {
        var host = new Host { MissingRoute = missing };
        Assert.That(new WalletCutoverCoordinator(host).TryFreeze(A, "wallet", "migration", "fence", out _, out _), Is.False);
        Assert.That(host.SourceReads, Is.Zero);
    }

    [TestCase("fence")]
    [TestCase("drained")]
    [TestCase("atomic")]
    public void ClientLocalFenceOrOutstandingLegacyRequestCannotAuthorizeMigration(string absent)
    {
        var host = new Host();
        if (absent == "fence") host.ServerFence = false;
        if (absent == "drained") host.Drained = false;
        if (absent == "atomic") host.Atomic = false;
        Assert.That(new WalletCutoverCoordinator(host).TryFreeze(A, "wallet", "migration", "fence", out _, out _), Is.False);
        Assert.That(host.SourceReads, Is.Zero);
    }

    [Test]
    public void SourceReadCannotSurviveFenceRevocationDuringThatRead()
    {
        var host = new Host { RevokeDuringSourceRead = true };
        Assert.That(new WalletCutoverCoordinator(host).TryFreeze(A, "wallet", "migration", "fence", out _, out _), Is.False);
        Assert.That(host.SourceReads, Is.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("previous-writer-inventory")]
    public void OutdatedRouteAuditCannotAuthorizeNewWriterInventory(string inventory)
    {
        var host = new Host { Inventory = inventory };
        Assert.That(new WalletCutoverCoordinator(host).TryFreeze(A, "wallet", "migration", "fence", out _, out _), Is.False);
        Assert.That(host.SourceReads, Is.Zero);
    }

    [TestCase("account")]
    [TestCase("row")]
    [TestCase("digest")]
    [TestCase("balance")]
    public void InvalidOrForeignServerSourceCannotCreateFreeze(string invalid)
    {
        var host = new Host { Source = new FrozenLegacyWallet(invalid == "account" ? "B" : "A",
            invalid == "row" ? "" : "row", invalid == "digest" ? "client-hash" : Digest,
            invalid == "balance" ? -1 : 100) };
        Assert.That(new WalletCutoverCoordinator(host).TryFreeze(A, "wallet", "migration", "fence", out _, out _), Is.False);
    }

    [Test]
    public void StableFrozenSourceImportsOnceAndBothOldClientAndAutosaveWritesStayDenied()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        var frozen = Freeze(model, journal);
        Assert.That(frozen.AllowsLegacyDiaWrite, Is.False);
        Assert.That(model.CanUseDatabaseWallet(A, frozen, "wallet", "migration", out _), Is.False);
        var pending = Prepare(model, journal);
        host.Import = Receipt(pending);
        Assert.That(model.TryConfirmImport(A, pending, out var activated, out string error), Is.True, error);
        Assert.That(journal.Commit(activated), Is.True);
        Assert.That(model.CanUseDatabaseWallet(A, journal.Record, "wallet", "migration", out _), Is.True);
        Assert.That(journal.Record.AllowsLegacyDiaWrite, Is.False);
        Assert.That(model.TryConfirmImport(A, journal.Record, out var replay, out error), Is.True, error);
        Assert.That(replay.RequiresCommit, Is.False);
        Assert.That(replay.After, Is.SameAs(journal.Record));
        Assert.That(model.TryPrepareImport(A, journal.Record, out _, out _), Is.False);
        Assert.That(journal.Commits, Is.EqualTo(3));
    }

    [TestCase("balance")]
    [TestCase("digest")]
    [TestCase("row")]
    public void AutosaveOrGachaChangesAfterFreezeCannotRebaseOpeningBalance(string changed)
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        var frozen = Freeze(model, journal);
        host.Source = new FrozenLegacyWallet("A", changed == "row" ? "replacement-row" : "row",
            changed == "digest" ? new string('b', 64) : Digest, changed == "balance" ? 70 : 100);
        Assert.That(model.TryPrepareImport(A, frozen, out _, out _), Is.False);
        Assert.That(journal.Record.Source.Balance, Is.EqualTo(100));
        Assert.That(journal.Record.State, Is.EqualTo(WalletCutoverState.Frozen));
        Assert.That(journal.Record.AllowsLegacyDiaWrite, Is.False);
    }

    [Test]
    public void TwoDevicesCannotCommitTwoFreezesOrTwoImportIntentsUnderRequiredCasContract()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Assert.That(model.TryFreeze(A, "wallet", "migration", "fence", out var first, out _), Is.True);
        Assert.That(model.TryFreeze(A, "other-wallet", "other-migration", "fence", out var other, out _), Is.True);
        Assert.That(journal.Commit(first), Is.True);
        Assert.That(journal.Commit(other), Is.False, "Duplicate account insert cannot overwrite the first migration.");
        Assert.That(model.TryPrepareImport(A, journal.Record, out var intent1, out _), Is.True);
        Assert.That(model.TryPrepareImport(A, journal.Record, out var intent2, out _), Is.True);
        Assert.That(journal.Commit(intent1), Is.True);
        Assert.That(journal.Commit(intent2), Is.False, "Only the committed plan may submit its import batch.");
        Assert.That(journal.Record.WalletId, Is.EqualTo("wallet"));
        Assert.That(journal.Commits, Is.EqualTo(2));
    }

    [Test]
    public void LostImportResponseKeepsOriginalKeyAcrossRecoveryAndCannotResubmitOrRollback()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal);
        host.Lookup = WalletImportLookup.Unknown;
        Assert.That(model.TryConfirmImport(A, pending, out _, out _), Is.False);
        Assert.That(model.TryMarkImportUnknown(A, pending, out var recovery, out _), Is.True);
        Assert.That(journal.Commit(recovery), Is.True);
        string originalKey = pending.ImportKey;
        var restarted = new WalletCutoverCoordinator(host);
        Assert.That(restarted.TryPrepareImport(A, journal.Record, out _, out _), Is.False);
        Assert.That(journal.Record.AllowsLegacyDiaWrite, Is.False);
        Assert.That(journal.Record.ImportKey, Is.EqualTo(originalKey));
        host.Lookup = WalletImportLookup.Found; host.Import = Receipt(pending);
        Assert.That(restarted.TryConfirmImport(A, journal.Record, out var confirmed, out _), Is.True);
        Assert.That(journal.Commit(confirmed), Is.True);
        Assert.That(journal.Record.ImportTransactionId, Is.EqualTo("transaction"));
        Assert.That(restarted.CanUseDatabaseWallet(A, journal.Record, "wallet", "migration", out _), Is.True);
    }

    [TestCase(WalletImportLookup.Unknown)]
    [TestCase(WalletImportLookup.Absent)]
    public void MissingImportQueryDoesNotProveNonApplicationOrUnlockNewRequest(WalletImportLookup outcome)
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal);
        host.Lookup = outcome;
        Assert.That(model.TryConfirmImport(A, pending, out _, out _), Is.False);
        Assert.That(model.TryPrepareImport(A, pending, out _, out _), Is.False);
        Assert.That(model.CanUseDatabaseWallet(A, pending, "wallet", "migration", out _), Is.False);
        Assert.That(pending.AllowsLegacyDiaWrite, Is.False);
    }

    [TestCase("account")]
    [TestCase("wallet")]
    [TestCase("migration")]
    [TestCase("key")]
    [TestCase("transaction")]
    [TestCase("source")]
    [TestCase("version")]
    public void PartialOrForeignImportReceiptCannotActivate(string wrong)
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal);
        host.Import = new WalletImportReceipt(wrong == "account" ? "B" : "A", wrong == "wallet" ? "other" : "wallet",
            wrong == "migration" ? "other" : "migration", wrong == "key" ? "other" : pending.ImportKey,
            wrong == "transaction" ? "" : "transaction", wrong == "source" ? new FrozenLegacyWallet("A", "row", Digest, 101) : pending.Source,
            wrong == "version" ? 1 : 0);
        Assert.That(model.TryConfirmImport(A, pending, out _, out _), Is.False);
        Assert.That(journal.Record.State, Is.EqualTo(WalletCutoverState.ImportPending));
        Assert.That(journal.Commits, Is.EqualTo(2));
    }

    [Test]
    public void ReplacedReceiptCannotReopenOrOverwriteCompletedMigration()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = Activated(model, host);
        host.Import = Receipt(journal.Record, "different-transaction");
        Assert.That(model.TryConfirmImport(A, journal.Record, out _, out _), Is.False);
        Assert.That(journal.Record.ImportTransactionId, Is.EqualTo("transaction"));
        Assert.That(journal.Record.State, Is.EqualTo(WalletCutoverState.DatabaseOnly));
        Assert.That(journal.Record.AllowsLegacyDiaWrite, Is.False);
    }

    [Test]
    public void AccountSwitchCannotConfirmAnotherAccountAndOldRevisionCannotReapplyOnReturn()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal); host.Import = Receipt(pending);
        Assert.That(model.TryConfirmImport(new AuthenticatedPrincipal("B"), pending, out _, out _), Is.False);
        Assert.That(model.TryConfirmImport(A, pending, out var oldAPlan, out _), Is.True);
        Assert.That(model.TryMarkImportUnknown(A, pending, out var newer, out _), Is.True);
        Assert.That(journal.Commit(newer), Is.True);
        Assert.That(journal.Commit(oldAPlan), Is.False, "A->B->A cannot bypass the persisted expected revision.");
        Assert.That(model.TryConfirmImport(new AuthenticatedPrincipal("A"), journal.Record, out var freshAPlan, out _), Is.True);
        Assert.That(journal.Commit(freshAPlan), Is.True);
    }

    [TestCase("fence")]
    [TestCase("drained")]
    [TestCase("route")]
    [TestCase("atomic")]
    public void RevokedServerProofDisablesDatabaseMutationsWithoutEnablingLegacyWrites(string revoked)
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = Activated(model, host);
        if (revoked == "fence") host.ServerFence = false;
        if (revoked == "drained") host.Drained = false;
        if (revoked == "route") host.MissingRoute = WalletMigrationRoute.Gacha;
        if (revoked == "atomic") host.Atomic = false;
        Assert.That(model.CanUseDatabaseWallet(A, journal.Record, "wallet", "migration", out _), Is.False);
        Assert.That(journal.Record.AllowsLegacyDiaWrite, Is.False);
    }

    [Test]
    public void RevokedProofDuringImportReadCannotProduceActivation()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal); host.Import = Receipt(pending);
        host.RevokeDuringImportRead = true;
        Assert.That(model.TryConfirmImport(A, pending, out _, out _), Is.False);
        Assert.That(journal.Record.State, Is.EqualTo(WalletCutoverState.ImportPending));
    }

    [Test]
    public void HostReadFailureCannotClearImportIntentOrEnableFallback()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = new Journal();
        Freeze(model, journal); var pending = Prepare(model, journal); host.ThrowOnQuery = true;
        Assert.That(model.TryConfirmImport(A, pending, out _, out _), Is.False);
        Assert.That(journal.Record, Is.SameAs(pending));
        Assert.That(pending.AllowsLegacyDiaWrite, Is.False);
    }

    [Test]
    public void ImportIdentityIsUnambiguousAndDoesNotUseRandomRetryIds()
    {
        Assert.That(WalletCutoverCoordinator.ImportKeyFor("a:b", "c"), Is.Not.EqualTo(WalletCutoverCoordinator.ImportKeyFor("a", "b:c")));
        Assert.That(WalletCutoverCoordinator.ImportKeyFor("계정", "이전"), Is.EqualTo(WalletCutoverCoordinator.ImportKeyFor("계정", "이전")));
        Assert.Throws<ArgumentException>(() => WalletCutoverCoordinator.ImportKeyFor("account\nother", "migration"));
    }

    [Test]
    public void CompletedMigrationIdentityDoesNotAuthorizeDifferentWalletOrMigration()
    {
        var host = new Host(); var model = new WalletCutoverCoordinator(host); var journal = Activated(model, host);
        Assert.That(model.CanUseDatabaseWallet(A, journal.Record, "other-wallet", "migration", out _), Is.False);
        Assert.That(model.CanUseDatabaseWallet(A, journal.Record, "wallet", "other-migration", out _), Is.False);
        Assert.That(model.CanUseDatabaseWallet(new AuthenticatedPrincipal("B"), journal.Record, "wallet", "migration", out _), Is.False);
    }

    private static readonly AuthenticatedPrincipal A = new AuthenticatedPrincipal("A");
    private static readonly string Digest = new string('a', 64);
    private static WalletCutoverRecord Freeze(WalletCutoverCoordinator model, Journal journal)
    {
        Assert.That(model.TryFreeze(A, "wallet", "migration", "fence", out var plan, out string error), Is.True, error);
        Assert.That(journal.Commit(plan), Is.True); return journal.Record;
    }
    private static WalletCutoverRecord Prepare(WalletCutoverCoordinator model, Journal journal)
    {
        Assert.That(model.TryPrepareImport(A, journal.Record, out var plan, out string error), Is.True, error);
        Assert.That(journal.Commit(plan), Is.True); return journal.Record;
    }
    private static Journal Activated(WalletCutoverCoordinator model, Host host)
    {
        var journal = new Journal(); Freeze(model, journal); Prepare(model, journal); host.Import = Receipt(journal.Record);
        Assert.That(model.TryConfirmImport(A, journal.Record, out var plan, out string error), Is.True, error);
        Assert.That(journal.Commit(plan), Is.True); return journal;
    }
    private static WalletImportReceipt Receipt(WalletCutoverRecord record, string transaction = "transaction")
        => new WalletImportReceipt(record.AccountId, record.WalletId, record.MigrationId, record.ImportKey, transaction, record.Source, 0);

    // Only the local contract of a required atomic journal is tested here; real Database CAS is unverified.
    private sealed class Journal
    {
        public WalletCutoverRecord Record;
        public int Commits;
        public bool Commit(WalletCutoverTransition plan)
        {
            if ((Record == null ? -1 : Record.Revision) != plan.ExpectedRevision ||
                (Record != null && Record.AccountId != plan.After.AccountId)) return false;
            if (plan.RequiresCommit) { Record = plan.After; Commits++; }
            return true;
        }
    }
    private sealed class Host : IWalletCutoverHostProofs
    {
        public string Inventory = WalletCutoverCoordinator.WriterInventoryVersion;
        public string VerifiedWriterInventoryVersion => Inventory;
        public bool ServerFence = true, Drained = true, Atomic = true;
        public bool RevokeDuringSourceRead, RevokeDuringImportRead, ThrowOnQuery;
        public WalletMigrationRoute? MissingRoute;
        public FrozenLegacyWallet Source = new FrozenLegacyWallet("A", "row", Digest, 100);
        public WalletImportReceipt Import;
        public WalletImportLookup Lookup = WalletImportLookup.Found;
        public int SourceReads;
        public bool AtomicDatabaseImportVerified => Atomic;
        public bool IsLegacyWriteFenceCurrent(string accountId, string fenceId) => ServerFence && accountId == "A" && fenceId == "fence";
        public bool AreLegacyWritesDrained(string accountId, string fenceId) => Drained;
        public bool IsRouteServerOwned(string accountId, string migrationId, WalletMigrationRoute route) => route != MissingRoute;
        public bool TryReadFrozenSource(string accountId, string fenceId, out FrozenLegacyWallet source)
        { SourceReads++; source = Source; if (RevokeDuringSourceRead) ServerFence = false; return true; }
        public WalletImportLookup ReadImport(string accountId, string importKey, out WalletImportReceipt receipt)
        {
            if (ThrowOnQuery) throw new InvalidOperationException("query timeout");
            receipt = Import; if (RevokeDuringImportRead) ServerFence = false; return Lookup;
        }
    }
}
#endif
