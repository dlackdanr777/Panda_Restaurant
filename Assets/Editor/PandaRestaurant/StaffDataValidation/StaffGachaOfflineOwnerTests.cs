#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEngine;

public sealed class StaffGachaOfflineOwnerTests
{
    [Test]
    public void OfflineOwner_RealUnityLifecycleNeverClaimsSingletonInitializesSdkOrPublishesGlobalEvents()
    {
        object singleton = Field("_instance", true).GetValue(null);
        bool saving = BackendManager.IsSaveEnabled;
        int actualDiamonds = UserInfo.Dia;
        int balance = 110, globalEvents = 0;
        var game = new MemoryGame();
        var stage = new NoStage();
        var wallet = new StaffPurchaseDiamondWallet(() => balance, value => balance = value, () => { });
        Action observer = () => globalEvents++;
        BackendManager.OnPauseHandler += observer;
        BackendManager.OnResumeHandler += observer;
        BackendManager.OnExitHandler += observer;
        BackendManager owner = null;
        try
        {
            owner = BackendManager.CreateEditorOfflineOwner(game, stage, wallet, _ => null, _ => { });
            Assert.That(owner, Is.Not.Null);
            Assert.That(owner.gameObject.activeSelf, Is.True);
            Assert.That(owner.IsEditorOfflineOwner, Is.True);
            Assert.That(Field("_instance", true).GetValue(null), Is.SameAs(singleton));
            Assert.That(Field("_gameDataInitializationPolicy").GetValue(owner), Is.Not.Null);
            // Real component construction, then explicit EditMode lifecycle probes. No fabricated MonoBehaviour.
            Invoke(owner, "Awake");
            Invoke(owner, "Init");
            Invoke(owner, "OnApplicationPause", true);
            Invoke(owner, "OnApplicationPause", false);
            Invoke(owner, "OnApplicationQuit");
            Invoke(owner, "CheckTokenValidity");
            Assert.That(globalEvents, Is.Zero);
            Assert.That(game.Calls, Is.Zero, "Lifecycle must not even request injected account data");
            Assert.That(stage.Calls, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => { var ignored = owner.ServerTime; });
            Assert.Throws<InvalidOperationException>(() => owner.FetchGamerIdAsync());
            Assert.Throws<InvalidOperationException>(() => owner.GuestLogin());
            Assert.Throws<InvalidOperationException>(() => owner.RefreshTheBackendToken(1));

            var attempt = owner.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
            game.LoggedIn = game.NativeLoggedIn = true;
            Assert.That(owner.CompleteGameDataAuthentication(attempt, Success()), Is.True);
            Assert.That(owner.IsLogin, Is.True);
            Assert.That(BackendManager.IsSaveEnabled, Is.EqualTo(saving));
            owner.LogOut();
            Assert.That(BackendManager.IsSaveEnabled, Is.EqualTo(saving), "Offline logout must not disable real account saves");
            Assert.That(Field("_instance", true).GetValue(null), Is.SameAs(singleton));
            Assert.That(UserInfo.Dia, Is.EqualTo(actualDiamonds));
            Assert.That(balance, Is.EqualTo(110));
        }
        finally
        {
            BackendManager.OnPauseHandler -= observer;
            BackendManager.OnResumeHandler -= observer;
            BackendManager.OnExitHandler -= observer;
            if (owner != null) owner.DestroyEditorOfflineOwner();
        }
        Assert.That(Field("_instance", true).GetValue(null), Is.SameAs(singleton));
        Assert.That(BackendManager.IsSaveEnabled, Is.EqualTo(saving));
    }

