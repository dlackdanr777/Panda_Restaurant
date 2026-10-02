#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class StaffGroundContactTests
{
    [TestCase("STAFF29", 2f)]
    [TestCase("STAFF27", .1f)]
    [TestCase("STAFF16", .1f)]
    [TestCase("STAFF19", .1f)]
    public void NativeIdleRoutine_OnlyBaraHoldsFinalDroppedOrangePoseThenReturns(string id, float lastDuration)
    {
        using (var board = new StaffGroundEvidenceBoard(new[] { id }, false))
        {
            var actor = board.Actors[0];
            var routine = actor.BeginNativeIdle();
            for (int frame = 0; frame < actor.Data.IdleSprites.Length; frame++)
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(actor.Body.sprite, Is.SameAs(actor.Data.IdleSprites[frame]));
                Assert.That(StaffGroundEvidenceActor.WaitSeconds(routine.Current),
                    Is.EqualTo(frame == actor.Data.IdleSprites.Length - 1 ? lastDuration : .1f).Within(.0001f));
            }
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(actor.Body.sprite, Is.SameAs(actor.Data.Sprite));
        }
    }

    [Test]
    public void BaraIdleHold_DoesNotDelayAnAlternateSkinSequence()
    {
        using (var board = new StaffGroundEvidenceBoard(new[] { "STAFF29" }, false))
        {
            var actor = board.Actors[0];
            var routine = actor.BeginNativeIdle((Sprite[])actor.Data.IdleSprites.Clone());
            while (routine.MoveNext()) Assert.That(StaffGroundEvidenceActor.WaitSeconds(routine.Current), Is.EqualTo(.1f).Within(.0001f));
            Assert.That(actor.Body.sprite, Is.SameAs(actor.Data.Sprite));
        }
    }

    [TestCase(EStaffState.Action)]
    [TestCase(EStaffState.Run)]
    public void BaraIdleHold_DoesNotOverwriteAWorkOrMovePoseAfterItsWait(EStaffState nextState)
    {
        using (var board = new StaffGroundEvidenceBoard(new[] { "STAFF29" }, false))
        {
            var actor = board.Actors[0];
            var routine = actor.BeginNativeIdle();
            for (int frame = 0; frame < actor.Data.IdleSprites.Length; frame++) Assert.That(routine.MoveNext(), Is.True);
            var nextSprite = nextState == EStaffState.Action ? actor.Data.BackSprite : actor.Data.Sprite;
            actor.SetNativeStateWithoutGameplay(nextState);
            actor.Body.sprite = nextSprite;
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(actor.Body.sprite, Is.SameAs(nextSprite));
        }
    }

    [Test]
    public void CaptureOptionalEvidence_BaraTwoSecondPoseUsesTheNativeCoroutine()
    {
        string directory = Environment.GetEnvironmentVariable("PHASE4_BARA_HOLD_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var timeline = new List<string> { "direction\tframe\ttime\tstaff\tsprite" };
        using (var board = new StaffGroundEvidenceBoard(new[] { "STAFF29", "STAFF27" }, false))
        {
            foreach (var label in board.Actors[0].Root.transform.parent.GetComponentsInChildren<TextMesh>())
                if (label.text == "STAFF27 AFTER") label.text = "STAFF27 CONTROL";
            foreach (bool right in new[] { false, true })
            {
                string destination = Path.Combine(directory, right ? "Bara-hold-right" : "Bara-hold-left");
                Directory.CreateDirectory(destination);
                var routines = new IEnumerator[board.Actors.Count];
                var nextAt = new float[board.Actors.Count];
                for (int frame = 0; frame < 72; frame++)
                {
                    float time = frame / 10f;
                    for (int column = 0; column < board.Actors.Count; column++)
                    {
                        var actor = board.Actors[column];
                        Sprite currentPose = actor.Body.sprite;
                        actor.Sample("Idle", time, right);
                        actor.Body.sprite = currentPose;
                        if (time + .0001f >= nextAt[column])
                        {
                            if (routines[column] == null) routines[column] = actor.BeginNativeIdle();
                            if (routines[column].MoveNext()) nextAt[column] = time + StaffGroundEvidenceActor.WaitSeconds(routines[column].Current);
                            else
                            {
                                // Explicit half-second baseline between two demonstrations;
                                // the production random 5–10 second repeat interval is unchanged.
                                routines[column] = null;
                                nextAt[column] = time + .5f;
                            }
                        }
                        timeline.Add((right ? "right" : "left") + "\t" + frame + "\t" + time.ToString("F1")
                            + "\t" + actor.Data.Id + "\t" + actor.Body.sprite.name);
                    }
                    board.Capture(Path.Combine(destination, frame.ToString("D3") + ".png"), 960);
                }
            }
        }
        File.WriteAllLines(Path.Combine(directory, "native-pose-timeline.txt"), timeline);
        File.WriteAllText(Path.Combine(directory, "capture-manifest.txt"),
            "Production Chef prefab and real Staff.IdleSpriteCoroutine, isolated EditMode; 10 fps, 72 frames per direction.\n" +
            "Left: STAFF29 Bara, right: unchanged STAFF27 control. Frames 0-10: existing 0.1 s sequence; frame 12 pose: 1.1 s through 3.0 s; baseline returns at 3.1 s.\n" +
            "Two demonstrations with a 0.5 s baseline gap for evidence only. Production idle repeat interval remains random 5-10 s.\n");
    }

    [TestCase("STAFF27", 7)]
    [TestCase("STAFF29", 12)]
    public void EveryIdleFrame_HasTheSameGroundPivotAsTheStandingBody(string id, int count)
    {
        var data = Resources.Load<ChefData>("StaffData/" + id);
        Assert.That(data.IdleSprites.Length, Is.EqualTo(count));
        foreach (var sprite in data.IdleSprites.Concat(new[] { data.Sprite, data.BackSprite }))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.BottomCenter), sprite.name);
            Assert.That(sprite.pivot.y, Is.EqualTo(0).Within(.001f), sprite.name);
            Assert.That(sprite.bounds.min.y, Is.InRange(-.001f, .04f), sprite.name);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100), sprite.name);
        }
    }

    [TestCase("STAFF27", false)] [TestCase("STAFF27", true)]
    [TestCase("STAFF29", false)] [TestCase("STAFF29", true)]
    [TestCase("STAFF16", false)] [TestCase("STAFF16", true)]
    [TestCase("STAFF19", false)] [TestCase("STAFF19", true)]
    public void ProductionChef_WholeCyclesKeepBodyAndHandsAboveGround(string id, bool right)
    {
        using (var board = new StaffGroundEvidenceBoard(new[] { id }, false))
        {
            var actor = board.Actors[0];
            foreach (string mode in StaffGroundEvidenceBoard.Modes)
            {
                int frames = StaffGroundEvidenceBoard.FrameCount(mode);
                for (int frame = 0; frame < frames; frame++)
                {
                    actor.Sample(mode, frame / 10f, right);
                    // Run/Action deliberately tilt the unchanged production mesh. The
                    // small existing toe swing is allowed; the old half-body drop is not.
                    Assert.That(actor.Body.bounds.min.y, Is.GreaterThan(-.45f), id + "/" + mode + "/" + frame);
                    Assert.That(actor.Root.transform.position.y, Is.EqualTo(0).Within(.00001f));
                    Assert.That(actor.Hand.transform.parent.localPosition,
                        Is.EqualTo((Vector3)actor.Data.HandOffset), id + " hand attachment");
                    Assert.That(actor.Hand.gameObject.activeSelf, Is.EqualTo(mode == "Action" || mode == "Skill"));
                    Assert.That(actor.Skill.gameObject.activeSelf, Is.EqualTo(mode == "Skill"));
                    if (mode == "Action" || mode == "Skill")
                    {
                        Assert.That(actor.Body.sprite, Is.SameAs(actor.Data.BackSprite));
                        Assert.That(actor.Hand.sprite, Is.SameAs(actor.Data.HandSprite));
                    }
                }
            }
        }
    }

    [Test]
    public void CaptureOptionalEvidence_ProductionPrefabAndAnimationClips()
    {
        string directory = Environment.GetEnvironmentVariable("PHASE4_STAFF_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using (var board = new StaffGroundEvidenceBoard(new[] { "STAFF27", "STAFF29", "STAFF16", "STAFF19" }, true))
        {
            foreach (string mode in StaffGroundEvidenceBoard.Modes)
            foreach (bool right in new[] { false, true })
            {
                string destination = Path.Combine(directory, mode + (right ? "-right" : "-left"));
                Directory.CreateDirectory(destination);
                for (int frame = 0; frame < StaffGroundEvidenceBoard.FrameCount(mode); frame++)
                {
                    foreach (var actor in board.Actors) actor.Sample(mode, frame / 10f, right);
                    board.Capture(Path.Combine(destination, frame.ToString("D3") + ".png"));
                }
            }
        }
        File.WriteAllText(Path.Combine(directory, "capture-manifest.txt"),
            "10 fps, normal playback speed. Idle: 5.2 s; Run: 1.4 s; Action/Skill: 2.0 s each, both directions.\n" +
            "Columns: STAFF27 before / STAFF27 after / STAFF29 before / STAFF29 after / STAFF16 unchanged / STAFF19 unchanged.\n" +
            "Real Production Chef.prefab, Chef_Idle/Run/Action clips and Staff_Skill effect sampled in isolated EditMode.\n" +
            "Before views reconstruct only the audited old center pivots in temporary in-memory sprites, using the identical imported textures.\n" +
            "Idle contact sweep covers all 7/12 frames more than twice at 0.1 s per frame; Bara's separate native-coroutine capture verifies its final-pose hold. Every animation clip repeats at least twice.\n" +
            "No Staff Init, gameplay movement, skill effects on game data, backend, Scene save, or original image writes.\n");
    }
}

