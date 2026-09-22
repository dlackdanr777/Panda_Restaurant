#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class DefaultTableRenderingTests
{
    private const string Folder = "Assets/Resources/FurnitureData/Sprites/Default/";
    private const string Body = Folder + "TABLE00_기본 테이블.png";
    private const string Left = Folder + "TABLE00_기본 테이블L.png";
    private const string Right = Folder + "TABLE00_기본 테이블R.png";
    private const string Thumbnail = Folder + "TABLE00_기본 테이블_썸네일.png";

    [TestCase(Body)]
    [TestCase(Left)]
    [TestCase(Right)]
    public void DefaultTable_PlacementSpritesKeepSourceResolutionAndLosslessAlpha(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        var settings = importer.GetDefaultPlatformTextureSettings();
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        var effective = standalone.overridden ? standalone : settings;
        Vector2Int source = ReadPngSize(path);

        Assert.That(effective.maxTextureSize, Is.GreaterThanOrEqualTo(Mathf.Max(source.x, source.y)),
            "The placement sprite must not be downsampled before the Game view renders it");
        Assert.That(effective.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed),
            "Block compression damages the thin outline and translucent edges of the basic table");
        Assert.That(effective.crunchedCompression, Is.False);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.texture.width, Is.EqualTo(source.x));
        Assert.That(sprite.texture.height, Is.EqualTo(source.y));
        Assert.That(sprite.rect, Is.EqualTo(new Rect(0f, 0f, source.x, source.y)),
            "Placement uses the full original sprite region, not a cropped shop thumbnail");
        Assert.That(sprite.texture.format, Is.EqualTo(TextureFormat.RGBA32));
    }

    [Test]
    public void DefaultTable_StageOneFallbackRendersSeparateBodyAndChairsNotTheThumbnail()
    {
        // Preview only: no scene saving, account restore, data manager creation,
        // purchase/equip calls or Play mode is needed to bind default artwork.
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        try
        {
            var body = AssetDatabase.LoadAssetAtPath<Sprite>(Body);
            var left = AssetDatabase.LoadAssetAtPath<Sprite>(Left);
            var right = AssetDatabase.LoadAssetAtPath<Sprite>(Right);
            var thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(Thumbnail);
            Assert.That(body, Is.Not.Null);
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            Assert.That(thumbnail, Is.Not.Null);
            Assert.That(thumbnail, Is.Not.SameAs(body));
            var tables = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TableFurniture>(true))
                .Where(table => new SerializedObject(table).FindProperty("_defalutSprite").objectReferenceValue != null).ToArray();
            Assert.That(tables.Length, Is.EqualTo(3), "The three floor defaults use the same independent table parts");
            foreach (var table in tables)
            {
                var serialized = new SerializedObject(table);
                Assert.That(table.Type, Is.EqualTo(FurnitureType.Table1));
                Assert.That(serialized.FindProperty("_defalutSprite").objectReferenceValue, Is.SameAs(body));
                Assert.That(serialized.FindProperty("_defalutLeftChairSprite").objectReferenceValue, Is.SameAs(left));
                Assert.That(serialized.FindProperty("_defalutRightChairSprite").objectReferenceValue, Is.SameAs(right));
                table.SetFurnitureData(null);
                AssertRenderer(serialized, "_spriteRenderer", body);
                AssertRenderer(serialized, "_leftChairSpriteRenderer", left);
                AssertRenderer(serialized, "_rightChairSpriteRenderer", right);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void AssertRenderer(SerializedObject table, string field, Sprite expected)
    {
        var renderer = table.FindProperty(field).objectReferenceValue as SpriteRenderer;
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sprite, Is.SameAs(expected), field);
        Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple), "The original artwork must not be sliced or tiled");
        Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.Not.EqualTo(Thumbnail));
    }

    private static Vector2Int ReadPngSize(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, data.Take(8).ToArray());
        int ReadBigEndian(int offset) => (data[offset] << 24) | (data[offset + 1] << 16)
            | (data[offset + 2] << 8) | data[offset + 3];
        return new Vector2Int(ReadBigEndian(16), ReadBigEndian(20));
    }
}
#endif
