#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEngine;

// DEV-SERVER-01B counterfactual evidence. Uses the existing real Stage1 preview host,
// production economy acknowledgement path and production fairy updates; never the SDK.
// The original failing economy test and all production fairy code remain unchanged.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void Server01B_Fairy_KitchenBirthMovedToHall_IsPausedAtOriginalPuffAssertionTime()
    {
        Server01BWithConfirmedKitchenBirth((host, duplicateAcknowledgement) =>
        {
            string[] identity = host.Identity();
            float elapsed = Server01BFairyElapsed(host);
            Vector2 birthPosition = host.Habitat.GetBirthPosition(host.Habitat.GetActiveBrain(0).ItemId);
            host.Move(RestaurantType.Hall);
            host.Advance(1f / 30f);
            Server01BLogFairy(host, "same-original-path: Hall after camera move");
            Assert.That(Server01BVisibleBirthArea(host).Contains(birthPosition), Is.False,
                "The Kitchen birth position is outside the Hall's production visible birth area");
            Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);

            Server01BAdvanceToOriginalPuffFrame(host);
            Server01BLogFairy(host, "same-original-path: original puff assertion time");
            Assert.That(Server01BFairyElapsed(host), Is.EqualTo(elapsed),
                "Advancing preview time in another room must preserve the paused birth's elapsed time");
            Assert.That(Server01BPuff(host).enabled, Is.False,
                "The original expectation of a visible puff conflicts with its move to the other room");
            Assert.That(host.Bodies().Single().color.a, Is.Zero);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(identity, host.Identity());
            duplicateAcknowledgement();
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Server01B_Fairy_ReturnToKitchen_ResumesSameBirthAndConsumesOnceAfterLanding()
    {
        Server01BWithConfirmedKitchenBirth((host, duplicateAcknowledgement) =>
        {
            string[] identity = host.Identity();
            string itemId = host.Habitat.GetActiveBrain(0).ItemId;
            Vector2 birthPosition = host.Habitat.GetBirthPosition(itemId);
            host.Move(RestaurantType.Hall);
            host.Advance(2f);
            float pausedElapsed = Server01BFairyElapsed(host);
            Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
            Server01BLogFairy(host, "return-path: paused in Hall");

            host.Move(RestaurantType.Kitchen);
            host.Advance(1f / 30f);
            Assert.That(Server01BVisibleBirthArea(host).Contains(birthPosition), Is.True);
            Assert.That(host.Habitat.ActiveBirthCount, Is.EqualTo(1));
            Assert.That(Server01BFairyElapsed(host), Is.EqualTo(pausedElapsed),
                "Returning resumes the retained presentation instead of replacing it");
            Assert.That(host.Habitat.GetBirthPosition(itemId), Is.EqualTo(birthPosition));
            CollectionAssert.AreEqual(identity, host.Identity());
            Server01BLogFairy(host, "return-path: resumed in Kitchen");

            Server01BAdvanceToOriginalPuffFrame(host);
            Server01BLogFairy(host, "return-path: visible puff");
            Assert.That(Server01BPuff(host).sprite, Is.Not.Null);
            Assert.That(Server01BPuff(host).enabled, Is.True);
            Assert.That(host.Bodies().Single().color.a, Is.GreaterThan(0f));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.IsPending(itemId), Is.True);

            host.Advance(host.Habitat.Settings.BirthDuration + .2f);
            Server01BLogFairy(host, "return-path: landed");
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
            Assert.That(Server01BPuff(host).enabled, Is.False);
            Assert.That(host.Habitat.GetActiveBrain(0).ElapsedSeconds, Is.GreaterThan(0f));
            duplicateAcknowledgement();
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            CollectionAssert.AreEqual(identity, host.Identity());
        });
    }

    [Test]
    public void Server01B_Fairy_ConfirmedBirthRemainsInKitchen_ShowsPuffAtOriginalAssertionTime()
    {
        Server01BWithConfirmedKitchenBirth((host, duplicateAcknowledgement) =>
        {
            string[] identity = host.Identity();
            Server01BAdvanceToOriginalPuffFrame(host);
            Server01BLogFairy(host, "visible-room counterfactual: original puff assertion time");
            Assert.That(Server01BPuff(host).sprite, Is.Not.Null);
            Assert.That(Server01BPuff(host).enabled, Is.True,
                "With the production camera still on its birth position, the same timing exposes the puff");
            Assert.That(host.Habitat.ActiveBirthCount, Is.EqualTo(1));
            Assert.That(host.Bodies().Single().color.a, Is.GreaterThan(0f));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            duplicateAcknowledgement();
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(identity, host.Identity());
        });
    }

    private void Server01BWithConfirmedKitchenBirth(Action<EnhancementFairyStage1Host, Action> observe)
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            using (var host = new EnhancementFairyStage1Host())
            {
                var service = CreateEconomyService(fixture, out var cleanup, productionItems: host.Catalog);
                var handler = (Action<GachaEconomyTransaction>)Delegate.CreateDelegate(typeof(Action<GachaEconomyTransaction>),
                    fixture.Manager, typeof(BackendManager).GetMethod("OnEconomyCommitted", BindingFlags.Instance | BindingFlags.NonPublic));
                service.Committed += handler;
                try
                {
                    host.Move(ERestaurantFloorType.Floor3);
                    host.Move(RestaurantType.Kitchen);
                    host.Advance(1f / 30f);
                    Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                    var transaction = service.LastTransaction;
                    Assert.That(EnhancementFairyCatalog.IsEligible(transaction.Results[0].Data), Is.True);
                    Assert.That(host.Habitat.ActiveCount, Is.Zero);
                    fixture.Game.WriteReplies[0](Bro("500", ""));
                    Assert.That(transaction.Status, Is.EqualTo(GachaTransactionStatus.Indeterminate));
                    Assert.That(host.Habitat.ActiveCount, Is.Zero);
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    Assert.That(transaction.IsCompleted, Is.True);
                    Assert.That(host.Habitat.ActiveCount, Is.EqualTo(1));
                    Assert.That(host.Habitat.ActiveBirthCount, Is.EqualTo(1));
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
                    Server01BLogFairy(host, "confirmed Kitchen birth and repeated acknowledgement");
                    observe(host, () => fixture.Game.WriteReplies[0](Bro("204", "")));
                }
                finally
                {
                    service.Committed -= handler;
                    fixture.Manager.InvalidateGameDataRestore();
                    cleanup();
                }
            }
        }
    }

    private static void Server01BAdvanceToOriginalPuffFrame(EnhancementFairyStage1Host host)
    {
        // Match RecordBirth's authored expected frame without changing its test or settings.
        const int fps = 20;
        int frames = Mathf.CeilToInt((host.Habitat.Settings.TrailSeconds + host.Habitat.Settings.GatherSeconds + .2f) * fps);
        for (int frame = 0; frame < frames; frame++) host.Habitat.AdvancePreview(1f / fps);
    }

    private static SpriteRenderer Server01BPuff(EnhancementFairyStage1Host host)
        => host.Bodies().Single().transform.parent.Find("Arrival puff").GetComponent<SpriteRenderer>();

    private static object Server01BFairyView(EnhancementFairyStage1Host host)
        => ((IEnumerable)EnhancementFairyStage1Host.Get(host.Habitat, "_views")).Cast<object>().Single();

    private static float Server01BFairyElapsed(EnhancementFairyStage1Host host)
        => (float)EnhancementFairyStage1Host.Get(Server01BFairyView(host), "BirthElapsed");

    private static Rect Server01BVisibleBirthArea(EnhancementFairyStage1Host host)
    {
        object[] arguments = { default(Rect) };
        bool available = (bool)EnhancementFairyStage1Host.Invoke(host.Habitat, "TryGetVisibleBirthArea", arguments);
        Assert.That(available, Is.True, "The production camera must expose a valid room birth area");
        return (Rect)arguments[0];
    }

    private static void Server01BLogFairy(EnhancementFairyStage1Host host, string point)
    {
        object view = Server01BFairyView(host);
        Debug.Log("[DEV-SERVER-01B Fairy] " + point + "; room=" + host.Controller.CurrentRestaurant
            + "; camera=" + host.Camera.transform.position.ToString("F4")
            + "; visibleArea=" + Server01BVisibleBirthArea(host).ToString("F4")
            + "; birthPosition=" + host.Habitat.GetBirthPosition(host.Habitat.GetActiveBrain(0).ItemId).ToString("F4")
            + "; retainedElapsed=" + Server01BFairyElapsed(host).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
            + "; arrivalRemaining=" + EnhancementFairyStage1Host.Get(view, "ArrivalRemaining")
            + "; activeBirths=" + host.Habitat.ActiveBirthCount + "; presentations=" + host.Habitat.ArrivalPresentationCount
            + "; puff=" + Server01BPuff(host).enabled + "; pending=" + EnhancementFairyAcquisitionEvents.Queue.PendingCount);
    }
}
#endif
