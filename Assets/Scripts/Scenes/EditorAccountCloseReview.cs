#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit existing-account review only. Never starts Play, falls back to Guest
// signup, writes game data, or issues a ticket/reward operation. The separately
// requested account switch is memory-only and owned until Play/reload cleanup.
public static class EditorAccountCloseReview
{
    private const string Key = "Panda.PopupStore.CloseReview.";
    private const string Menu = "Tools/Panda Restaurant/Popup Store/실계정 안전 진입/";
    private const string DirectoryPath = "Library/PopupStoreVerification/DEVFINALCLOSE";
    private static BackendManager _owner;
    private static object _authentication;
    private static string _account;
    private static bool _loginStarted, _reading, _permission;
    private static int _mainStartCount;
    private static object _protectedMainAuthentication;
    private static bool _accountRunRequested;
    private static PopupStoreAccountPhaseConfiguration _ownedConfiguration;
    private static string _configurationPath, _configurationHash;
    private static ServerSnapshot _latestSnapshot;
    public static bool Active => SessionState.GetBool(Key + "Active", false);

    public sealed class ServerSnapshot
    {
        public string Owner, Row;
        public int Diamonds;
        public PopupStoreGameDataRecord Record;
    }

    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= RestoreAccountMode;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreAccountMode;
        if (!EditorApplication.isPlaying && SessionState.GetBool(Key + "OwnsAccountMode", false)) RestoreAccountMode();
    }

    private static void Poll()
    {
        if (SessionState.GetBool(Key + "OwnsAccountMode", false) && !SameAuthentication()) RestoreAccountMode();
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string path = Path.Combine(DirectoryPath, "live-request.json");
        if (!File.Exists(path)) return;
        try
        {
            var request = Parse(File.ReadAllText(path));
            File.Delete(path); // Consume this explicit request once, never retry a server operation.
            switch ((string)request["command"])
            {
                case "prepare": Prepare((string)request["expectedGamerId"]); break;
                case "inspect-live": Inspect(); break;
                case "read-server": ReadServer(null, false); break;
                case "arm-account-run": ArmAccountRun(); break;
                case "finish": Finish(); break;
                default: Record("BLOCKED", "Unknown explicit review command"); break;
            }
        }
        catch (Exception error) { Record("BLOCKED", "Review request failed: " + error.GetType().Name); }
    }

    private static void Prepare(string expected)
    {
        if (!Guid.TryParse(expected, out _) || Active || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorUtility.scriptCompilationFailed) { Record("BLOCKED", "Invalid preparation state"); return; }
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        bool first = scenes.Length == 1 && scenes[0].path == "Assets/Scenes/FirstLoadingScene.unity"
            && PrefabStageUtility.GetCurrentPrefabStage() == null
            && (EditorSceneManager.playModeStartScene == null
                || AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == scenes[0].path);
        var options = EditorSettings.enterPlayModeOptionsEnabled ? EditorSettings.enterPlayModeOptions : EnterPlayModeOptions.None;
        if (!EditorAccountSafetyPolicy.CanEnterPlay(true, EditorApplication.isPlayingOrWillChangePlaymode,
            EditorApplication.isCompiling, EditorApplication.isUpdating, scenes.Any(s => s.isDirty), first,
            (options & EnterPlayModeOptions.DisableDomainReload) == 0, (options & EnterPlayModeOptions.DisableSceneReload) == 0,
            Backend.IsInitialized, Backend.IsLogin, BackendManager.ExistingInstance != null, out string reason))
        { Record("BLOCKED", reason); return; }
        bool owned = !EditorAccountSafety.IsArmed;
        if (owned && !EditorApplication.ExecuteMenuItem(Menu + "안전 잠금 준비 (Play 실행 안 함)"))
        { Record("BLOCKED", "Safety preparation menu unavailable"); return; }
        if (!EditorAccountSafety.IsArmed || !EditorAccountSafety.DebugActionsBlocked)
        { Record("BLOCKED", "Safety latch unavailable"); return; }
        SessionState.SetBool(Key + "OwnsLatch", owned);
        SessionState.SetString(Key + "Expected", expected);
        SessionState.SetBool(Key + "IdentityMatched", false);
        SessionState.SetBool(Key + "Active", true);
        _loginStarted = _reading = _permission = false; _mainStartCount = 0;
        _accountRunRequested = false; _protectedMainAuthentication = null; _latestSnapshot = null;
        SessionState.EraseBool(Key + "AccountRunRequested");
        _owner = null; _authentication = null; _account = null;
        Record("PREPARED", "Existing-token login only; Play not started; account mode unchanged");
    }

    public static void BeginLogin(BackendManager owner, Action continuation)
    {
        if (!Active || !EditorAccountSafety.IsArmed || !EditorApplication.isPlaying || _loginStarted
            || owner == null || owner.EditorCloseReviewOfflineOwner || !EditorAccountSafety.RequireFreshAuthentication())
        { Record("BLOCKED", "Login admission rejected"); return; }
        _loginStarted = true; _owner = owner;
        Record("TOKEN_LOGIN_REQUESTED", "No Guest fallback or signup path");
        bool tokenDelivered = false;
        owner.TokenLoginAsync(reply =>
        {
            if (!TryAcceptReply(ref tokenDelivered)) return;
            if (!Active || !EditorAccountSafety.IsArmed || !EditorApplication.isPlaying
                || !ReferenceEquals(owner, BackendManager.ExistingInstance) || reply == null
                || !IsSuccessfulExistingTokenResponse(reply.IsSuccess(), reply.GetStatusCode()) || !owner.IsLogin || !Backend.IsLogin)
            {
                Record("BLOCKED", "Existing token login was not confirmed", new JObject {
                    ["statusCode"] = reply?.GetStatusCode(), ["replySuccess"] = reply != null && reply.IsSuccess(),
                    ["active"] = Active, ["armed"] = EditorAccountSafety.IsArmed,
                    ["isPlaying"] = EditorApplication.isPlaying,
                    ["sameOwner"] = ReferenceEquals(owner, BackendManager.ExistingInstance),
                    ["ownerLogin"] = owner != null && owner.IsLogin, ["sdkLogin"] = Backend.IsLogin });
                return;
            }
            Record("TOKEN_LOGIN_CONFIRMED", "Existing-token API success contract confirmed", new JObject {
                ["statusCode"] = reply.GetStatusCode(), ["replySuccess"] = reply.IsSuccess(),
                ["sameOwner"] = ReferenceEquals(owner, BackendManager.ExistingInstance),
                ["ownerLogin"] = owner.IsLogin, ["sdkLogin"] = Backend.IsLogin });
            _account = Backend.UserInDate;
            _authentication = owner.EditorCloseReviewAuthenticationIdentity;
            if (!SameAuthentication()) { Record("BLOCKED", "Authentication identity unavailable"); return; }
            bool identityDelivered = false;
            Backend.BMember.GetUserInfo(identity =>
            {
                if (!TryAcceptReply(ref identityDelivered)) return;
                if (!SameAuthentication()) { Record("BLOCKED", "Authentication changed during identity lookup"); return; }
                try
                {
                    var root = identity != null && identity.IsSuccess() ? Parse(identity.GetReturnValue()) : null;
                    string actual = (string)root?["row"]?["gamerId"];
                    if (!IsExpectedIdentity(SessionState.GetString(Key + "Expected", ""), actual, identity?.GetStatusCode()))
                    { Record("BLOCKED", "Expected existing account did not match"); return; }
                    SessionState.SetBool(Key + "IdentityMatched", true);
                    ReadServer(() => { _permission = true; continuation?.Invoke(); }, true);
                }
                catch (Exception error) { Record("BLOCKED", "Identity response rejected: " + error.GetType().Name); }
            });
        }, _ => { if (TryAcceptReply(ref tokenDelivered)) Record("BLOCKED", "Token login failed; no fallback or retry requested"); });
    }

    public static bool IsExpectedIdentity(string expected, string actual, string statusCode)
        => statusCode == "200" && Guid.TryParse(expected, out _) && string.Equals(expected, actual, StringComparison.Ordinal);

    // LoginWithTheBackendToken returns 201 for an existing token login; it is not
    // the GuestLogin/signup endpoint. Identity GET remains separately restricted to 200.
    public static bool IsSuccessfulExistingTokenResponse(bool success, string statusCode)
        => success && statusCode == "201";

    private static bool SameAuthentication() => Active && EditorAccountSafety.IsArmed && EditorApplication.isPlaying
        && _owner != null && ReferenceEquals(_owner, BackendManager.ExistingInstance)
        && _owner.IsLogin && Backend.IsLogin && _authentication != null
        && ReferenceEquals(_authentication, _owner.EditorCloseReviewAuthenticationIdentity)
        && !string.IsNullOrWhiteSpace(_account) && string.Equals(_account, Backend.UserInDate, StringComparison.Ordinal);

    public static bool TryConsumeContinuation(bool active, bool sameAuthentication, ref bool permission)
    {
        if (!active || !sameAuthentication || !permission) return false;
        permission = false; return true;
    }

    public static bool TryAcceptReply(ref bool delivered)
    {
        if (delivered) return false;
        delivered = true; return true;
    }

    public static bool ConsumeRestorationPermission()
    {
        bool allowed = TryConsumeContinuation(Active, SameAuthentication(), ref _permission);
        Record(allowed ? "RESTORE_ALLOWED" : "BLOCKED", allowed ? "Original restoration admitted once" : "Restoration permission unavailable");
        return allowed;
    }

    private static void ReadServer(Action continuation, bool admission)
    {
        if (!SameAuthentication() || !SessionState.GetBool(Key + "IdentityMatched", false) || _reading)
        { Record("BLOCKED", "Fresh read requires the same verified login"); return; }
        _reading = true;
        bool readDelivered = false;
        var authentication = _authentication;
        var where = new Where(); where.Equal("owner_inDate", _account);
        try
        {
            Backend.GameData.Get("GameData", where, reply =>
            {
                if (!TryAcceptReply(ref readDelivered)) return;
                _reading = false;
                if (!SameAuthentication() || !ReferenceEquals(authentication, _authentication))
                { Record("BLOCKED", "Late server response"); return; }
                if (reply == null || !reply.IsSuccess() || reply.GetStatusCode() != "200")
                { Record("BLOCKED", "Original GameData lookup failed; no retry requested"); return; }
                if (!TryValidateGameDataRaw(reply.GetReturnValue(), _account, out var snapshot, out string error))
                { Record("BLOCKED", error); return; }
                var journal = new PopupStoreAccountFileJournal(Path.Combine(Application.persistentDataPath, "PopupStoreAccountRecovery"));
                if (!journal.TryRead(new GameDataSaveTarget(snapshot.Owner, snapshot.Row), out var intent, out _)
                    || intent != null || admission && (snapshot.Record.IsRewardPending || snapshot.Record.State == PopupStoreGameDataState.Started))
                { Record("BLOCKED", "Existing pending transaction/recovery requires separate review"); return; }
                bool recorded = Record(admission ? "BASELINE_READ" : "SERVER_READ", "Read-only original; no memory restore or write", new JObject {
                    ["serverDia"] = snapshot.Diamonds, ["ownerHash"] = Hash(snapshot.Owner), ["rowHash"] = Hash(snapshot.Row),
                    ["identityMatched"] = true, ["usedCount"] = snapshot.Record.UsedCount, ["dayKey"] = snapshot.Record.DayKey,
                    ["popupState"] = snapshot.Record.State.ToString(), ["revision"] = snapshot.Record.Revision,
                    ["rewardDiamonds"] = snapshot.Record.Diamonds, ["sessionHash"] = Hash(snapshot.Record.SessionId),
                    ["decisionHash"] = Hash(snapshot.Record.DecisionId), ["requestHash"] = Hash(snapshot.Record.LastRequestId) });
                if (!recorded) return; // Never restore without preserving the original read evidence.
                _latestSnapshot = snapshot;
                if (admission) SessionState.SetInt(Key + "BaselineDia", snapshot.Diamonds);
                if (SameAuthentication()) continuation?.Invoke();
            });
        }
        catch (Exception error) { _reading = false; Record("BLOCKED", "Original lookup failed: " + error.GetType().Name); }
    }

    public static bool TryValidateGameDataRaw(string raw, string expectedOwner, out ServerSnapshot snapshot, out string error)
    {
        snapshot = null; error = "Existing GameData original is invalid";
        try
        {
            var root = Parse(raw);
            if (!(root["rows"] is JArray rows) || rows.Count != 1 || !(rows[0] is JObject row)
                || root["firstKey"] != null && root["firstKey"].Type != JTokenType.Null) return false;
            string owner = Wrapped(row["owner_inDate"], "S"), id = Wrapped(row["inDate"], "S");
            string dia = Wrapped(row["Dia"], "N"), nickname = Wrapped(row["UserId"], "S");
            var clear = row["IsFirstTutorialClear"] as JObject;
            if (string.IsNullOrWhiteSpace(owner) || owner != expectedOwner || string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(nickname) || clear == null || clear.Count != 1
                || clear["BOOL"]?.Type != JTokenType.Boolean || !(bool)clear["BOOL"]
                || string.IsNullOrEmpty(dia) || dia.Any(c => c < '0' || c > '9')
                || !int.TryParse(dia, NumberStyles.None, CultureInfo.InvariantCulture, out int diamonds)) return false;
            bool present = row.Property(PopupStoreGameDataRecord.FieldName, StringComparison.Ordinal) != null;
            if (!PopupStoreGameDataRecord.TryRead(present ? Wrapped(row[PopupStoreGameDataRecord.FieldName], "S") : null,
                present, out var record, out _)) return false;
            snapshot = new ServerSnapshot { Owner = owner, Row = id, Diamonds = diamonds, Record = record };
            error = null; return true;
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException || ex is OverflowException || ex is ArgumentException)
        { return false; }
    }

    private static string Wrapped(JToken value, string type) => value is JObject wrapped && wrapped.Count == 1
        && wrapped[type]?.Type == JTokenType.String ? (string)wrapped[type] : null;
    private static JObject Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 4000000) throw new FormatException();
        using (var text = new StringReader(raw))
        using (var reader = new JsonTextReader(text) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
        {
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new FormatException(); return result;
        }
    }

    private static void Inspect()
    {
        if (!SameAuthentication()) { Record("BLOCKED", "No same-account live inspection available"); return; }
        bool ready = EditorAccountSafety.IsReady(out string reason);
        var stages = _owner.StageMigrationCollection;
        var config = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
        Record("LIVE_INSPECTION", "Memory evidence, not fresh server balance", new JObject {
            ["ready"] = ready, ["readyReason"] = reason, ["userInfoDia"] = UserInfo.Dia,
            ["restoreStatus"] = _owner.GameDataRestoreResult.Status.ToString(),
            ["requiredStages"] = stages?.RequiredStages.Count ?? 0, ["completedStages"] = stages?.CompletedStageCount ?? 0,
            ["stagesApplied"] = new JArray(stages?.Applications.Select(a => a.Stage + ":" + a.Status) ?? Enumerable.Empty<string>()),
            ["debugBlocked"] = EditorAccountSafety.DebugActionsBlocked, ["accountMode"] = config != null && config.EnableAccountMode,
            ["accountModeOwned"] = SessionState.GetBool(Key + "OwnsAccountMode", false),
            ["pendingStageSaves"] = _owner.EditorHasPendingStageSaves,
            ["saveState"] = _owner.CurrentGameDataSaveCoordinator?.State.ToString() ?? "none",
            ["trade"] = CaptureTradeProof() });
    }

    private static JObject CaptureTradeProof()
    {
        // Read existing objects only. Never create a bridge or submit/reconcile a transaction.
        var bridge = _owner.EditorCloseReviewExistingPopupBridge;
        var record = bridge?.CurrentRecord;
        var operation = bridge?.CurrentOperation;
        var coordinator = _owner.CurrentGameDataSaveCoordinator;
        var receipt = coordinator?.LastReceipt;
        return new JObject {
            ["bridgeExists"] = bridge != null, ["bridgeReady"] = bridge != null && bridge.IsReady,
            ["record"] = record == null ? null : new JObject {
                ["state"] = record.State.ToString(), ["usedCount"] = record.UsedCount,
                ["dayKey"] = record.DayKey, ["revision"] = record.Revision,
                ["score"] = record.FinalScore, ["successfulOrders"] = record.SuccessfulOrders,
                ["grade"] = record.HighestGrade.ToString(), ["endReason"] = record.EndReason.ToString(),
                ["rewardDiamonds"] = record.Diamonds, ["rewardRoll"] = record.RewardRoll,
                ["sessionHash"] = Hash(record.SessionId), ["decisionHash"] = Hash(record.DecisionId),
                ["startRequestHash"] = Hash(record.StartRequestId), ["lastRequestHash"] = Hash(record.LastRequestId) },
            ["operation"] = operation == null ? null : new JObject {
                ["status"] = operation.Status.ToString(), ["committed"] = operation.Committed,
                ["rejectedBeforeSend"] = operation.RejectedBeforeSend, ["rewardDelta"] = operation.RewardDelta,
                ["sourceState"] = operation.Source.State.ToString(), ["candidateState"] = operation.Candidate.State.ToString(),
                ["requestHash"] = Hash(operation.Identity.RequestId) },
            ["receipt"] = receipt == null ? null : new JObject {
                ["disposition"] = receipt.Disposition.ToString(), ["sendStarted"] = receipt.SendStarted,
                ["success"] = receipt.RawResponse != null && receipt.RawResponse.IsSuccess,
                ["statusCode"] = receipt.RawResponse?.StatusCode,
                ["requestHash"] = Hash(receipt.Identity?.RequestId),
                ["matchesPopupOperation"] = operation != null && operation.Identity.Matches(receipt.Identity) },
            ["canStartPurchase"] = coordinator != null && coordinator.CanStartPurchase,
            ["phases"] = new JArray(Resources.FindObjectsOfTypeAll<PopupStorePhasePreview>()
                .Where(p => p != null && p.gameObject.scene.IsValid()).Select(p => new JObject {
                    ["active"] = p.gameObject.activeInHierarchy, ["accountMode"] = p.IsAccountMode,
                    ["authorityReady"] = p.AccountAuthorityReady, ["claimConfirmed"] = p.AccountClaimConfirmed,
                    ["confirmedDiamonds"] = p.AccountConfirmedDiamonds })) };
    }

    public static void ObserveMainSceneStart()
    {
        if (!Active) return;
        _mainStartCount++;
        _protectedMainAuthentication = _mainStartCount == 1 && SameAuthentication()
            && EditorAccountSafety.DebugActionsBlocked && MainScene.SuppressEditorDebugGrantsForTesting ? _authentication : null;
        Record("MAIN_SCENE_START", "Actual Start entry; debug grant branch remains guarded", new JObject {
            ["startCount"] = _mainStartCount, ["sameAuthentication"] = SameAuthentication(),
            ["armed"] = EditorAccountSafety.IsArmed, ["debugBlocked"] = EditorAccountSafety.DebugActionsBlocked,
            ["legacySuppression"] = MainScene.SuppressEditorDebugGrantsForTesting,
            ["userInfoDia"] = UserInfo.Dia, ["baselineServerDia"] = SessionState.GetInt(Key + "BaselineDia", -1) });
    }

    public static bool CanArmAccountRun(bool sameAuthentication, bool ready, bool mainStartProtected,
        bool requestUnused, bool configurationOff, bool popupReadyForFirstEntry, bool saveIdle, bool freshUnusedRecord)
        => sameAuthentication && ready && mainStartProtected && requestUnused && configurationOff
            && popupReadyForFirstEntry && saveIdle && freshUnusedRecord;

    private static bool AccountRunConditions(PopupStoreAccountPhaseConfiguration config, bool requestUnused, bool fresh)
    {
        var restored = _owner?.RestoredGameData;
        bool idle = _owner != null && !_owner.EditorHasPendingStageSaves
            && (_owner.CurrentGameDataSaveCoordinator == null || _owner.CurrentGameDataSaveCoordinator.State == GameDataSaveCoordinatorState.Idle);
        bool original = _latestSnapshot != null && _latestSnapshot.Record.UsedCount == 0
            && !_latestSnapshot.Record.IsRewardPending && _latestSnapshot.Record.State != PopupStoreGameDataState.Started;
        if (fresh) original = original && restored != null && _latestSnapshot.Owner == restored.Target.AccountInDate
            && _latestSnapshot.Row == restored.Target.RowInDate && _latestSnapshot.Diamonds == UserInfo.Dia
            && MemoryRecordMatches(_latestSnapshot.Record);
        return CanArmAccountRun(SameAuthentication(), EditorAccountSafety.IsReady(out _),
            _mainStartCount == 1 && ReferenceEquals(_protectedMainAuthentication, _authentication)
                && EditorAccountSafety.DebugActionsBlocked && MainScene.SuppressEditorDebugGrantsForTesting,
            requestUnused, config != null && !config.EnableAccountMode && !EditorUtility.IsDirty(config),
            ScenePopups().All(IsFreshInactivePopupForAccountRun), idle, original);
    }

    private static UIPopupStore[] ScenePopups() => Resources.FindObjectsOfTypeAll<UIPopupStore>()
        .Where(p => p != null && p.gameObject.scene.IsValid()).ToArray();

    public static bool IsFreshInactivePopupForAccountRun(UIPopupStore popup)
    {
        if (popup == null || !popup.gameObject.scene.IsValid() || popup.gameObject.activeSelf
            || popup.gameObject.activeInHierarchy || popup.VisibleState != VisibleState.Disappeared
            || popup.GetComponent<PopupStorePhasePreview>() != null
            || popup.GetComponent<PopupStoreAccountRecoveryPresentation>() != null) return false;
        var start = popup.GetComponent<PopupStoreStartController>();
        var clock = popup.GetComponent<PopupStoreBusinessClock>();
        var lifetime = popup.GetComponent<PopupStoreSessionLifetime>();
        var grade = popup.GetComponent<PopupStoreGradeSession>();
        var score = popup.GetComponent<PopupStoreScore>();
        var packing = popup.GetComponent<PopupStorePacking>();
        var rewards = popup.GetComponent<PopupStoreRewardSession>();
        var counters = popup.GetComponentsInChildren<PopupStoreSalesCounter>(true);
        if (start == null || !start.enabled || start.Host != null || start.IsApproved
            || clock == null || clock.State != PopupStoreBusinessState.NotStarted || clock.IsPhasePreview
            || clock.EndedCount != 0 || clock.AbandonCount != 0 || lifetime == null || lifetime.IsRetired
            || grade == null || grade.IsFinalized || score == null || score.TotalScore != 0 || score.SuccessfulOrderCount != 0
            || packing == null || packing.SuccessfulPackCount != 0 || packing.CommittedReceipts.Count != 0
            || rewards == null || rewards.IsResolved || rewards.RandomDrawCount != 0
            || counters.Length != 1 || counters[0].Count != 0) return false;
        // OnEnable/Refresh permanently binds these owners, including after Show -> Hide.
        // Therefore an inactive authored view is allowed, but a previously opened view is not.
        var scoreOwner = typeof(PopupStoreGradeSession).GetField("_ownerScore", BindingFlags.Instance | BindingFlags.NonPublic);
        var clockOwner = typeof(PopupStoreGradeSession).GetField("_ownerClock", BindingFlags.Instance | BindingFlags.NonPublic);
        return scoreOwner != null && clockOwner != null && scoreOwner.GetValue(grade) == null && clockOwner.GetValue(grade) == null;
    }

    private static JObject PopupAdmissionDetails()
    {
        var popups = ScenePopups();
        return new JObject { ["popupCount"] = popups.Length,
            ["allPopupsFreshInactive"] = popups.All(IsFreshInactivePopupForAccountRun),
            ["popups"] = new JArray(popups.Select(p => new JObject {
                ["activeSelf"] = p.gameObject.activeSelf, ["activeInHierarchy"] = p.gameObject.activeInHierarchy,
                ["visibleState"] = p.VisibleState.ToString(), ["freshInactive"] = IsFreshInactivePopupForAccountRun(p),
                ["clockState"] = p.GetComponent<PopupStoreBusinessClock>()?.State.ToString(),
                ["hostBound"] = p.GetComponent<PopupStoreStartController>()?.Host != null,
                ["hasPhase"] = p.GetComponent<PopupStorePhasePreview>() != null,
                ["hasRecovery"] = p.GetComponent<PopupStoreAccountRecoveryPresentation>() != null })) };
    }

    private static void ArmAccountRun()
    {
        var config = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
        if (_reading || !AccountRunConditions(config, !_accountRunRequested && !SessionState.GetBool(Key + "AccountRunRequested", false), false))
        { Record("BLOCKED", "Account run requires an unused protected ready session, idle saves, original OFF configuration and unopened inactive popups", PopupAdmissionDetails()); return; }
        _accountRunRequested = true; // A failed or repeated request cannot silently rearm this run.
        SessionState.SetBool(Key + "AccountRunRequested", true);
        var authentication = _authentication;
        ReadServer(() =>
        {
            if (!ReferenceEquals(authentication, _authentication) || !AccountRunConditions(config, true, true))
            { Record("BLOCKED", "Fresh original account snapshot did not match the ready unused in-memory state", PopupAdmissionDetails()); return; }
            var field = typeof(PopupStoreAccountPhaseConfiguration).GetField("_enableAccountMode", BindingFlags.Instance | BindingFlags.NonPublic);
            string path = AssetDatabase.GetAssetPath(config);
            if (field == null || field.FieldType != typeof(bool) || !EditorUtility.IsPersistent(config)
                || path != "Assets/Resources/UI/PopupStore/Account/PopupStoreAccountPhaseConfiguration.asset" || !File.Exists(path))
            { Record("BLOCKED", "Original account configuration contract unavailable"); return; }
            _configurationPath = path; _configurationHash = Hash(File.ReadAllText(path)); _ownedConfiguration = config;
            SessionState.SetString(Key + "OwnedConfigurationPath", path);
            SessionState.SetString(Key + "OwnedConfigurationHash", _configurationHash);
            SessionState.SetBool(Key + "OwnsAccountMode", true);
            try
            {
                field.SetValue(config, true); // Do not mark dirty or save the asset.
                if (!config.EnableAccountMode || EditorUtility.IsDirty(config)
                    || Hash(File.ReadAllText(path)) != _configurationHash
                    || !Record("ACCOUNT_RUN_ARMED", "Explicit memory-only switch; user must enter and start through the original UI", new JObject {
                        ["accountMode"] = true, ["persistentFileUnchanged"] = true, ["usedCount"] = _latestSnapshot.Record.UsedCount,
                        ["serverDia"] = _latestSnapshot.Diamonds, ["automaticStart"] = false, ["automaticReward"] = false }))
                    RestoreAccountMode();
            }
            catch { RestoreAccountMode(); Record("BLOCKED", "Memory-only account switch failed"); }
        }, true);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) RestoreAccountMode();
    }

    private static bool MemoryRecordMatches(PopupStoreGameDataRecord original)
    {
        var field = typeof(UserInfo).GetField("_popupStoreAccountJson", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(string)) return false;
        string json = (string)field.GetValue(null);
        return PopupStoreGameDataRecord.TryRead(json, json != null, out var memory, out _) && original.Matches(memory);
    }

    private static void RestoreAccountMode()
    {
        if (!SessionState.GetBool(Key + "OwnsAccountMode", false)) return;
        var config = _ownedConfiguration != null ? _ownedConfiguration
            : AssetDatabase.LoadAssetAtPath<PopupStoreAccountPhaseConfiguration>(SessionState.GetString(Key + "OwnedConfigurationPath", ""));
        var field = typeof(PopupStoreAccountPhaseConfiguration).GetField("_enableAccountMode", BindingFlags.Instance | BindingFlags.NonPublic);
        if (config == null || field == null || field.FieldType != typeof(bool))
        { Record("BLOCKED", "Owned memory-only account switch could not be restored"); return; }
        field.SetValue(config, false);
        string path = SessionState.GetString(Key + "OwnedConfigurationPath", "");
        bool unchanged = File.Exists(path) && Hash(File.ReadAllText(path)) == SessionState.GetString(Key + "OwnedConfigurationHash", "");
        Record("ACCOUNT_MODE_RESTORED", "Owned account switch returned to OFF without saving the asset", new JObject {
            ["accountMode"] = config.EnableAccountMode, ["persistentFileUnchanged"] = unchanged, ["assetDirty"] = EditorUtility.IsDirty(config) });
        if (config.EnableAccountMode) return;
        SessionState.EraseBool(Key + "OwnsAccountMode"); SessionState.EraseString(Key + "OwnedConfigurationPath");
        SessionState.EraseString(Key + "OwnedConfigurationHash"); _ownedConfiguration = null;
    }

    private static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || _reading)
        { Record("BLOCKED", "Finish requires Play OFF without an active read"); return; }
        RestoreAccountMode();
        if (SessionState.GetBool(Key + "OwnsAccountMode", false)) return;
        if (Active && SessionState.GetBool(Key + "OwnsLatch", false))
        {
            EditorApplication.ExecuteMenuItem(Menu + "안전 잠금 해제 (Play OFF)");
            if (EditorAccountSafety.IsArmed) { Record("BLOCKED", "Owned safety latch was not restored"); return; }
        }
        SessionState.EraseBool(Key + "Active"); SessionState.EraseBool(Key + "OwnsLatch");
        SessionState.EraseBool(Key + "IdentityMatched"); SessionState.EraseString(Key + "Expected");
        SessionState.EraseInt(Key + "BaselineDia");
        _owner = null; _authentication = null; _account = null; _permission = _loginStarted = false;
        Record("FINISHED", "Owned latch restored; no logout or account-mode change");
    }

    private static string Hash(string value)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").ToLowerInvariant();
    }
    private static bool Record(string state, string reason, JObject details = null)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "live-evidence.json");
            var root = File.Exists(path) ? Parse(File.ReadAllText(path)) : new JObject { ["events"] = new JArray() };
            var item = details ?? new JObject(); item["state"] = state; item["reason"] = reason; item["utc"] = DateTime.UtcNow.ToString("O");
            ((JArray)root["events"]).Add(item); root["latest"] = item.DeepClone();
            File.WriteAllText(path, root.ToString(Formatting.Indented));
            Debug.Log("ACCOUNT_CLOSE_REVIEW: " + state + "; " + reason);
            return true;
        }
        catch { Debug.LogWarning("ACCOUNT_CLOSE_REVIEW: evidence could not be written"); return false; }
    }
}

namespace Muks.BackEnd
{
    public partial class BackendManager
    {
        internal bool EditorCloseReviewOfflineOwner => IsOfflineOwner;
        internal PopupStoreGameDataAccountBridge EditorCloseReviewExistingPopupBridge => _popupAccountBridge;
        // Read-only completed authentication identity, including A -> B -> A changes.
        // Missing/changed internal layout returns null and the Editor review fails closed.
        internal object EditorCloseReviewAuthenticationIdentity => _gameDataRestoreContext == null ? null
            : typeof(GameDataRestoreContext).GetField("_authentication", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_gameDataRestoreContext);
    }
}
#endif
