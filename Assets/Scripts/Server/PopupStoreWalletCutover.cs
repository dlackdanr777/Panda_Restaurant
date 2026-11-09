using System;
using System.Globalization;
using System.Text;

namespace PandaRestaurant.PopupStoreServer
{
    public enum WalletCutoverState { Frozen, ImportPending, RecoveryRequired, DatabaseOnly }
    public enum WalletMigrationRoute
    {
        Autosave, AccountLoad, Gacha, StaffGacha, ShopAndUpgrade, Attendance, Mail,
        Payment, Advertisement, Challenge, MiniGame, TutorialAndDebug
    }
    public enum WalletImportLookup { Unknown, Absent, Found }

    /// <summary>Server-read GameData source after a durable global write fence, never UserInfo.Dia.</summary>
    public sealed class FrozenLegacyWallet
    {
        public string AccountId { get; }
        public string RowId { get; }
        public string SnapshotDigest { get; }
        public int Balance { get; }
        public FrozenLegacyWallet(string accountId, string rowId, string snapshotDigest, int balance)
        { AccountId = accountId; RowId = rowId; SnapshotDigest = snapshotDigest; Balance = balance; }
        public bool IsValid => WalletCutoverCoordinator.ValidId(AccountId) && WalletCutoverCoordinator.ValidId(RowId)
            && IsDigest(SnapshotDigest) && Balance >= 0;
        public bool Same(FrozenLegacyWallet other) => other != null && AccountId == other.AccountId
            && RowId == other.RowId && SnapshotDigest == other.SnapshotDigest && Balance == other.Balance;
        private static bool IsDigest(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }

    /// <summary>
    /// Durable import receipt inserted atomically with the opening Database wallet. The opening balance
    /// is historical evidence, not a current-wallet query and must never be copied into UserInfo.
    /// </summary>
    public sealed class WalletImportReceipt
    {
        public string AccountId { get; }
        public string WalletId { get; }
        public string MigrationId { get; }
        public string ImportKey { get; }
        public string TransactionId { get; }
        public FrozenLegacyWallet Source { get; }
        public long OpeningVersion { get; }
        public WalletImportReceipt(string accountId, string walletId, string migrationId, string importKey,
            string transactionId, FrozenLegacyWallet source, long openingVersion)
        {
            AccountId = accountId; WalletId = walletId; MigrationId = migrationId; ImportKey = importKey;
            TransactionId = transactionId; Source = source; OpeningVersion = openingVersion;
        }
    }

    /// <summary>
    /// Trusted host port, deliberately lacking a production implementation. Every read must use server
    /// authentication and deployment evidence. A client flag, Inspector value, local save lease, or an
    /// old-client version string is not a fence. The fence must reject server writes from ALL clients,
    /// include already queued legacy operations, and remain active throughout and after migration.
    /// </summary>
    public interface IWalletCutoverHostProofs
    {
        string VerifiedWriterInventoryVersion { get; }
        bool IsLegacyWriteFenceCurrent(string accountId, string fenceId);
        bool AreLegacyWritesDrained(string accountId, string fenceId);
        bool IsRouteServerOwned(string accountId, string migrationId, WalletMigrationRoute route);
        bool AtomicDatabaseImportVerified { get; }
        bool TryReadFrozenSource(string accountId, string fenceId, out FrozenLegacyWallet source);
        WalletImportLookup ReadImport(string accountId, string importKey, out WalletImportReceipt receipt);
    }

    /// <summary>Immutable migration journal, stored only by a trusted host using Revision CAS.</summary>
    public sealed class WalletCutoverRecord
    {
        public string AccountId { get; }
        public string WalletId { get; }
        public string MigrationId { get; }
        public string FenceId { get; }
        public string ImportKey { get; }
        public string ImportTransactionId { get; }
        public FrozenLegacyWallet Source { get; }
        public WalletCutoverState State { get; }
        public long Revision { get; }
        internal WalletCutoverRecord(string account, string wallet, string migration, string fence,
            FrozenLegacyWallet source, WalletCutoverState state, long revision, string transactionId = null)
        {
            AccountId = account; WalletId = wallet; MigrationId = migration; FenceId = fence; Source = source;
            State = state; Revision = revision; ImportTransactionId = transactionId;
            ImportKey = WalletCutoverCoordinator.ImportKeyFor(account, migration);
        }
        internal WalletCutoverRecord Next(WalletCutoverState state, string transactionId = null)
            => new WalletCutoverRecord(AccountId, WalletId, MigrationId, FenceId, Source, state,
                checked(Revision + 1), transactionId ?? ImportTransactionId);

