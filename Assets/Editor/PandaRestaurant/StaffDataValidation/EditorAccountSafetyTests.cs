#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;

// Pure admission/readiness checks only. These tests never create an account owner,
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
