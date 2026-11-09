#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authored tutorial and actual isolated admission. Never approves an account or starts Play.</summary>
public sealed class PopupStorePhaseTutorialTests
{
    private const string Prefab = "Assets/Editor/PandaRestaurant/StaffDataValidation/UI/PopupStorePhasePreviewView.prefab";
    private static T Field<T>(Object target, string name) where T : Object
        => new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
    private sealed class ViewHost : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly PopupStorePhasePreviewView View;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal int Plays, Closes;
        internal bool Accept = true;
        internal ViewHost()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            Root.SetActive(false); _fonts.BindBeforeActivation(Root); Root.SetActive(true);
            View = Root.GetComponent<PopupStorePhasePreviewView>();
            View.Bind(() => { Plays++; return Accept; }, () => { Closes++; return Accept; });
            View.RenderTutorial(true, true, "이용권 1/1");
        }
        internal void Last() { while (View.TryNext()) { } }
        public void Dispose() { Object.DestroyImmediate(Root); _fonts.Dispose(); }
    }

    [Test]
    public void AuthoredPreviewHasFiveValidPagesAndEditorOnlyAssetLocation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<PopupStorePhasePreviewView>().ValidateConfiguration(out var error), Is.True, error);
        Assert.That(prefab.GetComponent<PopupStorePhasePreviewView>().PageCount, Is.EqualTo(5));
        Assert.That(Prefab.Contains("/Editor/"), Is.True);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void OnlyFinalPageShowsPlayAndTicket(int page)
    {
        using (var h = new ViewHost())
        {
            for (int i = 0; i < page; i++) Assert.That(h.View.TryNext(), Is.True);
            Assert.That(Field<Button>(h.View, "_playButton").gameObject.activeSelf, Is.EqualTo(page == 4));
            Assert.That(Field<Image>(h.View, "_ticketImage").gameObject.activeSelf, Is.EqualTo(page == 4));
            Assert.That(Field<Button>(h.View, "_closeButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(h.View.TryPrevious(), Is.EqualTo(page > 0));
        }
    }
    [Test]
    public void PageBoundariesDoNotWrapOrStartGameplay()
    {
        using (var h = new ViewHost())
        {
            Assert.That(h.View.TryPrevious(), Is.False);
            for (int i = 0; i < 4; i++) { Assert.That(h.View.TryPlay(), Is.False); Assert.That(h.View.TryNext(), Is.True); }
            Assert.That(h.View.TryNext(), Is.False);
            Assert.That(h.Plays, Is.Zero);
        }
    }
    [Test]
    public void SuccessfulPlayDispatchesOnceAndLocksNavigation()
    {
        using (var h = new ViewHost())
        {
            h.Last(); Assert.That(h.View.TryPlay(), Is.True);
            Assert.That(h.View.TryPlay(), Is.False); Assert.That(h.View.TryPrevious(), Is.False);
            Assert.That(h.Plays, Is.EqualTo(1)); Assert.That(h.View.PlayRequestCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void RejectedPlayCanRetryWithoutPermanentButtonLatch()
    {
        using (var h = new ViewHost())
        {
            h.Last(); h.Accept = false;
            Assert.That(h.View.TryPlay(), Is.False); Assert.That(h.View.PlayRequestCount, Is.Zero);
            h.Accept = true; Assert.That(h.View.TryPlay(), Is.True);
            Assert.That(h.View.PlayRequestCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void RejectedCloseCanRetryAndDoesNotConsumeStart()
    {
        using (var h = new ViewHost())
        {
            h.Accept = false; Assert.That(h.View.TryClose(), Is.False);
            h.Accept = true; Assert.That(h.View.TryClose(), Is.True); Assert.That(h.View.TryClose(), Is.False);
            Assert.That(h.Plays, Is.Zero); Assert.That(h.View.CloseRequestCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void UnavailableTicketCannotDispatchPlayButCanClose()
    {
        using (var h = new ViewHost())
        {
            h.Last(); h.View.RenderTutorial(true, false, "미니게임 시작권 0/1");
            Assert.That(h.View.TryPlay(), Is.False); Assert.That(h.View.TryClose(), Is.True);
            Assert.That(h.Plays, Is.Zero);
        }
    }
    [TestCase("3", "9663e27b1ec495e478552fb4d877cac3")]
    [TestCase("2", "736db4e6770994947b89aee320551adc")]
    [TestCase("1", "3c52935ff89c9c947845952667d1bdda")]
    [TestCase("GameStart", "7226f71494decb14c92108da2072f9d7")]
    [TestCase("Success", "efb0eff2d5acd764eb8f0a86af0e45ea")]
    [TestCase("GameOver", "46b26607e5b1dcb4e81d7b2efa12a2ef")]
    public void CueUsesExactOriginalRecipeSprite(string cue, string guid)
    {
        using (var h = new ViewHost())
        {
            h.View.RenderCue(cue, 2, .5f);
            Assert.That(h.View.IsTutorialVisible, Is.False); Assert.That(h.View.IsCueVisible, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Field<Image>(h.View, "_cueImage").sprite)), Is.EqualTo(guid));
            Assert.That(Field<Image>(h.View, "_cueImage").preserveAspect, Is.True);
            h.View.RenderCue(cue, 2, .8f); Assert.That(h.View.CueShowCount, Is.EqualTo(1));
        }
    }
    [TestCase(1)] [TestCase(6)]
    public void StageNoticeDisplaysExactStageNumber(int stage)
    {
        using (var h = new ViewHost())
        {
            h.View.RenderCue("Stage", stage, 0);
            Assert.That(Field<TMP_Text>(h.View, "_stageText").text, Is.EqualTo("STAGE " + stage));
            Assert.That(Field<Image>(h.View, "_cueImage").gameObject.activeSelf, Is.False);
        }
    }
    [Test]
    public void HiddenViewCannotStartOrCloseAndStopsBlockingInput()
    {
        using (var h = new ViewHost())
        {
            h.Last(); h.View.HideAll();
            Assert.That(h.View.TryPlay(), Is.False); Assert.That(h.View.TryClose(), Is.False);
            Assert.That(h.Root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }
    }
    [Test]
    public void PopupStoreTicketReplacesGachaArtWhileOriginalXIsReusedExactly()
    {
        using (var h = new ViewHost())
        {
            var path = AssetDatabase.GetAssetPath(Field<Image>(h.View, "_ticketImage").sprite);
            Assert.That(path, Is.EqualTo(PopupStorePhasePreviewViewBuilder.TicketPath));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.EqualTo("077813c0407b1b54b892895bcdef2d63"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Field<Button>(h.View, "_closeButton").image.sprite)),
                Is.EqualTo("8e684365c2478c0448d931e218907c21"));
        }
    }
    [Test]
    public void TutorialPagesReferenceActualPopupScreenshotsAndNeverRecipeReferenceJpegs()
    {
        using (var h = new ViewHost())
        {
            var pages = new SerializedObject(h.View).FindProperty("_pages");
            for (int i = 0; i < pages.arraySize; i++)
            {
                var sprite = pages.GetArrayElementAtIndex(i).FindPropertyRelative("Screenshot").objectReferenceValue as Sprite;
                var path = AssetDatabase.GetAssetPath(sprite);
                Assert.That(path, Does.StartWith("Assets/Editor/PandaRestaurant/StaffDataValidation/UI/PopupStoreTutorial"));
                Assert.That(sprite.texture.width, Is.EqualTo(1920)); Assert.That(sprite.texture.height, Is.EqualTo(1080));
            }
        }
    }
    [Test]
    public void RealTutorialReplacesLongStartPanelAndBlocksDirectAdmissionUntilLastPage()
    {
        using (var h = new PopupStorePhasePreviewIntegrationTests.Host(false, false))
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), h.Root.transform, false);
            using var fonts = new StaffGachaOfflineFonts();
            root.SetActive(false); fonts.BindBeforeActivation(root); root.SetActive(true);
            var view = root.GetComponent<PopupStorePhasePreviewView>();
            var originalX = Field<Button>(h.Root.GetComponent<UIPopupStore>(), "_closeButton");
            Assert.That(h.Preview.AttachView(view, originalX), Is.True);
            Assert.That(h.Root.GetComponent<PopupStoreStartPanel>().IsVisible, Is.False);
            Assert.That(view.IsTutorialVisible, Is.True); Assert.That(originalX.gameObject.activeSelf, Is.False);
            Assert.That(h.Root.GetComponent<PopupStoreStartController>().RequestStart(), Is.False);
            Assert.That(h.Admission.Access.Remaining, Is.EqualTo(1)); Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            while (view.TryNext()) { }
            Assert.That(view.TryPlay(), Is.True); Assert.That(view.TryPlay(), Is.False);
            Assert.That(h.Admission.Access.Remaining, Is.Zero); Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Stage")); Assert.That(h.Preview.CanPlay, Is.False);
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero); Assert.That(originalX.gameObject.activeSelf, Is.True);
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(h.Preview, h.Clock);
            Assert.That(h.Preview.CanPlay, Is.True); h.AssertNoR1();
        }
    }

    [TestCase(0, "주문을 보고 포장해요", "손님이 원하는 조합을 확인해요")]
    [TestCase(1, "순서는 상관없어요", "순서와 상관없이 재료를 담으면 성공!")]
    [TestCase(2, "손님에게 전달해요", "포장하기로 손님에게 물건을 전달하세요!\n잘못된 포장은 판매대 비우기로 다시 시작해 보아요")]
    [TestCase(3, "스테이지를 통과해요", "스테이지를 통과해서 다양한 보상을 노려봅시다!")]
    [TestCase(4, "더 높은 단계로 갈수록 커져요", "높은 스테이지를 통과할수록\n더 좋은 상자와 더 많은 다이아를 얻을 수 있어요!")]
    public void RequestedFivePageCopyIsExact(int page, string title, string body)
    {
        using (var h = new ViewHost())
        {
            for (int i = 0; i < page; i++) Assert.That(h.View.TryNext(), Is.True);
            Assert.That(Field<TMP_Text>(h.View, "_titleText").text, Is.EqualTo(title));
            Assert.That(Field<TMP_Text>(h.View, "_bodyText").text, Is.EqualTo(body));
            Assert.That(Field<TMP_Text>(h.View, "_pageText").text, Is.EqualTo((page + 1) + " / 5"));
            Assert.That(h.Plays, Is.Zero); Assert.That(h.Closes, Is.Zero);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void EachPageShowsOnlyItsOwnReadOnlyIllustration(int page)
    {
        using (var h = new ViewHost())
        {
            for (int i = 0; i < page; i++) h.View.TryNext();
            var pages = new SerializedObject(h.View).FindProperty("_pages");
            for (int i = 0; i < pages.arraySize; i++)
            {
                var illustration = pages.GetArrayElementAtIndex(i).FindPropertyRelative("Illustration").objectReferenceValue as GameObject;
                if (illustration == null) { Assert.That(i, Is.Zero); continue; }
                Assert.That(illustration.activeSelf, Is.EqualTo(i == page));
                Assert.That(illustration.GetComponentsInChildren<Button>(true), Is.Empty);
                foreach (var graphic in illustration.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            }
            Assert.That(Field<RectTransform>(h.View, "_screenshotRegion").gameObject.activeSelf, Is.EqualTo(page == 0 || page == 2));
            Assert.That(Field<TMP_Text>(h.View, "_ticketStatusText").gameObject.activeSelf, Is.EqualTo(page == 4));
        }
    }

    [Test]
    public void UnorderedIllustrationUsesThreeDistinctCatalogMaterialsInTheActualFirstThreeTraySlots()
    {
        using (var h = new ViewHost())
        {
            h.View.TryNext();
            var art = Illustration(h.View, 1);
            var images = art.GetComponentsInChildren<Image>(true);
            var tray = images.Single(i => i.name == "Original Five Slot Tray");
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tray.sprite)), Is.EqualTo("6d3f562418854f9b0056bf9ab69d97b5"));
            var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>("Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset");
            string[] ids = { "BOX_PINK", "RIBBON_YELLOW", "STAFF01" };
            var kinds = new[] { PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon, PopupStoreMaterialKind.Plush };
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.That(catalog.TryGetMaterials(kinds[i], out var entries, out var error), Is.True, error);
                var image = images.Single(item => item.name == "Selected " + ids[i]);
                Assert.That(image.sprite, Is.SameAs(entries.Single(entry => entry.Id == ids[i]).Sprite));
                Assert.That(image.preserveAspect, Is.True);
                if (i > 0) Assert.That(image.rectTransform.anchoredPosition.x,
                    Is.GreaterThan(images.Single(item => item.name == "Selected " + ids[i - 1]).rectTransform.anchoredPosition.x));
            }
            Assert.That(images.Length, Is.EqualTo(4), "No completed gift should masquerade as selected counter materials.");
        }
    }

    [Test]
    public void DeliveryPageUsesActualShippingCaptureAndOriginalCustomerJoyWithoutGameplayCallbacks()
    {
        using (var h = new ViewHost())
        {
            h.View.TryNext(); h.View.TryNext();
            var page = new SerializedObject(h.View).FindProperty("_pages").GetArrayElementAtIndex(2);
            Assert.That(AssetDatabase.GetAssetPath(page.FindPropertyRelative("Screenshot").objectReferenceValue),
                Is.EqualTo(PopupStorePhasePreviewViewBuilder.GameplayPath));
            Assert.That(page.FindPropertyRelative("ShowScreenshot").boolValue, Is.True);
            var joy = Illustration(h.View, 2).GetComponentInChildren<Image>(true);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(joy.sprite)), Is.EqualTo("db89297b5d95d18449f4445cc8de9ee2"));
            Assert.That(joy.preserveAspect, Is.True);
            Assert.That(Illustration(h.View, 2).GetComponentsInChildren<MonoBehaviour>(true).All(c => c is Image), Is.True);
        }
    }

    [Test]
    public void FourthPageShowsApprovedSssAndOriginalDiamondsAsAnUnquantifiedTutorialExample()
    {
        using (var h = new ViewHost())
        {
            for (int i = 0; i < 3; i++) h.View.TryNext();
            var art = Illustration(h.View, 3);
            var images = art.GetComponentsInChildren<Image>(true);
            Assert.That(AssetDatabase.GetAssetPath(images.Single(i => i.name == "Approved SSS Box Example").sprite),
                Is.EqualTo("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_SSS_APPROVED.png"));
            var diamonds = images.Where(i => i.name.StartsWith("Example Diamond ", StringComparison.Ordinal)).ToArray();
            Assert.That(diamonds.Length, Is.GreaterThanOrEqualTo(6));
            foreach (var diamond in diamonds) Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(diamond.sprite)),
                Is.EqualTo("5f06dc035955150408e8560c9df72e81"));
            Assert.That(art.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text), Is.EqualTo(new[] { "튜토리얼 예시" }));
            Assert.That(h.Plays, Is.Zero);
        }
    }

    [Test]
    public void FinalPageUsesAllSixApprovedBoxesOnceInIncreasingSizeAndMinimalTicketFooter()
    {
        using (var h = new ViewHost())
        {
            h.Last();
            var art = Illustration(h.View, 4);
            string[] grades = { "C", "B", "A", "S", "SS", "SSS" };
            float previousSize = 0, previousX = float.NegativeInfinity;
            foreach (var grade in grades)
            {
                var image = art.GetComponentsInChildren<Image>(true).Single(i => i.name == "Approved " + grade + " Growth");
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png"));
                Assert.That(image.rectTransform.sizeDelta.x, Is.GreaterThan(previousSize));
                Assert.That(image.rectTransform.anchoredPosition.x, Is.GreaterThan(previousX));
                Assert.That(image.preserveAspect, Is.True);
                previousSize = image.rectTransform.sizeDelta.x; previousX = image.rectTransform.anchoredPosition.x;
            }
            Assert.That(Field<TMP_Text>(h.View, "_ticketStatusText").text, Is.EqualTo("이용권 1/1"));
            Assert.That(h.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Ready To Play"), Is.False);
            Assert.That(Field<Button>(h.View, "_playButton").interactable, Is.True);
            Assert.That(h.Plays, Is.Zero);
        }
    }

    [Test]
    public void AuthoredStageNoticeNowLastsOnePointFiveSecondsWithoutChangingOtherCueDurations()
    {
        using (var h = new ViewHost())
        {
            Assert.That(h.View.StageNoticeSeconds, Is.EqualTo(1.5f));
            Assert.That(h.View.GameStartSeconds, Is.EqualTo(.7f));
            Assert.That(h.View.GameOverSeconds, Is.EqualTo(1.1f));
        }
    }

    [Test]
    public void SharedIllustrationsFailValidationInsteadOfShowingContentFromAnotherPage()
    {
        using (var h = new ViewHost())
        {
            var serialized = new SerializedObject(h.View);
            var pages = serialized.FindProperty("_pages");
            pages.GetArrayElementAtIndex(3).FindPropertyRelative("Illustration").objectReferenceValue = Illustration(h.View, 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.ValidateConfiguration(out _), Is.False);
        }
    }

    [Test]
    public void GameplayButtonsCannotBeEmbeddedIntoTutorialIllustrations()
    {
        using (var h = new ViewHost())
        {
            Illustration(h.View, 1).AddComponent<Button>();
            Assert.That(h.View.ValidateConfiguration(out _), Is.False);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void EachPageMovesItsVisualAndBodyDownWithoutMovingTitleOrPageControls(int page)
    {
        using (var h = new ViewHost())
        {
            for (int i = 0; i < page; i++) h.View.TryNext();
            var definition = new SerializedObject(h.View).FindProperty("_pages").GetArrayElementAtIndex(page);
            var offset = definition.FindPropertyRelative("VisualOffset").vector2Value;
            Assert.That(offset.y, Is.LessThanOrEqualTo(-40), "The correction must be visually meaningful, including page five.");
            if (definition.FindPropertyRelative("ShowScreenshot").boolValue)
                Assert.That(Field<RectTransform>(h.View, "_screenshotRegion").anchoredPosition.y, Is.LessThan(-20));
            if (Illustration(h.View, page) != null)
                Assert.That(((RectTransform)Illustration(h.View, page).transform).anchoredPosition, Is.EqualTo(offset));
            Assert.That(Field<TMP_Text>(h.View, "_bodyText").rectTransform.anchoredPosition.y,
                Is.LessThan(page == 4 ? -102 : -225));
            Assert.That(Field<TMP_Text>(h.View, "_titleText").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 347)));
            Assert.That(Field<TMP_Text>(h.View, "_pageText").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 287)));
            Assert.That(Field<Button>(h.View, "_closeButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(711, 351)));
            Assert.That(Field<Button>(h.View, "_previousButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(-722, -25)));
            Assert.That(Field<Button>(h.View, "_nextButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(722, -25)));
            Assert.That(h.Plays, Is.Zero);
        }
    }

    [Test]
    public void FinalTicketAndPlayShareOneBalancedRowAndDoNotKeepTheOldDuplicatePrompt()
    {
        using (var h = new ViewHost())
        {
            h.Last();
            var ticket = Field<Image>(h.View, "_ticketImage").rectTransform;
            var label = Field<TMP_Text>(h.View, "_ticketStatusText").rectTransform;
            var play = Field<Button>(h.View, "_playButton").GetComponent<RectTransform>();
            Assert.That(label.anchoredPosition.y, Is.LessThan(-300), "The ticket count moves below the former horizontal row.");
            Assert.That(play.anchoredPosition.y, Is.GreaterThan(-300), "Play moves slightly above the former row.");
            Assert.That(label.anchoredPosition.x, Is.EqualTo(ticket.anchoredPosition.x), "The count belongs beneath its ticket icon.");
            Assert.That(label.anchoredPosition.y + label.sizeDelta.y / 2, Is.LessThan(ticket.anchoredPosition.y));
            float ticketGroupTop = ticket.anchoredPosition.y + ticket.sizeDelta.y / 2;
            float ticketGroupBottom = label.anchoredPosition.y - label.sizeDelta.y / 2;
            Assert.That(play.anchoredPosition.y, Is.EqualTo((ticketGroupTop + ticketGroupBottom) / 2).Within(15), "Align play with the combined ticket/count block.");
            float groupRight = Mathf.Max(ticket.anchoredPosition.x + ticket.sizeDelta.x / 2, label.anchoredPosition.x + label.sizeDelta.x / 2);
            float gap = play.anchoredPosition.x - play.sizeDelta.x / 2 - groupRight;
            Assert.That(gap, Is.GreaterThan(0).And.LessThan(80), "Keep a small clear gap so both parts read as one footer.");
            Assert.That(h.Root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("준비됐다면") || t.text.Contains("미니게임 시작권")), Is.False);
        }
    }

    [Test]
    public void InspectorPageOffsetAndFinalFooterLayoutAreAppliedWithoutRequestingAdmission()
    {
        using (var h = new ViewHost())
        {
            var serialized = new SerializedObject(h.View);
            serialized.FindProperty("_pages").GetArrayElementAtIndex(4).FindPropertyRelative("VisualOffset").vector2Value = new Vector2(12, -55);
            serialized.FindProperty("_ticketPosition").vector2Value = new Vector2(-350, -310);
            serialized.FindProperty("_ticketStatusPosition").vector2Value = new Vector2(-100, -310);
            serialized.FindProperty("_playPosition").vector2Value = new Vector2(290, -310);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.ValidateConfiguration(out var error), Is.True, error);
            h.Last();
            Assert.That(((RectTransform)Illustration(h.View, 4).transform).anchoredPosition, Is.EqualTo(new Vector2(12, -55)));
            Assert.That(Field<Image>(h.View, "_ticketImage").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(-350, -310)));
            Assert.That(Field<TMP_Text>(h.View, "_ticketStatusText").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(-100, -310)));
            Assert.That(Field<Button>(h.View, "_playButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(290, -310)));
            Assert.That(h.Plays, Is.Zero);
        }
    }

    [TestCase("_ticketPosition", false)] [TestCase("_ticketSize", true)]
    [TestCase("_ticketStatusPosition", false)] [TestCase("_ticketStatusSize", true)]
    [TestCase("_playPosition", false)] [TestCase("_playSize", true)]
    public void InvalidInspectorFooterLayoutIsRejected(string field, bool size)
    {
        using (var h = new ViewHost())
        {
            var serialized = new SerializedObject(h.View);
            serialized.FindProperty(field).vector2Value = size ? Vector2.zero : new Vector2(float.NaN, 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.ValidateConfiguration(out _), Is.False);
        }
    }

    [Test]
    public void NonFinitePageOffsetIsRejectedBeforeTheTutorialCanRenderIt()
    {
        using (var h = new ViewHost())
        {
            var serialized = new SerializedObject(h.View);
            serialized.FindProperty("_pages").GetArrayElementAtIndex(0).FindPropertyRelative("VisualOffset").vector2Value = new Vector2(0, float.PositiveInfinity);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.ValidateConfiguration(out _), Is.False);
        }
    }

    private static GameObject Illustration(PopupStorePhasePreviewView view, int page)
        => new SerializedObject(view).FindProperty("_pages").GetArrayElementAtIndex(page)
            .FindPropertyRelative("Illustration").objectReferenceValue as GameObject;
}
#endif
