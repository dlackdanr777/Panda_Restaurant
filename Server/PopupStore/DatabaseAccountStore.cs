// SERVER-ONLY ADAPTER. Documented Function 1.1.1 / Database 1.0.0 surface;
// actual SDK assembly build and development-server transactions are still unverified.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BACKND.Database;
using PandaRestaurant.PopupStoreServer;

namespace BackendFunction
{
    // One physical user table, logically separated by Kind. No composite PK assumption.
    // Root/wallet, day, business, request, receipt and grant share the SAME Database transaction.
    // Existing GameData is NOT in that transaction. Never create/alter this table automatically.
    [Table("popup_store_records_v2", TableType.UserTable,
        ReadPermissions = new[] { TablePermission.SELF }, WritePermissions = new TablePermission[0])]
    public sealed class PopupStoreRecordRow : BaseModel
    {
        [PrimaryKey]
        [Column(NotNull = true)] public string Key { get; set; }
        [Column(NotNull = true)] public string OwnerId { get; set; }
        [Column(NotNull = true)] public string Kind { get; set; }
        [Column(NotNull = true)] public string LogicalId { get; set; }
        [Column(NotNull = true)] public string ParentId { get; set; }
        [Column(NotNull = true)] public int Ordinal { get; set; }
        [Column(NotNull = true)] public long Revision { get; set; }
        [Column(NotNull = true)] public PopupStoreRecordDocument Payload { get; set; }
    }

    public sealed class PopupStoreRecordDocument
    {
        public string Codec { get; set; }
        public string Data { get; set; }
        public string Sha256 { get; set; }
        public int DecodedBytes { get; set; }
    }

    /// <summary>Trusted deployment evidence only. Local fake tests do not satisfy these flags.
    /// All wallet writers and legacy-client fences must be verified before enabling writes.</summary>
    public sealed class PopupStoreDatabaseVerification
    {
        public bool DevelopmentEnvironmentVerified { get; }
        public bool SchemaAndPermissionsVerified { get; }
        public bool AtomicPredicatesVerified { get; }
        public bool PayloadRoundTripVerified { get; }
        public bool AllWalletWritersMigrated { get; }
        public string ExpectedMigrationId { get; }
        public PopupStoreDatabaseVerification(bool developmentEnvironmentVerified = false,
            bool schemaAndPermissionsVerified = false, bool atomicPredicatesVerified = false,
            bool payloadRoundTripVerified = false, bool allWalletWritersMigrated = false,
            string expectedMigrationId = null)
        {
            DevelopmentEnvironmentVerified = developmentEnvironmentVerified;
            SchemaAndPermissionsVerified = schemaAndPermissionsVerified;
            AtomicPredicatesVerified = atomicPredicatesVerified;
            PayloadRoundTripVerified = payloadRoundTripVerified;
            AllWalletWritersMigrated = allWalletWritersMigrated;
            ExpectedMigrationId = expectedMigrationId;
        }
        internal PartitionStoreVerification PartitionEvidence() => new PartitionStoreVerification
        {
            DevelopmentEnvironmentVerified = DevelopmentEnvironmentVerified,
            SchemaAndPermissionsVerified = SchemaAndPermissionsVerified,
            AtomicPredicatesVerified = AtomicPredicatesVerified,
            PayloadRoundTripVerified = PayloadRoundTripVerified,
            AllWalletWritersMigrated = AllWalletWritersMigrated,
            MigrationId = ExpectedMigrationId
        };
    }

    /// <summary>Authenticated per-invocation adapter. The existing authority stays unchanged.
    /// A complete manifest-checked scan is bounded at 10,000 records; capacity/latency is an explicit
    /// deployment review gate, not silent history truncation. No migration/provisioning is exposed.</summary>
    public sealed class DatabaseAccountStore : IAtomicAccountStore
    {
        private readonly PopupStorePartitionAccountStore _store;
        public DatabaseAccountStore(Client database, AuthenticatedPrincipal trustedPrincipal,
            PopupStoreDatabaseVerification verification)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var evidence = verification ?? new PopupStoreDatabaseVerification();
            _store = new PopupStorePartitionAccountStore(new DatabasePort(database, trustedPrincipal?.AccountId),
                trustedPrincipal, evidence.PartitionEvidence());
        }
        public bool AtomicAccountTransactionsVerified => _store.AtomicAccountTransactionsVerified;
        public bool FullWalletMigrationVerified => _store.FullWalletMigrationVerified;
        public AccountState Read(string accountId) { RequireServerContext(); return _store.Read(accountId); }
        public Response Transact(string accountId, Func<AccountState, AtomicMutation> operation)
        { RequireServerContext(); return _store.Transact(accountId, operation); }
        private static void RequireServerContext()
        {
            if (SynchronizationContext.Current != null)
                throw new InvalidOperationException("synchronous_database_bridge_requires_server_context");
        }