internal sealed class StaffGroundEvidenceBoard : IDisposable
{
    internal static readonly string[] Modes = { "Idle", "Run", "Action", "Skill" };
    internal static int FrameCount(string mode) => mode == "Idle" ? 52 : mode == "Run" ? 14 : 20;
    private readonly Scene _scene;
    private readonly GameObject _host;
    private readonly Material _material;
    private readonly List<Object> _temporary = new List<Object>();
    private readonly Camera _camera;
    internal readonly List<StaffGroundEvidenceActor> Actors = new List<StaffGroundEvidenceActor>();

    internal StaffGroundEvidenceBoard(string[] ids, bool comparisons)
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _host = new GameObject("Chef ground evidence (isolated production visuals)");
        SceneManager.MoveGameObjectToScene(_host, _scene);
        _material = new Material(Shader.Find("Sprites/Default"));
        var cameraObject = new GameObject("Chef evidence camera", typeof(Camera));
        cameraObject.transform.SetParent(_host.transform, false);
        _camera = cameraObject.GetComponent<Camera>();
        _camera.enabled = false;
        _camera.orthographic = true;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(.96f, .93f, .86f);
        _camera.cullingMask = 1 << 30;
        _camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(_scene);
        _camera.nearClipPlane = .1f;
        _camera.farClipPlane = 100;
        foreach (string id in ids)
        {
            if (comparisons && (id == "STAFF27" || id == "STAFF29")) AddActor(id, true);
            AddActor(id, false);
        }
        float width = Actors.Count * 5.2f;
        _camera.orthographicSize = 4.9f;
        _camera.aspect = width / 9.8f;
        _camera.transform.position = new Vector3((Actors.Count - 1) * 2.6f, 1.5f, -20f);
        var groundObject = new GameObject("Ground datum", typeof(SpriteRenderer));
        groundObject.transform.SetParent(_host.transform, false);
        groundObject.layer = 30;
        groundObject.transform.localPosition = new Vector3((Actors.Count - 1) * 2.6f, -.015f, 0);
        groundObject.transform.localScale = new Vector3(width, .03f, 1);
        var ground = groundObject.GetComponent<SpriteRenderer>();
        var lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        _temporary.Add(lineSprite);
        ground.sprite = lineSprite;
        ground.sharedMaterial = _material;
        ground.color = new Color(.18f, .5f, .3f);
        ground.sortingOrder = 100;
    }

    private void AddActor(string id, bool before)
    {
        var actor = new StaffGroundEvidenceActor(_host.transform, _material, id, before, Actors.Count * 5.2f);
        Actors.Add(actor);
        var labelObject = new GameObject("Evidence label", typeof(TextMesh));
        labelObject.transform.SetParent(_host.transform, false);
        labelObject.layer = 30;
        labelObject.transform.localPosition = new Vector3(actor.Root.transform.position.x, 5.6f, 0);
        var label = labelObject.GetComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.text = id + (before ? " BEFORE" : id == "STAFF16" || id == "STAFF19" ? " CONTROL" : " AFTER");
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 36;
        label.characterSize = .1f;
        label.color = before ? new Color(.6f, .12f, .12f) : new Color(.15f, .27f, .18f);
    }

    internal void Capture(string path, int width = 1560)
    {
        int height = Mathf.RoundToInt(width / _camera.aspect);
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var active = RenderTexture.active;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            _camera.targetTexture = target;
            _camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            Assert.That(pixels.GetPixels32().Where((pixel, index) => index % 31 == 0)
                .Distinct().Take(16).Count(), Is.EqualTo(16), "Evidence camera must render the Chef sprites, not only the clear color.");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            _camera.targetTexture = null;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(pixels);
        }
    }

    public void Dispose()
    {
        foreach (var actor in Actors) actor.Dispose();
        foreach (var item in _temporary) Object.DestroyImmediate(item);
        Object.DestroyImmediate(_host);
        Object.DestroyImmediate(_material);
        EditorSceneManager.ClosePreviewScene(_scene);
    }
}

