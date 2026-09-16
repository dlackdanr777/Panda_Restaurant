#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.DataBind;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KimchiRecipeGuidanceTests
{
    [Test]
    public void KimchiGuidance_UsesFood04NativeMiniGameButtonAndCentersTheActualGridRow()
    {
        using (var scope = new Fixture())
        {
            var foodRow = Resources.Load<TextAsset>("FoodData/FoodDataList").text.Split('\n')
                .Select(Utility.SplitCsvLine).Single(row => row[0] == "FOOD04");
            var questRow = Resources.Load<TextAsset>("Challenge/REWARD_MAIN").text.Split('\n')
                .Select(Utility.SplitCsvLine).Single(row => row[0] == "MainReward13");
            Assert.That(foodRow[7], Is.EqualTo("GOTCHA91"));
            Assert.That(questRow[4], Is.EqualTo("FOOD04"));
            Assert.That(scope.Recipe.FocusRecipe("FOOD04"), Is.True);
            Assert.That(scope.Recipe.SelectedData.Id, Is.EqualTo("FOOD04"));
            Assert.That(scope.Recipe.MiniGameButtonRect.gameObject.activeSelf, Is.True);
            Assert.That(scope.Recipe.BuyButtonRect.gameObject.activeSelf, Is.False,
                "Kimchi is made through the native mini-game, not the ordinary gold purchase button");

            Canvas.ForceUpdateCanvases();
            var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scope.Viewport, scope.TargetSlot);
            Assert.That(targetBounds.center.y, Is.EqualTo(scope.Viewport.rect.center.y).Within(1f),
                "The recipe grid must use the slot's laid-out row instead of index / item count");
            Assert.That(scope.Recipe.FocusRecipe("NOT_A_RECIPE"), Is.False);
            Assert.That(scope.Recipe.SelectedData.Id, Is.EqualTo("FOOD04"));
            scope.AssertUnchanged();
        }
    }

    [Test]
    public void KimchiGuidance_HighlightDoesNotStartMiniGameAndLeavesWhenNativeNavigationChanges()
    {
        using (var scope = new Fixture())
        {
            var routine = (IEnumerator)typeof(GachaTutorial).GetMethod("GuideKimchiRecipeInput",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope.Tutorial, null);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(scope.Guide.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            var hole = (HoleClickHandler)Get(scope.Guide, "_customHole");
            Assert.That(Vector3.Distance(hole.HoleRect.position, scope.Recipe.MiniGameButtonRect.position), Is.LessThan(0.1f));
            var wrapper = (UIButtonImageText)Get(scope.Preview, "_minigameButton");
            var button = (Button)Get(wrapper, "_button");
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            Assert.That(clicks, Is.Zero, "Preparing guidance cannot click, grant or start anything");
            Assert.That(scope.Time.GetTime("FOOD04_MiniGame"), Is.Zero);
            scope.AssertUnchanged();

            // Model the resulting native view transition without starting a
            // mini-game or writing any recipe/timer/account state in this test.
            scope.ActiveViews.Clear();
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(scope.Guide.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(((GameObject)Get(scope.Guide, "_uiPunchHole")).activeSelf, Is.False);
            Assert.That(clicks, Is.Zero);
            scope.AssertUnchanged();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KimchiGuidance_NativeRecipeClickClearsPointerBeforeSeparateMiniGameNavigation(bool failingObserver)
    {
        using (var scope = new Fixture())
        {
            var previousMainView = scope.ActiveViews.Single();
            if (failingObserver)
            {
                scope.Recipe.BeforeMiniGameNavigation += () => throw new InvalidOperationException("isolated presentation failure");
                LogAssert.Expect(LogType.Warning, "[UIRecipePreview] Recipe hint cleanup failed: InvalidOperationException");
            }
            var routine = scope.BeginGuidance();
            var delayed = scope.CapturePointerDelay();
            delayed();
            Assert.That(((RectTransform)Get(scope.Guide, "_customHoleCursorParent")).gameObject.activeSelf, Is.True);
            var miniGame = scope.BindNativeMiniGameButton(() =>
            {
                // This executes inside the real separate navigator's Push/Show,
                // before the next coroutine frame or mini-game screen can draw.
                scope.AssertPointerHidden();
                Assert.That(scope.ActiveViews.Single(), Is.SameAs(previousMainView),
                    "The real shop navigator remains unchanged when the mini-game opens");
            });
            Assert.That(miniGame.Shows, Is.Zero);
            scope.MiniGameButton.onClick.Invoke();
            Assert.That(miniGame.Shows, Is.EqualTo(1));
            Assert.That(scope.Time.GetTime("FOOD04_MiniGame"), Is.EqualTo(900),
                "Only the normal controller starts the isolated fixture cooldown");
            delayed();
            scope.AssertPointerHidden();
            Assert.That(routine.MoveNext(), Is.False);
            var remainingObserver = Get(scope.Preview, "BeforeMiniGameNavigation") as Delegate;
            if (failingObserver) Assert.That(remainingObserver, Is.Not.Null, "Unrelated observers are preserved");
            else Assert.That(remainingObserver, Is.Null, "The guide removes its own observer");
            scope.AssertUnchanged();
        }
    }

    [Test]
    public void KimchiGuidance_DisablingOwnerUnsubscribesAndOldTweenCannotRevivePointer()
    {
        using (var scope = new Fixture())
        {
            var routine = scope.BeginGuidance();
            var delayed = scope.CapturePointerDelay();
            delayed();
            scope.Tutorial.runInEditMode = true;
            scope.Tutorial.gameObject.SetActive(true);
            scope.Tutorial.gameObject.SetActive(false);
            Assert.That(Get(scope.Preview, "BeforeMiniGameNavigation"), Is.Null);
            scope.AssertPointerHidden();
            delayed();
            scope.AssertPointerHidden();
            Assert.That(routine.MoveNext(), Is.False);
            scope.AssertUnchanged();
        }
    }

    [Test]
    public void KimchiGuidance_DisablingOverlayClearsBackdropInputAndRejectsOldTweenOnReenable()
    {
        using (var scope = new Fixture())
        {
            var routine = scope.BeginGuidance();
            var delayed = scope.CapturePointerDelay();
            delayed();
            scope.Guide.runInEditMode = true;
            scope.Guide.gameObject.SetActive(false);
            scope.Guide.gameObject.SetActive(true);
            delayed();
            scope.AssertPointerHidden();
            Assert.That(((Button)Get(scope.Guide, "_screenButton")).gameObject.activeSelf, Is.False);
            ((IDisposable)routine).Dispose();
            scope.AssertUnchanged();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<Object> _objects = new List<Object>();
        private readonly Scene _scene;
        private readonly long _money = UserInfo.Money;
        private readonly int _dia = UserInfo.Dia, _count = UserInfo.TotalUseGachaMachineCount;
        private readonly bool _tutorial = UserInfo.IsTutorialStart, _sdk = BackEnd.Backend.IsInitialized;
        private readonly UnityEngine.Random.State _random = UnityEngine.Random.state;
        public readonly UIRecipeTab Recipe;
        public readonly UIRecipePreview Preview;
        public readonly RectTransform Viewport, TargetSlot;
        public readonly UITutorial Guide;
        public readonly TestTutorial Tutorial;
        public readonly TimeManager Time;
        public readonly List<MobileUIView> ActiveViews;
        private readonly GameObject _waitParent;
        public Button MiniGameButton => (Button)Get(Get(Preview, "_minigameButton"), "_button");

        public Fixture()
        {
            Replace(typeof(DataBind), "_unityActionBindDic", new DataBindContainer<UnityEngine.Events.UnityAction>());
            _waitParent = new GameObject("isolated kimchi pointer delay queue"); _objects.Add(_waitParent);
            Replace(typeof(Muks.Tween.Tween), "_waitQueueParent", _waitParent);
            Replace(typeof(Muks.Tween.Tween), "_tweenWaitQueue", new Queue<Muks.Tween.TweenWait>());
            Replace(typeof(UserInfo), "_stageInfos", new[] { new StageInfo(), new StageInfo(), new StageInfo() });
            Replace(typeof(UserInfo), "_giveRecipeLevelDic", new Dictionary<string, int>());
            Replace(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int> { ["GOTCHA91"] = 1 });
            Replace(typeof(GameManager), "_instance", Inactive<GameManager>());
            Time = Inactive<TimeManager>();
            Replace(typeof(TimeManager), "_instance", Time);
            Replace(typeof(ItemManager), "_instance", Inactive<ItemManager>());
            var item = new GachaItemData("GOTCHA91", "Kimchi recipe", "", 0, 0, 4, default, 0, 0, 1, null);
            Replace(typeof(ItemManager), "_gachaItemDataDic", new Dictionary<string, GachaItemData> { [item.Id] = item });
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            Recipe = Find<UIRecipeTab>();
            Preview = Find<UIRecipePreview>();
            var shop = Find<UIRestaurantAdmin>();
            shop.transform.SetParent(null, false);
            shop.gameObject.SetActive(true);
            shop.VisibleState = VisibleState.Appeared;
            var canvas = (CanvasGroup)Get(shop, "_canvasGroup");
            canvas.alpha = 1f; canvas.interactable = canvas.blocksRaycasts = true;
            ((RectTransform)Get(shop, "_dontTouchArea")).gameObject.SetActive(false);
            Recipe.transform.SetParent(shop.transform, false);
            Recipe.gameObject.SetActive(true);
            Preview.transform.SetParent(Recipe.transform, false);
            Preview.gameObject.SetActive(true);
            Set(Recipe, "_isInitialized", true);

            var scrollHost = new GameObject("detached recipe grid", typeof(RectTransform), typeof(ScrollRect));
            _objects.Add(scrollHost);
            Viewport = (RectTransform)scrollHost.transform;
            Viewport.sizeDelta = new Vector2(360, 200);
            var content = new GameObject("grid content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(Viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(360, 600);
            var scroll = scrollHost.GetComponent<ScrollRect>();
            scroll.viewport = Viewport; scroll.content = content; scroll.horizontal = false;
            var foods = new List<FoodData>();
            var slots = new UIRestaurantAdminFoodTypeSlot[18];
            for (int i = 0; i < slots.Length; i++)
            {
                var food = ScriptableObject.CreateInstance<FoodData>(); _objects.Add(food);
                Set(food, "_id", i == 10 ? "FOOD04" : "fixture-recipe-" + i);
                Set(food, "_name", "recipe " + i); Set(food, "_foodType", FoodType.Natural);
                Set(food, "_moneyType", MoneyType.Gold); Set(food, "_buyScore", 0);
                Set(food, "_needItem", i == 10 ? "GOTCHA91" : null);
                foods.Add(food);
                var slot = new GameObject("slot " + i, typeof(RectTransform), typeof(UIRestaurantAdminFoodTypeSlot));
                var rect = (RectTransform)slot.transform;
                rect.SetParent(content, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(100, 90);
                rect.anchoredPosition = new Vector2(60 + (i % 3) * 120, -50 - (i / 3) * 100);
                slots[i] = slot.GetComponent<UIRestaurantAdminFoodTypeSlot>();
                if (i == 10) TargetSlot = rect;
            }
            Set(Recipe, "_foodDataList", foods); Set(Recipe, "_slots", slots);
            scroll.verticalNormalizedPosition = 1f;

            Guide = Find<UITutorial>();
            Guide.transform.SetParent(null, false);
            Guide.Show();
            var nav = Inactive<MobileUINavigation>();
            ActiveViews = new List<MobileUIView> { shop };
            Set(nav, "_activeViewList", ActiveViews);
            Tutorial = Inactive<TestTutorial>();
            Set(Tutorial, "_mainNav", nav); Set(Tutorial, "_uiTutorial", Guide);
        }

        public IEnumerator BeginGuidance()
        {
            var routine = (IEnumerator)typeof(GachaTutorial).GetMethod("GuideKimchiRecipeInput",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Tutorial, null);
            Assert.That(routine.MoveNext(), Is.True);
            return routine;
        }

        public Action CapturePointerDelay()
        {
            typeof(UITutorial).GetMethod("OnCustomHoleAnimeCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Guide, null);
            return (Action)Get(_waitParent.GetComponentsInChildren<Muks.Tween.TweenWait>(true).Last(), "_onCompleted");
        }

        public TestMiniGameView BindNativeMiniGameButton(Action beforeShow)
        {
            var miniGame = Inactive<TestMiniGameView>();
            miniGame.BeforeShow = beforeShow;
            miniGame.VisibleState = VisibleState.Disappeared;
            var separateNav = Inactive<MobileUINavigation>();
            Set(separateNav, "_viewDic", new Dictionary<string, MobileUIView> { ["UIMiniGame"] = miniGame });
            Set(miniGame, "_uiNav", separateNav);
            Set(miniGame, "_miniGame1", Inactive<TestMiniGame>());
            Set(miniGame, "_miniGameFever", Inactive<MiniGameFever>());
            Set(Preview, "_uiMiniGame", miniGame);
            // Install the same actual button callback as product initialization.
            Preview.Init(_ => Assert.Fail("No recipe purchase during guidance"), _ => Assert.Fail("No upgrade during guidance"));
            return miniGame;
        }

        public void AssertPointerHidden()
        {
            Assert.That(((GameObject)Get(Guide, "_uiPunchHole")).activeSelf, Is.False);
            Assert.That(((RectTransform)Get(Guide, "_customHoleCursorParent")).gameObject.activeSelf, Is.False);
            Assert.That(((GameObject)Get(Guide, "_customHoleCursorUp")).activeSelf, Is.False);
            Assert.That(((GameObject)Get(Guide, "_customHoleCursorDown")).activeSelf, Is.False);
            Assert.That(((HoleClickHandler)Get(Guide, "_customHole")).Interactable, Is.False);
        }

        public void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(_count));
            Assert.That(UserInfo.IsTutorialStart, Is.EqualTo(_tutorial));
            Assert.That(UserInfo.IsGiveRecipe("FOOD04"), Is.False);
            Assert.That(UserInfo.GetGiveGachaItemCountDic()["GOTCHA91"], Is.EqualTo(1));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        }

        private T Find<T>() where T : Component => _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private T Inactive<T>() where T : Component
        {
            var host = new GameObject("detached recipe dependency " + typeof(T).Name);
            host.SetActive(false); _objects.Add(host); return host.AddComponent<T>();
        }
        private void Replace(Type type, string name, object value)
        {
            var field = Field(type, name); var old = field.GetValue(null);
            _restore.Add(() => field.SetValue(null, old)); field.SetValue(null, value);
        }
        public void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            for (int i = _objects.Count - 1; i >= 0; --i) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            for (int i = _restore.Count - 1; i >= 0; --i) _restore[i]();
            UnityEngine.Random.state = _random;
        }
    }

    public sealed class TestTutorial : GachaTutorial
    {
        protected override bool IsCapturedSessionCurrent() => true;
    }
    public sealed class TestMiniGameView : UIMiniGameController
    {
        public Action BeforeShow;
        public int Shows;
        public override void Show()
        {
            BeforeShow?.Invoke();
            Shows++;
            VisibleState = VisibleState.Appeared;
        }
    }
    public sealed class TestMiniGame : MiniGame1
    {
        public override void Show(FoodData foodData, Action onComplete = null) { }
    }
    private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }
}
#endif
