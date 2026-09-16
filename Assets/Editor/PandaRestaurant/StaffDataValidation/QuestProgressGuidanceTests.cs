#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public partial class StaffStageMigrationCollectionTests
{
    [TestCase("MainReward01", "STAFF06")]
    [TestCase("MainReward04", "STAFF16")]
    [TestCase("MainReward05", "STAFF01")]
    [TestCase("MainReward19", "STAFF21")]
    public void QuestGuidance_SuccessTextRequiresCurrentConfirmedReceiptAndNeverStartsAnotherGrant(string questId, string staffId)
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture(questId, out var state, out _);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out string error), Is.True, error);
            Assert.That(operation.Before.RequiredStaffId, Is.EqualTo(staffId));
            bool Ready(string quest, GameDataRestoreQuery query) => (bool)typeof(QuestProgressGuidance)
                .GetMethod("IsConfirmedResultCurrent", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { operation, quest, query });
            Assert.That(Ready(questId, operation.Query), Is.False, "Accepted/sending is not acquired");
            int sends = fixture.Game.Writes;
            for (int i = 0; i < 3; i++) Assert.That(Ready(questId, operation.Query), Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(sends));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(Ready(questId, operation.Query), Is.True);
            foreach (string otherQuest in new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" }.Where(id => id != questId))
                Assert.That(Ready(otherQuest, operation.Query), Is.False, "Another current quest cannot borrow success text");
            Assert.That(Ready(questId, null), Is.False);
            Assert.That(Ready(questId, new GameDataRestoreContext(() => "other-guide-owner").BeginQuery()), Is.False);
            sends = fixture.Game.Writes;
            for (int i = 0; i < 3; i++) Assert.That(Ready(questId, operation.Query), Is.True);
            Assert.That(fixture.Game.Writes, Is.EqualTo(sends), "Presentation polling must not send/re-grant");
            Assert.That(operation.CompletionCount, Is.EqualTo(1));
            Assert.That(state.Notifications, Is.EqualTo(1));
            Assert.That(state.Claimed, Is.False);
            fixture.Game.WriteReplies[1](Bro("204", ""));
            fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            Assert.That(fixture.Manager.CanPresentQuestStaffGrant(operation), Is.False,
                "A new restore generation invalidates even a previously confirmed operation");
        }
    }

    [TestCase("MainReward01")]
    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void QuestGuidance_IndeterminateGrantHasNoSuccessTextAndDoesNotReleaseProtection(string questId)
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture(questId, out var state, out _);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out _), Is.True);
            fixture.Game.WriteReplies[0](Bro("500", ""));
            Assert.That(typeof(QuestProgressGuidance).GetMethod("IsConfirmedResultCurrent", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { operation, questId, operation.Query }), Is.EqualTo(false));
            Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(state.Mask, Is.Zero);
            Assert.That(state.Notifications, Is.Zero);
        }
    }
}

public sealed class QuestProgressGuidanceViewTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void QuestGuidance_StaffAndTablePagesKeepTheirExactSeparateAcknowledgements()
    {
        var draw = Static<Dictionary<string, string[]>>("StaffDrawPages");
        var result = Static<Dictionary<string, string[]>>("StaffResultPages");
        CollectionAssert.AreEquivalent(new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" }, draw.Keys);
        CollectionAssert.AreEquivalent(draw.Keys, result.Keys);
        CollectionAssert.AreEqual(new[]
        {
            "와, 매니저를 뽑으셨군요!",
            "상점에서 직원을 배치해봅시다.",
            "닫기를 눌러 상점으로 돌아가볼까요?"
        }, result["MainReward01"]);
        CollectionAssert.AreEqual(new[]
        {
            "역시 주방장이 없으니 조금 답답하죠?",
            "이번에는 주방장을 한번 뽑아볼까요?"
        }, draw["MainReward04"]);
        CollectionAssert.AreEqual(new[]
        {
            "와, 주방장 도치가 함께하게 되었어요!",
            "주방장은 조리 효율을 높여주고\n자동으로 설거지를 진행해준답니다.",
            "도치를 배치해볼까요?"
        }, result["MainReward04"]);
        CollectionAssert.AreEqual(new[]
        {
            "근데 웨이터가 없다면\n식당이 원활하게 돌아가지 않겠죠?",
            "이번에는 웨이터를 뽑아봅시다!"
        }, draw["MainReward05"]);
        CollectionAssert.AreEqual(new[]
        {
            "장난꾸러기 지지가 웨이터로 합류했어요!",
            "웨이터는 음식 주문과 배달을\n자동으로 진행해줘요.",
            "빠질 수 없는 직원이겠죠?"
        }, result["MainReward05"]);
        CollectionAssert.AreEqual(new[]
        {
            "식당이 더러우면 아무도 찾아오지 않을 거예요.",
            "청소부를 고용해볼까요?"
        }, draw["MainReward19"]);
        CollectionAssert.AreEqual(new[]
        {
            "청소부 판다 멜로를 획득하셨군요!",
            "청소부는 자동으로 테이블과 쓰레기를 치워줘요.",
            "손님이 지불한 코인도 대신 획득해준답니다."
        }, result["MainReward19"]);
        CollectionAssert.AreEqual(new[]
        {
            "제한 평점을 달성해서\n테이블을 구매할 수 있게 되었어요!",
            "새로운 테이블을 구매해봅시다!"
        }, Static<string[]>("TableReadyPages"));
        foreach (string quest in draw.Keys)
        {
            Assert.That(result[quest], Is.Not.SameAs(draw[quest]));
            Assert.That(draw[quest].Intersect(result[quest]), Is.Empty,
                "The pre-draw invitation must not reuse acquisition-success wording: " + quest);
        }
    }

    [Test]
    public void QuestGuidance_InformationalRecipeAndWallpaperCopyUsesExplicitReadablePages()
    {
        CollectionAssert.AreEqual(new[]
        {
            "매니저는 자동으로 손님을 배치해 줍니다.\n손쉽게 레스토랑을 운영해봅시다!"
        }, Static<string[]>("ManagerEquippedPages"));
        CollectionAssert.AreEqual(new[]
        {
            "레시피를 배워봅시다!",
            "다양한 레시피를 배우면\n다양한 손님이 등장한답니다."
        }, Static<string[]>("RecipeLearnPages"));
        CollectionAssert.AreEqual(new[] { "다양한 레시피를 모아봅시다!" }, Static<string[]>("RecipeLearnedPages"));
        CollectionAssert.AreEqual(new[]
        {
            "평점이 모자라면 다양한 상품들을 구매할 수 없어요.",
            "선물로 받은 벽지를 이용해서 평점을 올려볼까요?"
        }, Static<string[]>("WallpaperExplanationPages"));
        CollectionAssert.AreEqual(new[] { "평범한 벽지를 받아볼까요?" }, Static<string[]>("WallpaperFreePages"));
    }

    [TestCase("staff-draw:MainReward01")]
    [TestCase("staff-draw:MainReward04")]
    [TestCase("staff-draw:MainReward05")]
    [TestCase("staff-draw:MainReward19")]
    [TestCase("manager-result")]
    [TestCase("staff-result:MainReward04")]
    [TestCase("staff-result:MainReward05")]
    [TestCase("staff-result:MainReward19")]
    [TestCase("manager-equip")]
    [TestCase("recipe-learn")]
    [TestCase("wallpaper-free")]
    [TestCase("wallpaper-equip")]
    [TestCase("wallpaper-table-ready")]
    public void QuestGuidance_ReadThenDismissHidesNpcAndPointsToNativeButtonWithoutCallingIt(string hint)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            bool tutorialBefore = UserInfo.IsTutorialStart;
            int callbacks = 0;
            button.onClick.AddListener(() => callbacks++);
            var eventObject = new GameObject("guidance native event data");
            try
            {
                var eventSystem = eventObject.AddComponent<EventSystem>();
                var input = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
                var target = (RectTransform)button.transform;
                string[] pages = ActionPages(hint);
                var caller = StartManualActionHint(host, npc, hint, target, pages);
                Assert.That(nav.Count, Is.EqualTo(2));
                foreach (string page in pages)
                {
                    Assert.That(caller.MoveNext(), Is.True, "Each page is a separate native dialogue yield");
                    Assert.That(caller.Current, Is.InstanceOf<Coroutine>());
                    Assert.That(Get<UIImageAndText>(npc, "_descriptionText2").gameObject.activeSelf, Is.True);
                    Assert.That(guide.GetCustomHoleActive(), Is.False,
                        "Do not ask the player to guess a button covered by any unfinished dialogue page");
                    Assert.That(callbacks, Is.Zero);
                    DismissNativeDialogue(npc, input, page, () =>
                    {
                        int lifetime = Get<int>(host, "_hintLifetime");
                        Invoke(host, "ShowDialogueHint", hint, target, pages);
                        Assert.That(Get<int>(host, "_hintLifetime"), Is.EqualTo(lifetime),
                            "Polling the same action must not restart a multi-page explanation");
                        Assert.That(guide.GetCustomHoleActive(), Is.False);
                        Assert.That(callbacks, Is.Zero);
                    });
                    Assert.That(guide.GetCustomHoleActive(), Is.False,
                        "The caller must complete all page yields before handing off to the real button");
                }
                Assert.That(caller.MoveNext(), Is.False,
                    "The actual parent iterator, not a directly invoked completion helper, finishes the handoff");
                AssertNoDescription(npc);
                Assert.That(guide.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(npc.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(guide.GetComponent<CanvasGroup>().IsRaycastLocationValid(Vector2.zero, null), Is.False);
                Assert.That(npc.GetComponent<CanvasGroup>().IsRaycastLocationValid(Vector2.zero, null), Is.False);
                Assert.That(button.interactable, Is.True);
                Assert.That(callbacks, Is.Zero, "Showing a pointer must not call its target");
                Assert.That(guide.GetCustomHoleActive(), Is.True);
                Assert.That(Get<HoleClickHandler>(guide, "_customHole").HoleRect.position,
                    Is.EqualTo(button.transform.position), "The focus uses the actual product control position");
                Invoke(guide, "OnCustomHoleAnimeCompleted");
                var waitRoot = (GameObject)typeof(Muks.Tween.Tween)
                    .GetField("_waitQueueParent", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Get<Action>(waitRoot.GetComponentsInChildren<Muks.Tween.TweenWait>(true).Last(), "_onCompleted")();
                Assert.That(Get<RectTransform>(guide, "_customHoleCursorParent").gameObject.activeSelf, Is.True);
                Assert.That(Get<GameObject>(guide, "_customHoleCursorDown").activeSelf, Is.True,
                    "The existing paw animation is visible after the dialogue, over the real control");
                button.OnPointerClick(input);
                Assert.That(callbacks, Is.EqualTo(1), "The original native UI button, not a guide action, receives input");
                Assert.That(UserInfo.IsTutorialStart, Is.EqualTo(tutorialBefore), "Passive hints must not block autosave or gacha entry");
                Invoke(host, "CancelPresentation");
                Assert.That(nav.Count, Is.Zero);
                Assert.That(guide.gameObject.activeSelf, Is.False);
                Assert.That(npc.gameObject.activeSelf, Is.False);
                Assert.That(guide.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(npc.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(UserInfo.IsTutorialStart, Is.EqualTo(tutorialBefore));
            }
            finally { Object.DestroyImmediate(eventObject); }
        });
    }

    [Test]
    public void QuestGuidance_OldDismissalCannotHideReplacementDialogueOrReviveClosedOverlay()
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            var target = (RectTransform)button.transform;
            Invoke(host, "ShowHint", "wallpaper-free", target, "무료 구매 안내");
            int oldLifetime = Get<int>(host, "_hintLifetime");
            Invoke(host, "ShowHint", "wallpaper-equip", target, "배치 안내");
            int equipLifetime = Get<int>(host, "_hintLifetime");
            Invoke(host, "CompleteActionHint", oldLifetime, "wallpaper-free", target);
            Assert.That(Get<UIImageAndText>(npc, "_descriptionText2").gameObject.activeSelf, Is.True,
                "A superseded draw/buy explanation cannot dismiss the next placement explanation");
            Assert.That(guide.GetCustomHoleActive(), Is.False);
            Invoke(host, "CancelPresentation");
            Invoke(host, "CompleteActionHint", equipLifetime, "wallpaper-equip", target);
            Assert.That(nav.Count, Is.Zero);
            Assert.That(npc.gameObject.activeSelf, Is.False);
            Assert.That(guide.gameObject.activeSelf, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void QuestGuidance_CancelledMultiPageCannotStartItsNextPageOrCompleteOverANewHint(bool cancelOnLastPage)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            int callbacks = 0;
            button.onClick.AddListener(() => callbacks++);
            var eventObject = new GameObject("cancelled multi-page event data");
            try
            {
                var input = new PointerEventData(eventObject.AddComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left };
                var target = (RectTransform)button.transform;
                string[] pages = ActionPages("staff-result:MainReward04");
                var previous = StartManualActionHint(host, npc, "staff-result:MainReward04", target, pages);
                int shown = cancelOnLastPage ? pages.Length : 1;
                for (int page = 0; page < shown; ++page)
                {
                    Assert.That(previous.MoveNext(), Is.True);
                    DismissNativeDialogue(npc, input, pages[page]);
                }
                Action oldReveal = LastDialogueDelay();
                Invoke(host, "CancelPresentation");
                Assert.That(nav.Count, Is.Zero);
                AssertNoDescription(npc);
                if (cancelOnLastPage)
                {
                    Invoke(host, "ShowDialogueHint", "staff-draw:MainReward05", target, ActionPages("staff-draw:MainReward05"));
                    npc.ShowGuidanceText("교체된 안내");
                }
                oldReveal();
                Assert.That(previous.MoveNext(), Is.False,
                    "A stale parent iterator cannot start another page or complete a different hint");
                Assert.That(callbacks, Is.Zero);
                Assert.That(guide.GetCustomHoleActive(), Is.False);
                if (cancelOnLastPage)
                {
                    Assert.That(nav.Count, Is.EqualTo(2));
                    Assert.That(Get<UIImageAndText>(npc, "_descriptionText2").gameObject.activeSelf, Is.True);
                    Assert.That(Get<UIImageAndText>(npc, "_descriptionText2").Text.text, Is.EqualTo("교체된 안내"));
                    Assert.That(Get<Button>(npc, "_screenButton").gameObject.activeSelf, Is.False);
                }
                else
                {
                    Assert.That(nav.Count, Is.Zero);
                    AssertNoDescription(npc);
                    Assert.That(npc.gameObject.activeSelf, Is.False);
                }
            }
            finally { Object.DestroyImmediate(eventObject); }
        });
    }

    [TestCase("MainReward04")]
    [TestCase("MainReward05")]
    [TestCase("MainReward19")]
    public void QuestGuidance_EmptyStaffPlacementPagesShowOnlyNativeButtonPointer(string quest)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            int callbacks = 0;
            button.onClick.AddListener(() => callbacks++);
            var target = (RectTransform)button.transform;
            Invoke(host, "ShowHint", "previous-result", target, "이미 읽은 획득 안내");
            Invoke(host, "ShowDialogueHint", "staff-equip:" + quest, target, Array.Empty<string>());
            Assert.That(nav.Count, Is.EqualTo(2));
            AssertNoDescription(npc);
            Assert.That(guide.GetCustomHoleActive(), Is.True);
            Assert.That(Get<HoleClickHandler>(guide, "_customHole").HoleRect.position, Is.EqualTo(target.position));
            Assert.That(guide.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(npc.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(Get<Coroutine>(host, "_hintDialogue"), Is.Null);
            Assert.That(button.interactable, Is.True);
            Assert.That(callbacks, Is.Zero, "Placement is offered but never performed by the guide");
        });
    }

    [TestCase("manager-equipped", "ManagerEquippedPages")]
    [TestCase("recipe-learned", "RecipeLearnedPages")]
    public void QuestGuidance_InformationClosesOnlyAfterNativeAcknowledgementAndNeverClicksProduct(string hint, string field)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            int callbacks = 0;
            button.onClick.AddListener(() => callbacks++);
            var eventObject = new GameObject("information acknowledgement event data");
            try
            {
                var input = new PointerEventData(eventObject.AddComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left };
                string[] pages = Static<string[]>(field);
                var caller = StartManualInformationHint(host, npc, hint, pages);
                foreach (string page in pages)
                {
                    Assert.That(caller.MoveNext(), Is.True);
                    Assert.That(caller.Current, Is.InstanceOf<Coroutine>());
                    DismissNativeDialogue(npc, input, page, () =>
                    {
                        int lifetime = Get<int>(host, "_hintLifetime");
                        Assert.That(Invoke(host, "ShowInformationHint", hint, pages), Is.EqualTo(true));
                        Assert.That(Get<int>(host, "_hintLifetime"), Is.EqualTo(lifetime),
                            "Polling must not restart or time out the message the user is reading");
                        Assert.That(nav.Count, Is.EqualTo(2));
                        Assert.That(Get<UIImageAndText>(npc, "_descriptionText2").gameObject.activeSelf, Is.True);
                        var group = npc.GetComponent<CanvasGroup>();
                        Assert.That(group == null || group.blocksRaycasts, Is.True,
                            "A fresh NPC needs no CanvasGroup; any existing passthrough filter must allow dialogue input");
                        Assert.That(Get<Button>(npc, "_screenButton").IsInteractable(), Is.True);
                        Assert.That(guide.GetCustomHoleActive(), Is.False);
                        Assert.That(callbacks, Is.Zero);
                    });
                    Assert.That(nav.Count, Is.EqualTo(2), "The caller resumes only after the user's acknowledgement");
                }
                Assert.That(caller.MoveNext(), Is.False);
                Assert.That(nav.Count, Is.Zero, "The last acknowledgement closes both owned overlays");
                Assert.That(Get<bool>(host, "_ownsOverlay"), Is.False);
                AssertNoDescription(npc);
                Assert.That(npc.gameObject.activeSelf, Is.False);
                Assert.That(guide.gameObject.activeSelf, Is.False);
                Assert.That(callbacks, Is.Zero);
            }
            finally { Object.DestroyImmediate(eventObject); }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void QuestGuidance_CancelledInformationCannotResumeNextPageOrCloseAReplacement(bool replaceAfterLastPage)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            var eventObject = new GameObject("cancelled information event data");
            try
            {
                var input = new PointerEventData(eventObject.AddComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left };
                string[] pages = { "첫 번째 안내", "두 번째 안내" };
                var previous = StartManualInformationHint(host, npc, "cancelled-information", pages);
                int shown = replaceAfterLastPage ? pages.Length : 1;
                for (int page = 0; page < shown; ++page)
                {
                    Assert.That(previous.MoveNext(), Is.True);
                    DismissNativeDialogue(npc, input, pages[page]);
                }
                Invoke(host, "CancelPresentation");
                Assert.That(nav.Count, Is.Zero);
                if (replaceAfterLastPage)
                {
                    Invoke(host, "ShowDialogueHint", "new-action", (RectTransform)button.transform, Array.Empty<string>());
                    Assert.That(guide.GetCustomHoleActive(), Is.True);
                }
                Assert.That(previous.MoveNext(), Is.False,
                    "A cancelled information iterator cannot reveal another page or release someone else's overlays");
                AssertNoDescription(npc);
                Assert.That(nav.Count, Is.EqualTo(replaceAfterLastPage ? 2 : 0));
                Assert.That(guide.GetCustomHoleActive(), Is.EqualTo(replaceAfterLastPage));
                Assert.That(Get<bool>(host, "_ownsOverlay"), Is.EqualTo(replaceAfterLastPage));
            }
            finally { Object.DestroyImmediate(eventObject); }
        });
    }

    [Test]
    public void QuestGuidance_FastTypingClickDoesNotAlsoAcknowledgeThePageOrActivateItsTarget()
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            int callbacks = 0;
            button.onClick.AddListener(() => callbacks++);
            var eventObject = new GameObject("fast native dialogue event data");
            try
            {
                var input = new PointerEventData(eventObject.AddComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left };
                string[] pages = Static<string[]>("RecipeLearnPages");
                var caller = StartManualActionHint(host, npc, "recipe-learn", (RectTransform)button.transform, pages);
                var card = Get<UIImageAndText>(npc, "_descriptionText2");
                var screen = Get<Button>(npc, "_screenButton");
                foreach (string page in pages)
                {
                    Assert.That(caller.MoveNext(), Is.True);
                    npc.StopAllCoroutines();
                    var text = (IEnumerator)Invoke(npc, "ShowDescriptionTextRoutine", card, page, 0.05f);
                    Assert.That(text.MoveNext(), Is.True);
                    Assert.That(card.Text.text, Is.Not.EqualTo(page));
                    LastDialogueDelay()(); // The existing initial input delay, not a dialogue timeout.
                    Assert.That(screen.gameObject.activeSelf, Is.True);
                    screen.OnPointerClick(input);
                    Assert.That(text.MoveNext(), Is.True);
                    Assert.That(card.Text.text, Is.EqualTo(page), "The first click only finishes the typewriter");
                    Assert.That(card.gameObject.activeSelf, Is.True);
                    Assert.That(screen.gameObject.activeSelf, Is.False);
                    screen.OnPointerClick(input); // A tap while hidden cannot leak into the next acknowledgement.
                    Assert.That(text.MoveNext(), Is.True);
                    Assert.That(screen.gameObject.activeSelf, Is.True);
                    Assert.That(text.MoveNext(), Is.True, "A separate visible acknowledgement is still required");
                    Assert.That(card.gameObject.activeSelf, Is.True);
                    Assert.That(guide.GetCustomHoleActive(), Is.False);
                    Assert.That(callbacks, Is.Zero);
                    screen.OnPointerClick(input);
                    Assert.That(text.MoveNext(), Is.False);
                    Assert.That(guide.GetCustomHoleActive(), Is.False);
                }
                Assert.That(caller.MoveNext(), Is.False);
                AssertNoDescription(npc);
                Assert.That(guide.GetCustomHoleActive(), Is.True);
                Assert.That(callbacks, Is.Zero);
            }
            finally { Object.DestroyImmediate(eventObject); }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void QuestGuidance_HiddenNpcRejectsDelayedTextAndScreenReveal(bool waitingForAcknowledgement)
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            nav.Push("UITutorialDescription");
            var card = Get<UIImageAndText>(npc, "_descriptionText2");
            var routine = (IEnumerator)Invoke(npc, "ShowDescriptionTextRoutine", card, "안내", 0.05f);
            Assert.That(routine.MoveNext(), Is.True);
            Action delayedScreenReveal = LastDialogueDelay();
            if (waitingForAcknowledgement)
            {
                var screen = Get<Button>(npc, "_screenButton");
                var cursors = Get<GameObject[]>(npc, "_cursorObjs");
                int steps = 0;
                while ((!screen.gameObject.activeSelf || !cursors.Any(item => item.activeSelf)) && steps++ < 30)
                    Assert.That(routine.MoveNext(), Is.True);
                Assert.That(steps, Is.LessThan(30));
            }

            npc.Hide();
            delayedScreenReveal();
            Assert.That(routine.MoveNext(), Is.False,
                "A text routine captured before Skip must terminate when resumed after cleanup");
            AssertNoDescription(npc);
            Assert.That(npc.gameObject.activeSelf, Is.False);
            Assert.That(npc.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            nav.PopNoAnime("UITutorialDescription");
        });
    }

    [Test]
    public void QuestGuidance_ReopenedNpcKeepsNewMessageWhenPreviousDialogueResumesLate()
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            nav.Push("UITutorialDescription");
            var card = Get<UIImageAndText>(npc, "_descriptionText2");
            var previous = (IEnumerator)Invoke(npc, "ShowDescriptionTextRoutine", card, "이전 안내", 0.05f);
            Assert.That(previous.MoveNext(), Is.True);
            Action delayedScreenReveal = LastDialogueDelay();

            npc.Hide();
            npc.Show();
            npc.ShowGuidanceText("새 안내");
            delayedScreenReveal();
            Assert.That(previous.MoveNext(), Is.False,
                "ActiveInHierarchy alone is not enough: a reused NPC must reject the old lifetime");
            Assert.That(card.gameObject.activeSelf, Is.True);
            Assert.That(card.Text.text, Is.EqualTo("새 안내"));
            Assert.That(Get<Button>(npc, "_screenButton").gameObject.activeSelf, Is.False);
            Assert.That(Get<GameObject[]>(npc, "_cursorObjs").Any(item => item.activeSelf), Is.False);
            Assert.That(npc.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            npc.Hide();
            nav.PopNoAnime("UITutorialDescription");
        });
    }

    [Test]
    public void QuestGuidance_DoesNotTakeExistingTutorialOrPopupAndRepeatedCloseRestoresOnlyItsOverlay()
    {
        WithProductViews((guide, npc, nav, host, button) =>
        {
            nav.Push("UITutorialDescription");
            Invoke(host, "ShowHint", "staff", (RectTransform)button.transform, "not allowed");
            Assert.That(nav.Count, Is.EqualTo(1), "A pre-existing tutorial/dialogue owns the navigation");
            Assert.That(Get<bool>(host, "_ownsOverlay"), Is.False);
            Invoke(host, "CancelPresentation");
            Assert.That(nav.Count, Is.EqualTo(1));
            nav.Pop("UITutorialDescription");
            UserInfo.IsTutorialStart = true;
            Invoke(host, "ShowHint", "staff", (RectTransform)button.transform, "not allowed");
            Assert.That(nav.Count, Is.Zero);
            Assert.That(UserInfo.IsTutorialStart, Is.True, "The guide does not clear someone else's tutorial flag");
            UserInfo.IsTutorialStart = false;
            Invoke(host, "ShowHint", "staff", (RectTransform)button.transform, "allowed");
            Invoke(host, "ShowHint", "staff", (RectTransform)button.transform, "allowed");
            Assert.That(nav.Count, Is.EqualTo(2));
            nav.AllPop(); // Existing navigation can close independently of the observer.
            Assert.DoesNotThrow(() => Invoke(host, "CancelPresentation"));
            Assert.DoesNotThrow(() => Invoke(host, "CancelPresentation"));
            Assert.That(nav.Count, Is.Zero);
        });
    }

    private static void WithProductViews(Action<UITutorial, UITutorialDescriptionNPC, MobileUINavigation, QuestProgressGuidance, Button> test)
    {
        bool active = UserInfo.IsTutorialStart;
        long money = UserInfo.Money;
        int dia = UserInfo.Dia, count = UserInfo.TotalUseGachaMachineCount;
        var backendField = typeof(BackendManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object backend = backendField.GetValue(null);
        var waitParentField = typeof(Muks.Tween.Tween).GetField("_waitQueueParent", BindingFlags.Static | BindingFlags.NonPublic);
        var waitQueueField = typeof(Muks.Tween.Tween).GetField("_tweenWaitQueue", BindingFlags.Static | BindingFlags.NonPublic);
        object waitParentBefore = waitParentField.GetValue(null), waitQueueBefore = waitQueueField.GetValue(null);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        var fixture = new GameObject("isolated guidance owner");
        fixture.SetActive(false);
        var waitRoot = new GameObject("isolated guidance dialogue wait queue");
        try
        {
            waitParentField.SetValue(null, waitRoot);
            waitQueueField.SetValue(null, new Queue<Muks.Tween.TweenWait>());
            UserInfo.IsTutorialStart = false;
            var all = scene.GetRootGameObjects();
            var guide = all.SelectMany(root => root.GetComponentsInChildren<UITutorial>(true)).Single();
            var npc = all.SelectMany(root => root.GetComponentsInChildren<UITutorialDescriptionNPC>(true)).Single();
            var preview = all.SelectMany(root => root.GetComponentsInChildren<UIStaffPreview>(true)).Single();
            var buttonGroup = Get<UIButtonAndText>(preview, "_buyButton");
            var button = Get<Button>(buttonGroup, "_button");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
            guide.transform.SetParent(null, false);
            npc.transform.SetParent(null, false);
            button.transform.SetParent(null, false);
            guide.gameObject.SetActive(false);
            npc.gameObject.SetActive(false);
            npc.Init();
            button.gameObject.SetActive(true);
            guide.PopEnabled = npc.PopEnabled = true;
            var nav = fixture.AddComponent<MobileUINavigation>();
            Set(nav, "_viewDic", new Dictionary<string, MobileUIView> { ["UITutorial"] = guide, ["UITutorialDescription"] = npc });
            var host = fixture.AddComponent<QuestProgressGuidance>();
            Set(host, "_guide", guide);
            Set(host, "_description", npc);
            Set(host, "_tutorialNav", nav);
            fixture.SetActive(true);
            test(guide, npc, nav, host, button);
            Invoke(host, "CancelPresentation");
        }
        finally
        {
            Object.DestroyImmediate(fixture);
            EditorSceneManager.ClosePreviewScene(scene);
            waitParentField.SetValue(null, waitParentBefore);
            waitQueueField.SetValue(null, waitQueueBefore);
            Object.DestroyImmediate(waitRoot);
            UserInfo.IsTutorialStart = active;
            Assert.That(UserInfo.Money, Is.EqualTo(money));
            Assert.That(UserInfo.Dia, Is.EqualTo(dia));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(count));
            Assert.That(backendField.GetValue(null), Is.SameAs(backend));
        }
    }

    // EditMode has no player coroutine clock. Preserve the real ShowDialogueHint
    // state, stop only the unscheduled player handles, and explicitly schedule
    // the real caller IEnumerator. Each yielded NPC coroutine is completed using
    // its actual text IEnumerator and screen input before advancing the caller.
    private static IEnumerator StartManualActionHint(QuestProgressGuidance host, UITutorialDescriptionNPC npc,
        string key, RectTransform target, string[] pages)
    {
        Invoke(host, "ShowDialogueHint", key, target, pages);
        host.StopAllCoroutines();
        npc.StopAllCoroutines();
        return (IEnumerator)Invoke(host, "ExplainActionHint", Get<int>(host, "_hintLifetime"), key, target, pages);
    }

    private static IEnumerator StartManualInformationHint(QuestProgressGuidance host, UITutorialDescriptionNPC npc,
        string key, string[] pages)
    {
        Assert.That(Invoke(host, "ShowInformationHint", key, pages), Is.EqualTo(true));
        host.StopAllCoroutines();
        npc.StopAllCoroutines();
        return (IEnumerator)Invoke(host, "ExplainInformationHint", Get<int>(host, "_hintLifetime"), key, pages);
    }

    private static void DismissNativeDialogue(UITutorialDescriptionNPC npc, PointerEventData input, string page,
        Action beforeAcknowledgement = null)
    {
        npc.StopAllCoroutines();
        var card = Get<UIImageAndText>(npc, "_descriptionText2");
        var screen = Get<Button>(npc, "_screenButton");
        var routine = (IEnumerator)Invoke(npc, "ShowDescriptionTextRoutine", card, page, 0.05f);
        var cursors = Get<GameObject[]>(npc, "_cursorObjs");
        int steps = 0;
        Assert.That(routine.MoveNext(), Is.True);
        while ((!screen.gameObject.activeSelf || !cursors.Any(item => item.activeSelf)) && steps++ < page.Length + 10)
            Assert.That(routine.MoveNext(), Is.True, "Dialogue waits for the player's own acknowledgement");
        Assert.That(steps, Is.LessThan(page.Length + 10));
        Assert.That(card.Text.text, Is.EqualTo(page), "Only the current page is displayed, not all pages joined together");
        Assert.That(routine.MoveNext(), Is.True, "The complete page still waits for a real acknowledgement click");
        Assert.That(card.gameObject.activeSelf, Is.True);
        beforeAcknowledgement?.Invoke();
        screen.OnPointerClick(input);
        Assert.That(routine.MoveNext(), Is.False);
    }

    private static string[] ActionPages(string hint)
    {
        if (hint.StartsWith("staff-draw:", StringComparison.Ordinal))
            return Static<Dictionary<string, string[]>>("StaffDrawPages")[hint.Substring("staff-draw:".Length)];
        if (hint.StartsWith("staff-result:", StringComparison.Ordinal))
            return Static<Dictionary<string, string[]>>("StaffResultPages")[hint.Substring("staff-result:".Length)];
        if (hint == "manager-result") return Static<Dictionary<string, string[]>>("StaffResultPages")["MainReward01"];
        if (hint == "wallpaper-table-ready") return Static<string[]>("TableReadyPages");
        if (hint == "recipe-learn") return Static<string[]>("RecipeLearnPages");
        if (hint == "wallpaper-free") return Static<string[]>("WallpaperFreePages");
        return new[] { "실제 버튼을 눌러주세요." };
    }

    private static T Static<T>(string name) => (T)typeof(QuestProgressGuidance)
        .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    private static void AssertNoDescription(UITutorialDescriptionNPC npc)
    {
        foreach (string field in new[] { "_descriptionText1", "_descriptionText2", "_descriptionText3", "_descriptionText4" })
            Assert.That(Get<UIImageAndText>(npc, field).gameObject.activeSelf, Is.False, field);
        Assert.That(Get<Button>(npc, "_screenButton").gameObject.activeSelf, Is.False);
        Assert.That(Get<GameObject[]>(npc, "_cursorObjs").Any(item => item.activeSelf), Is.False);
    }

    private static Action LastDialogueDelay()
    {
        var waitRoot = (GameObject)typeof(Muks.Tween.Tween)
            .GetField("_waitQueueParent", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        return Get<Action>(waitRoot.GetComponentsInChildren<Muks.Tween.TweenWait>(true).Last(), "_onCompleted");
    }

    private static T Get<T>(object value, string name) => (T)Field(value.GetType(), name).GetValue(value);
    private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, Fields | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }
    private static object Invoke(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Fields).Invoke(target, args);
}
#endif