    [Test]
    public void OfflineOwner_MissingDependencyFailsBeforeActivationAndNeverUsesProductionFallbacks()
    {
        int before = Resources.FindObjectsOfTypeAll<BackendManager>().Length;
        object singleton = Field("_instance", true).GetValue(null);
        int balance = 110;
        var game = new MemoryGame();
        var stage = new NoStage();
        var wallet = new StaffPurchaseDiamondWallet(() => balance, value => balance = value, () => { });
        Func<IReadOnlyList<GachaData>, GachaStaffData> draw = _ => null;
        Action<StaffGachaPurchaseExecution> effects = _ => { };
        Assert.Throws<ArgumentNullException>(() => BackendManager.CreateEditorOfflineOwner(null, stage, wallet, draw, effects));
        Assert.Throws<ArgumentNullException>(() => BackendManager.CreateEditorOfflineOwner(game, null, wallet, draw, effects));
        Assert.Throws<ArgumentNullException>(() => BackendManager.CreateEditorOfflineOwner(game, stage, null, draw, effects));
        Assert.Throws<ArgumentNullException>(() => BackendManager.CreateEditorOfflineOwner(game, stage, wallet, null, effects));
        Assert.Throws<ArgumentNullException>(() => BackendManager.CreateEditorOfflineOwner(game, stage, wallet, draw, null));
        Assert.That(Resources.FindObjectsOfTypeAll<BackendManager>().Length, Is.EqualTo(before));
        var owner = BackendManager.CreateEditorOfflineOwner(game, stage, wallet, draw, effects);
        try
        {
            owner.StaffPurchaseWallet = null;
            Assert.Throws<InvalidOperationException>(() => { var ignored = owner.StaffPurchaseWallet; });
            Assert.That(owner.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out var rejected, out var error), Is.False);
            Assert.That(rejected, Is.Null); Assert.That(error, Is.Not.Empty);
            Field("_gameDataTransport").SetValue(owner, null);
            var gameGetter = typeof(BackendManager).GetProperty("GameDataTransport", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(Assert.Throws<TargetInvocationException>(() => gameGetter.GetValue(owner)).InnerException,
                Is.TypeOf<InvalidOperationException>());
            Field("_stageDataTransport").SetValue(owner, null);
            var stageGetter = typeof(BackendManager).GetProperty("StageDataTransport", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(Assert.Throws<TargetInvocationException>(() => stageGetter.GetValue(owner)).InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(game.Calls, Is.Zero); Assert.That(stage.Calls, Is.Zero);
        }
        finally { owner.DestroyEditorOfflineOwner(); }
        Assert.That(Field("_instance", true).GetValue(null), Is.SameAs(singleton));
        Assert.That(Resources.FindObjectsOfTypeAll<BackendManager>().Length, Is.EqualTo(before));
    }

    [Test]
    public void OfflineOwner_ExplicitEditModeDisposalReleasesOwnedResultsAndCannotPublishLateCompletion()
    {
        int wrappers = Resources.FindObjectsOfTypeAll<GachaStaffData>().Length;
        object singleton = Field("_instance", true).GetValue(null);
        var session = new StaffGachaOfflineSession(Resources.LoadAll<StaffData>("StaffData"));
        var owner = session.Owner;
        try
        {
            Assert.That(session.TryStart(StaffGachaOfflineCase.SingleNew, out string error), Is.True, error);
            var request = session.Request;
            Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(wrappers + 1));
            owner.DestroyEditorOfflineOwner();
            owner.DestroyEditorOfflineOwner(); // Idempotent even when native Unity object was already destroyed.
            Assert.That(request.DrawnStaff, Is.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(wrappers));
            Assert.That(session.ReplySuccess(), Is.True, "A retained memory response may arrive after display-owner destruction");
            Assert.That(request.IsCompleted, Is.False);
            Assert.That(session.Diamonds, Is.EqualTo(110));
            Assert.That(session.RecordNotifications, Is.Zero);
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(request.Identity.Target), Is.True,
                "Offline owner cleanup is not a request Reset or a target-lock bypass");
            Assert.That(Field("_instance", true).GetValue(null), Is.SameAs(singleton));
        }
        finally { session.Dispose(); }
    }

    private static FieldInfo Field(string name, bool isStatic = false) => typeof(BackendManager).GetField(name,
        BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
    private static void Invoke(BackendManager owner, string name, params object[] args) => typeof(BackendManager)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    private static BackendReturnObject Success()
    {
        var response = new BackendReturnObject();
        foreach (var pair in new Dictionary<string, string> { ["StatusCode"] = "200", ["ReturnValue"] = "", ["ErrorCode"] = "", ["Message"] = "" })
        {
            var property = typeof(BackendReturnObject).GetProperty(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property.SetValue(response, Convert.ChangeType(pair.Value, property.PropertyType, CultureInfo.InvariantCulture));
        }
        return response;
    }
    private sealed class MemoryGame : IGameDataBackendTransport
    {
        public bool LoggedIn { get; set; }
        public bool NativeLoggedIn { get; set; }
        public string AccountInDate { get; } = "offline-owner-test:" + Guid.NewGuid().ToString("N");
        public bool GameplaySaveAllowed => true;
        public int Calls;
        public void Get(string account, Action<BackendReturnObject> callback) { Calls++; throw new InvalidOperationException("Unexpected test lookup"); }
        public void Insert(Param values, Action<BackendReturnObject> callback) { Calls++; throw new InvalidOperationException("Unexpected test insert"); }
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback) { Calls++; throw new InvalidOperationException("Unexpected test update"); }
        public Param LatestValues() { Calls++; return new Param(); }
        public IReadOnlyList<StaffData> ReadCatalog() { Calls++; return Array.Empty<StaffData>(); }
        public bool Restore(BackendReturnObject response) { Calls++; return false; }
        public Param InitialValues() { Calls++; return new Param(); }
    }
    private sealed class NoStage : IStageDataLoadTransport
    {
        public int Calls;
        public void Get(EStage stage, string account, Func<bool> current, Action<BackendReturnObject> callback) { Calls++; throw new InvalidOperationException("No offline Stage loading"); }
        public BackendReturnObject Get(EStage stage, string account, Func<bool> current) { Calls++; throw new InvalidOperationException("No offline Stage loading"); }
        public bool Apply(EStage stage, BackendReturnObject response, Func<bool> current, bool asynchronous) { Calls++; return false; }
        public StaffStageRuntimeSnapshot ReadStaff(EStage stage) { Calls++; return null; }
    }
}
#endif
