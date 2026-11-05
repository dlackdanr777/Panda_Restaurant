#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KitchenSpriteAssetTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const string Root = "Assets/Resources/KitchenUtensilData/Sprites";

    [Test]
    public void NumberedResources_KeepSingleSpritesNamesAndLocalFileIds()
    {
        string[] paths = Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/')).ToArray();
        Assert.That(paths.Length, Is.EqualTo(342));
        var numbered = paths.Where(path => Regex.IsMatch(path.Split('/')[4], @"^\d{2}$")).ToArray();
        Assert.That(numbered.Length, Is.EqualTo(340));
        foreach (string path in paths.Except(numbered))
        {
            Assert.That(Path.GetFileName(path), Does.Match(@"^(SINK|COOKER)00_"));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(path)).textureType, Is.EqualTo(TextureImporterType.Default));
        }
        foreach (string path in numbered)
        {
            Assert.That(path.Split('/')[4], Does.Match(@"^\d{2}$"), path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(fileId, Is.EqualTo(21300000));
            string resource = path.Substring("Assets/Resources/".Length);
            Assert.That(Resources.Load<Sprite>(resource.Substring(0, resource.Length - 4)), Is.SameAs(sprite));
        }
        Assert.That(Resources.LoadAll<Sprite>("KitchenUtensilData/Sprites").Length, Is.EqualTo(numbered.Length));
    }

    [Test]
    public void ProductionCatalog_ShopPortraitsPreviewAndThreeFloorPlacementKeepOriginalSprites()
    {
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        using (var state = new CatalogScope())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var catalog = (List<KitchenUtensilData>)typeof(KitchenUtensilDataManager)
                .GetField("_kitchenUtensilDataList", Static).GetValue(null);
            Assert.That(catalog, Is.Not.Empty);
            foreach (var data in catalog)
            {
                Assert.That(data.Sprite, Is.Not.Null, data.Id);
                Assert.That(data.ThumbnailSprite, Is.Not.Null, data.Id);
                Assert.That(data.Sprite.name.Split('_')[0], Is.EqualTo(data.Id.Split('_')[0]));
            }
            var racks = catalog.Where(data => data.Type == KitchenUtensilType.Kitchenrack).ToArray();
            Assert.That(racks.Length, Is.EqualTo(26));
            var stage = new StageInfo();
            state.Swap(typeof(UserInfo), "_currentStage", EStage.Stage1);
            state.Swap(typeof(UserInfo), "_stageInfos", new[] { stage, new StageInfo(), new StageInfo() });
            ((List<string>)Field(typeof(StageInfo), "_giveKitchenUtensilList").GetValue(stage))
                .AddRange(racks.Select(data => data.Id));
            var equipped = (KitchenUtensilData[,])Field(typeof(StageInfo), "_equipKitchenUtensilDatas").GetValue(stage);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<UIKitchen>(true)).Single();
                view.gameObject.SetActive(false);
                fonts.BindBeforeActivation(view.gameObject);
                var preview = (UIKitchenPreview)new SerializedObject(view)
                    .FindProperty("_uikitchenPreview").objectReferenceValue;
                var group = (UIImageAndText)new SerializedObject(preview)
                    .FindProperty("_selectGroup").objectReferenceValue;
                var portrait = (Image)new SerializedObject(group).FindProperty("_image").objectReferenceValue;
                var viewSettings = new SerializedObject(view);
                var prefab = (UIRestaurantAdminFoodTypeSlot)viewSettings.FindProperty("_slotPrefab").objectReferenceValue;
                var parent = (Transform)viewSettings.FindProperty("_slotParnet").objectReferenceValue;
                var slot = Object.Instantiate(prefab, parent);
                slot.gameObject.SetActive(false);
                fonts.BindBeforeActivation(slot.gameObject);
                var slotPortrait = (Image)new SerializedObject(slot).FindProperty("_image").objectReferenceValue;
                var floorGroups = roots.SelectMany(root => root.GetComponentsInChildren<KitchenUtensilGroup>(true)).ToArray();
                Assert.That(floorGroups.Length, Is.EqualTo(3));
                foreach (var data in racks)
                    foreach (var floorGroup in floorGroups)
                    {
                        Array.Clear(equipped, 0, equipped.Length);
                        equipped[(int)floorGroup.FloorType, (int)data.Type] = data;
                        slot.SetUse(data.ThumbnailSprite, data.Name, "", floorGroup.FloorType);
                        Assert.That(slotPortrait.sprite, Is.SameAs(data.ThumbnailSprite), data.Id);
                        slot.SetOperate(data.ThumbnailSprite, data.Name, "");
                        Assert.That(slotPortrait.sprite, Is.SameAs(data.ThumbnailSprite), data.Id);
                        preview.SetData(floorGroup.FloorType, data);
                        Assert.That(preview.SelectedData, Is.SameAs(data));
                        Assert.That(portrait.sprite, Is.SameAs(data.ThumbnailSprite));
                        var items = new SerializedObject(floorGroup).FindProperty("_kitchenUtensils");
                        var shelf = Enumerable.Range(0, items.arraySize)
                            .Select(i => (KitchenUtensil)items.GetArrayElementAtIndex(i).objectReferenceValue)
                            .Single(item => item.Type == KitchenUtensilType.Kitchenrack);
                        var renderer = (SpriteRenderer)new SerializedObject(shelf)
                            .FindProperty("_spriteRenderer").objectReferenceValue;
                        Vector3 scale = renderer.transform.localScale;
                        shelf.SetData(data);
                        Assert.That(renderer.sprite, Is.SameAs(data.Sprite));
                        Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
                        Assert.That(renderer.gameObject.activeSelf, Is.True);
                        Assert.That(renderer.bounds.size.x, Is.GreaterThan(0));
                        Assert.That(renderer.bounds.size.y, Is.GreaterThan(0));
                    }
                Debug.Log("KITCHEN_SPRITE_BINDINGS_VERIFIED catalog=" + catalog.Count + "; shelves=26; floors=3");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
    }

    private static FieldInfo Field(Type type, string name)
        => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new MissingFieldException(type.FullName, name);

    private sealed class CatalogScope : IDisposable
    {
        private readonly List<Action> restore = new List<Action>();

        public CatalogScope()
        {
            foreach (var field in typeof(KitchenUtensilDataManager).GetFields(Static))
            {
                if (field.Name == "_instance") continue;
                object prior = field.GetValue(null);
                object empty = field.FieldType.IsArray
                    ? Array.CreateInstance(field.FieldType.GetElementType(), ((Array)prior).Length)
                    : Activator.CreateInstance(field.FieldType);
                Swap(typeof(KitchenUtensilDataManager), field.Name, empty);
            }
            foreach (string csv in new[] { "CookerData", "FridgeData", "CabinetData", "WindowData",
                         "CooktoolData", "KitchenrackData", "SinkData" })
                foreach (string row in Resources.Load<TextAsset>("KitchenUtensilData/CSVData/" + csv).text.Split('\n').Skip(1))
                    if (string.IsNullOrWhiteSpace(row.Split(',')[0]))
                        LogAssert.Expect(LogType.Error, "Id값이 이상합니다: " + row.Split(',')[0].Trim());
            try
            {
                typeof(KitchenUtensilDataManager).GetMethod("InitData", Static).Invoke(null, null);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Swap(Type type, string name, object value)
        {
            var field = Field(type, name);
            object prior = field.GetValue(null);
            restore.Add(() => field.SetValue(null, prior));
            field.SetValue(null, value);
        }

        public void Dispose()
        {
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
            restore.Clear();
        }
    }
}
#endif