internal sealed class StaffGroundEvidenceActor : IDisposable
{
    internal readonly GameObject Root;
    internal readonly ChefData Data;
    internal readonly SpriteRenderer Body, Hand, Skill;
    private readonly bool _before;
    private readonly List<Sprite> _temporary = new List<Sprite>();
    private readonly Dictionary<Sprite, Sprite> _oldPivots = new Dictionary<Sprite, Sprite>();
    private readonly AnimationClip _idle, _run, _action, _skill;
    private readonly Vector3 _rootPosition;

    internal IEnumerator BeginNativeIdle(Sprite[] frames = null)
    {
        var staff = Root.GetComponent<StaffChef>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Staff).GetField("_staffData", flags).SetValue(staff, Data);
        typeof(Staff).GetField("_sprite", flags).SetValue(staff, Data.Sprite);
        typeof(Staff).GetField("_idleSprites", flags).SetValue(staff, frames ?? Data.IdleSprites);
        SetNativeStateWithoutGameplay(EStaffState.None);
        return (IEnumerator)typeof(Staff).GetMethod("IdleSpriteCoroutine", flags).Invoke(staff, null);
    }

    internal void SetNativeStateWithoutGameplay(EStaffState state)
    {
        typeof(Staff).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Root.GetComponent<StaffChef>(), state);
    }

    internal static float WaitSeconds(object instruction)
    {
        Assert.That(instruction, Is.InstanceOf<WaitForSeconds>());
        return (float)typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instruction);
    }

    internal StaffGroundEvidenceActor(Transform parent, Material material, string id, bool before, float x)
    {
        Data = Resources.Load<ChefData>("StaffData/" + id);
        _before = before;
        Root = Object.Instantiate(Resources.Load<GameObject>("ObjectPool/Staff/Chef"), parent);
        Root.name = id + (before ? " before" : " after");
        foreach (var script in Root.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var animator in Root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var transform in Root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 30;
        foreach (var renderer in Root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = material;
        Root.transform.localPosition = _rootPosition = new Vector3(x, 0, 0);
        Body = Root.transform.Find("Sprite Parent/Staff Sprite").GetComponent<SpriteRenderer>();
        Hand = Root.transform.Find("Sprite Parent/HandParent/Hand").GetComponent<SpriteRenderer>();
        Skill = Root.transform.Find("Sprite Parent/Skill Effect").GetComponent<SpriteRenderer>();
        Hand.transform.parent.localPosition = Data.HandOffset;
        Hand.sprite = Data.HandSprite;
        _idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Staff/Chef/Chef_Idle.anim");
        _run = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Staff/Chef/Chef_Run.anim");
        _action = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Staff/Chef/Chef_Action.anim");
        _skill = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Staff/Staff_Skill.anim");
        Root.SetActive(true);
    }

    internal void Sample(string mode, float time, bool right)
    {
        Root.transform.localPosition = _rootPosition;
        Root.transform.localRotation = Quaternion.identity;
        Root.transform.localScale = new Vector3(right ? -1 : 1, 1, 1);
        Body.transform.localPosition = Vector3.zero;
        Body.transform.localRotation = Quaternion.identity;
        Body.transform.localScale = Vector3.one * .3f;
        Hand.transform.parent.localPosition = Data.HandOffset;
        bool working = mode == "Action" || mode == "Skill";
        var clip = working ? _action : mode == "Run" ? _run : _idle;
        clip.SampleAnimation(Root, time % clip.length);
        if (mode == "Idle")
        {
            int index = Mathf.FloorToInt((time + .001f) * 10) % Data.IdleSprites.Length;
            Body.sprite = WithOldPivot(Data.IdleSprites[index], index);
        }
        else Body.sprite = working ? Data.BackSprite : Data.Sprite;
        Hand.gameObject.SetActive(working);
        Skill.gameObject.SetActive(mode == "Skill");
        if (mode == "Skill") _skill.SampleAnimation(Skill.gameObject, time % _skill.length);
    }

    private Sprite WithOldPivot(Sprite sprite, int index)
    {
        bool wrongPivot = Data.Id == "STAFF27" ? index >= 3 : Data.Id == "STAFF29" && index >= 8;
        if (!_before || !wrongPivot) return sprite;
        if (_oldPivots.TryGetValue(sprite, out var result)) return result;
        result = Sprite.Create(sprite.texture, sprite.rect, Vector2.one * .5f, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        result.name = sprite.name + " (original center pivot)";
        _temporary.Add(result);
        _oldPivots.Add(sprite, result);
        return result;
    }

    public void Dispose()
    {
        Object.DestroyImmediate(Root);
        foreach (var sprite in _temporary) Object.DestroyImmediate(sprite);
    }
}
#endif
