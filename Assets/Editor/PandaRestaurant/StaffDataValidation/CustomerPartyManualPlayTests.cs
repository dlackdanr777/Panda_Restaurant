#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CustomerPartyManualPlayTests
{
    [Test]
    public void FailedManualInitializationCannotCreateOnlineBackendDuringCleanup()
    {
        Assert.That(Muks.BackEnd.BackendManager.ExistingInstance, Is.Null);
        var active = typeof(CustomerPartyManualPlayBootstrap).GetProperty("Active");
        active.SetValue(null, true);
        GameObject host = null;
        try
        {
            Assert.Throws<System.InvalidOperationException>(() => { var unused = Muks.BackEnd.BackendManager.Instance; });
            host = new GameObject("manual-failed-start-guard");
            var backend = host.AddComponent<Muks.BackEnd.BackendManager>();
            typeof(Muks.BackEnd.BackendManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(backend, null);
            Assert.That(backend.enabled, Is.False);
            Assert.That(Muks.BackEnd.BackendManager.ExistingInstance, Is.Null);
            Assert.That(BackEnd.Backend.IsInitialized, Is.False);
        }
        finally { if (host != null) Object.DestroyImmediate(host); active.SetValue(null, false); }
    }
    [Test]
    public void UnarmedBootstrapDoesNotTouchNormalEditorSceneOrStartSdk()
    {
        Assert.That(SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false), Is.False);
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        typeof(CustomerPartyManualPlayBootstrap).GetMethod("FenceBeforeAwake", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        CustomerPartyManualPlayBootstrap.NotifyUserDataInitialized();
        Assert.That(CustomerPartyManualPlayBootstrap.Active, Is.False);
        Assert.That(BackEnd.Backend.IsInitialized, Is.False);
        witness.AssertUnchanged("unarmed manual bootstrap");
    }
    [Test]
    public void ManualStartCannotInterruptRunningRegressionWork()
    {
        var running = typeof(GachaReviewTestRunner).GetField("_running", BindingFlags.NonPublic | BindingFlags.Static);
        bool previous = (bool)running.GetValue(null);
        try
        {
            running.SetValue(null, true);
            Assert.That(CustomerPartyStage1Preview.CanStart(out var reason), Is.False);
            Assert.That(reason, Does.Contain("회귀 검사"));
        }
        finally { running.SetValue(null, previous); }
    }
    [Test]
    public void BootstrapFencesAwakeAndWaitsForUserDataBeforeMemoryLoad()
    {
        var methods=typeof(CustomerPartyManualPlayBootstrap).GetMethods(BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(methods.Single(m=>m.Name=="FenceBeforeAwake").GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType, Is.EqualTo(RuntimeInitializeLoadType.BeforeSceneLoad));
        Assert.That(methods.Single(m=>m.Name=="InitializeAfterUserData").GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType, Is.EqualTo(RuntimeInitializeLoadType.AfterSceneLoad));
    }
    [Test]
    public void ManualToolHasNoDiskCopySceneSaveExternalEditorOrSyntheticInputEntry()
    {
        string source=File.ReadAllText("Assets/Editor/PandaRestaurant/StaffDataValidation/CustomerPartyStage1Preview.cs");
        foreach(var forbidden in new[]{"CopyFolder(","File.Copy(","Process.Start(","SaveScene(","OpenScene(","DestroyImmediate(","ExecuteEvents.","onClick.Invoke(","EditorApplication.Exit("})
            Assert.That(source, Does.Not.Contain(forbidden), forbidden);
        Assert.That(source, Does.Contain("playModeStartScene"));
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(CustomerPartyStage1Preview.ScenePath), Is.Not.Null);
    }
}
#endif
