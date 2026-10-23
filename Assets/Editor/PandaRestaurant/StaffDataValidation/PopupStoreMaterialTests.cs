using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authored assets and memory-only instances. No scene loading, account setup or SDK calls.</summary>
public sealed class PopupStoreMaterialTests
{
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string SpriteFolder = "Assets/Sprites/PopupStore/";

    // Expected official source-to-ID association, independent of the catalog's serialized order.
    private static readonly string[] PlushFiles =
    {
        "Plush_Jiji_Approved.png",
        "STAFF02_덕덕_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF03_추추_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF04_루피_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF05_샐린_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF06_포야_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF07_바쿠_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF08_판다 마쉬_PlushKeyring_APPROVED_VISUAL.png",
        "Plush_Luna_Approved.png",
        "STAFF10_타로_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF11_토루루_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF12_제레미_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF13_라라_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF14_와와_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF15_폴_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF16_도치_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF17_염소아치_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF18_루파_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF19_양아치_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF20_포코_PlushKeyring_APPROVED_VISUAL.png",
        "Plush_Mello_Approved.png",
        "STAFF22_제리_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF23_피크_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF24_비비_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF25_카논_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF26_베르베르_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF27_고든 팬지_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF28_치치_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF29_셰프 바라_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF30_피터_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF31_호량_PlushKeyring_APPROVED_VISUAL.png",
        "STAFF32_슈슈_PlushKeyring_APPROVED_VISUAL.png"
    };

    [TestCase(PopupStoreMaterialKind.Plush, 32)]
    [TestCase(PopupStoreMaterialKind.Box, 3)]
    [TestCase(PopupStoreMaterialKind.Ribbon, 3)]
    public void CatalogHasExactlyTheApprovedIdsSpritesAndOrder(PopupStoreMaterialKind kind, int count)
    {
        var catalog = Catalog();
        Assert.That(catalog.TryValidate(out string error), Is.True, error);
        Assert.That(catalog.TryGetMaterials(kind, out var materials, out error), Is.True, error);
        Assert.That(materials, Has.Length.EqualTo(count));
        string[] expectedIds = ExpectedIds(kind);
        CollectionAssert.AreEqual(expectedIds, materials.Select(item => item.Id).ToArray());
        CollectionAssert.AllItemsAreUnique(materials.Select(item => item.DisplayOrder).ToArray());

        for (int i = 0; i < materials.Length; i++)
        {
            Assert.That(materials[i].Kind, Is.EqualTo(kind));
            Assert.That(materials[i].Sprite, Is.Not.Null, materials[i].Id);
            string file = kind == PopupStoreMaterialKind.Plush ? PlushFiles[i]
                : kind == PopupStoreMaterialKind.Box ? new[] { "Box_Pink.png", "Box_Blue.png", "Box_Mint.png" }[i]
                : new[] { "Ribbon_Pink.png", "Ribbon_Yellow.png", "Ribbon_Mint.png" }[i];
            Assert.That(AssetDatabase.GetAssetPath(materials[i].Sprite), Is.EqualTo(SpriteFolder + file), materials[i].Id);
        }
    }

    [Test]
    public void All38SpritesAreUniqueAndSpecialStaffMappingsRetainApprovedSources()
    {
        var entries = Entries(Catalog());
        Assert.That(entries, Has.Length.EqualTo(38));
        CollectionAssert.AllItemsAreUnique(entries.Select(item => item.Id).ToArray());
        CollectionAssert.AllItemsAreUnique(entries.Select(item => item.Sprite).ToArray());
        AssertSource(entries.Single(item => item.Id == "STAFF28"), "치치", "1OncHY8K2gm77MqCY4_i6z34MqZrEpyHm");
        AssertSource(entries.Single(item => item.Id == "STAFF29"), "셰프 바라", "14cP7wUdCSEIj6M68aCsSL7m8bM7RagFL");
    }

