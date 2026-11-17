#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit renderer-only evidence from the original saved Stage1. Not Play or GameView evidence.</summary>
public static class ThirdFloorApprovedArtEvidence
{
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Art evidence requires idle EditMode without a live account.");
        Scene source = default, renderScene = default;
        RenderTexture target = null;
        var previewMaterials = new System.Collections.Generic.List<Material>();
        var previousTarget = RenderTexture.active;
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            var roots = source.GetRootGameObjects();
            var art = roots.SelectMany(r => r.GetComponentsInChildren<ThirdFloorApprovedArt>(true)).Single();
            art.Refresh();
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<CameraController>(true)).Single();
            float hallX, kitchenX, floorY;
            using (var data = new SerializedObject(controller))
            {
                hallX = data.FindProperty("_hallPos_X").floatValue;
                kitchenX = data.FindProperty("_kitchenPos_X").floatValue;
                floorY = data.FindProperty("_floor3Pos_Y").floatValue;
            }
            renderScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Third-floor renderer-only evidence") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, renderScene);
            var area = new Bounds(new Vector3((hallX + kitchenX) / 2, floorY, 0), new Vector3(95, 30, 50));
            var rendererDetails = new System.Text.StringBuilder();
            foreach (var original in roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (!original.enabled || !original.gameObject.activeInHierarchy || original.sprite == null
                    || !original.bounds.Intersects(area)
                    || original.GetComponentInParent<FloorLockGroup>() != null
                    || original.GetComponentInParent<FloorLocker>() != null) continue;
                var visual = new GameObject(original.name) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                visual.transform.SetParent(root.transform, false);
                visual.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                visual.transform.localScale = original.transform.lossyScale;
                var sprite = visual.AddComponent<SpriteRenderer>();
                sprite.sprite = original.sprite;
                sprite.color = original.color;
                sprite.flipX = original.flipX; sprite.flipY = original.flipY;
                sprite.drawMode = original.drawMode; sprite.size = original.size;
                sprite.sortingLayerID = original.sortingLayerID; sprite.sortingOrder = original.sortingOrder;
                // Use a separate texture binding per source renderer for manual SRP camera rendering.
                var previewMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                previewMaterial.SetTexture("_MainTex", original.sprite.texture);
                previewMaterials.Add(previewMaterial);
                sprite.sharedMaterial = previewMaterial;
                rendererDetails.AppendLine(original.name + " | " + original.sprite.name + " | "
                    + original.bounds + " | " + original.sortingLayerName + " | " + original.sortingOrder
                    + " | " + original.transform.position + " | " + original.sprite.texture.name);
            }
            var cameraObject = new GameObject("Source camera framing") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = renderScene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(renderScene);
            camera.enabled = false; camera.orthographic = true;
            camera.aspect = 1920f / 1080f;
            // Same 16:9 adjustment as CameraController.AdjustCamera; no production camera is changed.
            camera.orthographicSize = controller.GetComponent<Camera>().orthographicSize / (camera.aspect / 2.3333f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f);
            camera.cullingMask = 1 << 31; camera.nearClipPlane = .3f; camera.farClipPlane = 100f;
            camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            const string folder = "Temp/PopupStoreVerification/ThirdFloorArtEvidence";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/renderers.txt", rendererDetails.ToString());
            foreach (var view in new[] { (name: "left-scene-render", x: kitchenX),
                (name: "right-scene-render", x: hallX), (name: "seam-scene-render", x: -19.624733f) })
            {
                camera.transform.position = new Vector3(view.x, floorY, -30f);
                camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                    if (image.GetPixels32().Where((pixel, index) => index % 31 == 0)
                        .Distinct().Take(16).Count() < 16)
                        throw new InvalidOperationException("The evidence camera produced an empty frame: " + view.name);
                    File.WriteAllBytes(folder + "/" + view.name + ".png", image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            File.WriteAllText(folder + "/scope.txt", "Original saved Stage1 SpriteRenderers at the production 1920x1080 camera framing, with per-renderer temporary Sprites/Default preview materials. FloorLockGroup/FloorLocker overlays are excluded only from this renderer-only evidence, matching the existing offline fairy preview. NOT GameView, production lighting, unlock rules, Play, HUD, audio, fairy runtime or user approval. No account operations or scene saving.\n" + DateTime.UtcNow.ToString("O"));
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var previewMaterial in previewMaterials) Object.DestroyImmediate(previewMaterial);
            if (renderScene.IsValid()) EditorSceneManager.ClosePreviewScene(renderScene);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
        }
    }
}
#endif
