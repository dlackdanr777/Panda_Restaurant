using System;
using System.Reflection;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PopupStoreNavigationTests
{
    [Test]
    public void AuthoredPrefabHasConnectedCloseDisabledControlsAndApprovedFont()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/PopupStore/UIPopupStore.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.activeSelf, Is.False);
        var view = prefab.GetComponent<UIPopupStore>();
        Assert.That(view, Is.Not.Null);
        Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<CanvasScaler>(), Is.Not.Null);
        Assert.That(view.PopEnabled, Is.True);

        var close = (Button)Read(typeof(UIPopupStore), view, "_closeButton");
        Assert.That(close, Is.Not.Null);
        Assert.That(close.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(close.enabled && close.interactable, Is.True);
        for (Transform current = close.transform; current != prefab.transform; current = current.parent)
            Assert.That(current.gameObject.activeSelf, Is.True, current.name);
        Assert.That(close.targetGraphic, Is.Not.Null);
        var closeImage = close.targetGraphic as Image;
        Assert.That(closeImage, Is.Not.Null);
        Assert.That(closeImage.sprite, Is.Not.Null);

        var displayOnly = (Button[])Read(typeof(UIPopupStore), view, "_displayOnlyButtons");
        // DEV-03C-2 connects packing; pausing remains display-only.
        Assert.That(displayOnly, Has.Length.EqualTo(1));
        CollectionAssert.AllItemsAreUnique(displayOnly);
        foreach (var button in displayOnly)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button, Is.Not.SameAs(close));
            Assert.That(button.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(button.interactable, Is.False, button.name);
            Assert.That(button.targetGraphic, Is.Not.Null, button.name);
        }

        var approvedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/영덕대게체.asset");
        Assert.That(approvedFont, Is.Not.Null);
        var labels = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.That(labels, Is.Not.Empty);
        foreach (var label in labels)
            Assert.That(label.font, Is.SameAs(approvedFont), label.name);
    }

    [Test]
    public void EntryAndRepeatedClosePreserveFloor3AndExistingAccountState()
    {
        using (var host = new Host())
        {
            long money = UserInfo.Money;
            int dia = UserInfo.Dia;
            object score = Read(typeof(UserInfo), null, "_score");
            int visits = UserInfo.TotalCumulativeCustomerCount;
            var backend = BackendManager.ExistingInstance;
            object gameManager = Read(typeof(GameManager), null, "_instance");
            long encounterContext = UserInfo.CustomerEncounterContextGeneration;
            bool sdkInitialized = BackEnd.Backend.IsInitialized;
            int shown = 0, hidden = 0;
            host.Navigation.OnShowUIHandler += () => shown++;
            host.Navigation.OnHideUIHandler += () => hidden++;

            host.Entry.onClick.Invoke();
            host.Entry.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(2));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Store));
            Assert.That(host.Store.gameObject.activeSelf, Is.True);
            Assert.That(host.Floor.gameObject.activeSelf, Is.True);
            Assert.That(shown, Is.EqualTo(1));

            host.Close.onClick.Invoke();
            host.Close.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Floor.VisibleState, Is.EqualTo(VisibleState.Appeared));
            Assert.That(host.Store.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(host.Store.gameObject.activeSelf, Is.False);
            Assert.That(host.Navigation.ViewsVisibleStateCheck(), Is.True);
            Assert.That(hidden, Is.EqualTo(1));

            Assert.That(UserInfo.Money, Is.EqualTo(money));
            Assert.That(UserInfo.Dia, Is.EqualTo(dia));
            Assert.That(Read(typeof(UserInfo), null, "_score"), Is.EqualTo(score));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(encounterContext));
            Assert.That(BackendManager.ExistingInstance, Is.SameAs(backend));
            Assert.That(Read(typeof(GameManager), null, "_instance"), Is.SameAs(gameManager));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(sdkInitialized));
        }
    }

    [TestCase("other-view")]
    [TestCase("floor-opening")]
    [TestCase("view-closing")]
    [TestCase("navigation-hidden")]
    [TestCase("popup-open")]
    [TestCase("popup-hiding")]
    public void EntryIsRejectedWhenFloor3IsNotTheSettledUncoveredView(string boundary)
    {
        using (var host = new Host())
        {
            switch (boundary)
            {
                case "other-view": host.Navigation.PushNoAnime("OtherView"); break;
                case "floor-opening": host.Floor.VisibleState = VisibleState.Appearing; break;
                case "view-closing": host.Other.VisibleState = VisibleState.Disappearing; break;
                case "navigation-hidden": Write(host.Navigation, "_isViewsInactive", true); break;
                case "popup-open": WriteStatic(typeof(PopupManager), "_isShowPopup", true); break;
                case "popup-hiding":
                    var popup = host.Create<UIPopup>("existing-popup");
                    WriteStatic(typeof(PopupManager), "_uiPopup", popup);
                    popup.gameObject.SetActive(true);
                    break;
            }
            int previousCount = host.Navigation.Count;
            host.Entry.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(previousCount));
            Assert.That(host.Navigation.CheckActiveView(UIPopupStore.ViewName), Is.False);
            Assert.That(host.Store.gameObject.activeSelf, Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CloseCannotPopAnotherViewOrInterruptAnUnfinishedTransition(bool transition)
    {
        using (var host = new Host())
        {
            host.Entry.onClick.Invoke();
            if (transition) host.Other.VisibleState = VisibleState.Disappearing;
            else host.Navigation.PushNoAnime("OtherView");
            int count = host.Navigation.Count;
            host.Close.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(count));
            Assert.That(host.Navigation.CheckActiveView(UIPopupStore.ViewName), Is.True);
            Assert.That(host.Navigation.CheckActiveView("UIFloor3"), Is.True);
        }
    }

    [Test]
    public void ReinitializationKeepsOneEntryAndDisablesDisplayOnlyControls()
    {
        using (var host = new Host())
        {
            host.Floor.Init();
            host.Floor.gameObject.SetActive(true);
            Write(host.Store, "_canvas", null);
            host.Store.Init();
            host.Store.Init();
            int shown = 0, hidden = 0;
            host.Navigation.OnShowUIHandler += () => shown++;
            host.Navigation.OnHideUIHandler += () => hidden++;
            Assert.That(host.DisplayOnly.interactable, Is.False);
            Assert.That(host.Close.interactable, Is.True);
            Assert.That(host.Store.Canvas, Is.SameAs(host.Store.GetComponent<Canvas>()));
            host.Entry.onClick.Invoke();
            host.Close.onClick.Invoke();
            Assert.That(shown, Is.EqualTo(1));
            Assert.That(hidden, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
        }
    }

    [TestCase("_popupStoreButton")]
    [TestCase("_popupStoreView")]
    public void UnconfiguredEntryGuardLeavesFloor3Open(string missingReference)
    {
        using (var host = new Host())
        {
            Write(host.Floor, missingReference, null);
            // Exercise the entry guard without invoking the retained generic toast.
            typeof(UIFloor3).GetMethod("OnPopupStoreClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host.Floor, null);
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Store.gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void MissingCloseButtonRollsBackOnlyTheFailedStoreOpening()
    {
        using (var host = new Host())
        {
            Write(host.Store, "_closeButton", null);
            Assert.Throws<InvalidOperationException>(() => host.Entry.onClick.Invoke());
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Store.gameObject.activeSelf, Is.False);
            Assert.That(host.Navigation.ViewsVisibleStateCheck(), Is.True);
        }
    }

    [Test]
    public void ViewBoundToAnotherNavigationCannotBeOpenedByFloor3()
    {
        using (var host = new Host())
        {
            var otherNavigation = host.Create<MobileUINavigation>("unrelated-navigation");
            host.Store.ViewInit(otherNavigation);
            host.Entry.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly GameObject _root;
        private readonly object _previousPopup, _previousPopupOpen;
        internal readonly MobileUINavigation Navigation;
        internal readonly UIFloor3 Floor;
        internal readonly UIPopupStore Store, Other;
        internal readonly Button Entry, Close, DisplayOnly;

        internal Host()
        {
            _previousPopup = Read(typeof(PopupManager), null, "_uiPopup");
            _previousPopupOpen = Read(typeof(PopupManager), null, "_isShowPopup");
            _root = new GameObject("popup-store-navigation-test");
            _root.SetActive(false);
            try
            {
                WriteStatic(typeof(PopupManager), "_uiPopup", null);
                WriteStatic(typeof(PopupManager), "_isShowPopup", false);
                Navigation = Create<MobileUINavigation>("navigation");
                Floor = Create<UIFloor3>("floor3");
                Store = Create<UIPopupStore>("store");
                Store.gameObject.AddComponent<Canvas>();
                Other = Create<UIPopupStore>("other-view");
                Entry = Create<Button>("entry");
                Close = Create<Button>("close");
                DisplayOnly = Create<Button>("display-only");
                Write(Floor, "_buttons", new[] { Entry });
                Write(Floor, "_popupStoreButton", Entry);
                Write(Floor, "_popupStoreView", Store);
                Write(Store, "_closeButton", Close);
                Write(Store, "_displayOnlyButtons", new[] { DisplayOnly, Close });
                Write(Other, "_closeButton", Create<Button>("other-close"));
                Write(Navigation, "_uiViews", new[]
                {
                    new MobileViewDicStruct { Name = "UIFloor3", UIView = Floor },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = Store },
                    new MobileViewDicStruct { Name = "OtherView", UIView = Other }
                });
                typeof(MobileUINavigation).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Navigation, null);
                _root.SetActive(true);
                Navigation.PushNoAnime("UIFloor3");
            }
            catch { Dispose(); throw; }
        }

        internal T Create<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_root.transform);
            T result = go.AddComponent<T>();
            go.SetActive(true);
            return result;
        }

        public void Dispose()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            WriteStatic(typeof(PopupManager), "_uiPopup", _previousPopup);
            WriteStatic(typeof(PopupManager), "_isShowPopup", _previousPopupOpen);
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object Read(Type type, object target, string name) => Field(type, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static void WriteStatic(Type type, string name, object value) => Field(type, name).SetValue(null, value);
}