    [TestCase("missing-entry")]
    [TestCase("null-entry")]
    [TestCase("unknown-id")]
    [TestCase("duplicate-id")]
    [TestCase("duplicate-sprite")]
    [TestCase("wrong-category")]
    [TestCase("unknown-category")]
    [TestCase("duplicate-order")]
    [TestCase("negative-order")]
    [TestCase("missing-sprite")]
    public void InvalidInspectorDataIsRejectedInsteadOfReturningAPartialList(string defect)
    {
        var catalog = Object.Instantiate(Catalog());
        try
        {
            var entries = Entries(catalog);
            switch (defect)
            {
                case "missing-entry": Write(catalog, "_entries", entries.Skip(1).ToArray()); break;
                case "null-entry": entries[0] = null; break;
                case "unknown-id": Write(entries[0], "_id", "STAFF99"); break;
                case "duplicate-id": Write(entries[0], "_id", entries[1].Id); break;
                case "duplicate-sprite": Write(entries[0], "_sprite", entries[1].Sprite); break;
                case "wrong-category": Write(entries[0], "_kind", PopupStoreMaterialKind.Box); break;
                case "unknown-category": Write(entries[0], "_kind", (PopupStoreMaterialKind)99); break;
                case "duplicate-order": Write(entries[0], "_displayOrder", entries[1].DisplayOrder); break;
                case "negative-order": Write(entries[0], "_displayOrder", -1); break;
                case "missing-sprite": Write(entries[0], "_sprite", null); break;
            }
            Assert.That(catalog.TryValidate(out string error), Is.False, defect);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var result, out error), Is.False);
            Assert.That(result == null || result.Length == 0, Is.True, "Invalid data must not expose a partial material list.");
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void DisplayOrderCanBeEditedWithoutChangingStableIdsOrSprites()
    {
        var catalog = Object.Instantiate(Catalog());
        try
        {
            var entries = Entries(catalog);
            var first = entries.Single(item => item.Id == "STAFF01");
            var second = entries.Single(item => item.Id == "STAFF02");
            int firstOrder = first.DisplayOrder;
            Write(first, "_displayOrder", second.DisplayOrder);
            Write(second, "_displayOrder", firstOrder);
            Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var result, out string error), Is.True, error);
            Assert.That(result[0], Is.SameAs(second));
            Assert.That(result[1], Is.SameAs(first));
            Assert.That(AssetDatabase.GetAssetPath(first.Sprite), Is.EqualTo(SpriteFolder + PlushFiles[0]));
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void PrefabHasThreeIndependentClippedListsAndCategoryLocalButtons()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var lists = prefab.GetComponentsInChildren<PopupStoreMaterialList>(true);
        Assert.That(lists, Has.Length.EqualTo(3));
        CollectionAssert.AreEquivalent(new[] { PopupStoreMaterialKind.Plush, PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon },
            lists.Select(Kind).ToArray());
        CollectionAssert.AllItemsAreUnique(lists.Select(Scroll).ToArray());
        var contents = lists.Select(Content).ToArray();
        Assert.That(contents.All(content => content != null), Is.True);
        // Transform is IEnumerable; compare native identities rather than its child collections.
        CollectionAssert.AllItemsAreUnique(contents.Select(content => content.GetInstanceID()).ToArray());
        CollectionAssert.AllItemsAreUnique(lists.SelectMany(list => new[] { Button(list, false), Button(list, true) }).ToArray());
        var ordered = lists.OrderBy(list => list.transform.localPosition.x).Select(Kind).ToArray();
        CollectionAssert.AreEqual(new[] { PopupStoreMaterialKind.Plush, PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon }, ordered);

