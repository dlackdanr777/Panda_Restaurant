#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GachaPhase3Tests
{
    [TestCase(GachaMachineKind.Item)] [TestCase(GachaMachineKind.Staff)]
    public void PaymentChoice_TwentyClicksWhileSendingAndUnknownNeverCreateAnotherTransaction(GachaMachineKind machine)
    {
        using (var host = new Phase3PaymentHost(machine, 100, 10))
        {
            host.Store.DeferNextSave = true;
            host.Open(true); host.View.CollectionPayment.TicketButton.onClick.Invoke();
            for (int i = 0; i < 20; i++)
            {
                host.View.CollectionPayment.DiamondButton.onClick.Invoke(); host.View.CollectionPayment.TicketButton.onClick.Invoke();
                host.Open(false); host.Open(true);
            }
            Assert.That(host.Store.SaveAttempts, Is.EqualTo(1)); Assert.That(host.Rolls, Is.EqualTo(11));
            Assert.That(host.Store.CommitCount, Is.Zero);
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Tickets(machine), Is.EqualTo(10));
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.True);
            host.Store.CompletePending(GachaStoreResult.Indeterminate);
            host.Open(true); Assert.That(host.Store.SaveAttempts, Is.EqualTo(1));
            host.Store.CompletePending(GachaStoreResult.Confirmed); host.Store.CompletePending(GachaStoreResult.Confirmed);
            Assert.That(host.Store.CommitCount, Is.EqualTo(1)); Assert.That(host.Rolls, Is.EqualTo(11));
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.False);
        }
    }
    [TestCase(GachaMachineKind.Staff, false, false, true)]
    [TestCase(GachaMachineKind.Staff, false, false, false)]
    [TestCase(GachaMachineKind.Staff, false, true, true)]
    [TestCase(GachaMachineKind.Staff, false, true, false)]
    [TestCase(GachaMachineKind.Staff, true, false, true)]
    [TestCase(GachaMachineKind.Staff, true, false, false)]
    [TestCase(GachaMachineKind.Staff, true, true, true)]
    [TestCase(GachaMachineKind.Staff, true, true, false)]
    [TestCase(GachaMachineKind.Item, false, false, true)]
    [TestCase(GachaMachineKind.Item, false, false, false)]
    [TestCase(GachaMachineKind.Item, false, true, true)]
    [TestCase(GachaMachineKind.Item, false, true, false)]
    [TestCase(GachaMachineKind.Item, true, false, true)]
    [TestCase(GachaMachineKind.Item, true, false, false)]
    [TestCase(GachaMachineKind.Item, true, true, true)]
    [TestCase(GachaMachineKind.Item, true, true, false)]
    public void PaymentChoice_ExactCostReadinessOneTransactionAndLatestGauge(GachaMachineKind machine, bool eleven, bool tickets, bool sufficient)
    {
        int ticketCost = eleven ? 10 : 1, diamondCost = eleven ? 100 : 10;
        using (var host = new Phase3PaymentHost(machine, tickets || sufficient ? diamondCost : diamondCost - 1,
            !tickets || sufficient ? ticketCost : ticketCost - 1))
        {
            host.Open(eleven);
            Assert.That(host.View.IsChoosingPayment, Is.True);
            Assert.That(host.Store.SaveAttempts, Is.Zero);
            Assert.That(host.Rolls, Is.Zero);
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.True, "The chooser is not a result presentation");
            Assert.That(host.View.CollectionPayment.DiamondButton.interactable, Is.True);
            Assert.That(host.View.CollectionPayment.TicketButton.interactable, Is.EqualTo(!tickets || sufficient));
            Assert.That(host.View.CollectionPayment.GetComponent<Image>().raycastTarget, Is.True);
            var labels = host.View.CollectionPayment.GetComponentsInChildren<TMPro.TMP_Text>(true);
            string prompt = (machine == GachaMachineKind.Staff ? "스텝" : "아이템") +
                (eleven ? " 10+1회 뽑기\n진행하시겠습니까?" : " 1회 뽑기\n진행하시겠습니까?");
            Assert.That(labels.Single(x => x.name == "Selected Product").text, Is.EqualTo(prompt));
            Assert.That(labels.Single(x => x.name == "Diamond Cost").text, Is.EqualTo("다이아 x" + diamondCost));
            Assert.That(labels.Single(x => x.name == "Ticket Cost").text, Is.EqualTo("뽑기권 x" + ticketCost));
            Assert.That(labels.Any(x => x.text.Contains("보유")), Is.False);
            Assert.That(labels.Any(x => x.text == "취소"), Is.False);
            Assert.That(labels.Single(x => x.name == "Selected Product").alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Center));
            Assert.That(labels.Single(x => x.name == "Selected Product").fontSize, Is.GreaterThanOrEqualTo(34));
            var plate = (RectTransform)host.View.CollectionPayment.transform.Find("Payment Frame");
            var promptLabel = labels.Single(x => x.name == "Selected Product");
            promptLabel.ForceMeshUpdate();
            Assert.That(promptLabel.isTextOverflowing, Is.False);
            Assert.That(promptLabel.textInfo.lineCount, Is.EqualTo(2));
            Assert.That(promptLabel.rectTransform.rect.height, Is.GreaterThanOrEqualTo(40));
            Assert.That(promptLabel.fontSize, Is.GreaterThan(labels.Single(x => x.name == "Diamond Cost").fontSize));
            var promptBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(plate, promptLabel.rectTransform);
            Assert.That(promptBounds.center.x, Is.EqualTo(plate.rect.center.x).Within(.01f));
            Assert.That(promptBounds.center.y, Is.EqualTo(plate.rect.center.y).Within(.01f), "Center guidance in the entire frame");
            foreach (var button in new[] { host.View.CollectionPayment.DiamondButton, host.View.CollectionPayment.TicketButton })
            {
                var buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(plate, button.transform);
                Assert.That(buttonBounds.min.y - plate.rect.yMin, Is.EqualTo(-22).Within(.01f), "Large payment choices slightly overhang the frame");
                Assert.That(buttonBounds.size.x, Is.GreaterThanOrEqualTo(208));
                Assert.That(promptBounds.min.y - buttonBounds.max.y, Is.GreaterThanOrEqualTo(10), "Separate guidance from the bottom choices");
            }
            Assert.That(AssetDatabase.GetAssetPath(plate.GetComponent<Image>().sprite),
                Is.EqualTo("Assets/Sprites/UI/Ad/UI_프레임_광고시청.png"));
            Assert.That(AssetDatabase.GetAssetPath(plate.Find("Close Payment").GetComponent<Image>().sprite),
                Is.EqualTo("Assets/Sprites/UI/Ad/UI_버튼_광고 나가기.png"));
            var capsule = plate.Find("Capsule Badge");
            Assert.That(Mathf.DeltaAngle(0, capsule.localEulerAngles.z), Is.EqualTo(-15).Within(.01f));
            Assert.That(AssetDatabase.GetAssetPath(plate.Find("Left Capsule/Capsule Top").GetComponent<Image>().sprite),
                Does.EndWith("가챠Ui_캡슐_초록색_상단.png"));
            Assert.That(AssetDatabase.GetAssetPath(plate.Find("Right Capsule/Capsule Top").GetComponent<Image>().sprite),
                Does.EndWith("가챠Ui_캡슐_빨간색_상단.png"));
            Assert.That(plate.GetComponentsInChildren<Image>().Count(x => x.name == "Capsule Top"), Is.EqualTo(7));
            Assert.That(AssetDatabase.GetAssetPath(capsule.Find("Capsule Top").GetComponent<Image>().sprite),
                Is.EqualTo("Assets/Sprites/UI/UIGacha/가챠 캡슐/가챠Ui_캡슐_주황색_상단.png"));
            Assert.That(capsule.Find("Capsule Bottom").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(capsule.Find("Capsule Top").GetSiblingIndex(), Is.GreaterThan(capsule.Find("Capsule Bottom").GetSiblingIndex()));
            foreach (var button in new[] { host.View.CollectionPayment.DiamondButton, host.View.CollectionPayment.TicketButton })
                Assert.That(AssetDatabase.GetAssetPath(button.GetComponent<Image>().sprite),
                    Is.EqualTo("Assets/Sprites/UI/UIGacha/가챠UI/가챠UI_1회 뽑기 버튼.png"));
            Assert.That(plate.rect.width, Is.LessThan(700)); Assert.That(plate.rect.height, Is.LessThan(340));
            var choice = tickets ? host.View.CollectionPayment.TicketButton : host.View.CollectionPayment.DiamondButton;
            choice.onClick.Invoke(); choice.onClick.Invoke();
            host.Machine.OnSingleGachaButtonClicked();
            if (!sufficient)
            {
                Assert.That(host.Store.SaveAttempts, Is.Zero);
                Assert.That(host.Rolls, Is.Zero);
                Assert.That(host.StoreOpens, Is.EqualTo(tickets ? 0 : 1));
                Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.True);
                return;
            }
            Assert.That(host.Store.SaveAttempts, Is.EqualTo(1));
            Assert.That(host.Rolls, Is.EqualTo(eleven ? 11 : 1));
            Assert.That(host.View.IsChoosingPayment, Is.False);
            Assert.That(host.Economy.LastTransaction.Results.Count, Is.EqualTo(eleven ? 11 : 1));
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Counter(machine), Is.EqualTo(eleven ? 10 : 1));
            Assert.That(host.Economy.Snapshot.Diamonds, Is.EqualTo(tickets ? diamondCost : 0));
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Tickets(machine), Is.EqualTo(tickets ? 0 : ticketCost));
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Tickets(machine == GachaMachineKind.Staff ? GachaMachineKind.Item : GachaMachineKind.Staff), Is.EqualTo(23));
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.False);
            host.View.SetStartGacha(false);
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.True);
            Assert.That(host.View.CollectionHud.CommittedCounter, Is.EqualTo(eleven ? 10 : 1));
            Assert.That(host.View.CollectionHud.DisplayedProgress, Is.EqualTo(eleven ? .1f : .01f).Within(.001f));
        }
    }

    [TestCase(GachaMachineKind.Item)] [TestCase(GachaMachineKind.Staff)]
    public void PaymentChoice_OutsideClickDisableAccountChangeNeverPaysOrKeepsModal(GachaMachineKind machine)
    {
        using (var host = new Phase3PaymentHost(machine, 100, 10))
        {
            var single = machine == GachaMachineKind.Staff ? host.Staff.SingleButton : host.Item.SingleButton;
            var ten = machine == GachaMachineKind.Staff ? host.Staff.TenButton : host.Item.TenButton;
            var singleGroup = single.gameObject.AddComponent<CanvasGroup>(); singleGroup.alpha = .65f;
            var tenGroup = ten.gameObject.AddComponent<CanvasGroup>(); tenGroup.alpha = .8f;
            void AssertSourceRestored()
            {
                Assert.That(singleGroup.alpha, Is.EqualTo(.65f));
                Assert.That(tenGroup.alpha, Is.EqualTo(.8f));
            }
            host.Open(true);
            var payment = host.View.CollectionPayment;
            Assert.That(singleGroup.alpha + tenGroup.alpha, Is.Zero);
            foreach (GameObject hit in new[] { payment.transform.Find("Payment Frame").gameObject, payment.TicketButton.gameObject })
            {
                payment.TicketButton.interactable = false;
                payment.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null) {
                    pointerCurrentRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = hit } });
                Assert.That(payment.IsOpen, Is.True, "Interior and disabled choices cannot dismiss the modal");
            }
            payment.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null) {
                pointerCurrentRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = payment.gameObject } });
            Assert.That(payment.IsOpen, Is.False);
            Assert.That(host.Store.SaveAttempts + host.Rolls, Is.Zero);
            AssertSourceRestored();
            host.Open(true);
            payment.transform.Find("Payment Frame/Close Payment").GetComponent<Button>().onClick.Invoke();
            Assert.That(payment.IsOpen, Is.False);
            Assert.That(host.View.IsChoosingPayment, Is.False);
            payment.DiamondButton.onClick.Invoke();
            Assert.That(host.Store.SaveAttempts + host.Rolls, Is.Zero, "Closing with X must retire the payment callback");
            AssertSourceRestored();
            host.Open(true); payment.gameObject.SetActive(false);
            // Runtime lifecycle messages are dispatched explicitly in this SDK-free EditMode host.
            typeof(GachaPaymentChoiceView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(payment, null);
            AssertSourceRestored();
            host.Open(false); host.View.SetEditorOfflineVisible(false);
            typeof(UIGacha).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host.View, null);
            AssertSourceRestored();
            host.View.CollectionPayment.DiamondButton.onClick.Invoke();
            Assert.That(host.Store.SaveAttempts + host.Rolls, Is.Zero);
            host.View.SetEditorOfflineVisible(true); host.Open(true);
            host.View.BindCollectionEconomy(null);
            host.View.CollectionPayment.TicketButton.onClick.Invoke();
            Assert.That(host.View.IsChoosingPayment, Is.False);
            Assert.That(host.Store.SaveAttempts + host.Rolls, Is.Zero);
            AssertSourceRestored();
        }
    }

    [TestCase(GachaMachineKind.Item)] [TestCase(GachaMachineKind.Staff)]
    public void TicketBundle_RejectedRetryReusesElevenResultsAndTenPityCredits(GachaMachineKind machine)
    {
        using (var host = new Phase3PaymentHost(machine, 0, 10))
        {
            host.Store.FailNextSave = true;
            host.Open(true); host.View.CollectionPayment.TicketButton.onClick.Invoke();
            var first = host.Economy.LastTransaction;
            Assert.That(first.Status, Is.EqualTo(GachaTransactionStatus.Rejected));
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Tickets(machine), Is.EqualTo(10));
            Assert.That(host.View.CollectionHud.IsGaugeVisible, Is.True);
            Assert.That(host.Economy.RetryRejected(null, out string error), Is.True, error);
            Assert.That(host.Economy.LastTransaction, Is.SameAs(first));
            Assert.That(host.Rolls, Is.EqualTo(11));
            Assert.That(host.Store.CommitCount, Is.EqualTo(1));
            Assert.That(host.Economy.Snapshot.Account.GachaEconomy.Counter(machine), Is.EqualTo(10));
            Assert.That(first.Results.Last().DrawRole, Is.EqualTo(GachaDrawRole.Bonus));
            Assert.That(first.Results.Last().CounterBefore, Is.EqualTo(first.Results.Last().CounterAfter));
        }
    }
}