        private sealed class DatabasePort : IPartitionDatabasePort
        {
            private readonly Client _database;
            private readonly string _caller;
            internal DatabasePort(Client database, string caller) { _database = database; _caller = caller; }
            public PartitionRecord ReadRoot(string accountId)
            {
                RequireCaller(accountId);
                string key = PopupStorePartitionCodec.RootKey(accountId);
                string owner = accountId;
                var row = _database.From<PopupStoreRecordRow>().OfCurrentUser()
                    .Where(x => x.Key == key && x.OwnerId == owner).FirstOrDefault().GetAwaiter().GetResult();
                return FromRow(row);
            }
            public IReadOnlyList<PartitionRecord> ReadRecords(string accountId, int maximumRecords)
            {
                RequireCaller(accountId);
                string owner = accountId;
                // Take/ToList are documented. Request one extra row so a history capacity breach
                // is visible. Manifest validation also rejects any SDK/server pagination truncation.
                var rows = _database.From<PopupStoreRecordRow>().OfCurrentUser()
                    .Where(x => x.OwnerId == owner).Take(checked(maximumRecords + 1)).ToList().GetAwaiter().GetResult();
                if (rows == null) throw new InvalidOperationException("database_scan_missing");
                return rows.Select(FromRow).ToArray();
            }
            public PartitionCommitResult Commit(string accountId, IReadOnlyList<PartitionWrite> writes)
            {
                RequireCaller(accountId);
                if (writes == null || writes.Count < 2 || writes.Count > PopupStorePartitionCodec.MaximumWrites
                    || writes.Select(x => x.Value.Key).Distinct(StringComparer.Ordinal).Count() != writes.Count)
                    throw new InvalidOperationException("database_transaction_shape");
                var transaction = _database.Transaction();
                foreach (var write in writes)
                {
                    var row = ToRow(write.Value);
                    if (row.OwnerId != accountId) throw new InvalidOperationException("database_owner_mismatch");
                    if (write.Insert)
                        transaction.From<PopupStoreRecordRow>().OfCurrentUser().Insert(row);
                    else
                    {
                        string key = row.Key, owner = accountId;
                        long revision = write.ExpectedRevision;
                        transaction.From<PopupStoreRecordRow>().OfCurrentUser()
                            .Where(x => x.Key == key && x.OwnerId == owner && x.Revision == revision).Update(row);
                    }
                }
                // A failed predicate, missing row or duplicate insert must roll back ALL writes.
                // These semantics are documented, but must still be demonstrated on the dev DB.
                // Never parse Error text to decide a safe retry. Missing/odd replies remain unknown.
                var result = transaction.Commit().GetAwaiter().GetResult();
                return new PartitionCommitResult { Success = result.Success,
                    OperationCount = result.OperationCount, AffectedRows = result.TotalAffectedRows };
            }
            private void RequireCaller(string owner)
            {
                if (string.IsNullOrWhiteSpace(_caller) || owner != _caller)
                    throw new InvalidOperationException("authenticated_account_mismatch");
            }
            private static PartitionRecord FromRow(PopupStoreRecordRow row) => row == null ? null : new PartitionRecord
            {
                Key = row.Key, OwnerId = row.OwnerId, Kind = row.Kind, LogicalId = row.LogicalId,
                ParentId = row.ParentId, Ordinal = row.Ordinal, Revision = row.Revision,
                Payload = row.Payload == null ? null : new PartitionPayload { Codec = row.Payload.Codec,
                    Data = row.Payload.Data, Sha256 = row.Payload.Sha256, DecodedBytes = row.Payload.DecodedBytes }
            };
            private static PopupStoreRecordRow ToRow(PartitionRecord row) => new PopupStoreRecordRow
            {
                Key = row.Key, OwnerId = row.OwnerId, Kind = row.Kind, LogicalId = row.LogicalId,
                ParentId = row.ParentId, Ordinal = row.Ordinal, Revision = row.Revision,
                Payload = new PopupStoreRecordDocument { Codec = row.Payload.Codec, Data = row.Payload.Data,
                    Sha256 = row.Payload.Sha256, DecodedBytes = row.Payload.DecodedBytes }
            };
        }
    }
}