        foreach (var list in lists)
        {
            var scroll = Scroll(list);
            Assert.That(Read(list, "_catalog"), Is.SameAs(Catalog()));
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(Content(list)));
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.vertical, Is.False);
            Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>().enabled, Is.True);
            var hitArea = scroll.viewport.GetComponent<Graphic>();
            Assert.That(hitArea, Is.Not.Null, "The viewport must receive pointer events, including over gaps between icons.");
            Assert.That(hitArea.raycastTarget, Is.True);
            Assert.That(Content(list).parent, Is.SameAs(scroll.viewport));
            var template = (Image)Read(list, "_itemTemplate");
            Assert.That(template.transform.parent, Is.SameAs(Content(list)));
            Assert.That(template.gameObject.activeSelf, Is.False);
            Assert.That(template.preserveAspect, Is.True);
            foreach (var button in new[] { Button(list, false), Button(list, true) })
            {
                Assert.That(button.transform.IsChildOf(list.transform), Is.True);
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
            }
        }
    }

    [TestCase(PopupStoreMaterialKind.Plush, "wheel")]
    [TestCase(PopupStoreMaterialKind.Plush, "drag")]
    [TestCase(PopupStoreMaterialKind.Plush, "button")]
    [TestCase(PopupStoreMaterialKind.Box, "wheel")]
    [TestCase(PopupStoreMaterialKind.Box, "drag")]
    [TestCase(PopupStoreMaterialKind.Box, "button")]
    [TestCase(PopupStoreMaterialKind.Ribbon, "wheel")]
    [TestCase(PopupStoreMaterialKind.Ribbon, "drag")]
    [TestCase(PopupStoreMaterialKind.Ribbon, "button")]
    public void RealScrollRectInputMovesOnlyItsOwnListAndClampsAtBothEnds(PopupStoreMaterialKind kind, string input)
    {
        using (var host = new Host())
        {
            var target = host.List(kind);
            // Exercise the short lists' scroll path using their existing Inspector-adjustable viewport.
            Scroll(target).viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 180f);
            Assert.That(target.Refresh(), Is.True);
            host.Layout();
            var before = host.Lists.ToDictionary(list => list, list => Content(list).anchoredPosition);
            var scroll = Scroll(target);
            Assert.That(scroll.enabled, Is.True);
            Assert.That(Button(target, true).interactable, Is.True);
            ApplyInput(target, input, 1);
            Assert.That(Content(target).anchoredPosition.x, Is.LessThan(before[target].x - 0.1f), input);
            foreach (var other in host.Lists.Where(list => list != target))
                Assert.That(Content(other).anchoredPosition, Is.EqualTo(before[other]), Kind(other).ToString());

            ApplyInput(target, input, 10000);
            Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            ApplyInput(target, input, -10000);
            Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Content(target).anchoredPosition.y, Is.EqualTo(before[target].y).Within(0.001f));
        }
    }

    [TestCase(PopupStoreMaterialKind.Box)]
    [TestCase(PopupStoreMaterialKind.Ribbon)]
    public void AllVisibleShortListDisablesScrollingAndBothPageButtons(PopupStoreMaterialKind kind)
    {
        using (var host = new Host())
        {
            var list = host.List(kind);
            Assert.That(list.MaterialCount, Is.EqualTo(3));
            Assert.That(Scroll(list).enabled, Is.False);
            Assert.That(Button(list, false).interactable, Is.False);
            Assert.That(Button(list, true).interactable, Is.False);
            var before = Content(list).anchoredPosition;
            ApplyInput(list, "wheel", 10000);
            Button(list, true).onClick.Invoke();
            Assert.That(Content(list).anchoredPosition, Is.EqualTo(before));
        }
    }

    [TestCase("_scrollRect")]
    [TestCase("_nextButton")]
    public void CrossPanelReferenceIsRejectedWithoutDisablingTheOtherList(string field)
    {
        using (var host = new Host())
        {
            var broken = host.List(PopupStoreMaterialKind.Box);
            var other = host.List(PopupStoreMaterialKind.Plush);
            Write(broken, field, Read(other, field));
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreMaterialList: "));
            Assert.That(broken.Refresh(), Is.False);
            Assert.That(broken.IsReady, Is.False);
            Assert.That(broken.MaterialCount, Is.Zero);
            Assert.That(other.IsReady, Is.True);
            Assert.That(Scroll(other).enabled, Is.True);
            Assert.That(Button(other, true).interactable, Is.True);
            float start = Content(other).anchoredPosition.x;
            Button(other, true).onClick.Invoke();
            Assert.That(Content(other).anchoredPosition.x, Is.LessThan(start));
        }
    }

    [Test]
    public void RefreshAndReentryReuse38MaterialIconsWithoutAddingCallbacksOrAccountChanges()
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long encounterContext = UserInfo.CustomerEncounterContextGeneration;
        using (var host = new Host())
        {
            var counter = host.Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
            Assert.That(counter, Is.Not.Null);
            Assert.That(counter.Count, Is.Zero);
            var originalIcons = host.Lists.SelectMany(Icons).Select(icon => icon.GetInstanceID()).ToArray();
            Assert.That(originalIcons, Has.Length.EqualTo(38));
            for (int i = 0; i < 3; i++)
            {
                foreach (var list in host.Lists) Assert.That(list.Refresh(), Is.True);
                host.Root.SetActive(false);
                host.Root.SetActive(true);
            }
            host.Layout();
            CollectionAssert.AreEquivalent(originalIcons, host.Lists.SelectMany(Icons).Select(icon => icon.GetInstanceID()).ToArray());
            foreach (var list in host.Lists)
            {
                Assert.That(list.IsReady, Is.True);
                Assert.That(Catalog().TryGetMaterials(Kind(list), out var entries, out string error), Is.True, error);
                var icons = Icons(list);
                CollectionAssert.AreEqual(entries.Select(item => item.Sprite).ToArray(), icons.Select(icon => icon.sprite).ToArray());
                foreach (var icon in icons)
                {
                    Assert.That(icon.GetComponentsInChildren<Selectable>(true), Is.Empty);
                    Assert.That(icon.preserveAspect, Is.True);
                    var input = icon.GetComponent<PopupStoreMaterialItem>();
                    Assert.That(input, Is.Not.Null);
                    Assert.That(input.Owner, Is.SameAs(list));
                    Assert.That(input.Material.Id, Is.EqualTo(icon.name));
                    // Material input exists in DEV-02B, but a click without its valid press cannot add an item.
                    Assert.That(ExecuteEvents.Execute(icon.gameObject, new PointerEventData(null), ExecuteEvents.pointerClickHandler), Is.True);
                    Assert.That(counter.Count, Is.Zero);
                }
            }
            var plush = host.List(PopupStoreMaterialKind.Plush);
            Scroll(plush).horizontalNormalizedPosition = 0f;
            Scroll(plush).Rebuild(CanvasUpdate.PostLayout);
            float page = (float)Read(plush, "_pageDistance");
            float start = Content(plush).anchoredPosition.x;
            Button(plush, true).onClick.Invoke();
            Assert.That(start - Content(plush).anchoredPosition.x, Is.EqualTo(page).Within(0.01f), "Reopening must not add duplicate page listeners.");
        }
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(encounterContext));
    }

    private static void ApplyInput(PopupStoreMaterialList list, string input, int amount)
    {
        var scroll = Scroll(list);
        if (input == "button")
        {
            var button = Button(list, amount > 0);
            for (int i = 0; i < Mathf.Min(Mathf.Abs(amount), 100); i++) button.onClick.Invoke();
            return;
        }
        var data = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        if (input == "wheel")
        {
            // The authored negative sensitivity maps a normal downward wheel to the next items.
            data.scrollDelta = new Vector2(0f, -amount);
            scroll.OnScroll(data);
            return;
        }
        data.position = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.position);
        scroll.OnInitializePotentialDrag(data);
        scroll.OnBeginDrag(data);
        data.position += Vector2.left * 80f * amount;
        scroll.OnDrag(data);
        scroll.OnEndDrag(data);
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly PopupStoreMaterialList[] Lists;
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-material-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                Assert.That(Lists, Has.Length.EqualTo(3));
                Root.SetActive(true);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True, list.name);
                Layout();
            }
            catch { Dispose(); throw; }
        }
        internal PopupStoreMaterialList List(PopupStoreMaterialKind kind) => Lists.Single(list => Kind(list) == kind);
        internal void Layout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var list in Lists) Scroll(list).Rebuild(CanvasUpdate.PostLayout);
        }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); }
    }

    private static PopupStoreMaterialCatalog Catalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        return catalog;
    }
    private static string[] ExpectedIds(PopupStoreMaterialKind kind)
    {
        if (kind == PopupStoreMaterialKind.Plush) return Enumerable.Range(1, 32).Select(id => "STAFF" + id.ToString("00")).ToArray();
        return kind == PopupStoreMaterialKind.Box ? new[] { "BOX_PINK", "BOX_BLUE", "BOX_MINT" }
            : new[] { "RIBBON_PINK", "RIBBON_YELLOW", "RIBBON_MINT" };
    }
    private static void AssertSource(PopupStoreMaterialEntry entry, string name, string sourceId)
    {
        string path = AssetDatabase.GetAssetPath(entry.Sprite);
        Assert.That(path, Does.Contain(entry.Id + "_" + name + "_"));
        Assert.That(File.ReadAllText(path + ".meta"), Does.Contain("https://drive.google.com/file/d/" + sourceId + "/view"));
    }
    private static PopupStoreMaterialEntry[] Entries(PopupStoreMaterialCatalog catalog) => (PopupStoreMaterialEntry[])Read(catalog, "_entries");
    private static PopupStoreMaterialKind Kind(PopupStoreMaterialList list) => (PopupStoreMaterialKind)Read(list, "_kind");
    private static ScrollRect Scroll(PopupStoreMaterialList list) => (ScrollRect)Read(list, "_scrollRect");
    private static RectTransform Content(PopupStoreMaterialList list) => (RectTransform)Read(list, "_content");
    private static Button Button(PopupStoreMaterialList list, bool next) => (Button)Read(list, next ? "_nextButton" : "_previousButton");
    private static Image[] Icons(PopupStoreMaterialList list) => Content(list).GetComponentsInChildren<Image>(false);
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