internal sealed class Phase3PaymentHost : IDisposable
{
    internal readonly GameObject Root;
    internal readonly UIGacha View;
    internal readonly UIStaffGacha Staff;
    internal readonly UIItemGacha Item;
    internal readonly GachaMachineParent Machine;
    internal readonly GachaEconomyMemoryStore Store;
    internal readonly GachaEconomyService Economy;
    internal int Rolls, StoreOpens;
    private readonly Scene _scene;
    private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
    private readonly GachaStaffData _staffData;
    private readonly GachaItemData _itemData;
    private readonly UnityEngine.Random.State _random;
    internal Phase3PaymentHost(GachaMachineKind machine, int diamonds, int tickets)
    {
        _random = UnityEngine.Random.state;
        _scene = EditorSceneManager.NewPreviewScene();
        _staffData = GachaStaffData.Create(Resources.Load<StaffData>("StaffData/STAFF01"));
        _itemData = ScriptableObject.CreateInstance<GachaItemData>();
        EnhancementFairyStage1Host.Set(_itemData, "_id", "PHASE3-ITEM");
        EnhancementFairyStage1Host.Set(_itemData, "_rank", Rank.Normal2);
        EnhancementFairyStage1Host.Set(_itemData, "_upgradeType", UpgradeType.UPGRADE01);
        Store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("phase3-ui", new StaffAccountSaveData(
            StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(), 10000,
            new GachaEconomySaveData(machine == GachaMachineKind.Item ? tickets : 23,
                machine == GachaMachineKind.Staff ? tickets : 23, 0, 0)), diamonds));
        Economy = new GachaEconomyService(Store, new GachaData[] { _staffData, _itemData },
            Resources.Load<GachaEconomySettings>("GachaEconomySettings"), (m, pool, guaranteed) => { Rolls++; return pool[0]; });
        Root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(_scene, out View, out Staff, includeNavigation: true, includeItemPresentation: true);
        Item = View.GetComponentInChildren<UIItemGacha>(true);
        _fonts.BindBeforeActivation(Root);
        Staff.ConfigureCollectionOffline(View, Economy);
        Item.ConfigureCollectionOffline(View, Economy);
        View.ConfigureEditorOfflineNavigation(Staff, Item);
        View.BindCollectionEconomy(Economy);
        _fonts.BindBeforeActivation(Root);
        View.EditorOfflineCollectionPresentation = transaction => View.SetStartGacha(true);
        View.EditorOfflineDiamondStore = () => StoreOpens++;
        Root.SetActive(true); View.SetEditorOfflineVisible(true); View.SelectCollectionOfflineMachine(machine);
        Machine = machine == GachaMachineKind.Staff ? (GachaMachineParent)Staff : Item;
    }
    internal void Open(bool eleven) { if (eleven) Machine.OnTenGachaButtonClicked(); else Machine.OnSingleGachaButtonClicked(); }
    public void Dispose()
    {
        foreach (var scroll in Root.GetComponentsInChildren<ScrollingImage>(true))
        {
            var material = (Material)EnhancementFairyStage1Host.Get(scroll, "_material");
            if (material != null) Object.DestroyImmediate(material);
        }
        Object.DestroyImmediate(Root); _fonts.Dispose();
        Object.DestroyImmediate(_staffData); Object.DestroyImmediate(_itemData);
        EditorSceneManager.ClosePreviewScene(_scene);
        UnityEngine.Random.state = _random;
    }
}
#endif
