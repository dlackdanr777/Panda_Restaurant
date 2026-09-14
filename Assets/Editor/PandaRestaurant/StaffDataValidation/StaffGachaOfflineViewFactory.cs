#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Copies only the existing machine/card hierarchy into an unsaved, empty scene before Play.
/// The source preview is closed before return; no gameplay scene or asset is saved or played.
/// </summary>
public static class StaffGachaOfflineViewFactory
{
    public const string RootName = "Staff Purchase Offline View";
    public const string SourceScenePath = "Assets/Scenes/Stage1.unity";
    private const string SourceViewPath = "Main Canvas/Gacha UI";

    public static GameObject BuildInEmptyEditorScene(out UIGacha view, out UIStaffGacha staff)
        => BuildInEmptyEditorScene(SceneManager.GetActiveScene(), out view, out staff);

    // An explicit empty preview destination lets EditMode tests exercise the same copy path
    // without changing, saving or closing the runner's or user's current untitled scene.
    public static GameObject BuildInEmptyEditorScene(Scene destination, out UIGacha view, out UIStaffGacha staff)
    {
        view = null;
        staff = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !destination.IsValid() ||
            !destination.isLoaded || !string.IsNullOrEmpty(destination.path) || destination.rootCount != 0)
            throw new InvalidOperationException("Create the offline view only in a new, empty unsaved scene before Play.");

        Scene preview = default;
        GameObject root = null;
        try
        {
            // Opening a preview scene in EditMode does not run ordinary gameplay MonoBehaviour Awake.
            // Do not enter Play until this source scene has been closed in finally.
            preview = EditorSceneManager.OpenPreviewScene(SourceScenePath);
            Transform group = preview.GetRootGameObjects().SingleOrDefault(item => item.name == "Canvas Group")?.transform;
            UIGacha source = group?.Find(SourceViewPath)?.GetComponent<UIGacha>();
            if (source == null || source.gameObject.activeSelf)
                throw new InvalidOperationException("The existing inactive Gacha UI source could not be verified.");

            root = new GameObject(RootName, typeof(RectTransform));
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, destination);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas originalCanvas = source.GetComponent<Canvas>();
            canvas.pixelPerfect = originalCanvas != null && originalCanvas.pixelPerfect;
            if (originalCanvas != null) canvas.additionalShaderChannels = originalCanvas.additionalShaderChannels;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            CanvasScaler originalScaler = source.GetComponent<CanvasScaler>();
            if (originalScaler == null)
                throw new InvalidOperationException("The source canvas scaling settings are missing.");
            EditorUtility.CopySerialized(originalScaler, scaler);
            root.AddComponent<GraphicRaycaster>();

            GameObject clone = Object.Instantiate(source.gameObject, root.transform, false);
            clone.name = source.name;
            clone.SetActive(false);
            view = clone.GetComponent<UIGacha>();
            staff = clone.GetComponentsInChildren<UIStaffGacha>(true).Single();

            // UINavigation normally supplies the full-screen view rect. Reproduce that container,
            // retaining all existing machine/card child dimensions, placement and art.
            RectTransform viewRect = (RectTransform)clone.transform;
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.offsetMin = viewRect.offsetMax = Vector2.zero;
            viewRect.localScale = Vector3.one;

            foreach (GachaMachineParent machine in clone.GetComponentsInChildren<GachaMachineParent>(true))
                if (machine != staff) machine.gameObject.SetActive(false);
            foreach (UIGachaSlotList list in clone.GetComponentsInChildren<UIGachaSlotList>(true))
                list.gameObject.SetActive(false);
            // The scene also stores a populated generic sample card directly under UI Components,
            // outside any machine. It is not a purchase result; keep every source card as a hidden
            // template. StaffGachaPurchaseDisplay activates only its own confirmed-result copy.
            foreach (UIGachaCard card in clone.GetComponentsInChildren<UIGachaCard>(true))
                card.gameObject.SetActive(false);

