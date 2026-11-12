using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Muks.BackEnd
{
    /// <summary>Non-secret pending command only. Never a server receipt, balance, auth token or grant authority.</summary>
    public sealed class PopupStorePendingServerRequest
    {
        public string RequestId { get; }
        public string CommandJson { get; }
        public PopupStorePendingServerRequest(string requestId, string commandJson)
        {
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128
                || string.IsNullOrWhiteSpace(commandJson) || commandJson.Length > 32768)
                throw new ArgumentException("Invalid pending command.");
            RequestId = requestId; CommandJson = commandJson;
        }
        public bool Matches(PopupStorePendingServerRequest other) => other != null
            && RequestId == other.RequestId && CommandJson == other.CommandJson;
    }

    public interface IPopupStoreServerRequestJournal
    {
        // Corruption throws: never silently convert an unreadable pending operation into no operation.
        PopupStorePendingServerRequest Read(string accountId);
        // True only for an atomic new create. False means the exact identity already exists;
        // that older operation may have been sent by another owner and must remain recoverable.
        bool Prepare(string accountId, PopupStorePendingServerRequest request);
        void Complete(string accountId, PopupStorePendingServerRequest expected);
    }

    /// <summary>Explicitly constructed durable retry journal. A checksum detects torn/corrupt local
    /// state, NOT malicious modification. Server QUERY and authentication remain authoritative even
    /// if this file is deleted. No credentials are accepted by the typed command adapter.</summary>
    public sealed class PopupStoreServerFileJournal : IPopupStoreServerRequestJournal
    {
        private const int Format = 1, MaximumBytes = 140000;
        private readonly string _directory;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public PopupStoreServerFileJournal(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Journal directory required.");
            _directory = Path.GetFullPath(directory);
        }

        public PopupStorePendingServerRequest Read(string accountId)
        {
            string key = AccountKey(accountId);
            if (!Directory.Exists(_directory)) return null;
            using (Acquire(key)) return ReadLocked(key);
        }

        public bool Prepare(string accountId, PopupStorePendingServerRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string key = AccountKey(accountId);
            Directory.CreateDirectory(_directory);
            using (Acquire(key))
            {
                var existing = ReadLocked(key);
                if (existing != null)
                {
                    if (!existing.Matches(request)) throw new InvalidOperationException("Recover the existing server request first.");
                    return false;
                }
                byte[] payload;
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Utf8, true))
                    { writer.Write(Format); writer.Write(key); writer.Write(request.RequestId); writer.Write(request.CommandJson); }
                    payload = stream.ToArray();
                }
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
                string temporary = Path.Combine(_directory, key + "." + Guid.NewGuid().ToString("N") + ".pending");
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(hash, 0, hash.Length); file.Write(payload, 0, payload.Length); file.Flush(true); }
                    // Same-directory atomic rename, no delete/replace window and no backup file.
                    File.Move(temporary, FilePath(key));
                    return true;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public void Complete(string accountId, PopupStorePendingServerRequest expected)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            string key = AccountKey(accountId);
            if (!Directory.Exists(_directory)) return;
            using (Acquire(key))
            {
                var current = ReadLocked(key);
                if (current == null) return;
                if (!current.Matches(expected)) throw new InvalidOperationException("Pending identity changed; recovery required.");
                File.Delete(FilePath(key));
            }
        }

        private PopupStorePendingServerRequest ReadLocked(string key)
        {
            string path = FilePath(key);
            if (!File.Exists(path)) return null;
            var info = new FileInfo(path);
            if (info.Length < 40 || info.Length > MaximumBytes) throw new InvalidDataException("Invalid pending journal length.");
            byte[] bytes = File.ReadAllBytes(path);
            byte[] digest;
            using (var sha = SHA256.Create()) digest = sha.ComputeHash(bytes, 32, bytes.Length - 32);
            int mismatch = 0;
            for (int i = 0; i < 32; i++) mismatch |= bytes[i] ^ digest[i];
            if (mismatch != 0) throw new InvalidDataException("Pending journal checksum mismatch; query server before continuing.");
            using (var stream = new MemoryStream(bytes, 32, bytes.Length - 32, false))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                if (reader.ReadInt32() != Format || reader.ReadString() != key)
                    throw new InvalidDataException("Pending journal format/account mismatch.");
                var result = new PopupStorePendingServerRequest(reader.ReadString(), reader.ReadString());
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected pending journal trailing data.");
                return result;
            }
        }

        private FileStream Acquire(string key)
            => new FileStream(Path.Combine(_directory, key + ".guard"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        private string FilePath(string key) => Path.Combine(_directory, key + ".request");
        private static string AccountKey(string account)
        {
            if (string.IsNullOrWhiteSpace(account) || account.Length > 1024) throw new ArgumentException("Authenticated account required.");
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(account))).Replace("-", "").ToLowerInvariant();
        }
    }
}
