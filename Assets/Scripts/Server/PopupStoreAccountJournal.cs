using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public interface IPopupStoreAccountClock
    { void Query(Action<DateTime> success, Action<string> failed); }

    public interface IPopupStoreAccountMemory
    {
        int Diamonds { get; }
        void SetRecord(string json);
        bool TryReserveReward(object owner, out string error);
        bool TryCommitReward(object owner, int amount, out string error);
        void ReleaseReward(object owner);
        void NotifyReward();
    }
    public interface IPopupStoreAccountJournalStore
    {
        bool TryRead(GameDataSaveTarget target, out PopupStoreAccountIntent intent, out string error);
        bool TryWrite(PopupStoreAccountIntent intent, out string error);
        bool TryClear(GameDataSaveTarget target, string requestId, out string error);
    }

    /// <summary>A local recovery hint, never proof of server commitment or payout authorization.</summary>
    public sealed class PopupStoreAccountIntent
    {
        public GameDataSaveTarget Target { get; }
        public string RequestId { get; }
        public PopupStoreGameDataRecord Source { get; }
        public PopupStoreGameDataRecord Candidate { get; }
        public string PayloadJson { get; }
        public bool DefinitelyNotSent { get; }
        public PopupStoreAccountIntent(GameDataSaveTarget target, string requestId, PopupStoreGameDataRecord source,
            PopupStoreGameDataRecord candidate, string payloadJson, bool definitelyNotSent = false)
        {
            if (target == null || !target.IsValid || string.IsNullOrWhiteSpace(requestId)
                || source == null || !source.IsValidSuccessor(candidate) || string.IsNullOrWhiteSpace(payloadJson)
                || !PopupStoreGameDataOperation.MatchesLogicalRequest(requestId, candidate.LastRequestId) || payloadJson.Length > 30000)
                throw new ArgumentException("Invalid popup recovery intent.");
            JObject payload;
            using (var text = new StringReader(payloadJson))
            using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
            {
                payload = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException();
            }
            if (payload[PopupStoreGameDataRecord.FieldName]?.Type != JTokenType.String
                || (string)payload[PopupStoreGameDataRecord.FieldName] != candidate.ToJson())
                throw new ArgumentException("Recovery payload and record differ.");
            bool claim = candidate.State == PopupStoreGameDataState.Claimed;
            if (payload.Count != (claim ? 2 : 1) || (claim && (payload["Dia"]?.Type != JTokenType.Integer
                || (long)payload["Dia"] < candidate.Diamonds || (long)payload["Dia"] > int.MaxValue)))
                throw new ArgumentException("Invalid recovery balance payload.");
            Target = target; RequestId = requestId; Source = source; Candidate = candidate;
            PayloadJson = payloadJson; DefinitelyNotSent = definitelyNotSent;
        }
        public string ToJson() => new JObject {
            ["Version"] = 1, ["Account"] = Target.AccountInDate, ["Row"] = Target.RowInDate,
            ["Request"] = RequestId, ["Source"] = Source.ToJson(), ["Candidate"] = Candidate.ToJson(),
            ["Payload"] = PayloadJson, ["NotSent"] = DefinitelyNotSent }.ToString(Formatting.None);
        public static PopupStoreAccountIntent Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 60000) throw new FormatException();
            JObject value;
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
            {
                value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException();
            }
            if (value.Count != 8 || value["Version"]?.Type != JTokenType.Integer || (int)value["Version"] != 1
                || value["NotSent"]?.Type != JTokenType.Boolean) throw new FormatException();
            foreach (var field in new[] { "Account", "Row", "Request", "Source", "Candidate", "Payload" })
                if (value[field]?.Type != JTokenType.String) throw new FormatException();
            if (!PopupStoreGameDataRecord.TryRead((string)value["Source"], true, out var source, out _)
                || !PopupStoreGameDataRecord.TryRead((string)value["Candidate"], true, out var candidate, out _)) throw new FormatException();
            return new PopupStoreAccountIntent(new GameDataSaveTarget((string)value["Account"], (string)value["Row"]),
                (string)value["Request"], source, candidate, (string)value["Payload"], (bool)value["NotSent"]);
        }
    }

    /// <summary>Atomic local intent scoped to the original GameData owner and row. Contains no credentials.</summary>
    public sealed class PopupStoreAccountFileJournal : IPopupStoreAccountJournalStore
    {
        private static readonly object ProcessGate = new object();
        private readonly string _directory;
        public PopupStoreAccountFileJournal(string directory) { _directory = Path.GetFullPath(directory); }
        private string PathFor(GameDataSaveTarget target)
        {
            if (target == null || !target.IsValid) throw new ArgumentException("A restored target is required.");
            using (var sha = SHA256.Create())
            {
                string key = target.AccountInDate.Length + ":" + target.AccountInDate + ":" + target.RowInDate;
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
                return Path.Combine(_directory, hash + ".json");
            }
        }
        public bool TryRead(GameDataSaveTarget target, out PopupStoreAccountIntent intent, out string error)
        { lock (ProcessGate) return TryReadCore(target, out intent, out error); }
        private bool TryReadCore(GameDataSaveTarget target, out PopupStoreAccountIntent intent, out string error)
        {
            intent = null; error = null;
            try
            {
                string path = PathFor(target);
                string json;
                try { json = File.ReadAllText(path, Encoding.UTF8); }
                catch (FileNotFoundException) { return true; }
                catch (DirectoryNotFoundException) { return true; }
                intent = PopupStoreAccountIntent.Read(json);
                if (!target.Matches(intent.Target)) throw new FormatException();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
            { intent = null; error = "저장 확인 기록을 읽을 수 없습니다. 기존 기록을 초기화하지 않습니다."; return false; }
        }
        public bool TryWrite(PopupStoreAccountIntent intent, out string error)
        {
            error = null;
            try
            {
                lock (ProcessGate)
                {
                    if (intent == null) throw new ArgumentException();
                    Directory.CreateDirectory(_directory);
                    string path = PathFor(intent.Target), temporary = path + ".pending";
                    using (var fileGate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (!TryReadCore(intent.Target, out var current, out error)) return false;
                        if (current != null && (!current.Source.Matches(intent.Source) || !current.Candidate.Matches(intent.Candidate)
                            || (!current.DefinitelyNotSent && (current.RequestId != intent.RequestId || current.PayloadJson != intent.PayloadJson))
                            || (current.RequestId == intent.RequestId && current.PayloadJson != intent.PayloadJson)))
                        { error = "미확정 복구 기록을 다른 영업이나 지급 요청으로 덮어쓸 수 없습니다."; return false; }
                        byte[] bytes = Encoding.UTF8.GetBytes(intent.ToJson());
                        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                        if (File.Exists(path)) File.Replace(temporary, path, null);
                        else File.Move(temporary, path);
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { error = "복구 기록을 안전하게 보관하지 못해 서버 저장을 시작하지 않습니다."; return false; }
        }
        public bool TryClear(GameDataSaveTarget target, string requestId, out string error)
        {
            error = null;
            try
            {
                lock (ProcessGate)
                {
                    string path = PathFor(target);
                    if (!Directory.Exists(_directory)) return true;
                    using (var fileGate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (!TryReadCore(target, out var current, out error)) return false;
                        if (current == null) return true;
                        if (current.RequestId != requestId) { error = "복구 기록의 요청이 변경되었습니다."; return false; }
                        File.Delete(path); return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { error = "확정된 복구 기록 정리가 지연됐습니다."; return false; }
        }
    }
}
