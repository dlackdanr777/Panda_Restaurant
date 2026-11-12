#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Admission/readiness policy and inactive authored-prefab checks only. These tests never create an account owner,
// call an SDK, alter UserInfo, or replace the Editor session's safety latch.
public sealed class EditorAccountSafetyTests
{
    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, true)]
    public void DebugAction_IsBlockedByEitherExplicitSafetyOwner(bool armed, bool legacy, bool expected)
    {
        Assert.That(EditorAccountSafetyPolicy.IsDebugActionBlocked(armed, legacy), Is.EqualTo(expected));
    }

    [Test]
    public void UnarmedAdmission_DoesNotReplaceExistingEditorPlayPolicy()
    {
        Assert.That(EditorAccountSafetyPolicy.CanEnterPlay(false, true, true, true, true,
            false, false, false, true, true, true, out _), Is.True);
    }

    [Test]
    public void ArmedAdmission_AllowsOnlyIdleCleanFirstLoadingWithBothReloadsAndNoLiveOwner()
    {
        Assert.That(CanEnter(), Is.True);
    }

    [TestCase("playing")]
    [TestCase("compiling")]
    [TestCase("updating")]
    [TestCase("dirty")]
    [TestCase("wrong-scene")]
    [TestCase("domain-reload-disabled")]
    [TestCase("scene-reload-disabled")]
    [TestCase("sdk-initialized")]
    [TestCase("sdk-logged-in")]
    [TestCase("backend-owner")]
    public void ArmedAdmission_RejectsEachUnsafeEntryCondition(string fault)
    {
        bool accepted = EditorAccountSafetyPolicy.CanEnterPlay(true,
            fault == "playing", fault == "compiling", fault == "updating", fault == "dirty",
            fault != "wrong-scene", fault != "domain-reload-disabled", fault != "scene-reload-disabled",
            fault == "sdk-initialized", fault == "sdk-logged-in", fault == "backend-owner", out string reason);
        Assert.That(accepted, Is.False, fault);
        Assert.That(reason, Is.Not.Null.And.Not.Empty, "A rejected launch must explain its blocking condition.");
    }

    [Test]
    public void ReadyAccount_RequiresAuthenticatedCurrentRestorationAndAllThreeAppliedStages()
    {
        Assert.That(EditorAccountSafetyPolicy.IsReady(Ready(), out _), Is.True);
    }

    [TestCase("SdkLoggedIn")]
    [TestCase("ManagerLoggedIn")]
    [TestCase("GameDataReady")]
    [TestCase("CurrentQuery")]
    [TestCase("SameQuery")]
    [TestCase("SameAccount")]
    [TestCase("FirstTutorialClear")]
    [TestCase("HasNickname")]
    [TestCase("StagesCurrent")]
    [TestCase("AllStagesApplied")]
    [TestCase("SaveIdle")]
    public void Readiness_RejectsEachMissingOrStalePrerequisite(string field)
    {
        var state = Ready();
        FieldInfo prerequisite = typeof(EditorAccountReadiness).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(prerequisite, Is.Not.Null, field);
        prerequisite.SetValue(state, false);
        Assert.That(EditorAccountSafetyPolicy.IsReady(state, out string reason), Is.False, field);
        Assert.That(reason, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(0, 3)]
    [TestCase(2, 3)]
    [TestCase(4, 3)]
    [TestCase(3, 0)]
    [TestCase(3, 2)]
    [TestCase(3, 4)]
    public void Readiness_DoesNotAcceptMissingExtraOrIncompleteStageCounts(int required, int completed)
    {
        var state = Ready();
        state.RequiredStageCount = required;
        state.CompletedStageCount = completed;
        Assert.That(EditorAccountSafetyPolicy.IsReady(state, out string reason), Is.False);
        Assert.That(reason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Readiness_NullOrUninitializedEvidenceCannotEnableGameProgress()
    {
        Assert.That(EditorAccountSafetyPolicy.IsReady(null, out string nullReason), Is.False);
        Assert.That(nullReason, Is.Not.Null.And.Not.Empty);
        Assert.That(EditorAccountSafetyPolicy.IsReady(new EditorAccountReadiness(), out string emptyReason), Is.False);
        Assert.That(emptyReason, Is.Not.Null.And.Not.Empty);
    }

    private const string ReviewAccount = "ed989c50-b1d8-11f1-9aad-41b11d5c4932";
    private const string ReviewOwner = "2026-11-01T00:00:00.000Z";
    private const string ExistingServerRow = "{\"rows\":[{\"owner_inDate\":{\"S\":\"2026-11-01T00:00:00.000Z\"},"
        + "\"inDate\":{\"S\":\"existing-row\"},\"Dia\":{\"N\":\"26527\"},"
        + "\"UserId\":{\"S\":\"Existing QA\"},\"IsFirstTutorialClear\":{\"BOOL\":true}}],\"firstKey\":null}";

    [Test]
    public void CloseReview_ExistingTokenIdentityRequiresExactApprovedAccountAndHttp200()
    {
        Assert.That(EditorAccountCloseReview.IsExpectedIdentity(ReviewAccount, ReviewAccount, "200"), Is.True);
    }

    [TestCase(true, "201", true)]
    [TestCase(true, "200", false)]
    [TestCase(false, "201", false)]
    [TestCase(true, "401", false)]
    [TestCase(true, null, false)]
    [TestCase(true, "", false)]
    public void CloseReview_TokenLoginUsesItsOwn201SuccessContract(bool success, string statusCode, bool expected)
    {
        Assert.That(EditorAccountCloseReview.IsSuccessfulExistingTokenResponse(success, statusCode), Is.EqualTo(expected));
    }

    [TestCase("201", "same")]
    [TestCase("401", "same")]
    [TestCase("", "same")]
    [TestCase("200", "different")]
    [TestCase("200", "uppercase")]
    [TestCase("200", "missing")]
    [TestCase("200", "invalid")]
    public void CloseReview_RejectsNewAccountResponseAndWrongTokenIdentity(string status, string identity)
    {
        string actual = identity == "same" ? ReviewAccount
            : identity == "different" ? "00000000-0000-0000-0000-000000000001"
            : identity == "uppercase" ? ReviewAccount.ToUpperInvariant()
            : identity == "missing" ? null : "not-an-account-guid";
        Assert.That(EditorAccountCloseReview.IsExpectedIdentity(ReviewAccount, actual, status), Is.False);
    }

    [Test]
    public void CloseReview_ReadOnlyRawEvidenceAcceptsExistingAccountWithoutPopupHistory()
    {
        Assert.That(EditorAccountCloseReview.TryValidateGameDataRaw(ExistingServerRow, ReviewOwner,
            out var snapshot, out string error), Is.True, error);
        Assert.That(snapshot.Diamonds, Is.EqualTo(26527));
        Assert.That(snapshot.Owner, Is.EqualTo(ReviewOwner));
        Assert.That(snapshot.Row, Is.EqualTo("existing-row"));
        // This is a synthetic fixture, not a claim about the real account's server balance.
    }

    [TestCase("no-rows")]
    [TestCase("two-rows")]
    [TestCase("pagination")]
    [TestCase("wrong-owner")]
    [TestCase("missing-row-id")]
    [TestCase("negative-dia")]
    [TestCase("overflow-dia")]
    [TestCase("fraction-dia")]
    [TestCase("nonnumeric-dia")]
    [TestCase("wrong-dia-type")]
    [TestCase("missing-dia")]
    [TestCase("duplicate-dia")]
    [TestCase("blank-nickname")]
    [TestCase("missing-nickname")]
    [TestCase("tutorial-incomplete")]
    [TestCase("missing-tutorial")]
    [TestCase("corrupt-popup-record")]
    [TestCase("malformed-json")]
    public void CloseReview_RejectsAmbiguousOrIncompleteOriginalServerEvidence(string fault)
    {
        string raw = ExistingServerRow;
        switch (fault)
        {
            case "no-rows": raw = "{\"rows\":[],\"firstKey\":null}"; break;
            case "two-rows":
                int from = raw.IndexOf("[{", System.StringComparison.Ordinal) + 1;
                int to = raw.IndexOf("],", System.StringComparison.Ordinal);
                string row = raw.Substring(from, to - from);
                raw = "{\"rows\":[" + row + "," + row + "],\"firstKey\":null}";
                break;
            case "pagination": raw = raw.Replace("\"firstKey\":null", "\"firstKey\":{\"inDate\":{\"S\":\"next-row\"}}"); break;
            case "wrong-owner": raw = raw.Replace(ReviewOwner, "different-owner"); break;
            case "missing-row-id": raw = raw.Replace("existing-row", ""); break;
            case "negative-dia": raw = raw.Replace("26527", "-1"); break;
            case "overflow-dia": raw = raw.Replace("26527", "2147483648"); break;
            case "fraction-dia": raw = raw.Replace("26527", "1.5"); break;
            case "nonnumeric-dia": raw = raw.Replace("26527", "not-a-number"); break;
            case "wrong-dia-type": raw = raw.Replace("\"Dia\":{\"N\":", "\"Dia\":{\"S\":"); break;
            case "missing-dia": raw = raw.Replace("\"Dia\":{\"N\":\"26527\"},", ""); break;
            case "duplicate-dia": raw = raw.Replace("\"Dia\":", "\"Dia\":{\"N\":\"0\"},\"Dia\":"); break;
            case "blank-nickname": raw = raw.Replace("Existing QA", " "); break;
            case "missing-nickname": raw = raw.Replace("\"UserId\":{\"S\":\"Existing QA\"},", ""); break;
            case "tutorial-incomplete": raw = raw.Replace("\"BOOL\":true", "\"BOOL\":false"); break;
            case "missing-tutorial": raw = raw.Replace(",\"IsFirstTutorialClear\":{\"BOOL\":true}", ""); break;
            case "corrupt-popup-record": raw = raw.Replace("\"IsFirstTutorialClear\":", "\"PopupStoreAccountV1\":{\"S\":\"not-json\"},\"IsFirstTutorialClear\":"); break;
            case "malformed-json": raw = "{"; break;
        }
        Assert.That(EditorAccountCloseReview.TryValidateGameDataRaw(raw, ReviewOwner,
            out _, out string error), Is.False, fault);
        Assert.That(error, Is.Not.Null.And.Not.Empty, fault);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void CloseReview_StaleOrInactiveCallbackCannotConsumeRestorePermission(bool active, bool sameAuthentication)
    {
        bool permission = true;
        Assert.That(EditorAccountCloseReview.TryConsumeContinuation(active, sameAuthentication, ref permission), Is.False);
        Assert.That(permission, Is.True, "Rejected callbacks must not consume another current callback's permission.");
    }

    [Test]
    public void CloseReview_RestoreContinuationIsConsumedBeforeFirstInvocationAndCannotRunTwice()
    {
        bool permission = true;
        Assert.That(EditorAccountCloseReview.TryConsumeContinuation(true, true, ref permission), Is.True);
        Assert.That(permission, Is.False);
        Assert.That(EditorAccountCloseReview.TryConsumeContinuation(true, true, ref permission), Is.False);
    }

    [Test]
    public void CloseReview_FirstSdkReplyConsumesItsAdmissionBeforeCallbackWork()
    {
        bool delivered = false;
        Assert.That(EditorAccountCloseReview.TryAcceptReply(ref delivered), Is.True);
        Assert.That(delivered, Is.True);
        Assert.That(EditorAccountCloseReview.TryAcceptReply(ref delivered), Is.False);
    }

    [Test]
    public void CloseReview_DuplicateSdkReplyCannotRearmConsumedRestorationPermission()
    {
        bool delivered = false, permission = false;
        int continuations = 0;
        for (int reply = 0; reply < 2; reply++)
        {
            if (!EditorAccountCloseReview.TryAcceptReply(ref delivered)) continue;
            permission = true;
            if (EditorAccountCloseReview.TryConsumeContinuation(true, true, ref permission)) continuations++;
        }
        Assert.That(continuations, Is.EqualTo(1));
        Assert.That(permission, Is.False);
    }

    [Test]
    public void CloseReview_AccountRunRequiresEveryIndependentVerifiedAdmissionCondition()
    {
        Assert.That(EditorAccountCloseReview.CanArmAccountRun(true, true, true, true, true, true, true, true), Is.True);
    }

    [TestCase("authentication-changed")]
    [TestCase("stage-restoration-incomplete")]
    [TestCase("main-start-unprotected")]
    [TestCase("review-already-used")]
    [TestCase("account-mode-already-enabled")]
    [TestCase("popup-already-used")]
    [TestCase("save-in-flight")]
    [TestCase("server-record-used-or-pending")]
    public void CloseReview_AccountRunIsBlockedByEachUnsafeAdmissionCondition(string fault)
    {
        Assert.That(EditorAccountCloseReview.CanArmAccountRun(
            fault != "authentication-changed", fault != "stage-restoration-incomplete",
            fault != "main-start-unprotected", fault != "review-already-used",
            fault != "account-mode-already-enabled", fault != "popup-already-used",
            fault != "save-in-flight", fault != "server-record-used-or-pending"), Is.False, fault);
    }

    [Test]
    public void CloseReview_AuthoredInactivePopupIsEligibleWithoutStartingOrRebindingIt()
    {
        using (var probe = new InactivePopupReadProbe())
        {
            Assert.That(EditorAccountCloseReview.IsFreshInactivePopupForAccountRun(probe.View), Is.True);
            Assert.That(probe.Root.activeSelf, Is.False);
            Assert.That(probe.Root.GetComponent<PopupStoreStartController>().Host, Is.Null);
            Assert.That(probe.Root.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(probe.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            Assert.That(probe.Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
            Assert.That(probe.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    [TestCase("active-self")]
    [TestCase("running")]
    [TestCase("retired")]
    [TestCase("phase-bound")]
    [TestCase("recovery-bound")]
    [TestCase("start-disabled")]
    public void CloseReview_ExistingPopupMustBeUnusedBeforeAccountBinding(string fault)
    {
        using (var probe = new InactivePopupReadProbe())
        {
            Assert.That(EditorAccountCloseReview.IsFreshInactivePopupForAccountRun(probe.View), Is.True, "Authored baseline");
            switch (fault)
            {
                case "active-self": probe.Root.SetActive(true); break;
                case "running": SetProbeField(probe.Root.GetComponent<PopupStoreBusinessClock>(), "_state", PopupStoreBusinessState.Running); break;
                case "retired": SetProbeField(probe.Root.GetComponent<PopupStoreSessionLifetime>(), "_completedForClose", true); break;
                case "phase-bound": probe.Root.AddComponent<PopupStorePhasePreview>(); break;
                case "recovery-bound": probe.Root.AddComponent<PopupStoreAccountRecoveryPresentation>(); break;
                case "start-disabled": probe.Root.GetComponent<PopupStoreStartController>().enabled = false; break;
            }
            // The parent remains inactive, including the activeSelf fault, so this
            // never activates gameplay or invokes a live account factory.
            Assert.That(probe.Root.activeInHierarchy, Is.False);
            Assert.That(EditorAccountCloseReview.IsFreshInactivePopupForAccountRun(probe.View), Is.False, fault);
        }
    }

    [Test]
    public void CloseReview_PreviouslyBoundGradeCannotBecomeFreshByHidingThePopup()
    {
        using (var probe = new InactivePopupReadProbe())
        {
            Assert.That(EditorAccountCloseReview.IsFreshInactivePopupForAccountRun(probe.View), Is.True);
            Assert.That(probe.Root.GetComponent<PopupStoreGradeSession>().Refresh(), Is.True);
            probe.View.Hide();
            Assert.That(probe.Root.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(probe.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(EditorAccountCloseReview.IsFreshInactivePopupForAccountRun(probe.View), Is.False);
        }
    }

    private static void SetProbeField(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(owner, value);
    }

    private sealed class InactivePopupReadProbe : System.IDisposable
    {
        private readonly GameObject _scope;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        public readonly UIPopupStore View;
        public GameObject Root => View.gameObject;

        public InactivePopupReadProbe()
        {
            _scope = new GameObject("account-safety-inactive-popup-probe");
            _scope.SetActive(false);
            try
            {
                var prefab = Resources.Load<GameObject>("UI/PopupStore/UIPopupStore");
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.activeSelf, Is.False);
                var root = Object.Instantiate(prefab, _scope.transform, false);
                _fonts.BindBeforeActivation(root);
                View = root.GetComponent<UIPopupStore>();
                Assert.That(View, Is.Not.Null);
                View.Init();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_scope != null) Object.DestroyImmediate(_scope);
            _fonts.Dispose();
        }
    }

    // Static integration evidence only: these assertions check the actual source
    // wiring; they do not execute Unity callbacks or prove a real Play/reload run.
    private static bool CanEnter() => EditorAccountSafetyPolicy.CanEnterPlay(true, false, false, false,
        false, true, true, true, false, false, false, out _);

    private static EditorAccountReadiness Ready() => new EditorAccountReadiness
    {
        SdkLoggedIn = true,
        ManagerLoggedIn = true,
        GameDataReady = true,
        CurrentQuery = true,
        SameQuery = true,
        SameAccount = true,
        FirstTutorialClear = true,
        HasNickname = true,
        StagesCurrent = true,
        AllStagesApplied = true,
        SaveIdle = true,
        RequiredStageCount = 3,
        CompletedStageCount = 3
    };
}
#endif
