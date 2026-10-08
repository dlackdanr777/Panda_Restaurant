#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class FairyBirthSequenceTests
{
    [Test]
    public void Queue_PreservesConfirmationOrderAndConsumesOnlyTheCompletedItem()
    {
        var queue = new EnhancementFairyArrivalQueue();
        Assert.That(queue.Confirm("commit-z", "Z"), Is.True);
        Assert.That(queue.Confirm("commit-a", "A"), Is.True);
        Assert.That(queue.Confirm("commit-z", "Z"), Is.False);
        Assert.That(queue.FirstPending, Is.EqualTo("Z"));
        CollectionAssert.AreEqual(new[] { "Z", "A" }, queue.PendingItems);
        Assert.That(queue.Consume("Z"), Is.True);
        Assert.That(queue.FirstPending, Is.EqualTo("A"));
        Assert.That(queue.Confirm("later-duplicate", "Z"), Is.False);
        queue.Clear(); Assert.That(queue.FirstPending, Is.Null);
    }

    [TestCase(17)]
    [TestCase(84)]
    [TestCase(239)]
    public void Stage1_ElevenBirthsScatterWithoutOverlapOrFixedIntervals(int seed)
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            EnhancementFairyStage1Host.Set(host.Habitat, "_birthRandom", new System.Random(seed));
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).OrderByDescending(x => x.Id).Take(11).ToArray();
            host.Restore(items.Select(x => new KeyValuePair<string, int>(x.Id, 1)));
            foreach (var item in items) EnhancementFairyAcquisitionEvents.PublishConfirmed("committed-" + item.Id, item.Id);
            host.Move(ERestaurantFloorType.Floor3);
            var order = new List<string>();
            var starts = new List<float>();
            int peak = 0;
            for (int frame = 0; frame < 360; frame++)
            {
                host.Advance(1f / 30f);
                var active = host.Habitat.ActiveBirthItemIds.ToArray();
                peak = Mathf.Max(peak, active.Length);
                Assert.That(active.Length, Is.LessThanOrEqualTo(5));
                for (int i = 0; i < active.Length; i++)
                {
                    if (!order.Contains(active[i])) { order.Add(active[i]); starts.Add(frame / 30f); }
                    Vector2 position = host.Habitat.GetBirthPosition(active[i]);
                    Assert.That(host.Habitat.Settings.BirthArea.Contains(position), Is.True);
                    Assert.That(position.x, Is.InRange(host.Habitat.Settings.SafeGroundArea.xMin, host.Habitat.Settings.SafeGroundArea.xMax), "Two airborne positions must not collapse onto a clamped landing edge");
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.IsPending(active[i]), Is.True, "Do not consume an in-flight birth");
                    for (int j = 0; j < i; j++)
                        Assert.That(Vector2.Distance(position, host.Habitat.GetBirthPosition(active[j])),
                            Is.GreaterThanOrEqualTo(host.Habitat.Settings.BirthMinSeparation - .001f));
                }
            }
            Assert.That(peak, Is.InRange(2, 5));
            CollectionAssert.AreEqual(items.Select(x => x.Id), order, "Waiting births are admitted in acquisition order without loss");
            Assert.That(starts.Skip(1).Select((time, i) => Mathf.RoundToInt((time - starts[i]) * 30)).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(11));
            Assert.That(EnhancementFairyAcquisitionEvents.PublishConfirmed("duplicate", items[0].Id), Is.False);
            host.Advance(4f);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(11));
        }
    }

    [Test]
    public void Stage1_ElevenConfirmedBirthsProduceTheFireworksEvidence()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            EnhancementFairyStage1Host.Set(host.Habitat, "_birthRandom", new System.Random(84));
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(11).ToArray();
            host.Restore(items.Select(x => new KeyValuePair<string, int>(x.Id, 1)));
            foreach (var item in items) EnhancementFairyAcquisitionEvents.PublishConfirmed("batch-" + item.Id, item.Id);
            host.Move(ERestaurantFloorType.Floor3); host.Advance(1f / 30f);
            host.RecordFireworks();
        }
    }

    [Test]
    public void Stage1_KitchenAlsoUsesItsVisibleWallWithoutWaitingForTheHall()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            host.Move(ERestaurantFloorType.Floor3); host.Move(RestaurantType.Kitchen);
            EnhancementFairyAcquisitionEvents.PublishConfirmed("kitchen-birth", item.Id);
            host.Advance(.05f);
            Assert.That(host.Habitat.ActiveBirthItemId, Is.EqualTo(item.Id));
            Vector3 point = host.Camera.WorldToViewportPoint(host.Floor.transform.TransformPoint(host.Habitat.GetBirthPosition(item.Id)));
            Assert.That(point.x, Is.InRange(.08f, .92f));
            Assert.That(point.y, Is.InRange(.06f, .88f));
            host.Advance(host.Habitat.Settings.BirthDuration + .1f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [TestCase("floor")]
    [TestCase("drag")]
    [TestCase("offscreen")]
    [TestCase("ui")]
    [TestCase("card")]
    public void Stage1_InterruptedBirthRemainsPendingAndRestartsInTheVisibleHall(string reason)
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            EnhancementFairyAcquisitionEvents.PublishConfirmed("committed", item.Id);
            host.Move(ERestaurantFloorType.Floor3);
            host.Advance(1.5f);
            Assert.That(host.Habitat.ActiveBirthItemId, Is.EqualTo(item.Id));
            Vector3 position = host.Camera.transform.position;
            if (reason == "floor") host.Move(ERestaurantFloorType.Floor2);
            if (reason == "drag") EnhancementFairyStage1Host.Set(host.Controller, "_isDraggingEnabled", true);
            if (reason == "offscreen") host.Camera.transform.position += Vector3.left * 120f;
            if (reason == "card") Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(host.Bodies().Single().bounds.center)), Is.True);
            if (reason == "ui") host.VerifyActualGachaOpenClose(() =>
            {
                host.Advance(4f);
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.IsPending(item.Id), Is.True);
                Assert.That(host.Habitat.ActiveBirthItemId, Is.Null);
            });
            else
            {
                host.Advance(4f);
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.IsPending(item.Id), Is.True);
                Assert.That(host.Habitat.ActiveBirthItemId, Is.Null);
            }
            if (reason == "floor") host.Move(ERestaurantFloorType.Floor3);
            if (reason == "drag") EnhancementFairyStage1Host.Set(host.Controller, "_isDraggingEnabled", false);
            if (reason == "offscreen") host.Camera.transform.position = position;
            if (reason == "card")
            {
                host.Habitat.CloseItemCard();
                Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
            }
            host.Advance(.1f);
            Assert.That(host.Habitat.ActiveBirthItemId, Is.EqualTo(item.Id));
            Assert.That(host.Habitat.BirthElapsed, Is.LessThan(.2f));
            host.Advance(host.Habitat.Settings.BirthDuration + .1f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Stage1_SceneReentryPreservesUnfinishedBirthAndAccountResetCancelsIt()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            EnhancementFairyAcquisitionEvents.PublishConfirmed("saved-before-scene-exit", item.Id);
            host.Move(ERestaurantFloorType.Floor3); host.Advance(.4f);
            host.ReloadStage(); host.Move(ERestaurantFloorType.Floor3);
            host.Advance(1f / 30f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.FirstPending, Is.EqualTo(item.Id));
            Assert.That(host.Habitat.ActiveBirthItemId, Is.EqualTo(item.Id));
            host.Advance(host.Habitat.Settings.BirthDuration + .1f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            host.ReloadStage(); host.Move(ERestaurantFloorType.Floor3);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero, "Consumed birth does not replay on load");
            EnhancementFairyAcquisitionEvents.ResetSession("different-offline-account");
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.ActiveBirthItemId, Is.Null);
        }
    }

    [Test]
    public void Stage1_ProductionBirthUsesVisibleWallPuffJumpAndThenFreeBehaviour()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            EnhancementFairyAcquisitionEvents.PublishConfirmed("confirmed-visual-evidence", item.Id);
            host.Move(ERestaurantFloorType.Floor3);
            Assert.That(host.Habitat.Settings.MaxConcurrentBirths, Is.EqualTo(5));
            Assert.That(host.Habitat.Settings.BirthDuration, Is.InRange(2f, 4f));
            host.Advance(1f / 30f);
            host.RecordBirth();
            Assert.That(host.Habitat.ActiveBirthItemId, Is.Null);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.GetActiveBrain(0).ElapsedSeconds, Is.GreaterThan(0f));
        }
    }
}
#endif
