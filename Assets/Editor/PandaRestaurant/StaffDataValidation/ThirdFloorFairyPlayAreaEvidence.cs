#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real furniture touch visuals in isolated Edit Mode; renderer evidence, never Play/GameView evidence.</summary>
public static class ThirdFloorFairyPlayAreaEvidence
{
    public const string OutputDirectory = "Temp/PopupStoreVerification/ThirdFloorFairyPlayAreaEvidence";

    [MenuItem("Tools/Panda Restaurant/3층 요정 가구/Edit Mode 검증 캡처")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin
            || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Furniture evidence requires idle, SDK-free Edit Mode outside Prefab Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty) throw new InvalidOperationException("Furniture evidence does not save or discard dirty user scenes.");
        }

        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Scope: saved original Stage1 loaded by the existing isolated EnhancementFairyStage1Host.");
        report.AppendLine("The real Floor3Controller, EnhancementFairyHabitat/Brain and authored PlayArea run in Edit Mode.");
        report.AppendLine("The existing memory-only ownership adapter supplies twelve actual catalog fairies; it restores all global state on disposal.");
        report.AppendLine("PlayArea uses its production BindToInitializedHabitat core, never ConfigureOffline. No fairy position staging, furniture AI command, reservation, ownership transaction or settings override.");
        report.AppendLine("Touches call the real guarded TryTapAtWorldPoint; consecutive frames advance the same private AdvanceHints core used by Update. Normal habitat updates remain active.");
        report.AppendLine("Saved frames are 1920x1080 Edit Mode Camera.Render snapshots with temporary per-sprite Sprites/Default materials. Production material, SpriteMask and lighting behavior are not reproduced.");
        report.AppendLine("FloorLocker/FloorLockGroup overlays are excluded in the renderer preview only, matching the existing offline evidence pattern.");
        report.AppendLine("NOT Play, GameView, production lighting, input, FPS, unlocking, audio, server, gacha transaction or persisted ownership evidence. No user scene saving.");

        var csv = new StringBuilder("kind,timeSeconds,touchAnimating,localX,localY,localZ,rotationZ,scaleX,scaleY,scaleZ,activeFairyCount\n");
        using (var host = new EnhancementFairyStage1Host())
        {
            var area = host.SceneComponents<ThirdFloorFairyPlayArea>().SingleOrDefault();
            if (area == null) throw new InvalidOperationException("Saved Stage1 has no connected ThirdFloorFairyPlayArea prefab.");
            var art = host.SceneComponents<ThirdFloorApprovedArt>().Single();
            art.Refresh();
            if (!area.gameObject.activeInHierarchy) throw new InvalidOperationException("The approved play area is disabled in saved Stage1.");
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(12).ToArray();
            if (items.Length != 12) throw new InvalidOperationException("Furniture evidence needs twelve eligible existing catalog fairies.");
            FairyAiHotfixProbeReflection.UseProductionAspect(host);
            host.Restore(items.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            host.Move(ERestaurantFloorType.Floor3);
            host.Move(RestaurantType.Kitchen);
            host.Advance(.6f);
            bool bound = (bool)EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat");
            if (!bound || !area.IsRuntimeBound || !area.IsRegistered)
                throw new InvalidOperationException("Saved Stage1 furniture could not bind to its production-initialized parent Habitat.");
            float hintClock = 0f;

            var kinds = new[] { FairyFurnitureKind.Cushion, FairyFurnitureKind.RockingHorse, FairyFurnitureKind.Box };
            for (int index = 0; index < kinds.Length; index++)
            {
                var point = area.Points.Single(value => value.Kind == kinds[index]);
                if (point.HintVisual == null || point.HintSprite == null || point.HintSprite.sprite == null
                    || point.TouchCollider == null || !point.TouchCollider.enabled
                    || !point.TouchCollider.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Furniture evidence requires connected, active visuals and fixed collider: " + point.Kind);
                area.CancelAll();
                Vector3 position = point.HintVisual.localPosition, scale = point.HintVisual.localScale;
                Quaternion rotation = point.HintVisual.localRotation;
                string name = point.Kind == FairyFurnitureKind.Cushion ? "cushion-touch-editmode"
                    : point.Kind == FairyFurnitureKind.RockingHorse ? "horse-touch-editmode" : "box-touch-editmode";
                Capture(host, name + "-rest", report);
                RecordPose(csv, host, area, point, 0f);
                Physics2D.SyncTransforms();
                Vector3 touch = point.TouchCollider is BoxCollider2D box
                    ? box.transform.TransformPoint(box.offset) : point.TouchCollider.bounds.center;
                if (!area.TryTapAtWorldPoint(touch) || !area.IsTouchAnimating(point))
                    throw new InvalidOperationException("The real guarded visual tap was rejected: " + point.Kind);
                float elapsed = 0f;
                foreach (float sample in new[] { .08f, .12f, .24f, .5f, .8f, 1.1f })
                {
                    AdvanceTo(host, area, ref elapsed, sample, ref hintClock);
                    Capture(host, name + "-" + Mathf.RoundToInt(sample * 1000f).ToString("0000") + "ms", report);
                    RecordPose(csv, host, area, point, elapsed);
                }
                float duration = point.Kind == FairyFurnitureKind.Cushion
                    ? point.PressDuration + point.ReleaseDuration : point.TouchDuration;
                AdvanceTo(host, area, ref elapsed, Mathf.Max(1.1f, duration + .05f), ref hintClock);
                bool restored = !area.IsTouchAnimating(point) && point.HintVisual.localPosition == position
                    && point.HintVisual.localRotation == rotation && point.HintVisual.localScale == scale;
                report.AppendLine(point.Kind + " | touchStarted=true | elapsed=" + F(elapsed)
                    + " | restored=" + restored + " | sprite=" + point.HintSprite.sprite.name
                    + " | sorting=" + point.HintSprite.sortingLayerName + "/" + point.HintSprite.sortingOrder
                    + " | collider=" + point.TouchCollider.bounds);
                if (!restored) throw new InvalidOperationException("Touch visual did not restore its authored pose: " + point.Kind);
                area.CancelAll();
            }
        }
        File.WriteAllText(Path.Combine(OutputDirectory, "touch-poses.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(OutputDirectory, "scope-and-state.txt"), report.ToString());
    }

    private static void AdvanceTo(EnhancementFairyStage1Host host, ThirdFloorFairyPlayArea area,
        ref float elapsed, float target, ref float hintClock)
    {
        while (elapsed < target - .000001f)
        {
            float step = Mathf.Min(1f / 60f, target - elapsed);
            host.Habitat.AdvancePreview(step);
            hintClock += step;
            EnhancementFairyStage1Host.Invoke(area, "AdvanceHints", step, hintClock);
            elapsed += step;
        }
    }

    private static void RecordPose(StringBuilder csv, EnhancementFairyStage1Host host,
        ThirdFloorFairyPlayArea area, FairyFurniturePoint point, float time)
    {
        var pivot = point.HintVisual;
        csv.AppendLine(point.Kind + "," + F(time) + "," + area.IsTouchAnimating(point)
            + "," + F(pivot.localPosition.x) + "," + F(pivot.localPosition.y) + "," + F(pivot.localPosition.z)
            + "," + F(pivot.localEulerAngles.z) + "," + F(pivot.localScale.x)
            + "," + F(pivot.localScale.y) + "," + F(pivot.localScale.z) + "," + host.Habitat.ActiveCount);
    }

    private static string F(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

    private static void Capture(EnhancementFairyStage1Host host, string name, StringBuilder report)
    {
        Scene renderScene = default;
        RenderTexture target = null;
        var materials = new List<Material>();
        var previousTarget = RenderTexture.active;
        try
        {
            renderScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Furniture renderer-only evidence") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, renderScene);
            var region = new Bounds(new Vector3(host.Camera.transform.position.x, host.Camera.transform.position.y, 0f),
                new Vector3(55f, 40f, 60f));
            int sprites = 0;
            foreach (var original in host.SceneComponents<SpriteRenderer>())
            {
                if (!original.enabled || !original.gameObject.activeInHierarchy || original.sprite == null
                    || !original.bounds.Intersects(region) || original.GetComponentInParent<FloorLockGroup>() != null
                    || original.GetComponentInParent<FloorLocker>() != null) continue;
                var visual = new GameObject(original.name) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                visual.transform.SetParent(root.transform, false);
                visual.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                visual.transform.localScale = original.transform.lossyScale;
                var sprite = visual.AddComponent<SpriteRenderer>();
                sprite.sprite = original.sprite;
                sprite.color = original.color;
                sprite.flipX = original.flipX;
                sprite.flipY = original.flipY;
                sprite.drawMode = original.drawMode;
                sprite.size = original.size;
                sprite.sortingLayerID = original.sortingLayerID;
                sprite.sortingOrder = original.sortingOrder;
                var material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetTexture("_MainTex", original.sprite.texture);
                materials.Add(material);
                sprite.sharedMaterial = material;
                sprites++;
            }

            var cameraObject = new GameObject("1920x1080 source framing") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = renderScene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(renderScene);
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1920f / 1080f;
            camera.orthographicSize = host.Camera.orthographicSize;
            camera.transform.position = new Vector3(host.Camera.transform.position.x, host.Camera.transform.position.y, -30f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .12f, .12f);
            camera.cullingMask = 1 << 31;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                if (texture.GetPixels32().Where((pixel, index) => index % 31 == 0).Distinct().Take(16).Count() < 16)
                    throw new InvalidOperationException("Furniture evidence camera produced an empty frame.");
                File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), texture.EncodeToPNG());
                report.AppendLine("Capture " + name + ": spriteCount=" + sprites + "; camera=" + camera.transform.position
                    + "; orthographicSize=" + camera.orthographicSize + "; renderResolution=1920x1080");
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var material in materials) Object.DestroyImmediate(material);
            if (renderScene.IsValid()) EditorSceneManager.ClosePreviewScene(renderScene);
        }
    }
}
#endif
