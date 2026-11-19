#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var randomState = UnityEngine.Random.state;
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
            SpriteRenderer sourceFrame = null, renderedFrame = null;
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
                if (original.name == "MapFrameSprite" && original.GetComponentInParent<ThirdFloorApprovedMapButton>() != null)
                {
                    sourceFrame = original;
                    renderedFrame = sprite;
                }
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
                Capture(camera, target, folder + "/" + view.name + ".png");
            }
            if (sourceFrame == null || renderedFrame == null)
                throw new InvalidOperationException("The authored map frame is missing from the source or render evidence.");
            CaptureMapAnimation(sourceFrame, renderedFrame, controller, camera, target, hallX, floorY, folder);
            File.WriteAllText(folder + "/scope.txt", "Original saved Stage1 SpriteRenderers at the production 1920x1080 camera framing, with per-renderer temporary Sprites/Default preview materials. FloorLockGroup/FloorLocker overlays are excluded only from this renderer-only evidence, matching the existing offline fairy preview. Map-frame pose images call the production ThirdFloorApprovedMapButton.Tick in the detached source scene, then copy its world transform to the render scene. The detached camera/floor is positioned at the right third-floor room. Press evidence uses down/up only, without clicking or opening a popup. NOT GameView, production lighting, unlock rules, Play, HUD, audio, fairy runtime or user approval. No account operations or scene saving.\n" + DateTime.UtcNow.ToString("O"));
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var previewMaterial in previewMaterials) Object.DestroyImmediate(previewMaterial);
            if (renderScene.IsValid()) EditorSceneManager.ClosePreviewScene(renderScene);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            UnityEngine.Random.state = randomState;
            witness.AssertUnchanged("approved third-floor renderer-only map-frame animation evidence");
        }
    }

    private static void CaptureMapAnimation(SpriteRenderer sourceFrame, SpriteRenderer renderedFrame,
        CameraController controller, Camera camera, RenderTexture target, float hallX, float floorY, string folder)
    {
        var button = sourceFrame.GetComponentInParent<ThirdFloorApprovedMapButton>();
        var buttonType = typeof(ThirdFloorApprovedMapButton);
        var tick = buttonType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
        var main = (MainScene)typeof(CameraController).GetField("_mainScene", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        if (main == null || tick == null) throw new InvalidOperationException("Missing original camera/floor or runtime animation step.");
        typeof(MainScene).GetField("_currentFloor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, ERestaurantFloorType.Floor3);
        controller.transform.position = new Vector3(hallX, floorY, -30f);
        controller.GetComponent<Camera>().orthographicSize = camera.orthographicSize;
        controller.GetComponent<Camera>().aspect = camera.aspect;
        camera.transform.position = new Vector3(hallX, floorY, -30f);
        buttonType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);
        var idle = (Transform)buttonType.GetField("_idlePivot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
        var press = (Transform)buttonType.GetField("_pressVisual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
        Quaternion restRotation = idle.localRotation;
        Vector3 restScale = press.localScale;
        var details = new System.Text.StringBuilder("pose,simulation_seconds,idle_angle_degrees,press_scale_ratio\n");
        float elapsed = 0f;

        void Advance(float seconds)
        {
            if (seconds == 0f) { tick.Invoke(button, new object[] { 0f }); return; }
            for (float advanced = 0f; advanced < seconds;)
            {
                float dt = Mathf.Min(1f / 60f, seconds - advanced);
                tick.Invoke(button, new object[] { dt });
                advanced += dt;
                elapsed += dt;
            }
        }

        void Pose(string name)
        {
            renderedFrame.transform.SetPositionAndRotation(sourceFrame.transform.position, sourceFrame.transform.rotation);
            renderedFrame.transform.localScale = sourceFrame.transform.lossyScale;
            Capture(camera, target, folder + "/" + name + ".png");
            details.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4}",
                name, elapsed, Quaternion.Angle(restRotation, idle.localRotation), press.localScale.x / restScale.x));
        }

        Advance(0f);
        Pose("map-frame-00-rest");
        float firstWait = (float)buttonType.GetField("_hintRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
        if (firstWait < 0f || !(bool)buttonType.GetField("_hintWaiting", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button))
            throw new InvalidOperationException("The detached authored map is not eligible for its normal idle hint.");
        Advance(firstWait);
        for (int index = 1; index <= 8; index++)
        {
            Advance(.1f);
            Pose("map-frame-idle-" + index.ToString("00"));
        }
        Advance(.1f);
        Pose("map-frame-idle-restored");
        if (idle.localRotation != restRotation) throw new InvalidOperationException("Idle hint did not restore its authored rotation.");

        var pointer = new PointerEventData(null)
        {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = new Vector2(300f, 300f), pressPosition = new Vector2(300f, 300f), eligibleForClick = true
        };
        button.OnPointerDown(pointer);
        Advance(.08f);
        Pose("map-frame-press-01-held");
        if (Mathf.Abs(press.localScale.x / restScale.x - .95f) > .001f)
            throw new InvalidOperationException("The authored frame did not reach the configured press scale.");
        button.OnPointerUp(pointer); // Deliberately do not click: no popup route runs in renderer-only evidence.
        Advance(.06f);
        Pose("map-frame-press-02-releasing");
        Advance(.08f);
        Pose("map-frame-press-03-restored");
        button.OnCancel(pointer);
        if (press.localScale != restScale) throw new InvalidOperationException("Press did not restore the authored visual scale.");
        File.WriteAllText(folder + "/map-frame-animation-poses.csv", details.ToString());
    }

    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            if (image.GetPixels32().Where((pixel, index) => index % 31 == 0).Distinct().Take(16).Count() < 16)
                throw new InvalidOperationException("The evidence camera produced an empty frame: " + path);
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(image); }
    }
}
#endif