            // Disabled custom behaviours still receive Awake on later activation. Remove them from
            // this disposable copy instead of allowing DataBind, pools, tutorials or decorative RNG.
            foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
            {
                if (component == null) throw new InvalidOperationException("The source view contains a missing script.");
                if (!IsDisplayBehaviour(component, view, staff)) Object.DestroyImmediate(component);
            }
            foreach (ScrollingImage image in clone.GetComponentsInChildren<ScrollingImage>(true)) image.enabled = false;
            foreach (Button button in clone.GetComponentsInChildren<Button>(true))
                button.onClick = new Button.ButtonClickedEvent();
            foreach (ScrollRect scroll in clone.GetComponentsInChildren<ScrollRect>(true))
            {
                scroll.onValueChanged = new ScrollRect.ScrollRectEvent();
                scroll.enabled = false;
            }
            foreach (Scrollbar scrollbar in clone.GetComponentsInChildren<Scrollbar>(true))
                scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
            foreach (Animator animator in clone.GetComponentsInChildren<Animator>(true))
            {
                animator.fireEvents = false;
                animator.enabled = animator.gameObject == staff.gameObject;
            }
            foreach (AudioSource audio in clone.GetComponentsInChildren<AudioSource>(true))
            {
                audio.playOnAwake = false;
                audio.Stop();
            }
            foreach (ParticleSystem particle in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.playOnAwake = false;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Instantiate remaps references within the subtree. Any remaining scene reference is
            // outside it (MainScene/tutorial/camera); do not retain the temporary source scene.
            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("The copy contains a missing component.");
                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object reference = property.objectReferenceValue;
                        Transform referencedTransform = reference is GameObject item ? item.transform : (reference as Component)?.transform;
                        if (referencedTransform == null || referencedTransform == root.transform ||
                            referencedTransform.IsChildOf(root.transform) || EditorUtility.IsPersistent(reference)) continue;
                        property.objectReferenceValue = null;
                        changed = true;
                    }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            ValidateRequiredBindings(staff);
            var cameraObject = new GameObject("Offline Background Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1f);
            camera.cullingMask = 0;
            var eventObject = new GameObject("Offline EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventObject.transform.SetParent(root.transform, false);
            return root;
        }
        catch
        {
            view = null;
            staff = null;
            if (root != null) Object.DestroyImmediate(root);
            throw;
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (destination.IsValid() && destination.isLoaded && !EditorSceneManager.IsPreviewScene(destination))
                SceneManager.SetActiveScene(destination);
        }
    }

    private static bool IsDisplayBehaviour(MonoBehaviour component, UIGacha view, UIStaffGacha staff)
    {
        if (component == view || component == staff || component is UIGachaCard ||
            component is UIItemStar || component is GachaCapsule || component is ScrollingImage) return true;
        // Exact assemblies exclude game subclasses; only rendering/layout and inert controls survive.
        // EventTrigger and other arbitrary UnityEvent dispatchers are deliberately not in the list.
        Type type = component.GetType();
        if (type.Assembly == typeof(TextMeshProUGUI).Assembly)
            return component is TextMeshProUGUI || component is TMP_SubMeshUI;
        return type.Assembly == typeof(Image).Assembly &&
            (component is Graphic || component is Mask || component is RectMask2D || component is CanvasScaler ||
             component is GraphicRaycaster || component is Button || component is ScrollRect || component is Scrollbar ||
             component is LayoutGroup || component is LayoutElement || component is ContentSizeFitter || component is AspectRatioFitter);
    }

    private static void ValidateRequiredBindings(UIStaffGacha staff)
    {
        string[] names = { "_scrollImage", "_gachaMacineAnimator", "_screenButton", "_singleButton", "_tenButton",
            "_skipButton", "_getStaffImage", "_gachaCard", "_capsule", "_getStaffSlotFrame", "_capsules", "_gachaSound" };
        using (var serialized = new SerializedObject(staff))
            foreach (string name in names)
                if (serialized.FindProperty(name)?.objectReferenceValue == null)
                    throw new InvalidOperationException("The existing staff presentation binding is missing: " + name);
    }
}

/// <summary>
/// Play-session-only copies: dynamic TMP atlas/glyph updates must never target project font assets.
/// Bind while both the view and its recreation template are inactive, before their TMP Awake.
/// </summary>
public sealed class StaffGachaOfflineFonts : IDisposable
{
    private readonly Dictionary<TMP_FontAsset, TMP_FontAsset> _fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
    private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
    private readonly Dictionary<Texture2D, Texture2D> _textures = new Dictionary<Texture2D, Texture2D>();
    private readonly HashSet<Object> _owned = new HashSet<Object>();
    private bool _disposed;

