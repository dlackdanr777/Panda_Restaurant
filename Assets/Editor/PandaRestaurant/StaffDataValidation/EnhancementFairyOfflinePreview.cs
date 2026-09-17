#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// A renderable, isolated third-floor copy. Only SpriteRenderers are copied from Stage1;
/// its ordinary scene, SDK, account, customer, furniture and MonoBehaviour code never runs.
/// </summary>
public sealed class EnhancementFairyOfflinePreview : IDisposable
{
    private const int PreviewLayer = 31;
    private readonly Scene _scene;
    private readonly GameObject _root;
    private readonly EnhancementFairySettings _settings;
    private readonly List<GachaItemData> _catalog = new List<GachaItemData>();
    private readonly List<string> _owned = new List<string>();
    private readonly EnhancementFairyArrivalQueue _queue = new EnhancementFairyArrivalQueue();
    private readonly Material _spriteMaterial;
    private int _nextItem;
    private int _transaction;
    private bool _unlocked = true;
    private bool _visible = true;
    public EnhancementFairyHabitat Habitat { get; private set; }
    public Camera Camera { get; private set; }
    public int CopiedFloorSpriteCount { get; private set; }

    /// <summary>Pre-Play renderer-only template. The disabled camera stores source framing across domain reload.</summary>
    public static GameObject BuildPassiveFloor(Transform parent)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("The source third-floor scene may only be copied in Edit Mode.");
        Scene source = default;
        GameObject floorCopy = null;
        try
        {
            source = EditorSceneManager.OpenPreviewScene(StaffGachaOfflineViewFactory.SourceScenePath);
            var objects = source.GetRootGameObjects();
            var floor = objects.SelectMany(o => o.GetComponentsInChildren<Floor3Controller>(true)).Single();
            var controller = objects.SelectMany(o => o.GetComponentsInChildren<CameraController>(true)).Single();
            float floorY, hallX;
            using (var serialized = new SerializedObject(controller))
            {
                floorY = serialized.FindProperty("_floor3Pos_Y").floatValue;
                hallX = serialized.FindProperty("_hallPos_X").floatValue;
            }
            float size = controller.GetComponent<Camera>().orthographicSize;
            var viewport = new Bounds(new Vector3(hallX, floorY, 0f), new Vector3(44f, size * 2f, 20f));
            floorCopy = new GameObject("Offline Third Floor Visuals");
            floorCopy.transform.SetParent(parent, false);
            floorCopy.transform.localPosition = floor.transform.position;
            foreach (var original in objects.SelectMany(o => o.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (!original.enabled || !original.gameObject.activeInHierarchy || original.sprite == null
                    || !original.bounds.Intersects(viewport)
                    || original.GetComponentInParent<FloorLockGroup>() != null
                    || original.GetComponentInParent<FloorLocker>() != null) continue;
                var go = new GameObject("Floor art: " + original.name);
                go.layer = 2;
                go.transform.SetParent(floorCopy.transform, false);
                go.transform.localPosition = original.transform.position - floor.transform.position;
                go.transform.localRotation = original.transform.rotation;
                go.transform.localScale = original.transform.lossyScale;
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sprite = original.sprite;
                copy.color = original.color;
                copy.flipX = original.flipX;
                copy.flipY = original.flipY;
                copy.drawMode = original.drawMode;
                copy.size = original.size;
                copy.sortingLayerID = original.sortingLayerID;
                copy.sortingOrder = original.sortingOrder;
            }
            var marker = new GameObject("Source third-floor camera framing");
            marker.transform.SetParent(floorCopy.transform, false);
            marker.transform.localPosition = new Vector3(hallX, floorY, -20f) - floor.transform.position;
            var framing = marker.AddComponent<Camera>();
            framing.enabled = false;
            framing.orthographic = true;
            framing.orthographicSize = size;
            marker.SetActive(false);
            floorCopy.SetActive(false);
            return floorCopy;
        }
        catch
        {
            if (floorCopy != null) Object.DestroyImmediate(floorCopy);
            throw;
        }
        finally { if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source); }
    }

    public EnhancementFairyOfflinePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open the isolated fairy preview in Edit Mode.");
        _scene = EditorSceneManager.NewPreviewScene();
        _root = new GameObject("Offline third-floor sprites only");
        SceneManager.MoveGameObjectToScene(_root, _scene);
        _settings = Object.Instantiate(Resources.Load<EnhancementFairySettings>(EnhancementFairySettings.ResourcePath));
        _settings.hideFlags = HideFlags.HideAndDontSave;
        _settings.VisualLayer = PreviewLayer;
        _spriteMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            CopyActualFloorSprites();
            LoadDetachedCatalog();
            for (int i = 0; i < Mathf.Min(8, _catalog.Count); i++) _owned.Add(_catalog[i].Id);
            _nextItem = _owned.Count;
            Habitat.ConfigureOffline(_catalog, _owned, _settings, _queue);
        }
        catch { Dispose(); throw; }
    }

    private void CopyActualFloorSprites()
    {
        Scene source = default;
        try
        {
            source = EditorSceneManager.OpenPreviewScene(StaffGachaOfflineViewFactory.SourceScenePath);
            var objects = source.GetRootGameObjects();
            var floor = objects.SelectMany(o => o.GetComponentsInChildren<Floor3Controller>(true)).Single();
            var cameraController = objects.SelectMany(o => o.GetComponentsInChildren<CameraController>(true)).Single();
            float floorY, hallX;
            using (var serialized = new SerializedObject(cameraController))
            {
                floorY = serialized.FindProperty("_floor3Pos_Y").floatValue;
                hallX = serialized.FindProperty("_hallPos_X").floatValue;
            }
            var cameraObject = new GameObject("Offline third-floor camera");
            cameraObject.transform.SetParent(_root.transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.enabled = false;
            Camera.orthographic = true;
            Camera.orthographicSize = cameraController.GetComponent<Camera>().orthographicSize;
            Camera.transform.position = new Vector3(hallX, floorY, -30f);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.97f, 0.91f, 0.80f);
            Camera.cullingMask = 1 << PreviewLayer;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            var viewport = new Bounds(new Vector3(hallX, floorY, 0f), new Vector3(44f, Camera.orthographicSize * 2f, 20f));

            foreach (var sourceRenderer in objects.SelectMany(o => o.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy || sourceRenderer.sprite == null
                    || !sourceRenderer.bounds.Intersects(viewport)
                    || sourceRenderer.GetComponentInParent<FloorLockGroup>() != null
                    || sourceRenderer.GetComponentInParent<FloorLocker>() != null) continue;
                var go = new GameObject("Floor art: " + sourceRenderer.name);
                go.layer = PreviewLayer;
                go.transform.SetParent(_root.transform, false);
                go.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
                go.transform.localScale = sourceRenderer.transform.lossyScale;
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sprite = sourceRenderer.sprite;
                copy.color = sourceRenderer.color;
                copy.flipX = sourceRenderer.flipX;
                copy.flipY = sourceRenderer.flipY;
                copy.drawMode = sourceRenderer.drawMode;
                copy.size = sourceRenderer.size;
                copy.sortingLayerID = sourceRenderer.sortingLayerID;
                copy.sortingOrder = sourceRenderer.sortingOrder;
                copy.sharedMaterial = _spriteMaterial;
                CopiedFloorSpriteCount++;
            }
            var habitatObject = new GameObject("Actual third-floor fairy origin");
            habitatObject.transform.SetParent(_root.transform, false);
            habitatObject.transform.SetPositionAndRotation(floor.transform.position, floor.transform.rotation);
            habitatObject.transform.localScale = floor.transform.lossyScale;
            Habitat = habitatObject.AddComponent<EnhancementFairyHabitat>();
        }
        finally
        {
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
        }
    }

    private void LoadDetachedCatalog()
    {
        var sprites = Resources.LoadAll<Sprite>("ItemData/GachaItemData/Sprites")
            .Where(sprite => sprite.name.StartsWith("GOTCHA", StringComparison.Ordinal))
            .GroupBy(sprite => sprite.name.Split('_')[0]).ToDictionary(group => group.Key, group => group.First());
        var csv = Resources.Load<TextAsset>("ItemData/GachaItemData/GachaItemList");
        foreach (string line in csv.text.Split('\n').Skip(1))
        {
            string[] cells = line.Trim().Split(',');
            if (cells.Length < 12 || !Enum.TryParse(cells[8].Trim(), out UpgradeType type)
                || type < UpgradeType.UPGRADE01 || type >= UpgradeType.Length) continue;
            string id = cells[0].Trim();
            if (!sprites.TryGetValue(id, out var sprite)) continue;
            int.TryParse(cells[4], out int score);
            int.TryParse(cells[5], out int tip);
            int.TryParse(cells[6], out int rank);
            int.TryParse(cells[11], out int maxLevel);
            float.TryParse(cells[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
            float.TryParse(cells[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float upgrade);
            var item = new GachaItemData(id, cells[1], cells[2], score, tip, rank, type, value, upgrade, maxLevel, sprite);
            item.hideFlags = HideFlags.HideAndDontSave;
            _catalog.Add(item);
        }
    }

    public string ConfirmNextAcquisition()
    {
        if (_nextItem >= _catalog.Count) return null;
        var item = _catalog[_nextItem++];
        _owned.Add(item.Id);
        Habitat.ConfirmAcquisition("offline-fairy-" + ++_transaction, item);
        return item.Name;
    }

    public void RestoreAllOwned()
    {
        _owned.Clear();
        foreach (var item in _catalog) _owned.Add(item.Id);
        Habitat.RestoreOwnership(_owned);
    }

    public void SetVisibility(bool unlocked, bool visible)
    {
        _unlocked = unlocked;
        _visible = visible;
        Habitat.SetFloorVisible(_unlocked, _visible);
    }

    public void Tick(float deltaTime) => Habitat.AdvancePreview(deltaTime);

    public Texture2D Render(int width = 1400, int height = 600)
    {
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        var previous = RenderTexture.active;
        var previousTarget = Camera.targetTexture;
        try
        {
            Camera.targetTexture = texture;
            Camera.aspect = (float)width / height;
            Camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image;
        }
        finally
        {
            Camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }

    public void CapturePng(string path, int width = 1400, int height = 600)
    {
        var image = Render(width, height);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(image); }
    }

    public void Dispose()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        foreach (var item in _catalog) if (item != null) Object.DestroyImmediate(item);
        _catalog.Clear();
        if (_settings != null) Object.DestroyImmediate(_settings);
        if (_spriteMaterial != null) Object.DestroyImmediate(_spriteMaterial);
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
    }
}
#endif
