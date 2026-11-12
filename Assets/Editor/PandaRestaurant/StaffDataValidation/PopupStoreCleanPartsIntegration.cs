#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, original-prefab-only v4 replacement. No business or session mutation.</summary>
public static class PopupStoreCleanPartsIntegration
{
    public const string Folder = "Assets/Sprites/PopupStore/CleanPartsV4/";
    [Serializable] private sealed class Manifest { public Part[] parts; }
    [Serializable] private sealed class Part { public string id, sha256; public int[] native_content_size; public int padding_px; }
    [Serializable] private sealed class Map { public Replacement[] replacements; }
    [Serializable] private sealed class Replacement { public string old_id, replacement_id; public int[] target_guide_rect_xywh; }
    [Serializable] private sealed class Audit { public string utc; public bool safeToDelete; public Dependency[] assets; }
    [Serializable] private sealed class Dependency { public string path, guid; public string[] dependents; }
    private const string Background = "V4_Background_NoOldSign";
    private const string OldBackground = "FG_Background_CafeCounter_APPROVED";
    private const string BackgroundSha = "c14f16e8aee392ca70b6101eeabd6629c9488e801eb021ba789353dd78d719d3";

    public static void Apply()
    {
        RequireIdle();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "new_parts_manifest.json"));
        var map = JsonUtility.FromJson<Map>(File.ReadAllText(Folder + "replacement_map_v4.json"));
        if (manifest?.parts?.Length != 24 || map?.replacements?.Length != 32)
            throw new InvalidOperationException("Expected approved 24 parts and 32 old bindings.");
        var parts = manifest.parts.ToDictionary(p => p.id, StringComparer.Ordinal);
        foreach (var part in manifest.parts) Verify(Folder + part.id + ".png", part.sha256);
        Verify(Folder + Background + ".png", BackgroundSha);
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        GameObject popup = null;
        try
        {
            popup = PrefabUtility.LoadPrefabContents(PopupStoreApprovedUiIntegration.PrefabPath);
            if (popup.activeSelf) throw new InvalidOperationException("Popup must remain inactive during authoring.");
            var root = (RectTransform)popup.transform;
            var layout = popup.GetComponent<PopupStoreFinalGuideLayout>();
            layout.ApplyLayout();
            var images = popup.GetComponentsInChildren<Image>(true);
            var replacements = map.replacements.ToDictionary(r => r.old_id, StringComparer.Ordinal);
            foreach (var image in images)
            {
                string sourceId = image.sprite != null && replacements.ContainsKey(image.sprite.name)
                    ? image.sprite.name : SourceId(image.transform);
                if (sourceId == null || !replacements.TryGetValue(sourceId, out var replacement)) continue;
                Rect content = RectOf(replacement.target_guide_rect_xywh);
                // Shared gauge/clock art still belongs to the original customer slot.
                for (int slot = 1; slot < 3; slot++)
                    if (image.transform.IsChildOf(root.Find("Customer Place " + (slot + 1)))
                        && (replacement.old_id.StartsWith("FG_Patience_", StringComparison.Ordinal)
                        || replacement.old_id == "FG_Icon_ClockBlue"))
                        content.x += slot == 1 ? 417 : 835;
                ApplyImage(root, layout, image, parts[replacement.replacement_id], content);
            }
            // Sprite fields in patience and page-indicator components use the same approved map.
            foreach (var component in popup.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing script on original popup.");
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator(); bool changed = false;
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is Sprite sprite
                        && replacements.TryGetValue(sprite.name, out var replacement))
                    { property.objectReferenceValue = Sprite(replacement.replacement_id); changed = true; }
                if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var background = root.Find("Approved Front Background").GetComponent<Image>();
            background.sprite = Sprite(Background); background.color = Color.white;
            var sign = root.Find("Approved Clean Sign") as RectTransform;
            if (sign == null)
            {
                var go = new GameObject("Approved Clean Sign", typeof(RectTransform), typeof(Image));
                go.layer = root.gameObject.layer; sign = (RectTransform)go.transform; sign.SetParent(root, false);
            }
            ApplyImage(root, layout, sign.GetComponent<Image>(), parts["V4_Sign_RopesLeaves_EMPTY"], new Rect(1, 0, 237, 241));
            sign.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            foreach (string name in new[] { "Background Warm Veil", "Hanging Sign Caption", "Wood Counter Foreground" })
            {
                var old = root.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            foreach (var target in new[] { root.Find("HUD") }.Concat(Enumerable.Range(1, 5)
                .Select(i => root.Find("Sales Counter - Five Slots/Empty Slot " + i))))
            {
                // These disabled legacy frame components have no functional references.
                // Keep the slot transforms and selected-material children used by the counter.
                var decoration = target.GetComponent<Image>();
                if (decoration != null)
                {
                    if (decoration.enabled) throw new InvalidOperationException("Unexpected live legacy frame: " + target.name);
                    UnityEngine.Object.DestroyImmediate(decoration);
                }
                var renderer = target.GetComponent<CanvasRenderer>();
                if (renderer != null && target.GetComponent<Graphic>() == null) UnityEngine.Object.DestroyImmediate(renderer);
            }
            layout.ApplyLayout();
            PrefabUtility.SaveAsPrefabAsset(popup, PopupStoreApprovedUiIntegration.PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Original popup save failed.");
        }
        finally
        {
            if (popup != null) PrefabUtility.UnloadPrefabContents(popup);
            witness.AssertUnchanged("UI04 original prefab replacement");
        }
        AuditDependencies();
        Debug.Log("POPUP_STORE_UI04_APPLIED original Images replaced; original layout/game identities retained; no scene save");
    }

    private static void ApplyImage(RectTransform root, PopupStoreFinalGuideLayout layout, Image image, Part part, Rect content)
    {
        bool bubble = part.id.StartsWith("V4_OrderBubble", StringComparison.Ordinal);
        bool fill = part.id.Contains("_Fill_");
        bool sliced = !bubble && !fill && Sprite(part.id).border != Vector4.zero;
        float scale = sliced || bubble || fill ? content.height / part.native_content_size[1]
            : Mathf.Min(content.width / part.native_content_size[0], content.height / part.native_content_size[1]);
        float pad = part.padding_px * scale;
        var outer = new Rect(content.x - pad, content.y - pad, content.width + pad * 2, content.height + pad * 2);
        var rect = image.rectTransform;
        var children = Enumerable.Range(0, rect.childCount).Select(i => rect.GetChild(i)).ToArray();
        var positions = children.Select(c => c.position).ToArray();
        SetGuideRect(root, layout, rect, outer);
        for (int i = 0; i < children.Length; i++) children[i].position = positions[i];
        image.sprite = Sprite(part.id); image.color = Color.white;
        image.type = sliced ? Image.Type.Sliced : fill ? Image.Type.Filled : Image.Type.Simple;
        image.preserveAspect = !sliced && !bubble && !fill;
        image.pixelsPerUnitMultiplier = sliced ? 1 / scale : 1;
        if (fill)
        {
            image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0;
            if (image.GetComponent<PopupStorePaddedFillVisual>() == null)
                image.gameObject.AddComponent<PopupStorePaddedFillVisual>();
        }
        if (bubble)
        {
            var visual = image.GetComponent<PopupStoreSpeechFrameVisual>() ?? image.gameObject.AddComponent<PopupStoreSpeechFrameVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("_sourceSprite").objectReferenceValue = image.sprite;
            so.FindProperty("_leftCornerPixels").floatValue = 64;
            so.FindProperty("_rightCornerPixels").floatValue = 64;
            so.FindProperty("_tailCenterPixels").floatValue = image.sprite.rect.width * .5f;
            so.FindProperty("_tailWidthPixels").floatValue = 82;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // Existing input Images retain their raycast ownership. New sign has none.
        if (part.id == "V4_Sign_RopesLeaves_EMPTY") image.raycastTarget = false;
    }

    private static void SetGuideRect(RectTransform root, PopupStoreFinalGuideLayout layout, RectTransform rect, Rect guide)
    {
        if (rect.parent == root)
        {
            var so = new SerializedObject(layout); var list = so.FindProperty("_placements"); int found = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Target").objectReferenceValue == rect) found = i;
            if (found < 0) { found = list.arraySize; list.arraySize++; }
            var entry = list.GetArrayElementAtIndex(found);
            entry.FindPropertyRelative("Target").objectReferenceValue = rect;
            entry.FindPropertyRelative("GuideRect").rectValue = guide;
            so.ApplyModifiedPropertiesWithoutUndo(); layout.ApplyLayout(); return;
        }
        float scale = Mathf.Min(root.rect.width / 1672, root.rect.height / 941);
        float parentScale = root.InverseTransformVector(rect.parent.TransformVector(Vector3.right)).magnitude;
        Vector3 center = root.TransformPoint(new Vector3((guide.center.x - 836) * scale, (470.5f - guide.center.y) * scale, 0));
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
        rect.sizeDelta = guide.size * (scale / parentScale); rect.position = center;
    }

    private static string SourceId(Transform image)
    {
        switch (image.name)
        {
            case "Approved Score Frame": return "FG_HUD_ScoreFrame";
            case "Approved Timer Frame": return "FG_HUD_TimerFrame";
            case "Approved Pink Clock": return "FG_Icon_ClockPink";
            case "Approved Blue Clock": return "FG_Icon_ClockBlue";
            case "Grade Progress Track": return "FG_Grade_Track";
            case "Grade Progress Fill": return "FG_Grade_Fill_Gold_DERIVED";
            case "Gauge Track": return "FG_Patience_Track";
            case "Gauge Fill": return "FG_Patience_Fill_Green";
        }
        for (Transform parent = image; parent != null; parent = parent.parent)
            for (int i = 1; i <= 3; i++)
                if (parent.name == "Customer Place " + i)
                {
                    if (image.name == "Order Balloon") return "FG_OrderBubble_0" + i;
                    if (image.name == "Approved Order Alert") return "FG_OrderAlert_0" + i;
                }
        foreach (string kind in new[] { "Box", "Plush", "Ribbon" })
        {
            if (image.name == kind + " Panel") return "FG_Panel_" + kind;
            if (image.parent == null || image.parent.name != kind + " Panel") continue;
            if (image.name == "Approved Category Tab") return "FG_Tab_" + kind + "_Blank";
            if (image.name == "Previous Page") return "FG_Arrow_" + kind + "_Left";
            if (image.name == "Next Page") return "FG_Arrow_" + kind + "_Right";
        }
        return null;
    }

    public static void AuditDependencies()
    {
        RequireIdle();
        var map = JsonUtility.FromJson<Map>(File.ReadAllText(Folder + "replacement_map_v4.json"));
        var paths = map.replacements.Select(r => PopupStoreFinalGuideIntegration.Folder + r.old_id + ".png")
            .Concat(new[] { PopupStoreFinalGuideIntegration.Folder + OldBackground + ".png" }).ToArray();
        var dependents = paths.ToDictionary(p => p, _ => new List<string>(), StringComparer.Ordinal);
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
            && !AssetDatabase.IsValidFolder(p) && !paths.Contains(p)))
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                if (dependents.TryGetValue(dependency, out var users)) users.Add(path);
        var report = new Audit { utc = DateTime.UtcNow.ToString("O"), safeToDelete = dependents.All(p => p.Value.Count == 0),
            assets = paths.Select(p => new Dependency { path = p, guid = AssetDatabase.AssetPathToGUID(p), dependents = dependents[p].ToArray() }).ToArray() };
        Directory.CreateDirectory("Temp/PopupStoreVerification");
        File.WriteAllText("Temp/PopupStoreVerification/ui04-dependency-audit.json", JsonUtility.ToJson(report, true));
    }

    private static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || PrefabStageUtility.GetCurrentPrefabStage() != null
            || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("UI04 requires settled clean EditMode; user workspace is never discarded.");
    }
    private static Rect RectOf(int[] xywh) => new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
    private static Sprite Sprite(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + id + ".png")
        ?? throw new InvalidOperationException("Missing approved v4 Sprite: " + id);
    private static void Verify(string path, string expected)
    {
        using (var sha = SHA256.Create())
            if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() != expected)
                throw new InvalidOperationException("Approved source mismatch: " + path);
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) throw new InvalidOperationException("Sprite not imported: " + path);
    }
}
#endif