        // Identity/state eligibility only. The host MUST also call the coordinator's live proof check.
        public bool CanUseDatabaseWallet(string accountId, string walletId, string migrationId)
            => State == WalletCutoverState.DatabaseOnly && AccountId == accountId && WalletId == walletId
                && MigrationId == migrationId && WalletCutoverCoordinator.ValidId(ImportTransactionId);
        public bool AllowsLegacyDiaWrite => false; // No rollback to a second writer after a freeze.
    }

    public sealed class WalletCutoverTransition
    {
        public WalletCutoverRecord Before { get; }
        public WalletCutoverRecord After { get; }
        public long ExpectedRevision => Before == null ? -1 : Before.Revision;
        public bool RequiresInsert => Before == null;
        public bool RequiresCommit => !ReferenceEquals(Before, After);
        internal WalletCutoverTransition(WalletCutoverRecord before, WalletCutoverRecord after)
        { Before = before; After = after; }
    }

    /// <summary>
    /// Pure migration planner, not an importer or an enabled wallet boundary. The host persists every
    /// transition with unique account key + expected revision; it must never act on an uncommitted plan.
    /// ImportPending must be durable BEFORE submission. The import inserts an absent wallet + unique
    /// receipt in ONE Database transaction, then its receipt is queried. No cross-GameData atomicity is
    /// assumed: the verified server fence and drained writers make the source stable before that batch.
    /// All SDK calls, permissions, migration approvals and irreversible cutover remain external gates.
    /// </summary>
    public sealed class WalletCutoverCoordinator
    {
        public const string WriterInventoryVersion = "dia-writers-2026-11-09-v1";
        private readonly IWalletCutoverHostProofs _proofs;
        public WalletCutoverCoordinator(IWalletCutoverHostProofs proofs = null) { _proofs = proofs; }

        public bool TryFreeze(AuthenticatedPrincipal principal, string walletId, string migrationId, string fenceId,
            out WalletCutoverTransition transition, out string error)
        {
            transition = null;
            if (!ValidId(principal?.AccountId) || !ValidId(walletId) || !ValidId(migrationId) || !ValidId(fenceId))
            { error = "Authenticated migration identity is missing."; return false; }
            if (!Ready(principal.AccountId, migrationId, fenceId, out error)) return false;
            try
            {
                if (!_proofs.TryReadFrozenSource(principal.AccountId, fenceId, out var source)
                    || source?.IsValid != true || source.AccountId != principal.AccountId)
                { error = "The frozen server GameData source is unavailable or belongs to another account."; return false; }
                if (!Ready(principal.AccountId, migrationId, fenceId, out error)) return false;
                transition = new WalletCutoverTransition(null, new WalletCutoverRecord(principal.AccountId,
                    walletId, migrationId, fenceId, source, WalletCutoverState.Frozen, 0));
                return true;
            }
            catch (Exception) { error = "Migration evidence is unavailable; keep the fence closed."; return false; }
        }

        public bool TryPrepareImport(AuthenticatedPrincipal principal, WalletCutoverRecord current,
            out WalletCutoverTransition transition, out string error)
        {
            transition = null;
            if (!Current(principal, current, out error) || !Ready(current.AccountId, current.MigrationId, current.FenceId, out error)) return false;
            if (current.State != WalletCutoverState.Frozen)
            { error = "An import is already pending or resolved; query its original key instead of resubmitting."; return false; }
            try
            {
                if (!_proofs.TryReadFrozenSource(current.AccountId, current.FenceId, out var source) || !current.Source.Same(source))
                { error = "Legacy source changed after the freeze; no automatic rebase or replacement is allowed."; return false; }
                if (!Ready(current.AccountId, current.MigrationId, current.FenceId, out error)) return false;
                transition = new WalletCutoverTransition(current, current.Next(WalletCutoverState.ImportPending));
                return true;
            }
            catch (Exception) { error = "Import preparation could not be verified."; return false; }
        }

