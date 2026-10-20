#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor only. Pause runtime scene objects before Awake; never edit/save source assets.
public static class CustomerPartyManualPlayBootstrap
{
    public const string SessionKey = "Panda.PartyManual.ExplicitSession";
    public static bool Active { get; private set; }
    public static GameObject[] Roots { get; private set; }
    public static HashSet<GameObject> ActiveRoots { get; private set; }
    private static bool userDataReady, memoryReady, preparationStarted;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void FenceBeforeAwake()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        Active = true; var scene = SceneManager.GetActiveScene();
        Roots = scene.GetRootGameObjects(); ActiveRoots = new HashSet<GameObject>(Roots.Where(r => r.activeSelf));
        foreach (var root in Roots) root.SetActive(false);
        if (scene.path != "Assets/Scenes/Stage1.unity" || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
        { Fail(new InvalidOperationException("Manual Play requires original Stage1 before external initialization.")); return; }
        PrepareWhenUserDataReady();
    }
    // UserInfo and this fence are both BeforeSceneLoad callbacks; their order is unspecified.
    public static void NotifyUserDataInitialized()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        userDataReady = true; PrepareWhenUserDataReady();
    }
    private static void PrepareWhenUserDataReady()
    {
        if (!Active || !userDataReady || preparationStarted) return;
        preparationStarted = true;
        try { InvokeEditor("PrepareMemoryBeforeAwake"); memoryReady = true; }
        catch (Exception error) { Fail(error); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterUserData()
    {
        if (!Active || !EditorApplication.isPlaying) return;
        try
        {
            if (!memoryReady) throw new InvalidOperationException("Manual memory owner was not ready before Awake.");
            InvokeEditor("InitializeManualSession");
        }
        catch (Exception error) { Fail(error); }
    }
    private static void InvokeEditor(string method) => Type.GetType("CustomerPartyStage1Preview, Assembly-CSharp-Editor", true)
        .GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    private static void Fail(Exception error)
    {
        // No online fallback on failed initialization.
        Debug.LogError("PARTY_MANUAL_START_FAILED: " + error); EditorApplication.isPlaying = false;
    }
    public static void Clear() { Active = false; Roots = null; ActiveRoots = null; userDataReady = memoryReady = preparationStarted = false; }
}
#endif
