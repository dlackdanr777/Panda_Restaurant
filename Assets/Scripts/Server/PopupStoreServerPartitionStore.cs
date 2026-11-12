using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace PandaRestaurant.PopupStoreServer
{
    // Server persistence only: no Unity registration, SDK calls, account provisioning or legacy
    // GameData writes. The SDK port must implement all-or-nothing writes in ONE Database transaction.
    public sealed class PartitionPayload
    {
        public string Codec, Data, Sha256;
        public int DecodedBytes;
    }

    public sealed class PartitionRecord
    {
        public string Key, OwnerId, Kind, LogicalId, ParentId;
        public int Ordinal;
        public long Revision;
        public PartitionPayload Payload;
    }

    public sealed class PartitionWrite
    {
        public PartitionRecord Value;
        public bool Insert;
        public long ExpectedRevision;
    }

    public sealed class PartitionCommitResult
    {
        public bool Success;
        public int OperationCount, AffectedRows;
    }

    public interface IPartitionDatabasePort
    {
        PartitionRecord ReadRoot(string authenticatedAccountId);
        // All reads are outside the transaction. A truncated/mixed scan must fail the root manifest.
        IReadOnlyList<PartitionRecord> ReadRecords(string authenticatedAccountId, int maximumRecords);
        PartitionCommitResult Commit(string authenticatedAccountId, IReadOnlyList<PartitionWrite> writes);
    }

    public sealed class PartitionStoreVerification
    {
        public bool DevelopmentEnvironmentVerified, SchemaAndPermissionsVerified,
            AtomicPredicatesVerified, PayloadRoundTripVerified, AllWalletWritersMigrated;
        public string MigrationId;
        public bool AtomicReady => DevelopmentEnvironmentVerified && SchemaAndPermissionsVerified
            && AtomicPredicatesVerified && PayloadRoundTripVerified;
        public bool WalletReady => AtomicReady && AllWalletWritersMigrated && !string.IsNullOrWhiteSpace(MigrationId);
    }

    public sealed class PartitionOutcomeUnknownException : Exception
    {
        public PartitionOutcomeUnknownException() : base("partition_commit_outcome_unconfirmed") { }
    }

    /// <summary>One account CAS row plus independently sized daily/business/request/receipt/grant
    /// records. No pruning or response reduction: exact historical responses survive retries.
    /// The bounded full-account read preserves the existing authority interface; production history
    /// retention/indexing and measured read latency still require a separate operational decision.</summary>
    public static class PopupStorePartitionCodec
    {
        public const int MaximumRecords = 10000;
        public const int MaximumPayloadCharacters = 10000;
        public const int MaximumDecodedBytes = 1024 * 1024;
        public const int MaximumSnapshotDecodedBytes = 16 * 1024 * 1024;
        public const int MaximumWrites = 10;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, MaxDepth = 64 };

        [Serializable]
        private sealed class Header
        {
            public AccountState Wallet;
            public int RecordCount;
            public string Manifest;
        }

        public static string RecordKey(string owner, string kind, string parent, string id)
        {
            string result = "v2." + Segment(owner) + "." + Segment(kind) + "." + Segment(parent ?? "") + "." + Segment(id);
            if (result.Length > 4000) throw new InvalidOperationException("partition_key_capacity");
            return result;
        }

        public static string RootKey(string owner) => RecordKey(owner, "account", "", owner);

        public static IReadOnlyList<PartitionRecord> Encode(AccountState state, long revision)
        {
            ValidateAccount(state);
            if (revision < 1) throw new InvalidOperationException("partition_revision_invalid");
            var rows = new List<PartitionRecord>();
            for (int i = 0; i < state.Usage.Count; i++)
                Add(rows, state.AccountId, "day", "", state.Usage[i]?.GameDay, i, revision, state.Usage[i]);
            for (int i = 0; i < state.Businesses.Count; i++)
            {
                var source = state.Businesses[i];
                if (source?.View == null || source.Policy == null || source.Receipts == null
                    || source.View.AccountId != state.AccountId || source.View.Orders == null)
                    throw new InvalidOperationException("partition_business_invalid");
                var business = Clone(source);
                business.Receipts.Clear();
                string id = business.View.BusinessSessionId;
                Add(rows, state.AccountId, "business", "", id, i, revision, business);
                for (int j = 0; j < source.Receipts.Count; j++)
                    Add(rows, state.AccountId, "receipt", id, source.Receipts[j]?.ReceiptId, j, revision, source.Receipts[j]);
                if (source.View.Grant != null)
                {
                    if (source.View.Grant.GrantKey != PopupStoreServerAuthority.GrantKey(state.AccountId, id))
                        throw new InvalidOperationException("partition_grant_key_invalid");
                    Add(rows, state.AccountId, "grant", id, "popup-reward", 0, revision, source.View.Grant);
                }
            }
            for (int i = 0; i < state.Requests.Count; i++)
                Add(rows, state.AccountId, "request", "", state.Requests[i]?.RequestId, i, revision, state.Requests[i]);
            if (rows.Count >= MaximumRecords) throw new InvalidOperationException("partition_history_capacity");
            var wallet = state.Copy(); wallet.Usage.Clear(); wallet.Businesses.Clear(); wallet.Requests.Clear();
            var header = new Header { Wallet = wallet, RecordCount = rows.Count, Manifest = Manifest(rows) };
            Add(rows, state.AccountId, "account", "", state.AccountId, 0, revision, header);
            if (rows.Sum(x => (long)x.Payload.DecodedBytes) > MaximumSnapshotDecodedBytes)
                throw new InvalidOperationException("partition_snapshot_capacity");
            return rows.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        }

        public static AccountState Decode(string owner, PartitionRecord root, IReadOnlyList<PartitionRecord> records)
        {
            if (root == null || root.Key != RootKey(owner) || root.Kind != "account" || records == null
                || records.Count > MaximumRecords) throw new InvalidOperationException("partition_snapshot_invalid");
            ValidateRecord(owner, root);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long decodedTotal = 0;
            foreach (var row in records)
            {
                ValidateRecord(owner, row);
                if (row.Revision > root.Revision) throw new InvalidOperationException("partition_future_revision");
                if (row.Payload.DecodedBytes < 1 || row.Payload.DecodedBytes > MaximumDecodedBytes
                    || (decodedTotal += row.Payload.DecodedBytes) > MaximumSnapshotDecodedBytes)
                    throw new InvalidOperationException("partition_snapshot_capacity");
                if (!seen.Add(row.Key)) throw new InvalidOperationException("partition_duplicate_record");
            }
            var scannedRoot = records.SingleOrDefault(x => x.Kind == "account");
            if (!SameVersion(root, scannedRoot)) throw new InvalidOperationException("partition_snapshot_changed");
            var header = Unpack<Header>(root.Payload);
            var data = records.Where(x => x.Kind != "account").ToArray();
            if (header?.Wallet == null || header.RecordCount != data.Length || header.Manifest != Manifest(data))
                throw new InvalidOperationException("partition_snapshot_incomplete");
            var state = header.Wallet;
            ValidateAccount(state);
            if (state.AccountId != owner || state.Usage.Count != 0 || state.Businesses.Count != 0 || state.Requests.Count != 0)
                throw new InvalidOperationException("partition_header_invalid");
            foreach (var row in data.Where(x => x.Kind == "day").OrderBy(x => x.Ordinal)) state.Usage.Add(Unpack<DailyUsageState>(row.Payload));
            foreach (var row in data.Where(x => x.Kind == "business").OrderBy(x => x.Ordinal)) state.Businesses.Add(Unpack<BusinessState>(row.Payload));
            foreach (var row in data.Where(x => x.Kind == "request").OrderBy(x => x.Ordinal)) state.Requests.Add(Unpack<RequestState>(row.Payload));
            foreach (var row in data.Where(x => x.Kind == "receipt").OrderBy(x => x.Ordinal))
            {
                var business = state.Businesses.SingleOrDefault(x => x.View.BusinessSessionId == row.ParentId);
                if (business == null) throw new InvalidOperationException("partition_receipt_orphan");
                business.Receipts.Add(Unpack<ReceiptState>(row.Payload));
            }
            // Re-encoding validates identity, ordinals, immutable grant copies and unknown row kinds.
            var rebuilt = Encode(state, root.Revision);
            if (rebuilt.Count != records.Count || Manifest(rebuilt) != Manifest(records))
                throw new InvalidOperationException("partition_payload_identity_mismatch");
            return state;
        }

        public static IReadOnlyList<PartitionWrite> Plan(string owner, IReadOnlyList<PartitionRecord> observed, AccountState candidate)
        {
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            var oldRoot = observed.Single(x => x.Kind == "account");
            var original = Decode(owner, oldRoot, observed);
            ValidateAccount(candidate);
            if (candidate.AccountId != owner || candidate.WalletId != original.WalletId
                || candidate.WalletMigrationId != original.WalletMigrationId || candidate.WalletVersion < original.WalletVersion
                || candidate.Requests.Count != original.Requests.Count + 1)
                throw new InvalidOperationException("partition_candidate_invalid");
            var next = Encode(candidate, checked(oldRoot.Revision + 1));
            var oldByKey = observed.ToDictionary(x => x.Key, StringComparer.Ordinal);
            var newByKey = next.ToDictionary(x => x.Key, StringComparer.Ordinal);
            foreach (var old in observed)
                if (!newByKey.ContainsKey(old.Key)) throw new InvalidOperationException("partition_history_removal_forbidden");
            var writes = new List<PartitionWrite>();
            foreach (var row in next)
            {
                if (oldByKey.TryGetValue(row.Key, out var prior))
                {
                    if (row.Ordinal != prior.Ordinal) throw new InvalidOperationException("partition_history_reorder_forbidden");
                    if (row.Payload.Sha256 == prior.Payload.Sha256 && row.Kind != "account") continue;
                    if (row.Kind == "request" || row.Kind == "receipt" || row.Kind == "grant")
                        throw new InvalidOperationException("partition_immutable_record_changed");
                    writes.Add(new PartitionWrite { Value = row, ExpectedRevision = prior.Revision });
                }
                else writes.Add(new PartitionWrite { Value = row, Insert = true });
            }
            if (writes.Count < 2 || writes.Count > MaximumWrites)
                throw new InvalidOperationException("partition_transaction_capacity");
            var grants = writes.Where(x => x.Value.Kind == "grant").ToArray();
            if (grants.Length > 1) throw new InvalidOperationException("partition_wallet_grant_count");
            long delta = (long)candidate.Diamonds - original.Diamonds;
            if (grants.Length == 0 && (delta != 0 || candidate.WalletVersion != original.WalletVersion))
                throw new InvalidOperationException("partition_wallet_without_grant");
            if (grants.Length == 1)
            {
                var grant = Unpack<GrantSnapshot>(grants[0].Value.Payload);
                if (!grants[0].Insert || grant.Diamonds <= 0 || delta != grant.Diamonds
                    || original.WalletVersion == long.MaxValue || candidate.WalletVersion != original.WalletVersion + 1
                    || grant.WalletVersion != candidate.WalletVersion || grant.BalanceAfter != candidate.Diamonds)
                    throw new InvalidOperationException("partition_wallet_grant_mismatch");
            }
            return writes.OrderBy(x => x.Value.Kind == "account" ? 0 : 1).ThenBy(x => x.Value.Key, StringComparer.Ordinal).ToArray();
        }

        public static bool SameVersion(PartitionRecord first, PartitionRecord second) => first != null && second != null
            && first.Key == second.Key && first.OwnerId == second.OwnerId && first.Revision == second.Revision
            && first.Payload?.Sha256 == second.Payload?.Sha256;

        public static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value, JsonSettings), JsonSettings);

        private static void ValidateAccount(AccountState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.AccountId) || string.IsNullOrWhiteSpace(state.WalletId)
                || !state.WalletMigrationComplete || string.IsNullOrWhiteSpace(state.WalletMigrationId)
                || state.Diamonds < 0 || state.WalletVersion < 0 || state.Usage == null || state.Businesses == null || state.Requests == null)
                throw new InvalidOperationException("partition_account_invalid");
        }

        private static void Add<T>(List<PartitionRecord> rows, string owner, string kind, string parent, string id, int ordinal, long revision, T value)
        {
            if (value == null || string.IsNullOrEmpty(id)) throw new InvalidOperationException("partition_identity_missing");
            string key = RecordKey(owner, kind, parent, id);
            if (rows.Any(x => x.Key == key)) throw new InvalidOperationException("partition_identity_duplicate");
            rows.Add(new PartitionRecord { Key = key, OwnerId = owner, Kind = kind, ParentId = parent, LogicalId = id,
                Ordinal = ordinal, Revision = revision, Payload = Pack(value) });
        }

        private static void ValidateRecord(string owner, PartitionRecord row)
        {
            if (row == null || row.OwnerId != owner || row.Revision < 1 || row.Ordinal < 0 || row.Payload == null
                || row.Key != RecordKey(owner, row.Kind, row.ParentId, row.LogicalId))
                throw new InvalidOperationException("partition_record_invalid");
        }

        private static PartitionPayload Pack<T>(T value)
        {
            var bytes = Utf8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None, JsonSettings));
            if (bytes.Length > MaximumDecodedBytes) throw new InvalidOperationException("partition_decoded_capacity");
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var stream = new DeflateStream(output, CompressionLevel.Optimal, true)) stream.Write(bytes, 0, bytes.Length);
                compressed = output.ToArray();
            }
            string data = Base64(compressed);
            if (data.Length > MaximumPayloadCharacters) throw new InvalidOperationException("partition_record_capacity");
            return new PartitionPayload { Codec = "deflate-json-v1", Data = data, Sha256 = Hash(bytes), DecodedBytes = bytes.Length };
        }

        private static T Unpack<T>(PartitionPayload payload)
        {
            if (payload == null || payload.Codec != "deflate-json-v1" || payload.Data == null || payload.Data.Length > MaximumPayloadCharacters
                || payload.DecodedBytes < 1 || payload.DecodedBytes > MaximumDecodedBytes || payload.Sha256 == null || payload.Sha256.Length != 64)
                throw new InvalidOperationException("partition_payload_invalid");
            string encoded = payload.Data.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var compressed = Convert.FromBase64String(encoded);
            byte[] bytes;
            using (var input = new MemoryStream(compressed))
            using (var stream = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > payload.DecodedBytes) throw new InvalidOperationException("partition_decompression_limit");
                    output.Write(buffer, 0, count);
                }
                bytes = output.ToArray();
            }
            if (bytes.Length != payload.DecodedBytes || Hash(bytes) != payload.Sha256)
                throw new InvalidOperationException("partition_payload_integrity");
            return JsonConvert.DeserializeObject<T>(Utf8.GetString(bytes), JsonSettings);
        }

        private static string Manifest(IEnumerable<PartitionRecord> rows)
        {
            var value = new StringBuilder();
            foreach (var row in rows.OrderBy(x => x.Key, StringComparer.Ordinal))
                value.Append(row.Key).Append(':').Append(row.Ordinal.ToString(CultureInfo.InvariantCulture)).Append(':').Append(row.Payload.Sha256).Append('\n');
            return Hash(Utf8.GetBytes(value.ToString()));
        }
        private static string Segment(string value)
        { if (value == null || Utf8.GetByteCount(value) > 1024) throw new InvalidOperationException("partition_key_invalid"); return Base64(Utf8.GetBytes(value)); }
        private static string Base64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static string Hash(byte[] value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
    }

    public sealed class PopupStorePartitionAccountStore : IAtomicAccountStore
    {
        private readonly IPartitionDatabasePort _port;
        private readonly string _account;
        private readonly PartitionStoreVerification _verification;
        public PopupStorePartitionAccountStore(IPartitionDatabasePort port, AuthenticatedPrincipal principal, PartitionStoreVerification verification = null)
        { _port = port ?? throw new ArgumentNullException(nameof(port)); _account = principal?.AccountId; _verification = verification ?? new PartitionStoreVerification(); }
        public bool AtomicAccountTransactionsVerified => _verification.AtomicReady;
        public bool FullWalletMigrationVerified => _verification.WalletReady;
        public AccountState Read(string accountId) { var rows = ReadStable(accountId); return rows == null ? null : Decode(accountId, rows); }
        public Response Transact(string accountId, Func<AccountState, AtomicMutation> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            var rows = ReadStable(accountId);
            var original = rows == null ? null : Decode(accountId, rows);
            var mutation = operation(original?.Copy());
            if (mutation?.Response == null) throw new InvalidOperationException("partition_mutation_invalid");
            if (!mutation.Commit) return mutation.Response;
            if (rows == null) throw new InvalidOperationException("partition_provisioning_not_supported");
            var writes = PopupStorePartitionCodec.Plan(accountId, rows, mutation.State);
            RequireReady(accountId);
            try
            {
                var result = _port.Commit(accountId, writes);
                if (result == null || !result.Success || result.OperationCount != writes.Count || result.AffectedRows != writes.Count)
                    throw new PartitionOutcomeUnknownException();
            }
            catch { throw new PartitionOutcomeUnknownException(); }
            return mutation.Response;
        }
        private IReadOnlyList<PartitionRecord> ReadStable(string accountId)
        {
            RequireReady(accountId);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var first = _port.ReadRoot(accountId);
                if (first == null) return null;
                var records = _port.ReadRecords(accountId, PopupStorePartitionCodec.MaximumRecords);
                var last = _port.ReadRoot(accountId);
                if (!PopupStorePartitionCodec.SameVersion(first, last)) continue;
                if (records == null || !PopupStorePartitionCodec.SameVersion(first, records.SingleOrDefault(x => x.Kind == "account")))
                    throw new InvalidOperationException("partition_scan_revision_mismatch");
                Decode(accountId, records);
                return records;
            }
            throw new InvalidOperationException("partition_read_contention");
        }
        private AccountState Decode(string owner, IReadOnlyList<PartitionRecord> records)
        {
            var state = PopupStorePartitionCodec.Decode(owner, records?.SingleOrDefault(x => x.Kind == "account"), records);
            if (state.WalletMigrationId != _verification.MigrationId) throw new InvalidOperationException("partition_migration_mismatch");
            return state;
        }
        private void RequireReady(string accountId)
        {
            if (!AtomicAccountTransactionsVerified || !FullWalletMigrationVerified) throw new InvalidOperationException("partition_deployment_unverified");
            if (string.IsNullOrWhiteSpace(_account) || _account != accountId) throw new InvalidOperationException("partition_authenticated_account_mismatch");
        }
    }
}