        public bool TryMarkImportUnknown(AuthenticatedPrincipal principal, WalletCutoverRecord current,
            out WalletCutoverTransition transition, out string error)
        {
            transition = null;
            if (!Current(principal, current, out error)) return false;
            if (current.State != WalletCutoverState.ImportPending && current.State != WalletCutoverState.RecoveryRequired)
            { error = "No submitted import is awaiting recovery."; return false; }
            transition = new WalletCutoverTransition(current, current.State == WalletCutoverState.RecoveryRequired
                ? current : current.Next(WalletCutoverState.RecoveryRequired));
            return true;
        }

        public bool TryConfirmImport(AuthenticatedPrincipal principal, WalletCutoverRecord current,
            out WalletCutoverTransition transition, out string error)
        {
            transition = null;
            if (!Current(principal, current, out error) || !Ready(current.AccountId, current.MigrationId, current.FenceId, out error)) return false;
            if (current.State == WalletCutoverState.Frozen)
            { error = "The durable import intent is missing."; return false; }
            try
            {
                if (_proofs.ReadImport(current.AccountId, current.ImportKey, out var receipt) != WalletImportLookup.Found
                    || receipt == null || receipt.AccountId != current.AccountId || receipt.WalletId != current.WalletId
                    || receipt.MigrationId != current.MigrationId || receipt.ImportKey != current.ImportKey
                    || !ValidId(receipt.TransactionId) || !current.Source.Same(receipt.Source) || receipt.OpeningVersion != 0
                    || (current.ImportTransactionId != null && current.ImportTransactionId != receipt.TransactionId))
                { error = "An exact atomic import receipt is not proven; keep the original migration pending."; return false; }
                if (!Ready(current.AccountId, current.MigrationId, current.FenceId, out error)) return false;
                transition = new WalletCutoverTransition(current, current.State == WalletCutoverState.DatabaseOnly
                    ? current : current.Next(WalletCutoverState.DatabaseOnly, receipt.TransactionId));
                return true;
            }
            catch (Exception) { error = "Import query is inconclusive; never enable the legacy writer again."; return false; }
        }

        public bool CanUseDatabaseWallet(AuthenticatedPrincipal principal, WalletCutoverRecord current,
            string walletId, string migrationId, out string error)
        {
            if (!Current(principal, current, out error)) return false;
            if (!current.CanUseDatabaseWallet(principal.AccountId, walletId, migrationId))
            { error = "This account has not completed the matching Database wallet cutover."; return false; }
            return Ready(current.AccountId, current.MigrationId, current.FenceId, out error);
        }

        private bool Ready(string accountId, string migrationId, string fenceId, out string error)
        {
            error = "Server wallet migration proof is unavailable.";
            if (_proofs == null) return false;
            try
            {
                if (_proofs.VerifiedWriterInventoryVersion != WriterInventoryVersion || !_proofs.AtomicDatabaseImportVerified
                    || !_proofs.IsLegacyWriteFenceCurrent(accountId, fenceId)
                    || !_proofs.AreLegacyWritesDrained(accountId, fenceId)) return false;
                foreach (WalletMigrationRoute route in Enum.GetValues(typeof(WalletMigrationRoute)))
                    if (!_proofs.IsRouteServerOwned(accountId, migrationId, route))
                    { error = "A legacy route is not server-owned: " + route; return false; }
                error = null; return true;
            }
            catch (Exception) { return false; }
        }
        private static bool Current(AuthenticatedPrincipal principal, WalletCutoverRecord current, out string error)
        {
            error = null;
            if (current != null && ValidId(principal?.AccountId) && principal.AccountId == current.AccountId) return true;
            error = "The migration does not belong to this authenticated account."; return false;
        }
        public static string ImportKeyFor(string accountId, string migrationId)
        {
            if (!ValidId(accountId) || !ValidId(migrationId)) throw new ArgumentException("Invalid import identity.");
            // Length prefix is unambiguous even when identifiers contain separators or non-ASCII text.
            return "wallet-import-v1:" + Part(accountId) + Part(migrationId);
        }
        private static string Part(string value) => Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture) + ":" + value;
        internal static bool ValidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return false;
            foreach (char c in value) if (char.IsControl(c) || char.IsSurrogate(c)) return false;
            return true;
        }
    }
}
