#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;
using static CustomerTableUseRegressionTests;

public static class UiSpriteOptimizationAudit
{
    private const string Output = "Logs/UiSpriteOptimization/atlas-audit.json";

    [MenuItem("Tools/Panda Restaurant/UI Optimization/Audit Android Atlases (No Settings Changes)")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || GachaReviewTestRunner.IsRunning)
            throw new InvalidOperationException("Atlas audit requires idle, SDK-free Edit Mode.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Select Android manually before this audit; the audit does not switch build targets.");

        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var paths = new[] {
            "Assets/Sprites/UI/Main/MainUI_Atlas.spriteatlasv2",
            "Assets/Sprites/UI/UIGacha/가챠UI/GachaUI_Atlas.spriteatlasv2",
            "Assets/Sprites/UI/UI공통 이미지/공통 이미지_Atlas.spriteatlasv2" };
        var selected = paths.Select(path => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path)
            ?? throw new InvalidOperationException("Cannot load atlas: " + path)).ToArray();
        SpriteAtlasUtility.PackAtlases(selected, BuildTarget.Android, false);
        var all = AssetDatabase.FindAssets("t:SpriteAtlas").Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => new { Path = path, Atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path) })
            .Where(item => item.Atlas != null).ToArray();
        var memberships = all.ToDictionary(item => item.Path, item => Sources(item.Atlas));
        var reports = new List<AtlasReport>();
        foreach (var path in paths)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            var android = atlas.GetPlatformSettings("Android");
            var previewMethod = typeof(SpriteAtlasExtensions).GetMethod("GetPreviewTextures",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(SpriteAtlas) }, null)
                ?? throw new NotSupportedException("This Unity editor does not expose atlas preview textures.");
            var textures = (Texture2D[])previewMethod.Invoke(null, new object[] { atlas });
            if (textures == null || textures.Length == 0)
                throw new InvalidOperationException("No Android preview pages after packing: " + path);
            reports.Add(new AtlasReport {
                path = path, spriteCount = atlas.spriteCount,
                textureSettings = new AtlasTextureSettings {
                    readable = atlas.GetTextureSettings().readable,
                    generateMipMaps = atlas.GetTextureSettings().generateMipMaps,
                    filterMode = atlas.GetTextureSettings().filterMode.ToString()
                },
                androidSettings = JsonUtility.ToJson(android),
                pages = textures.Select(texture => new Page {
                    width = texture.width, height = texture.height, format = texture.format.ToString(),
                    graphicsFormat = texture.graphicsFormat.ToString() }).ToArray(),
                sources = memberships[path].OrderBy(value => value).ToArray(),
                overlaps = memberships.Where(item => item.Key != path)
                    .Select(item => new Overlap {
                        atlas = item.Key, sources = memberships[path].Intersect(item.Value).OrderBy(value => value).ToArray() })
                    .Where(item => item.sources.Length > 0).ToArray()
            });
        }
        witness.AssertUnchanged("Android atlas packing audit");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Report {
            unity = Application.unityVersion, target = EditorUserBuildSettings.activeBuildTarget.ToString(),
            spritePackerMode = EditorSettings.spritePackerMode.ToString(),
            androidBuildSubtarget = EditorUserBuildSettings.androidBuildSubtarget.ToString(),
            graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(api => api.ToString()).ToArray(),
            androidMinSdk = PlayerSettings.Android.minSdkVersion.ToString(),
            androidArchitectures = PlayerSettings.Android.targetArchitectures.ToString(),
            sceneBefore = witness.Summary, sceneAfter = GachaCollectionPreviewSceneWitness.Capture().Summary,
            atlases = reports.ToArray()
        }, true));
        Debug.Log("UI_SPRITE_ATLAS_AUDIT_COMPLETED: " + Path.GetFullPath(Output));
    }

    private static HashSet<string> Sources(SpriteAtlas atlas)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (Object packable in atlas.GetPackables())
        {
            string path = AssetDatabase.GetAssetPath(packable);
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                    sources.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else sources.Add(path);
        }
        return sources;
    }

    public static void CaptureSlotInventory(Action<Component> verify = null)
    {
        using (var scope = new Flow())
        {
            foreach (Type type in new[] { typeof(FurnitureDataManager), typeof(KitchenUtensilDataManager) })
            {
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
                {
                    if (field.Name == "_instance") continue;
                    object empty = field.FieldType.IsArray
                        ? Array.CreateInstance(field.FieldType.GetElementType(), ((Array)field.GetValue(null)).Length)
                        : Activator.CreateInstance(field.FieldType);
                    scope.Swap(type, field.Name, empty);
                }
                type.GetMethod("InitData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, null);
            }
            scope.Swap(typeof(FurnitureDataManager), "_instance", scope.Create<FurnitureDataManager>());
            scope.Swap(typeof(KitchenUtensilDataManager), "_instance", scope.Create<KitchenUtensilDataManager>());
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            try
            {
                var furniture = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIFurniture>(true)).Single();
                var kitchen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIKitchen>(true)).Single();
                furniture.gameObject.SetActive(false);
                kitchen.gameObject.SetActive(false);
                var reports = new List<SlotInventory>();
                foreach (Component view in new Component[] { furniture, kitchen })
                {
                    int categoryCount = view is UIFurniture ? (int)FurnitureType.Length : (int)KitchenUtensilType.Length;
                    var capacities = new int[categoryCount];
                    for (int type = 0; type < categoryCount; type++)
                        for (int floor = 0; floor < (int)ERestaurantFloorType.Length; floor++)
                            capacities[type] = Mathf.Max(capacities[type], view is UIFurniture
                                ? FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)type, (ERestaurantFloorType)floor).Count
                                : KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)type, (ERestaurantFloorType)floor).Count);
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Invoke(view, "InitializeSlots");
                    watch.Stop();
                    var slots = Get<List<UIRestaurantAdminFoodTypeSlot>>(view, "_slots");
                    if (slots.Count != capacities.Max())
                        throw new InvalidOperationException("Shared slot pool does not cover every category and floor.");
                    reports.Add(new SlotInventory {
                        view = view.GetType().Name, counts = capacities,
                        total = slots.Count, maxCategory = capacities.Max(),
                        editModeInitializeMilliseconds = watch.Elapsed.TotalMilliseconds
                    });
                    verify?.Invoke(view);
                }
                Directory.CreateDirectory("Logs/UiSpriteOptimization");
                File.WriteAllText("Logs/UiSpriteOptimization/slot-inventory.json",
                    JsonUtility.ToJson(new SlotInventories { views = reports.ToArray() }, true));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    public static void ConsolidateVerifiedWallpapers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || GachaReviewTestRunner.IsRunning)
            throw new InvalidOperationException("Consolidation requires idle, SDK-free Edit Mode.");
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var candidates = new List<Wallpaper>();
        var atlasSources = AssetDatabase.FindAssets("t:SpriteAtlas").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SpriteAtlas>).Where(atlas => atlas != null)
            .SelectMany(Sources).ToHashSet(StringComparer.Ordinal);
        var dependencyPaths = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
            && new[] { ".unity", ".prefab", ".asset", ".anim", ".controller", ".overrideController", ".mat", ".playable",
                ".spriteatlas", ".spriteatlasv2" }.Contains(Path.GetExtension(path))).ToArray();
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in dependencyPaths)
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                if (dependency != path) dependencies.Add(dependency);
        var loadedRoots = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(item => item.scene.IsValid() && item.transform.parent == null).Cast<Object>().ToArray();
        foreach (Object dependency in EditorUtility.CollectDependencies(loadedRoots))
            dependencies.Add(AssetDatabase.GetAssetPath(dependency));
        foreach (int id in Enumerable.Range(19, 17))
        {
            string duplicateFolder = "Assets/Resources/FurnitureData/Sprites/벽지";
            string[] matches = Directory.GetFiles(duplicateFolder, "WALLPAPER" + id + "_*.png");
            if (matches.Length != 1) throw new InvalidOperationException("Wallpaper candidate is ambiguous: " + id);
            string duplicate = matches[0].Replace('\\', '/');
            string canonical = "Assets/Resources/FurnitureData/Sprites/" + id + "/" + Path.GetFileName(duplicate);
            if (!File.Exists(canonical)) throw new FileNotFoundException("Canonical wallpaper missing.", canonical);
            using (var hash = SHA256.Create())
                if (!hash.ComputeHash(File.ReadAllBytes(canonical)).SequenceEqual(hash.ComputeHash(File.ReadAllBytes(duplicate))))
                    throw new InvalidOperationException("Wallpaper PNG differs: " + duplicate);
            Func<string, string> importer = path => Regex.Replace(File.ReadAllText(path + ".meta"), @"(?m)^guid: \w+\r?\n", "");
            if (importer(canonical) != importer(duplicate))
                throw new InvalidOperationException("Wallpaper importer differs: " + duplicate);
            if (dependencies.Contains(duplicate) || atlasSources.Contains(duplicate))
                throw new InvalidOperationException("Wallpaper has serialized/atlas dependencies: " + duplicate);
            var canonicalSprites = AssetDatabase.LoadAllAssetsAtPath(canonical).OfType<Sprite>().ToArray();
            var duplicateSprites = AssetDatabase.LoadAllAssetsAtPath(duplicate).OfType<Sprite>().ToArray();
            if (canonicalSprites.Length != 1 || duplicateSprites.Length != 1)
                throw new InvalidOperationException("Only single-sprite wallpapers were reviewed: " + duplicate);
            Sprite a = canonicalSprites[0], b = duplicateSprites[0];
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string canonicalGuid, out long canonicalId)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(b, out string duplicateGuid, out long duplicateId))
                throw new InvalidOperationException("Cannot resolve wallpaper sprite IDs: " + duplicate);
            if (canonicalId != duplicateId || a.name != b.name || a.rect != b.rect || a.pivot != b.pivot
                || a.pixelsPerUnit != b.pixelsPerUnit || a.border != b.border)
                throw new InvalidOperationException("Wallpaper sprite identity/geometry differs: " + duplicate);
            candidates.Add(new Wallpaper { canonical = canonical, duplicate = duplicate, canonicalGuid = canonicalGuid,
                duplicateGuid = duplicateGuid, fileId = canonicalId, sourceBytes = new FileInfo(duplicate).Length });
        }
        // The production loader enumerates numeric sets 01..68; it never loads the category folder.
        // Identical wallpaper names and single-sprite IDs are retained at those existing resource paths.
        foreach (var item in candidates)
        {
            if (!AssetDatabase.DeleteAsset(item.duplicate))
                throw new IOException("Could not remove reviewed duplicate: " + item.duplicate);
            item.removed = true;
            Directory.CreateDirectory("Logs/UiSpriteOptimization");
            File.WriteAllText("Logs/UiSpriteOptimization/wallpaper-consolidation.json",
                JsonUtility.ToJson(new Wallpapers { items = candidates.ToArray() }, true));
            if (AssetDatabase.AssetPathToGUID(item.canonical) != item.canonicalGuid
                || AssetDatabase.LoadAssetAtPath<Sprite>(item.canonical) == null)
                throw new InvalidOperationException("Canonical wallpaper changed: " + item.canonical);
        }
        witness.AssertUnchanged("reviewed wallpaper consolidation");
        Debug.Log("UI_SPRITE_WALLPAPERS_CONSOLIDATED: " + candidates.Count);
    }

    [Serializable] private sealed class Wallpapers { public Wallpaper[] items; }
    [Serializable] private sealed class Wallpaper
    {
        public string canonical, duplicate, canonicalGuid, duplicateGuid;
        public long fileId, sourceBytes;
        public bool removed;
    }

    [Serializable] private sealed class SlotInventories
    {
        public string scope = "Actual InitializeSlots in an inactive disposable EditMode PreviewScene; not gameplay first-open CPU or GC Alloc.";
        public SlotInventory[] views;
    }
    [Serializable] private sealed class SlotInventory
    {
        public string view;
        public int[] counts;
        public int total, maxCategory;
        public double editModeInitializeMilliseconds;
    }

    [Serializable] private sealed class Report
    {
        public string unity, target, spritePackerMode, androidBuildSubtarget, sceneBefore, sceneAfter, androidMinSdk, androidArchitectures;
        public string[] graphicsApis;
        public AtlasReport[] atlases;
    }
    [Serializable] private sealed class AtlasReport
    {
        public string path, androidSettings;
        public AtlasTextureSettings textureSettings;
        public int spriteCount;
        public Page[] pages;
        public string[] sources;
        public Overlap[] overlaps;
    }
    [Serializable] private sealed class Page { public int width, height; public string format, graphicsFormat; }
    [Serializable] private sealed class Overlap { public string atlas; public string[] sources; }
    [Serializable] private sealed class AtlasTextureSettings { public bool readable, generateMipMaps; public string filterMode; }
}
#endif
