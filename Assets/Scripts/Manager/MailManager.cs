using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>?ㅻ걹 ?고렪(硫붿씪?? ?쒖뒪?쒖쓣 愿由ы븯???깃???留ㅻ땲?</summary>
public class MailManager : MonoBehaviour
{
    public static MailManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("MailManager");
                _instance = obj.AddComponent<MailManager>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    private static MailManager _instance;

    /// <summary>硫붿씪 紐⑸줉??媛깆떊?????몄텧?⑸땲??/summary>
    public event Action OnMailListRefreshed;

    /// <summary>誘몄닔??硫붿씪 議댁옱 ?щ?媛 諛붾????몄텧?⑸땲??(true = ?뚮엺 ?덉쓬)</summary>
    public event Action<bool> OnAlarmChanged;

    /// <summary>硫붿씪 ??嫄??섎졊 ?꾨즺 ???몄텧?⑸땲??/summary>
    public event Action<MailData> OnMailReceived;

    /// <summary>?꾩껜 硫붿씪 ?섎졊 ?꾨즺 ???몄텧?⑸땲??/summary>
    public event Action OnAllMailReceived;

    private const string HISTORY_TABLE = "MailHistory";

    private List<MailData> _mailList = new List<MailData>();
    public IReadOnlyList<MailData> MailList => _mailList;
    private MailReceiveOperation _receiveOperation;
    private IMailReceiveEnvironment _receiveEnvironment;
    private IMailReceiveTransport _receiveTransport;
    private Func<GameDataRestoreQuery> _readReceiveQuery;
    private Func<GameDataRestoreQuery, bool> _isReceiveQueryCurrent;
    private Func<MailData, bool> _isReceiveExpired;
    private Action<MailData> _recordMemoryApplied;
    private IReadOnlyList<MailData> _receivingMails;
    private Action _receiveSucceeded;
    private Action _receiveFailed;
    private bool _receiveNotificationSent;
    private int _mailListLoadSerial;
    public MailReceiveOperation CurrentReceiveOperation => _receiveOperation;
    public GameDataSaveRequest CurrentReceiveSaveRequest { get; private set; }
    public string LastReceiveError { get; private set; }

    // The operation outlives all mailbox windows. This boundary also permits account-free EditMode fakes.
    public void ConfigureReceiveBoundary(IMailReceiveEnvironment environment, IMailReceiveTransport transport,
        Func<GameDataRestoreQuery> readQuery, Func<GameDataRestoreQuery, bool> isCurrent,
        Func<MailData, bool> isExpired, Action<MailData> recordMemoryApplied)
    {
        if (_receiveOperation != null) throw new InvalidOperationException("An existing mail operation cannot be reset.");
        _receiveEnvironment = environment ?? throw new ArgumentNullException(nameof(environment));
        _receiveTransport = transport ?? throw new ArgumentNullException(nameof(transport));
        _readReceiveQuery = readQuery ?? throw new ArgumentNullException(nameof(readQuery));
        _isReceiveQueryCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        _isReceiveExpired = isExpired ?? throw new ArgumentNullException(nameof(isExpired));
        _recordMemoryApplied = recordMemoryApplied ?? throw new ArgumentNullException(nameof(recordMemoryApplied));
    }

    private GameDataRestoreQuery ReadReceiveQuery() => _readReceiveQuery != null
        ? _readReceiveQuery() : BackendManager.Instance.CurrentMailReceiveQuery;
    private bool IsReceiveCurrent(GameDataRestoreQuery query) => query != null &&
        (_isReceiveQueryCurrent != null ? _isReceiveQueryCurrent(query) : BackendManager.Instance.IsCurrentGameDataQuery(query));
    private bool IsReceiveExpired(MailData mail) => _isReceiveExpired != null ? _isReceiveExpired(mail) : mail.IsExpired;

    public bool CanReceive(MailData mail)
    {
        if (mail == null || mail.IsReceived || !mail.HasValidRewardManifest || IsReceiveExpired(mail)
            || (_receiveOperation != null && _receiveOperation.IsBusy) || !IsReceiveCurrent(mail.SourceQuery)) return false;
        return _receiveOperation?.GetKnownEntry(mail.PostType, mail.InDate, mail.SourceQuery.AccountInDate) == null;
    }

    public string GetReceiveStatusText(MailData mail)
    {
        if (mail == null) return string.Empty;
        var entry = mail.SourceQuery == null ? null :
            _receiveOperation?.GetKnownEntry(mail.PostType, mail.InDate, mail.SourceQuery.AccountInDate);
        if (entry != null)
        {
            switch (entry.Status)
            {
                case MailReceiveEntryStatus.Receiving: return "수령 처리 중";
                case MailReceiveEntryStatus.Applying: return "보상 메모리 반영 중";
                case MailReceiveEntryStatus.AppliedInMemory: return "메모리 반영 완료 · 저장 확인 별도";
                case MailReceiveEntryStatus.Indeterminate: return "서버 수령 여부 확인 필요";
                case MailReceiveEntryStatus.ConsumedPendingApplication: return "수령 후 보상 반영 확인 필요";
            }
        }
        if (mail.IsReceived) return "서버 수령 기록 · 보상 저장 확인 별도";
        if (!mail.HasValidRewardManifest) return "보상 원본 확인 필요";
        if (!IsReceiveCurrent(mail.SourceQuery)) return "계정·목록 재확인 필요";
        return string.Empty;
    }

    public bool IsReceiveDisplayComplete(MailData mail)
    {
        if (mail == null) return false;
        var entry = mail.SourceQuery == null ? null :
            _receiveOperation?.GetKnownEntry(mail.PostType, mail.InDate, mail.SourceQuery.AccountInDate);
        return entry == null ? mail.IsReceived : entry.Status == MailReceiveEntryStatus.AppliedInMemory;
    }

    /// <summary>이미 MailHistory에 저장된 우편의 originalInDate 모음</summary>
    private HashSet<string> _historyOrigInDates = new HashSet<string>();

    /// <summary>사용자가 삭제(숨김) 처리한 우편의 originalInDate 모음</summary>
    private HashSet<string> _hiddenOrigInDates = new HashSet<string>();

    /// <summary>愿由ъ옄 ?고렪 以??섎졊 媛?ν븳(誘몄닔?뮤룸?留뚮즺) 媛쒖닔</summary>
    public int UnreceivedCount
    {
        get
        {
            int count = 0;
            foreach (var mail in _mailList)
                if (CanReceive(mail)) count++;
            return count;
        }
    }

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>硫붿씪 紐⑸줉 媛깆떊 + ?뚮엺 ?대깽?몃? ?④퍡 諛쒖깮?쒗궢?덈떎</summary>
    private void InvokeRefreshed()
    {
        OnMailListRefreshed?.Invoke();
        OnAlarmChanged?.Invoke(UnreceivedCount > 0);
    }

    // ?????????????????????????????????????????
    #region 硫붿씪 紐⑸줉 議고쉶 (愿由ъ옄 + 荑좏룿 ?숈떆 濡쒕뱶)

    /// <summary>愿由ъ옄 ?고렪怨?荑좏룿 ?고렪??紐⑤몢 鍮꾨룞湲곕줈 媛?몄샃?덈떎</summary>
    public void LoadMailListAsync(Action onSuccess = null, Action onFail = null)
    {
        GameDataRestoreQuery query = ReadReceiveQuery();
        int serial = ++_mailListLoadSerial;
        Func<bool> current = () => serial == _mailListLoadSerial && IsReceiveCurrent(query);
        if (!current()) { onFail?.Invoke(); return; }
        _mailList.Clear();
        _historyOrigInDates.Clear();
        _hiddenOrigInDates.Clear();

        // Admin ??Coupon ???섎졊 ?대젰 ?쒖쑝濡?濡쒕뱶
        LoadByTypeAsync(PostType.Admin, query, current, () =>
        {
            LoadByTypeAsync(PostType.Coupon, query, current, () =>
            {
                LoadReceivedHistoryAsync(query, current, () =>
                {                    SaveNewMailsToHistory();                    RemoveHiddenMailsFromList();                    onSuccess?.Invoke();
                    InvokeRefreshed();
                    Debug.Log($"[MailManager] ?꾩껜 硫붿씪 {_mailList.Count}嫄?濡쒕뱶 ?꾨즺 (誘몄닔?? {UnreceivedCount}嫄?");
                });
            },
            // 荑좏룿 ?ㅽ뙣?대룄 Admin + ?대젰 濡쒕뱶
            () =>
            {
                LoadReceivedHistoryAsync(query, current, () =>
                {
                    SaveNewMailsToHistory();
                    RemoveHiddenMailsFromList();
                    onSuccess?.Invoke();
                    InvokeRefreshed();
                });
            });
        }, onFail);
    }

    private void LoadReceivedHistoryAsync(GameDataRestoreQuery query, Func<bool> current, Action onDone)
    {
        Where where = new Where();
        where.Equal("owner_inDate", query.AccountInDate);

        BackendManager.Instance.ProcessBackendAPI(
            $"{HISTORY_TABLE} 議고쉶",
            callback => Backend.GameData.Get(HISTORY_TABLE, where, bro => callback(bro)),
            bro =>
            {
                try
                {
                    JsonData rows = bro.FlattenRows();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        JsonData row = rows[i];
                        string histInDate = row.ContainsKey("inDate") ? row["inDate"].ToString() : string.Empty;
                        string origInDate = row.ContainsKey("originalInDate") ? row["originalInDate"].ToString() : string.Empty;

                        // 서버 만료일 초과 → 서버에서 삭제
                        string expStr = row.ContainsKey("expirationDate") ? row["expirationDate"].ToString() : string.Empty;
                        bool isServerExpired = !string.IsNullOrEmpty(expStr) &&
                                               DateTime.TryParse(expStr, out DateTime expDate) &&
                                               expDate < UserInfo.GetKoreanTime();

                        if (isServerExpired)
                        {
                            if (!string.IsNullOrEmpty(histInDate))
                                DeleteHistoryRecord(histInDate);
                            continue;
                        }

                        // 도착일+30일 초과 → UI에서만 숨김, 서버 데이터 유지
                        bool isUiExpired = DateTime.TryParse(origInDate, out DateTime arrivedDate) &&
                                           UserInfo.GetKoreanTime() > arrivedDate.AddDays(30);

                        if (isUiExpired) continue;

                        // 사용자가 삭제(숨김) 처리한 이력은 UI에 표시하지 않음
                        bool isHidden = row.ContainsKey("isHidden") && row["isHidden"].ToString() == "1";
                        if (isHidden)
                        {
                            _hiddenOrigInDates.Add(origInDate);
                            continue;
                        }

                        // UPost 목록에 이미 있는 메일은 중복 추가 방지, HistoryInDate 연결
                        bool exists = false;
                        for (int j = 0; j < _mailList.Count; j++)
                        {
                            if (_mailList[j].InDate == origInDate)
                            {
                                exists = true;
                                _mailList[j].LinkHistoryRecord(histInDate);
                                break;
                            }
                        }
                        _historyOrigInDates.Add(origInDate);

                        if (!exists)
                        {
                            MailData histMail = MailData.CreateFromHistory(histInDate, row, query);
                            _mailList.Add(histMail);
                        }
                    }
                    Debug.Log($"[MailManager] ?섎졊 ?대젰 濡쒕뱶 ?꾨즺 ({rows.Count}嫄?");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MailManager] ?섎졊 ?대젰 ?뚯떛 ?ㅻ쪟: {ex.Message}");
                }
                onDone?.Invoke();
            },
            state =>
            {
                // NotFoundException = ?뚯씠釉?誘몄깮???????앹뾽 ?놁씠 臾댁떆
                Debug.LogWarning($"[MailManager] ?섎졊 ?대젰 濡쒕뱶 ?ㅽ뙣(?뚯씠釉??놁쓣 ???덉쓬): {state}");
                onDone?.Invoke();
            },
            maxRetries: 0,
            usePopup: false,
            isCurrent: current
        );
    }

    private void SaveMailToHistory(MailData mail, GameDataRestoreQuery expectedQuery = null)
    {
        Param param = mail.ToHistoryParam();
        BackendManager.Instance.ProcessBackendAPI(
            "수령 이력 저장",
            callback => Backend.GameData.Insert(HISTORY_TABLE, param, bro => callback(bro)),
            bro =>
            {
                string histInDate = bro.GetInDate();
                mail.SetHistoryInDate(histInDate);
                Debug.Log($"[MailManager] ?섎졊 ?대젰 ????꾨즺: {mail.Title}");
            },
            state => Debug.LogWarning($"[MailManager] ?섎졊 ?대젰 ????ㅽ뙣: {state}"),
            maxRetries: 1,
            usePopup: false,
            isCurrent: expectedQuery == null ? (Func<bool>)null : () => IsReceiveCurrent(expectedQuery)
        );
    }
    /// <summary>이미 저장된 이력 레코드를 수령 완료로 갱신합니다.</summary>
    private void UpdateMailHistory(MailData mail, GameDataRestoreQuery expectedQuery = null)
    {
        if (string.IsNullOrEmpty(mail.HistoryInDate)) return;
        Param param = mail.ToHistoryParam();
        BackendManager.Instance.ProcessBackendAPI(
            "우편 이력 갱신",
            callback => Backend.GameData.UpdateV2(HISTORY_TABLE, mail.HistoryInDate,
                expectedQuery == null ? Backend.UserInDate : expectedQuery.AccountInDate, param, bro => callback(bro)),
            bro =>
            {
                mail.SetHistoryInDate(mail.HistoryInDate); // IsFromHistory = true 갱신
                Debug.Log($"[MailManager] 우편 이력 갱신 완료: {mail.Title}");
            },
            state => Debug.LogWarning($"[MailManager] 우편 이력 갱신 실패: {state}"),
            maxRetries: 1,
            usePopup: false,
            isCurrent: expectedQuery == null ? (Func<bool>)null : () => IsReceiveCurrent(expectedQuery)
        );
    }

    /// <summary>UPost 메일을 수령 전에 이력에 미리 등록합니다 (수령 없이 만료 시에도 내역 보존).</summary>
    private void SaveMailRecordToHistory(MailData mail)
    {
        if (!IsReceiveCurrent(mail.SourceQuery)) return;
        if (!string.IsNullOrEmpty(mail.HistoryInDate)) return; // 이미 저장됨
        Param param = mail.ToHistoryParam();
        BackendManager.Instance.ProcessBackendAPI(
            "우편 이력 사전 저장",
            callback => Backend.GameData.Insert(HISTORY_TABLE, param, bro => callback(bro)),
            bro =>
            {
                string histInDate = bro.GetInDate();
                mail.LinkHistoryRecord(histInDate); // IsFromHistory는 변경하지 않음
                Debug.Log($"[MailManager] 우편 이력 사전 저장 완료: {mail.Title}");
            },
            state => Debug.LogWarning($"[MailManager] 우편 이력 사전 저장 실패: {state}"),
            maxRetries: 1,
            usePopup: false,
            isCurrent: () => IsReceiveCurrent(mail.SourceQuery)
        );
    }

    /// <summary>MailHistory에 없는 UPost 메일을 일괄 저장합니다.</summary>
    private void SaveNewMailsToHistory()
    {
        foreach (var mail in _mailList)
        {
            if (!mail.IsFromHistory && !_historyOrigInDates.Contains(mail.InDate))
                SaveMailRecordToHistory(mail);
        }
    }

    /// <summary>isHidden으로 마킹된 UPost 메일을 _mailList에서 제거합니다.</summary>
    private void RemoveHiddenMailsFromList()
    {
        _mailList.RemoveAll(m => !m.IsFromHistory && _hiddenOrigInDates.Contains(m.InDate));
    }
    private void LoadByTypeAsync(PostType postType, GameDataRestoreQuery query, Func<bool> current, Action onSuccess, Action onFail)
    {
        BackendManager.Instance.ProcessBackendAPI(
            $"{postType} ?고렪 紐⑸줉 議고쉶",
            callback => Backend.UPost.GetPostList(postType, 100, bro => callback(bro)),
            bro =>
            {
                ParseMailList(postType, bro, query);
                onSuccess?.Invoke();
            },
            state =>
            {
                Debug.LogError($"[MailManager] {postType} ?고렪 紐⑸줉 議고쉶 ?ㅽ뙣: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: false,
            isCurrent: current
        );
    }

    private void ParseMailList(PostType postType, BackendReturnObject bro, GameDataRestoreQuery query)
    {
        try
        {
            JObject raw;
            using (var text = new StringReader(bro.GetReturnValue()))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                raw = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (!(raw["postList"] is JArray rows)) throw new FormatException("postList is not an array.");
            JsonData json = JsonMapper.ToObject(rows.ToString(Formatting.None));
            for (int i = 0; i < json.Count; i++)
            {
                MailData mail = new MailData(postType, json[i], query);
                if (!string.IsNullOrEmpty(mail.InDate))
                    _mailList.Add(mail);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailManager] {postType} ?고렪 ?뚯떛 ?ㅻ쪟: {ex.Message}");
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ?섎졊 (?④굔)

    /// <summary>?뱀젙 硫붿씪??蹂댁긽??鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveMailAsync(MailData mail, Action<MailData> onSuccess = null, Action onFail = null)
    {
        StartReceive(new[] { mail }, () => onSuccess?.Invoke(mail), onFail);
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ?꾩껜 ?섎졊

    /// <summary>愿由ъ옄 ?고렪 ?꾩껜瑜?鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveAllMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Admin, onSuccess, onFail);
    }

    /// <summary>荑좏룿 ?고렪 ?꾩껜瑜?鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveAllCouponMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Coupon, onSuccess, onFail);
    }

    private void ReceiveAllByTypeAsync(PostType postType, Action onSuccess, Action onFail)
    {
        var query = ReadReceiveQuery();
        StartReceive(_mailList.Where(mail => mail.PostType == postType && !mail.IsReceived && !IsReceiveExpired(mail)).ToArray(),
            () => { InvokeReceiveCallbacks(query, onSuccess); InvokeReceiveCallbacks(query, OnAllMailReceived); }, onFail);
    }

    public void ReceiveAllAvailableMailAsync(Action onSuccess = null, Action onFail = null)
    {
        // Freeze both post types now; one operation consumes each ID through the single-post endpoint.
        var query = ReadReceiveQuery();
        StartReceive(_mailList.Where(mail => !mail.IsReceived && !IsReceiveExpired(mail)).ToArray(),
            () => { InvokeReceiveCallbacks(query, onSuccess); InvokeReceiveCallbacks(query, OnAllMailReceived); }, onFail);
    }

    private void StartReceive(IReadOnlyList<MailData> mails, Action onSuccess, Action onFail)
    {
        if (_receiveOperation != null && !_receiveOperation.CanStart)
        { LastReceiveError = "진행 중이거나 결과 확인이 필요한 수령이 있습니다."; onFail?.Invoke(); return; }
        if (mails == null || mails.Count == 0 || mails.Any(mail => !CanReceive(mail)))
        { LastReceiveError = "우편의 계정·원본 보상·수령 상태를 확인해 주세요."; onFail?.Invoke(); return; }
        _receivingMails = Array.AsReadOnly(mails.ToArray());
        CurrentReceiveSaveRequest = null;
        _receiveSucceeded = onSuccess;
        _receiveFailed = onFail;
        _receiveNotificationSent = false;
        LastReceiveError = null;
        if (_receiveOperation == null)
            _receiveOperation = new MailReceiveOperation(_receiveEnvironment ?? new RuntimeMailEnvironment(),
                _receiveTransport ?? new RuntimeMailTransport(), OnMemoryApplied, OnReceiveChanged);
        var targets = mails.Select(mail => new MailReceiveTarget(mail.PostType, mail.InDate,
            mail.RewardManifest, mail.SourceQuery)).ToArray();
        if (!_receiveOperation.TryStart(targets, out string error))
        {
            LastReceiveError = error;
            CompleteReceiveNotification(false);
        }
        else if (_receiveOperation.Status == MailReceiveStatus.Receiving) OnReceiveChanged(_receiveOperation);
    }

    private void OnMemoryApplied(MailReceiveEntry entry)
    {
        if (!IsReceiveCurrent(entry.Target.SourceQuery)) throw new InvalidOperationException("Stale mail completion.");
        MailData mail = _receivingMails.First(item => item.PostType == entry.Target.PostType && item.InDate == entry.Target.InDate);
        mail.SetReceived();
        if (_recordMemoryApplied != null) _recordMemoryApplied(mail);
        else
        {
            // Only the memory-applied state is recorded here. Neither request proves that rewards were saved.
            if (!string.IsNullOrEmpty(mail.HistoryInDate)) UpdateMailHistory(mail, entry.Target.SourceQuery);
            else SaveMailToHistory(mail, entry.Target.SourceQuery);
            if (!IsReceiveCurrent(entry.Target.SourceQuery)) throw new InvalidOperationException("Account changed during mail history request.");
            CurrentReceiveSaveRequest = BackendManager.Instance.RequestGameDataAutosave(); // queued behind the mail lease, never a success claim
        }
        if (!IsReceiveCurrent(entry.Target.SourceQuery)) throw new InvalidOperationException("Account changed during mail completion.");
        if (OnMailReceived != null)
            foreach (Action<MailData> handler in OnMailReceived.GetInvocationList())
                InvokeReceiveCallbacks(entry.Target.SourceQuery, () => handler(mail));
    }

    private void OnReceiveChanged(MailReceiveOperation operation)
    {
        LastReceiveError = operation.Error;
        var query = _receivingMails != null && _receivingMails.Count > 0 ? _receivingMails[0].SourceQuery : null;
        if (!IsReceiveCurrent(query)) return; // old replies must not drive a new account's UI or persistence
        InvokeReceiveCallbacks(query, OnMailListRefreshed);
        if (OnAlarmChanged != null)
            foreach (Action<bool> handler in OnAlarmChanged.GetInvocationList())
                InvokeReceiveCallbacks(query, () => handler(UnreceivedCount > 0));
        if (!IsReceiveCurrent(query)) return;
        if (operation.Status == MailReceiveStatus.MemoryApplied) CompleteReceiveNotification(true);
        else if (operation.Status == MailReceiveStatus.RejectedBeforeSend || operation.Status == MailReceiveStatus.Indeterminate
            || operation.Status == MailReceiveStatus.ConsumedPendingApplication) CompleteReceiveNotification(false);
    }

    private void CompleteReceiveNotification(bool success)
    {
        if (_receiveNotificationSent) return;
        _receiveNotificationSent = true;
        Action callback = success ? _receiveSucceeded : _receiveFailed;
        _receiveSucceeded = null;
        _receiveFailed = null;
        var query = _receivingMails != null && _receivingMails.Count > 0 ? _receivingMails[0].SourceQuery : null;
        InvokeReceiveCallbacks(query, callback);
    }

    private void InvokeReceiveCallbacks(GameDataRestoreQuery query, Action callbacks)
    {
        if (callbacks == null) return;
        foreach (Action callback in callbacks.GetInvocationList())
        {
            if (!IsReceiveCurrent(query)) return;
            // UI observers cannot undo an applied reward or strand the remaining completion callbacks.
            try { callback(); }
            catch (Exception ex) { Debug.LogWarning("[MailManager] Receipt observer failed: " + ex.GetType().Name); }
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ??젣
    /// <summary>메일을 UI 목록에서 제거하고 서버 이력에 isHidden=1로 저장합니다.</summary>
    public void DeleteMailAsync(MailData mail, Action onSuccess = null, Action onFail = null)
    {
        if (mail == null)
        {
            onFail?.Invoke();
            return;
        }

        mail.SetHidden();
        _mailList.Remove(mail);
        onSuccess?.Invoke();
        InvokeRefreshed();
        Debug.Log($"[MailManager] 메일 UI 목록에서 제거 (서버 보존): {mail.Title}");

        // MailHistory에 isHidden=1 저장 (재로드 시에도 표시 안 되도록)
        if (!string.IsNullOrEmpty(mail.HistoryInDate))
            UpdateMailHistory(mail);
        else
            Debug.LogWarning($"[MailManager] HistoryInDate 없음 - 재로드 시 재등장 가능: {mail.Title}");
    }


    /// <summary>만료일이 지난 MailHistory 레코드를 서버에서 삭제합니다.</summary>
    private void DeleteHistoryRecord(string histInDate)
    {
        BackendManager.Instance.ProcessBackendAPI(
            "만료 이력 서버 삭제",
            callback => Backend.GameData.DeleteV2(HISTORY_TABLE, histInDate, Backend.UserInDate, bro => callback(bro)),
            bro => Debug.Log($"[MailManager] 만료 이력 삭제 완료: {histInDate}"),
            state => Debug.LogWarning($"[MailManager] 만료 이력 삭제 실패: {state}"),
            maxRetries: 1,
            usePopup: false
        );
    }

    public void DeleteAllReadMailAsync(Action onSuccess = null)
    {
        List<MailData> readMails = new List<MailData>();
        foreach (var mail in _mailList)
            if (mail.IsReceived) readMails.Add(mail);

        if (readMails.Count == 0)
        {
            onSuccess?.Invoke();
            return;
        }

        int remaining = readMails.Count;
        foreach (var mail in readMails)
        {
            DeleteMailAsync(mail, onSuccess: () =>
            {
                remaining--;
                if (remaining <= 0) onSuccess?.Invoke();
            });
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 蹂댁긽 ?고렪 諛쒖넚 (?먯떊?먭쾶)

    /// <summary>
    /// MailRewardConfig 湲곕컲?쇰줈 ?먯떊?먭쾶 蹂댁긽 ?고렪??諛쒖넚?⑸땲??
    /// ?? ?쒗넗由ъ뼹 ?대━?? ?낆쟻 ?ъ꽦, ?대깽??蹂댁긽 ??
    /// </summary>
    public void SendRewardMailToSelfAsync(MailRewardConfig config, Action onSuccess = null, Action onFail = null)
    {
        if (config == null)
        {
            Debug.LogError("[MailManager] SendRewardMailToSelfAsync: config媛 null?낅땲??");
            onFail?.Invoke();
            return;
        }

        if (string.IsNullOrEmpty(config.TableName) ||
            string.IsNullOrEmpty(config.Column) ||
            string.IsNullOrEmpty(config.RowInDate))
        {
            Debug.LogError($"[MailManager] SendRewardMailToSelfAsync: config({config.name}) ?꾨뱶媛 鍮꾩뼱?덉뒿?덈떎.");
            onFail?.Invoke();
            return;
        }

        PostItem postItem = new PostItem
        {
            TableName  = config.TableName,
            Column     = config.Column,
            RowInDate  = config.RowInDate
        };

        string receiverInDate = Backend.UserInDate;

        BackendManager.Instance.ProcessBackendAPI(
            $"蹂댁긽 ?고렪 諛쒖넚: {config.Description}",
            callback => Backend.UPost.SendUserPost(receiverInDate, postItem, bro => callback(bro)),
            bro =>
            {
                onSuccess?.Invoke();
                Debug.Log($"[MailManager] 蹂댁긽 ?고렪 諛쒖넚 ?꾨즺: {config.Description}");
            },
            state =>
            {
                Debug.LogError($"[MailManager] 蹂댁긽 ?고렪 諛쒖넚 ?ㅽ뙣: {config.Description} / {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: false
        );
    }

    /// <summary>
    /// ?щ윭 蹂댁긽???쒖감?곸쑝濡??먯떊?먭쾶 諛쒖넚?⑸땲??
    /// 紐⑤몢 ?깃났?댁빞 onSuccess媛 ?몄텧?⑸땲??
    /// </summary>
    public void SendRewardMailsToSelfAsync(MailRewardConfig[] configs, Action onSuccess = null, Action onFail = null)
    {
        if (configs == null || configs.Length == 0)
        {
            onFail?.Invoke();
            return;
        }

        int remaining = configs.Length;
        bool anyFailed = false;

        foreach (var config in configs)
        {
            SendRewardMailToSelfAsync(
                config,
                onSuccess: () =>
                {
                    remaining--;
                    if (remaining <= 0)
                    {
                        if (anyFailed) onFail?.Invoke();
                        else onSuccess?.Invoke();
                    }
                },
                onFail: () =>
                {
                    anyFailed = true;
                    remaining--;
                    if (remaining <= 0) onFail?.Invoke();
                }
            );
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 蹂댁긽 吏湲?

    private sealed class RuntimeMailTransport : IMailReceiveTransport
    {
        public void ReceiveOnce(MailReceiveTarget target, Action<MailReceiveResponse> onResponse)
        {
            // No ProcessBackendAPI, popup or application retry around a consuming request.
            Backend.UPost.ReceivePostItem(target.PostType, target.InDate, response =>
            {
                MailReceiveResponse copied;
                try { copied = new MailReceiveResponse(response != null && response.IsSuccess(), response?.GetReturnValue(),
                    response == null ? "우편 응답 없음" : null); }
                catch (Exception ex) { copied = new MailReceiveResponse(false, null, ex.GetType().Name); }
                onResponse(copied);
            });
        }
    }

    private enum RewardKind { Gold, Dia, GachaItem, CustomerSkin, StaffSkin, Staff, Furniture, Kitchen, Recipe }
    private sealed class ResolvedReward
    {
        public RewardKind Kind;
        public object Data;
    }

    private static bool HasStageApplicationEvidence(StaffStageMigrationCollection source,
        StaffStageApplicationResult application, StaffStageMigrationCollection current,
        GameDataRestoreQuery query, EStage stage, object stageOwner, StaffStageRuntimeSnapshot now)
    {
        return source != null && ReferenceEquals(current, source)
            && ReferenceEquals(current.Query, query) && current.RefreshValidity()
            && application != null && application.Stage == stage
            && application.Status == StaffStageApplicationStatus.Succeeded
            && application.Snapshot != null && !application.Snapshot.IsApplying
            && ReferenceEquals(application.Snapshot.OwnerToken, stageOwner)
            && current.Applications.Any(item => ReferenceEquals(item, application))
            && now != null && !now.IsApplying && ReferenceEquals(now.OwnerToken, stageOwner);
    }

    private sealed class RuntimeMailEnvironment : IMailReceiveEnvironment
    {
        private GameDataMailSaveLease _lease;
        private EStage _stage;
        private object _stageOwner;
        private StaffStageMigrationCollection _sourceStageCollection;
        private StaffStageApplicationResult _sourceStageApplication;
        private bool _requiresStageApplication;
        private readonly Dictionary<string, ResolvedReward> _resolved = new Dictionary<string, ResolvedReward>(StringComparer.Ordinal);

        public bool TryAcquire(out IMailReceiveProtection protection, out string error)
        {
            protection = null;
            if (!BackendManager.Instance.TryAcquireMailSaveLease(out _lease, out error)) return false;
            bool accepted = false;
            try
            {
                _stage = UserInfo.CurrentStage;
                _requiresStageApplication = false;
                _sourceStageCollection = BackendManager.Instance.StageMigrationCollection;
                _sourceStageApplication = _sourceStageCollection?.Applications.FirstOrDefault(item => item.Stage == _stage);
                StaffStageRuntimeSnapshot snapshot = UserInfo.CaptureStageStaffRuntimeSnapshot(_stage);
                if (snapshot == null || snapshot.IsApplying)
                { error = "원래 Stage 직원 상태가 준비되지 않았습니다."; return false; }
                _stageOwner = snapshot.OwnerToken;
                if (!IsCurrent()) { error = "보호 확보 중 계정 또는 Stage가 변경되었습니다."; return false; }
                protection = _lease;
                accepted = true;
                return true;
            }
            catch (Exception ex) { error = "원래 Stage 확인 실패: " + ex.GetType().Name; return false; }
            finally { if (!accepted) _lease.TryCancelBeforeRequest(); }
        }

        private bool IsCurrent()
        {
            try
            {
                if (_lease == null || !_lease.IsCurrent) return false;
                StaffStageRuntimeSnapshot snapshot = UserInfo.CaptureStageStaffRuntimeSnapshot(_stage);
                if (snapshot == null || snapshot.IsApplying || !ReferenceEquals(_stageOwner, snapshot.OwnerToken)) return false;
                return !_requiresStageApplication || HasCurrentStageApplication();
            }
            catch { return false; }
        }

        private bool HasCurrentStageApplication()
        {
            StaffStageMigrationCollection current = BackendManager.Instance.StageMigrationCollection;
            // A new collection in the same GameData query must not inherit the old round's application proof.
            StaffStageRuntimeSnapshot now = UserInfo.CaptureStageStaffRuntimeSnapshot(_stage);
            return HasStageApplicationEvidence(_sourceStageCollection, _sourceStageApplication, current,
                    _lease.Query, _stage, _stageOwner, now)
                && _lease.IsCurrent && ReferenceEquals(BackendManager.Instance.StageMigrationCollection, current);
        }

        public bool TryValidate(IReadOnlyList<MailRewardItem> rewards, out string error)
        {
            error = null;
            try
            {
                if (!IsCurrent() || rewards == null) { error = "계정 또는 원래 Stage가 변경되었습니다."; return false; }
                long gold = 0, dia = 0, skinTokens = 0;
                var itemCounts = new Dictionary<string, long>(StringComparer.Ordinal);
                var staffSkins = new HashSet<string>(StringComparer.Ordinal);
                IReadOnlyList<StaffData> staffCatalog = null;
                foreach (MailRewardItem reward in rewards)
                {
                    if (reward == null || string.IsNullOrWhiteSpace(reward.Id) || reward.Count <= 0 ||
                        !TryResolve(reward.Id, out ResolvedReward resolved))
                    { error = "확인할 수 없는 우편 보상입니다."; return false; }
                    _resolved[reward.Id] = resolved;
                    if (resolved.Kind == RewardKind.Furniture || resolved.Kind == RewardKind.Kitchen
                        || (resolved.Kind == RewardKind.Staff && BackendManager.Instance.StaffRuntime.Mode == StaffAccountRuntimeMode.Legacy))
                    {
                        _requiresStageApplication = true;
                        if (!HasCurrentStageApplication())
                        { error = "원래 Stage의 해당 조회 세대에서 성공한 복원 기록이 없습니다."; return false; }
                    }
                    switch (resolved.Kind)
                    {
                        case RewardKind.Gold: gold = checked(gold + reward.Count); break;
                        case RewardKind.Dia: dia = checked(dia + reward.Count); break;
                        case RewardKind.GachaItem:
                            itemCounts.TryGetValue(reward.Id, out long count);
                            itemCounts[reward.Id] = checked(count + reward.Count);
                            break;
                        case RewardKind.StaffSkin:
                            if (UserInfo.IsGiveStaffSkin(reward.Id) || !staffSkins.Add(reward.Id))
                                skinTokens = checked(skinTokens + ((StaffSkinData)resolved.Data).DuplicationToken);
                            break;
                        case RewardKind.Staff:
                            if (!BackendManager.Instance.StaffRuntime.CanMutate)
                            { error = "직원 보상을 반영할 현재 계정 상태가 아닙니다."; return false; }
                            if (staffCatalog == null)
                            {
                                staffCatalog = Resources.LoadAll<StaffData>("StaffData");
                                if (GameDataRestoreContext.TryBuildStaffMaximums(() => staffCatalog, out _, out _) != GameDataRestoreStatus.Ready)
                                { error = "현재 직원 등록 목록을 검증할 수 없습니다."; return false; }
                            }
                            if (!ReferenceEquals(staffCatalog.FirstOrDefault(staff => staff.Id == reward.Id), resolved.Data))
                            { error = "직원 표시 자료와 실제 등록 자료가 일치하지 않습니다."; return false; }
                            break;
                    }
                }
                checked
                {
                    long afterGold = UserInfo.Money + gold;
                    long afterTotal = UserInfo.TotalAddMoney + gold;
                    long afterDaily = UserInfo.DailyAddMoney + gold;
                    long afterWeekly = UserInfo.WeeklyAddMoney + gold;
                    if (UserInfo.Money < 0 || UserInfo.Dia < 0 || UserInfo.SkinToken < 0 ||
                        UserInfo.Dia + dia > int.MaxValue || UserInfo.SkinToken + skinTokens > int.MaxValue || skinTokens < 0)
                    { error = "재화 범위를 초과하거나 현재 잔액이 잘못되었습니다."; return false; }
                    foreach (var item in itemCounts)
                    {
                        int before = UserInfo.GetGiveItemCount((GachaItemData)_resolved[item.Key].Data);
                        if (before < 0 || before + item.Value > int.MaxValue)
                        { error = "가챠 아이템 보유 수량 범위를 초과합니다."; return false; }
                    }
                }
                return IsCurrent();
            }
            catch (Exception ex) { error = "우편 전체 보상 사전 확인 실패: " + ex.GetType().Name; return false; }
        }

        private static bool TryResolve(string id, out ResolvedReward result)
        {
            result = null;
            if (id == "골드" || id == "Gold") result = new ResolvedReward { Kind = RewardKind.Gold };
            else if (id == "다이아" || id == "Dia") result = new ResolvedReward { Kind = RewardKind.Dia };
            else if (ItemManager.Instance.IsGachaItem(id))
                result = new ResolvedReward { Kind = RewardKind.GachaItem, Data = ItemManager.Instance.GetGachaItemData(id) };
            else if (id.StartsWith("SKIN_CUSTOMER", StringComparison.OrdinalIgnoreCase))
                result = new ResolvedReward { Kind = RewardKind.CustomerSkin, Data = SkinDataManager.Instance.GetCustomerSkinData(id) };
            else if (id.StartsWith("SKIN_STAFF", StringComparison.OrdinalIgnoreCase))
                result = new ResolvedReward { Kind = RewardKind.StaffSkin, Data = SkinDataManager.Instance.GetStaffSkinData(id) };
            else if (id.StartsWith("STAFF", StringComparison.OrdinalIgnoreCase))
                result = new ResolvedReward { Kind = RewardKind.Staff, Data = StaffDataManager.Instance.GetStaffData(id) };
            else
            {
                try { result = new ResolvedReward { Kind = RewardKind.Furniture, Data = FurnitureDataManager.Instance.GetFurnitureData(id) }; } catch { }
                if (result?.Data == null)
                    try { result = new ResolvedReward { Kind = RewardKind.Kitchen, Data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id) }; } catch { }
                if (result?.Data == null)
                    try { result = new ResolvedReward { Kind = RewardKind.Recipe, Data = FoodDataManager.Instance.GetFoodData(id) }; } catch { }
            }
            return result != null && (result.Kind == RewardKind.Gold || result.Kind == RewardKind.Dia || result.Data != null);
        }

        public MailRewardApplyResult Apply(MailRewardItem reward)
        {
            if (!TryValidate(new[] { reward }, out string error) || !_resolved.TryGetValue(reward.Id, out ResolvedReward resolved))
                return MailRewardApplyResult.Rejected(error ?? "지급 전 원래 계정·Stage·보상 확인에 실패했습니다.");
            switch (resolved.Kind)
            {
                case RewardKind.Gold:
                    long gold = UserInfo.Money;
                    return ApplyOnce(reward, () => UserInfo.AddMoney(reward.Count),
                        () => UserInfo.Money == checked(gold + reward.Count));
                case RewardKind.Dia:
                    int dia = UserInfo.Dia;
                    return ApplyOnce(reward, () => UserInfo.AddDia(reward.Count),
                        () => UserInfo.Dia == checked(dia + reward.Count));
                case RewardKind.Staff:
                    bool accepted = false;
                    bool ownedBefore = UserInfo.IsGiveStaff(_stage, reward.Id);
                    return ApplyOnce(reward, () => accepted = UserInfo.GiveStaff(_stage, (StaffData)resolved.Data),
                        () => UserInfo.IsGiveStaff(_stage, reward.Id) && (accepted || !ownedBefore), () => !accepted);
                case RewardKind.CustomerSkin:
                    return ApplyOnce(reward, () => UserInfo.GiveCustomerSkin((CustomerSkinData)resolved.Data),
                        () => UserInfo.IsGiveCustomerSkin(reward.Id));
                case RewardKind.StaffSkin:
                    bool duplicate = UserInfo.IsGiveStaffSkin(reward.Id);
                    int tokens = UserInfo.SkinToken;
                    var skin = (StaffSkinData)resolved.Data;
                    return ApplyOnce(reward, () => UserInfo.GiveStaffSkin(skin),
                        () => duplicate ? UserInfo.SkinToken == checked(tokens + skin.DuplicationToken) : UserInfo.IsGiveStaffSkin(reward.Id));
                case RewardKind.Furniture:
                    return ApplyOnce(reward, () => UserInfo.GiveFurniture(_stage, (FurnitureData)resolved.Data),
                        () => UserInfo.IsGiveFurniture(_stage, reward.Id));
                case RewardKind.Kitchen:
                    return ApplyOnce(reward, () => UserInfo.GiveKitchenUtensil(_stage, (KitchenUtensilData)resolved.Data),
                        () => UserInfo.IsGiveKitchenUtensil(_stage, reward.Id));
                case RewardKind.GachaItem:
                case RewardKind.Recipe:
                    return ApplyRepeated(reward, resolved);
                default: return MailRewardApplyResult.Rejected("지원하지 않는 보상입니다.");
            }
        }

        private MailRewardApplyResult ApplyOnce(MailRewardItem reward, Action apply, Func<bool> observed, Func<bool> rejected = null)
        {
            bool threw = false;
            try { apply(); } catch { threw = true; }
            if (!IsCurrent()) return MailRewardApplyResult.Unknown(0, "항목 처리 중 원래 계정·Stage가 변경되었습니다.");
            try
            {
                if (observed()) return threw ? MailRewardApplyResult.Partial(reward.Count, "보상은 반영되었으나 알림 예외가 발생했습니다.")
                    : MailRewardApplyResult.Applied(reward.Count);
                if (!threw && rejected != null && rejected()) return MailRewardApplyResult.Rejected("직원 지급이 거절되었습니다.");
            }
            catch { }
            return MailRewardApplyResult.Unknown(0, "보상 변경 결과를 확정할 수 없습니다. 전체 보상을 다시 지급하지 않습니다.");
        }

        private MailRewardApplyResult ApplyRepeated(MailRewardItem reward, ResolvedReward resolved)
        {
            int done = 0;
            for (; done < reward.Count; done++)
            {
                if (!IsCurrent()) return MailRewardApplyResult.Partial(done, "반복 지급 중 원래 계정·Stage가 변경되었습니다.");
                bool accepted = true;
                int before = resolved.Kind == RewardKind.GachaItem ? UserInfo.GetGiveItemCount((GachaItemData)resolved.Data) : 0;
                bool threw = false;
                try
                {
                    if (resolved.Kind == RewardKind.GachaItem) accepted = UserInfo.GiveGachaItem((GachaItemData)resolved.Data);
                    else UserInfo.GiveRecipe((FoodData)resolved.Data);
                }
                catch { threw = true; }
                if (!IsCurrent()) return MailRewardApplyResult.Unknown(done, "반복 지급 결과의 계정이 변경되었습니다.");
                bool applied;
                try { applied = resolved.Kind == RewardKind.GachaItem
                    ? UserInfo.GetGiveItemCount((GachaItemData)resolved.Data) == checked(before + 1)
                    : UserInfo.IsGiveRecipe(reward.Id); }
                catch { return MailRewardApplyResult.Unknown(done, "반복 보상 적용 후 상태를 읽을 수 없습니다."); }
                if (applied)
                {
                    if (threw) return MailRewardApplyResult.Partial(done + 1, "반복 보상 반영 후 알림 예외가 발생했습니다.");
                    continue;
                }
                return !threw && !accepted ? MailRewardApplyResult.Partial(done, "반복 보상 지급이 거절되었습니다.")
                    : MailRewardApplyResult.Unknown(done, "반복 보상 일부 결과를 확인할 수 없습니다.");
            }
            return MailRewardApplyResult.Applied(done);
        }
    }

    #endregion
}


