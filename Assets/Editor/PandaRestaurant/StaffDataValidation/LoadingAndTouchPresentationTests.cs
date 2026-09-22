#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class LoadingAndTouchPresentationTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void LoadingScene_FirstYieldOnlyDefersOneFrame()
    {
        FieldInfo nextScene = typeof(LoadingSceneManager).GetField("_nextScene", StaticPrivate);
        Assert.That(nextScene, Is.Not.Null);
        object previous = nextScene.GetValue(null);
        var owner = new GameObject("loading scene regression owner");

        try
        {
            nextScene.SetValue(null, "Stage1");
            IEnumerator routine = CreateLoadingRoutine(owner.AddComponent<LoadingSceneManager>());

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.Current, Is.Null,
                "The target scene must begin loading after one frame, not after a fade callback gate.");
        }
        finally
        {
            nextScene.SetValue(null, previous);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void LoadingScene_MissingTargetStopsBeforeRequest()
    {
        FieldInfo nextScene = typeof(LoadingSceneManager).GetField("_nextScene", StaticPrivate);
        Assert.That(nextScene, Is.Not.Null);
        object previous = nextScene.GetValue(null);
        var owner = new GameObject("missing loading target regression owner");

        try
        {
            nextScene.SetValue(null, null);
            IEnumerator routine = CreateLoadingRoutine(owner.AddComponent<LoadingSceneManager>());
            LogAssert.Expect(LogType.Error,
                "[LoadingSceneManager] 다음 씬이 지정되지 않아 로딩을 시작할 수 없습니다.");
            Assert.That(routine.MoveNext(), Is.False);
        }
        finally
        {
            nextScene.SetValue(null, previous);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void LoadingScene_DoesNotBlockStartupWithForcedCollectionOrFadeWait()
    {
        string sourcePath = Path.Combine(Application.dataPath, "Scripts", "Manager", "LoadingSceneManager.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("GC.Collect"));
        Assert.That(source, Does.Not.Contain("new WaitUntil"));
        Assert.That(source, Does.Not.Contain("_isStart"));
    }

    [Test]
    public void TouchPointer_QuickReleaseTransitionsDirectlyToEnd()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animation/Touch/TouchAnimator.controller");
        Assert.That(controller, Is.Not.Null);

        AnimatorState[] states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
        AnimatorState start = states.Single(state => state.name == "Touch_Start");
        AnimatorState idle = states.Single(state => state.name == "Touch_Idle");
        AnimatorState end = states.Single(state => state.name == "Touch_End");
        AnimatorStateTransition directEnd = start.transitions.Single(transition => transition.destinationState == end);

        Assert.That(start.transitions.Any(transition => transition.destinationState == idle), Is.True);
        Assert.That(directEnd.hasExitTime, Is.False);
        Assert.That(directEnd.duration, Is.EqualTo(0f));
        Assert.That(directEnd.conditions.Length, Is.EqualTo(1));
        Assert.That(directEnd.conditions[0].parameter, Is.EqualTo("Touch"));
        Assert.That(directEnd.conditions[0].mode, Is.EqualTo(AnimatorConditionMode.IfNot));
    }

    [Test]
    public void TouchPointer_EndIsShortMonotonicFadeWithOneHideEvent()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Touch/Touch_End.anim");
        Assert.That(clip, Is.Not.Null);

        EditorCurveBinding alphaBinding = AnimationUtility.GetCurveBindings(clip).Single(binding =>
            binding.path == "Image" && binding.type == typeof(Image) && binding.propertyName == "m_Color.a");
        AnimationCurve alpha = AnimationUtility.GetEditorCurve(clip, alphaBinding);
        Assert.That(alpha, Is.Not.Null);
        Assert.That(alpha.keys.Length, Is.EqualTo(2));
        Assert.That(alpha.keys[0].time, Is.EqualTo(0f).Within(0.00001f));
        Assert.That(alpha.keys[0].value, Is.EqualTo(0.5882353f).Within(0.00001f));
        Assert.That(alpha.keys[1].time, Is.EqualTo(0.15f).Within(0.00001f));
        Assert.That(alpha.keys[1].value, Is.EqualTo(0f).Within(0.00001f));

        float previous = alpha.Evaluate(0f);
        for (int sample = 1; sample <= 150; sample++)
        {
            float current = alpha.Evaluate(0.15f * sample / 150f);
            Assert.That(current, Is.InRange(-0.00001f, 0.5882453f));
            Assert.That(current, Is.LessThanOrEqualTo(previous + 0.00001f));
            previous = current;
        }

        Assert.That(clip.length, Is.EqualTo(0.15f).Within(0.00001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        Assert.That(events.Length, Is.EqualTo(1));
        Assert.That(events[0].functionName, Is.EqualTo("Hide"));
        Assert.That(events[0].time, Is.EqualTo(0.15f).Within(0.00001f));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Manager/TouchManager/UITouchImage.prefab");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animation/Touch/TouchAnimator.controller");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(controller, Is.Not.Null);
        Assert.That(prefab.GetComponent<UITouchImage>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Animator>().runtimeAnimatorController, Is.SameAs(controller));
    }

    private static IEnumerator CreateLoadingRoutine(LoadingSceneManager manager)
    {
        MethodInfo method = typeof(LoadingSceneManager).GetMethod("LoadScene", InstancePrivate);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(manager, null);
    }
}
#endif