    public void BindBeforeActivation(params GameObject[] roots)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StaffGachaOfflineFonts));
        if (roots == null || roots.Any(root => root == null || root.activeInHierarchy))
            throw new InvalidOperationException("Offline fonts must be bound before view activation.");
        foreach (GameObject root in roots)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                TMP_FontAsset source = text.font != null ? text.font : TMP_Settings.defaultFontAsset;
                if (source == null) throw new InvalidOperationException("The offline text has no font or TMP default font.");
                TMP_FontAsset font = CloneFont(source);
                // Preserve TMP's local fallback order, then mirror global fallbacks/default locally.
                // Do not replace TMP_Settings or let a supported glyph reach a persistent fallback.
                foreach (TMP_FontAsset fallback in TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>())
                    AddFallback(font, CloneFont(fallback));
                AddFallback(font, CloneFont(TMP_Settings.defaultFontAsset));
                RebindSerializedReferences(text, font);
            }
            foreach (TMP_SubMeshUI submesh in root.GetComponentsInChildren<TMP_SubMeshUI>(true))
            {
                RebindSerializedReferences(submesh, null);
                submesh.fallbackSourceMaterial = CloneMaterial(submesh.fallbackSourceMaterial);
                if (submesh.fallbackMaterial != null) submesh.fallbackMaterial = CloneMaterial(submesh.fallbackMaterial);
            }
        }
    }

    private void RebindSerializedReferences(Component component, TMP_FontAsset defaultFont)
    {
        using (var serialized = new SerializedObject(component))
        {
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object source = property.objectReferenceValue;
                if (source is TMP_FontAsset font) property.objectReferenceValue = CloneFont(font);
                else if (source is Material material) property.objectReferenceValue = CloneMaterial(material);
            }
            if (defaultFont != null)
            {
                serialized.FindProperty("m_fontAsset").objectReferenceValue = defaultFont;
                SerializedProperty material = serialized.FindProperty("m_sharedMaterial");
                if (material.objectReferenceValue == null) material.objectReferenceValue = defaultFont.material;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private TMP_FontAsset CloneFont(TMP_FontAsset source)
    {
        if (source == null) return null;
        if (_owned.Contains(source)) return source;
        if (_fonts.TryGetValue(source, out TMP_FontAsset existing)) return existing;
        TMP_FontAsset clone = Object.Instantiate(source);
        // TMP_FontAsset.OnDestroy destroys its atlas/material. Detach these shared references
        // immediately, so even cleanup after a later exception cannot destroy source resources.
        clone.atlasTextures = Array.Empty<Texture2D>();
        clone.material = null;
        Own(clone);
        _fonts.Add(source, clone); // Register before traversing possible cyclic fallback/weight links.
        clone.atlasTextures = source.atlasTextures?.Select(CloneTexture).ToArray() ?? Array.Empty<Texture2D>();
        clone.material = CloneMaterial(source.material);
        clone.fallbackFontAssetTable = (source.fallbackFontAssetTable ?? new List<TMP_FontAsset>())
            .Select(CloneFont).ToList();
        TMP_FontWeightPair[] weights = clone.fontWeightTable;
        if (weights != null)
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i].regularTypeface = CloneFont(weights[i].regularTypeface);
                weights[i].italicTypeface = CloneFont(weights[i].italicTypeface);
            }
        clone.ReadFontAssetDefinition();
        return clone;
    }

    private Material CloneMaterial(Material source)
    {
        if (source == null) return null;
        if (_owned.Contains(source)) return source;
        if (_materials.TryGetValue(source, out Material existing)) return existing;
        Material clone = Own(Object.Instantiate(source));
        _materials.Add(source, clone);
        foreach (string property in source.GetTexturePropertyNames())
            if (source.GetTexture(property) is Texture2D texture) clone.SetTexture(property, CloneTexture(texture));
        return clone;
    }

    private Texture2D CloneTexture(Texture2D source)
    {
        if (source == null) return null;
        if (_owned.Contains(source)) return source;
        if (_textures.TryGetValue(source, out Texture2D existing)) return existing;
        Texture2D clone = Own(Object.Instantiate(source));
        _textures.Add(source, clone);
        return clone;
    }

    private T Own<T>(T clone) where T : Object
    {
        clone.name += " (Staff Offline)";
        clone.hideFlags = HideFlags.HideAndDontSave;
        _owned.Add(clone);
        return clone;
    }

    private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
    {
        if (fallback != null && fallback != font && !font.fallbackFontAssetTable.Contains(fallback))
            font.fallbackFontAssetTable.Add(fallback);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Font cleanup also owns any additional dynamic atlas pages created during this Play session.
        foreach (TMP_FontAsset font in _fonts.Values) if (font != null) Object.DestroyImmediate(font);
        foreach (Object item in _owned) if (item != null) Object.DestroyImmediate(item);
        _fonts.Clear(); _materials.Clear(); _textures.Clear(); _owned.Clear();
    }
}
#endif
