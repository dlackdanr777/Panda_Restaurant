#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class FairyBirthInputTests
{
    [Test]
    public void Stage1_MouseBackgroundTapDoesNotRestartInFlightBirth()
    {
        VerifyBackgroundTap(false);
    }

    [Test]
    public void Stage1_TouchBackgroundTapDoesNotRestartInFlightBirth()
    {
        VerifyBackgroundTap(true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_FiftyBackgroundTapsAcrossEveryBirthPhaseCompleteOnceOnTime(bool touch)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3();
            var birth = new BirthState(host, sound.Items[0].Id);
            float lastElapsed = birth.Elapsed;
            float expectedRemaining = host.Habitat.Settings.BirthDuration - lastElapsed;
            double start = sound.Clock, completed = -1d;
            int cameraStarts = 0, taps = 0;
            host.Controller.OnStartMoveCameraHandler += () => cameraStarts++;
            using (var background = new OwnedDragBackground(host))
            {
                for (int frame = 0; frame < 300; frame++)
                {
                    if (frame % 6 == 0) PointerDown(host, touch, background.ScreenPosition);
                    if (frame % 6 == 2) PointerMove(host, touch, background.ScreenPosition, false);
                    if (frame % 6 == 3) { PointerUp(host, touch, background.ScreenPosition); taps++; }
                    sound.Advance(1f / 60f);
                    birth.AssertPath();
                    Assert.That(birth.Elapsed, Is.GreaterThanOrEqualTo(lastElapsed), "Birth elapsed regressed at frame " + frame);
                    lastElapsed = birth.Elapsed;
                    if (completed < 0d && !EnhancementFairyAcquisitionEvents.Queue.IsPending(birth.Id)) completed = sound.Clock - start;
                    Assert.That(host.Habitat.IsItemCardOpen, Is.False);
                }
            }
            TestContext.WriteLine("FIFTY_TAPS input=" + touch + "; taps=" + taps + "; seconds=" + (sound.Clock - start)
                + "; completedAt=" + completed + "; birthDuration=" + host.Habitat.Settings.BirthDuration);
            Assert.That(taps, Is.EqualTo(50));
            Assert.That(cameraStarts, Is.Zero);
            Assert.That(completed, Is.InRange(expectedRemaining - 2d / 60d, expectedRemaining + 2d / 60d));
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Stage1_StationaryHoldAndSubThresholdJitterDoNotInterruptBirth(bool touch, bool jitter)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            float before = birth.Elapsed;
            int cameraStarts = 0;
            host.Controller.OnStartMoveCameraHandler += () => cameraStarts++;
            using (var background = new OwnedDragBackground(host))
            {
                PointerDown(host, touch, background.ScreenPosition);
                for (int frame = 0; frame < 60; frame++)
                {
                    float offset = jitter ? DragThreshold(host) * .4f * (frame % 2 == 0 ? 1f : -1f) : 0f;
                    PointerMove(host, touch, background.ScreenPosition + Vector2.right * offset, jitter);
                    Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.False);
                    sound.Advance(1f / 60f); birth.AssertPath();
                }
                PointerUp(host, touch, background.ScreenPosition);
            }
            Assert.That(birth.Elapsed, Is.EqualTo(before + 1f).Within(.0001f));
            Assert.That(cameraStarts, Is.Zero);
            sound.Advance(host.Habitat.Settings.BirthDuration);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_BackgroundTapBetweenPopAndVoiceDoesNotLoseTheVoice(bool touch)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3();
            sound.Advance(host.Habitat.Settings.TrailSeconds + host.Habitat.Settings.GatherSeconds + .025f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1)); Assert.That(sound.Voices.Length, Is.Zero);
            using (var background = new OwnedDragBackground(host))
            {
                PointerDown(host, touch, background.ScreenPosition);
                PointerUp(host, touch, background.ScreenPosition);
            }
            sound.Advance(host.Habitat.Settings.VoiceDelay + .04f);
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
            sound.Advance(host.Habitat.Settings.BirthDuration);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Stage1_ActualShortDragResumesTheSameBirthAndOutAndBackAvoidsEmptySnap(bool touch, bool outAndBack)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            float before = birth.Elapsed;
            Vector3 cameraBefore = host.Camera.transform.position;
            int cameraStarts = 0;
            host.Controller.OnStartMoveCameraHandler += () => cameraStarts++;
            using (var background = new OwnedDragBackground(host))
            {
                Vector2 moved = background.ScreenPosition + Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f);
                PointerDown(host, touch, background.ScreenPosition);
                PointerMove(host, touch, moved, true);
                Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.True);
                Assert.That(host.Camera.transform.position, Is.Not.EqualTo(cameraBefore));
                sound.Advance(.5f);
                Assert.That(birth.Elapsed, Is.EqualTo(before)); birth.AssertPath();
                Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
                if (outAndBack) { PointerMove(host, touch, background.ScreenPosition, true); moved = background.ScreenPosition; }
                PointerUp(host, touch, moved);
            }
            Assert.That(cameraStarts, Is.EqualTo(outAndBack ? 0 : 1));
            if (!outAndBack) host.CompleteCameraTween();
            Assert.That(Vector3.Distance(host.Camera.transform.position, cameraBefore), Is.LessThan(.0001f));
            sound.Advance(.05f); birth.AssertPath();
            Assert.That(birth.Elapsed, Is.GreaterThanOrEqualTo(before));
            sound.Advance(host.Habitat.Settings.BirthDuration - before + .1f);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false, "floor")]
    [TestCase(true, "floor")]
    [TestCase(false, "room")]
    [TestCase(true, "room")]
    public void Stage1_ActualSwipeAwayAndBackPreservesPausedBirth(bool touch, string destination)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            float before = birth.Elapsed;
            Vector2 direction = destination == "floor" ? Vector2.up : Vector2.right;
            Swipe(host, touch, direction);
            Assert.That(destination == "floor" ? host.Controller.CurrentFloor == ERestaurantFloorType.Floor2
                : host.Controller.CurrentRestaurant == RestaurantType.Kitchen, Is.True);
            sound.Advance(1f); birth.AssertPath();
            Assert.That(birth.Elapsed, Is.EqualTo(before));
            Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
            Swipe(host, touch, -direction);
            Assert.That(host.Controller.CurrentFloor, Is.EqualTo(ERestaurantFloorType.Floor3));
            Assert.That(host.Controller.CurrentRestaurant, Is.EqualTo(RestaurantType.Hall));
            sound.Advance(.05f); birth.AssertPath();
            Assert.That(birth.Elapsed, Is.GreaterThanOrEqualTo(before));
            sound.Advance(host.Habitat.Settings.BirthDuration - before + .1f);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_TouchCanceledDoesNotTapAndOnlyAnActualDragSnaps(bool dragged)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            int starts = 0, taps = 0;
            host.Controller.OnStartMoveCameraHandler += () => starts++;
            host.Controller.OnWorldTapHandler += _ => { taps++; return false; };
            using (var background = new OwnedDragBackground(host))
            {
                Vector2 point = background.ScreenPosition;
                PointerDown(host, true, point);
                if (dragged) { point += Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f); PointerMove(host, true, point, true); }
                Touch(host, point, TouchPhase.Canceled);
            }
            Assert.That(taps, Is.Zero); Assert.That(starts, Is.EqualTo(dragged ? 1 : 0));
            Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDragging"), Is.False);
            Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.False);
            if (dragged) host.CompleteCameraTween();
            sound.Advance(.05f); birth.AssertPath();
            sound.Advance(host.Habitat.Settings.BirthDuration);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase("gacha")]
    [TestCase("shop")]
    public void Stage1_ActualNavigationPausesAndResumesTheRemainingBirth(string view)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            float before = birth.Elapsed;
            Action whileOpen = () =>
            {
                sound.Advance(1f); birth.AssertPath();
                Assert.That(birth.Elapsed, Is.EqualTo(before));
                Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            };
            if (view == "gacha") host.VerifyActualGachaOpenClose(whileOpen);
            else host.VerifyFairyInputShopOpenClose(whileOpen);
            Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
            sound.Advance(.05f); birth.AssertPath();
            Assert.That(birth.Elapsed, Is.GreaterThanOrEqualTo(before));
            sound.Advance(host.Habitat.Settings.BirthDuration - before + .1f);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_ActualPointerOpensExistingItemCardAndDismissRestoresInput(bool touch)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            foreach (var itemUi in host.SceneComponents<UIItemGacha>())
            {
                itemUi.gameObject.SetActive(false); // Owned Stage1 preview copy only; font cloning requires an inactive root.
                fonts.BindBeforeActivation(itemUi.gameObject);
            }
            host.Restore(sound.Items.Take(2).Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            sound.StartFloor3();
            EnhancementFairyAcquisitionEvents.PublishConfirmed("input-card-birth", sound.Items[0].Id);
            sound.Advance(.25f);
            var birth = new BirthState(host, sound.Items[0].Id);
            float before = birth.Elapsed;
            var body = host.Bodies().Single(value => value.transform.parent.name == "Fairy " + sound.Items[1].Id);
            Vector2 point = host.Camera.WorldToScreenPoint(body.bounds.center);
            using (var background = new OwnedDragBackground(host, point))
            {
                PointerDown(host, touch, point); PointerUp(host, touch, point);
                Assert.That(host.Habitat.IsItemCardOpen, Is.True);
                Assert.That(host.Controller.IsWorldInputBlocked, Is.True);
                sound.Advance(1f); birth.AssertPath();
                Assert.That(birth.Elapsed, Is.EqualTo(before));
                DismissActualItemCard(host);
            }
            Assert.That(host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
            sound.Advance(.05f); birth.AssertPath();
            sound.Advance(host.Habitat.Settings.BirthDuration - before + .1f);
            AssertSingleCompletion(host, sound);
            using (var background = new OwnedDragBackground(host))
            { PointerDown(host, touch, background.ScreenPosition); PointerUp(host, touch, background.ScreenPosition); }
        }
    }

    [Test]
    public void Stage1_FivePausedBirthsResumeBeforeNewQueuedItemsAndKeepTheCap()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(5); sound.StartFloor3(); sound.Advance(2f);
            Assert.That(host.Habitat.ActiveBirthCount, Is.EqualTo(5));
            var births = sound.Items.Take(5).Select(item => new BirthState(host, item.Id)).ToArray();
            float[] elapsed = births.Select(birth => birth.Elapsed).ToArray();
            double pauseAt = sound.Clock;
            host.Move(ERestaurantFloorType.Floor2);
            host.Restore(sound.Items.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            foreach (var item in sound.Items.Skip(5)) EnhancementFairyAcquisitionEvents.PublishConfirmed("queued-after-pause-" + item.Id, item.Id);
            sound.Advance(1f);
            for (int i = 0; i < births.Length; i++) { births[i].AssertPath(); Assert.That(births[i].Elapsed, Is.EqualTo(elapsed[i])); }
            host.Move(ERestaurantFloorType.Floor3); sound.Advance(1f / 60f);
            CollectionAssert.AreEquivalent(births.Select(birth => birth.Id), host.Habitat.ActiveBirthItemIds);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(5));
            for (int frame = 0; frame < 720; frame++)
            {
                sound.Advance(1f / 60f);
                Assert.That(host.Habitat.ActiveBirthCount, Is.LessThanOrEqualTo(5));
                foreach (var birth in births) birth.AssertPath();
            }
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(11));
            foreach (var item in sound.Items) Assert.That(EnhancementFairyAcquisitionEvents.PublishConfirmed("duplicate-" + item.Id, item.Id), Is.False);
            string[] expected = sound.Items.Select(item => item.Id).ToArray();
            var popClaims = ((HashSet<string>)EnhancementFairyStage1Host.Get(EnhancementFairyAcquisitionEvents.Queue, "_popPresented")).ToArray();
            var voiceClaims = ((HashSet<string>)EnhancementFairyStage1Host.Get(EnhancementFairyAcquisitionEvents.Queue, "_voicePresented")).ToArray();
            CollectionAssert.AreEquivalent(expected, popClaims, "Every completed birth claims its pop exactly once, even if the existing owner cap drops playback.");
            CollectionAssert.AreEquivalent(expected, voiceClaims, "Paused voice suppression and accepted playback share the existing single-claim policy.");
            Assert.That(sound.Pops.Length, Is.InRange(1, expected.Length));
            Assert.That(sound.Voices.Length, Is.InRange(1, expected.Length));
            int maxPops = 0, maxVoices = 0;
            foreach (var current in sound.Plays)
            {
                int concurrent = sound.Plays.Count(previous => ReferenceEquals(previous.Owner, current.Owner)
                    && previous.Time <= current.Time
                    && Math.Min(previous.Time + (previous.Clip.length - previous.StartSeconds) / previous.Pitch,
                        previous.Time <= pauseAt ? pauseAt : double.PositiveInfinity) > current.Time + .00001d);
                bool pop = current.Clip == host.Habitat.Settings.BirthPopClip;
                int limit = pop ? host.Habitat.Settings.MaxConcurrentPops : host.Habitat.Settings.MaxConcurrentVoices;
                Assert.That(concurrent, Is.LessThanOrEqualTo(Mathf.Clamp(limit, 1, 5)));
                if (pop) maxPops = Mathf.Max(maxPops, concurrent); else maxVoices = Mathf.Max(maxVoices, concurrent);
            }
            var evidence = new QueueSoundEvidence { itemIds = expected, popClaimIds = popClaims, voiceClaimIds = voiceClaims,
                completedBirths = host.Habitat.ArrivalPresentationCount, pending = EnhancementFairyAcquisitionEvents.Queue.PendingCount,
                acceptedPops = sound.Pops.Length, acceptedVoices = sound.Voices.Length, peakConcurrentPops = maxPops, peakConcurrentVoices = maxVoices,
                popLimit = host.Habitat.Settings.MaxConcurrentPops, voiceLimit = host.Habitat.Settings.MaxConcurrentVoices };
            string json = JsonUtility.ToJson(evidence, true);
            TestContext.WriteLine("PAUSED_QUEUE_SOUND_COUNTS " + json);
            string output = Environment.GetEnvironmentVariable("PANDA_FAIRY_INPUT_EVIDENCE_DIR");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "paused-queue-sound-counts.json"), json);
            }
        }
    }

    [Test]
    public void Stage1_ResetWhilePausedCancelsProgressBeforeAConfirmedNewSessionBirth()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(.5f);
            var oldBirth = new BirthState(host, sound.Items[0].Id);
            host.Move(ERestaurantFloorType.Floor2);
            EnhancementFairyAcquisitionEvents.ResetSession("input-paused-new-account");
            Assert.That(oldBirth.Elapsed, Is.Zero); Assert.That(oldBirth.Remaining, Is.Zero);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(EnhancementFairyAcquisitionEvents.PublishConfirmed("input-new-session-confirm", sound.Items[0].Id), Is.True);
            host.Move(ERestaurantFloorType.Floor3); sound.Advance(1f / 60f);
            Assert.That(host.Habitat.BirthElapsed, Is.LessThan(.05f));
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(2));
            sound.Advance(host.Habitat.Settings.BirthDuration + .1f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(sound.Pops.Length, Is.EqualTo(1)); Assert.That(sound.Voices.Length, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_FiftyTapNormalSpeedVideoAndOriginalAudioEvidence(bool touch)
    {
        string previous = EnhancementFairyStage1Host.EvidenceDirectory;
        string root = Environment.GetEnvironmentVariable("PANDA_FAIRY_INPUT_EVIDENCE_DIR");
        Assert.That(string.IsNullOrWhiteSpace(root), Is.False, "Use the scoped runner with its external evidence directory.");
        string output = Path.Combine(root, touch ? "touch-50-taps" : "mouse-50-taps");
        EnhancementFairyStage1Host.EvidenceDirectory = output;
        try
        {
            using (var host = new EnhancementFairyStage1Host())
            using (var sound = new FairyBirthSoundScope(host))
            {
                const int fps = 20, frames = 120;
                sound.Confirm(1); sound.StartFloor3();
                var birth = new BirthState(host, sound.Items[0].Id);
                var timeline = new List<string> { "frame,time,input,screenX,screenY,dragArmed,dragEnabled,birthElapsed,remaining,pending,pops,voices" };
                float lastElapsed = birth.Elapsed;
                float expectedRemaining = host.Habitat.Settings.BirthDuration - lastElapsed;
                int taps = 0, cameraStarts = 0;
                double completed = -1d;
                host.Controller.OnStartMoveCameraHandler += () => cameraStarts++;
                using (var background = new OwnedDragBackground(host))
                using (var audio = new Phase5AudioEvidence(sound.Sound, output, fps))
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        audio.BeginFrame(frame);
                        string input = "none";
                        if (frame < 100 && frame % 2 == 0) { PointerDown(host, touch, background.ScreenPosition); input = touch ? "Began" : "down"; }
                        if (frame < 100 && frame % 2 == 1) { PointerUp(host, touch, background.ScreenPosition); taps++; input = touch ? "Ended" : "up"; }
                        sound.Advance(1f / fps);
                        birth.AssertPath();
                        Assert.That(birth.Elapsed, Is.GreaterThanOrEqualTo(lastElapsed)); lastElapsed = birth.Elapsed;
                        if (completed < 0d && EnhancementFairyAcquisitionEvents.Queue.PendingCount == 0) completed = (frame + 1d) / fps;
                        timeline.Add(string.Join(",", new[] { frame.ToString(CultureInfo.InvariantCulture), ((double)frame / fps).ToString("F3", CultureInfo.InvariantCulture), input,
                            background.ScreenPosition.x.ToString("R", CultureInfo.InvariantCulture), background.ScreenPosition.y.ToString("R", CultureInfo.InvariantCulture),
                            EnhancementFairyStage1Host.Get(host.Controller, "_isDragging").ToString(), EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled").ToString(),
                            birth.Elapsed.ToString("R", CultureInfo.InvariantCulture), birth.Remaining.ToString("R", CultureInfo.InvariantCulture),
                            EnhancementFairyAcquisitionEvents.Queue.PendingCount.ToString(), sound.Pops.Length.ToString(), sound.Voices.Length.ToString() }));
                        host.Capture("frames/" + frame.ToString("D5") + ".png");
                    }
                    audio.Save((double)frames / fps);
                }
                File.WriteAllLines(Path.Combine(output, "input-timeline.csv"), timeline);
                File.WriteAllText(Path.Combine(output, "input-evidence.json"), JsonUtility.ToJson(new InputEvidence
                {
                    input = touch ? "touch" : "mouse", taps = taps, frames = frames, fps = fps, durationSeconds = (double)frames / fps,
                    completedAtSeconds = completed, birthDurationSeconds = host.Habitat.Settings.BirthDuration, cameraStarts = cameraStarts,
                    arrivals = host.Habitat.ArrivalPresentationCount, pops = sound.Pops.Length, voices = sound.Voices.Length,
                    width = 1400, height = 600, itemId = birth.Id
                }, true));
                Assert.That(taps, Is.EqualTo(50)); Assert.That(cameraStarts, Is.Zero);
                Assert.That(completed, Is.InRange(expectedRemaining - 2d / fps, expectedRemaining + 2d / fps));
                AssertSingleCompletion(host, sound);
            }
        }
        finally { EnhancementFairyStage1Host.EvidenceDirectory = previous; }
    }

    [Serializable] private sealed class InputEvidence
    {
        public string input, itemId;
        public int taps, frames, fps, width, height, cameraStarts, arrivals, pops, voices;
        public double durationSeconds, completedAtSeconds, birthDurationSeconds;
        public string method = "Actual isolated Stage1 Camera.Render and production ProcessMouseInputFrame/ProcessTouchInputFrame replay. Each 20 FPS frame advances 0.05 simulation seconds; encode at 20 FPS without acceleration. Temporary owned default-physics collider is deleted before the scene/dirty/Undo witness comparison.";
        public string limitation = "Deterministic Editor input replay, not device/Play or real-time performance evidence. Audio reconstructs original PCM using accepted runtime dispatch events, not native mixer/speaker capture.";
    }

    [Serializable] private sealed class QueueSoundEvidence
    {
        public string[] itemIds, popClaimIds, voiceClaimIds;
        public int completedBirths, pending, acceptedPops, acceptedVoices, peakConcurrentPops, peakConcurrentVoices, popLimit, voiceLimit;
        public string policy = "All 11 item IDs have one pop/voice presentation claim. Accepted runtime dispatches may be fewer because the existing concurrent-owner cap drops overlap, and a paused post-pop cue is never replayed. The cap and session claims are preserved.";
    }

    private static void AssertSingleCompletion(EnhancementFairyStage1Host host, FairyBirthSoundScope sound)
    {
        Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        Assert.That(sound.Pops.Length, Is.EqualTo(1)); Assert.That(sound.Voices.Length, Is.EqualTo(1));
    }

    internal static float DragThreshold(EnhancementFairyStage1Host host)
        => (float)EnhancementFairyStage1Host.Get(host.Controller, "_initialTouchThreshold") * (Screen.dpi > 0f ? Screen.dpi : 96f) / 2.54f;

    internal static void PointerDown(EnhancementFairyStage1Host host, bool touch, Vector2 position)
    {
        if (touch) Touch(host, position, TouchPhase.Began); else Mouse(host, position, true, true, false);
        Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDragging"), Is.True, "Production down must hit the owned default-physics background.");
        Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.False);
    }

    internal static void PointerMove(EnhancementFairyStage1Host host, bool touch, Vector2 position, bool moved)
    {
        if (touch) Touch(host, position, moved ? TouchPhase.Moved : TouchPhase.Stationary);
        else Mouse(host, position, false, true, false);
    }

    internal static void PointerUp(EnhancementFairyStage1Host host, bool touch, Vector2 position)
    { if (touch) Touch(host, position, TouchPhase.Ended); else Mouse(host, position, false, false, true); }

    private static void Swipe(EnhancementFairyStage1Host host, bool touch, Vector2 direction)
    {
        using (var background = new OwnedDragBackground(host))
        {
            float distance = (float)EnhancementFairyStage1Host.Get(host.Controller, "_moveThreshold") + 1f;
            float pixels = distance / (Time.fixedDeltaTime * (float)EnhancementFairyStage1Host.Get(host.Controller, "_dragSpeed"));
            Vector2 end = background.ScreenPosition + direction * pixels;
            PointerDown(host, touch, background.ScreenPosition); PointerMove(host, touch, end, true);
            Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.True);
            var sound = SoundManager.TryGetExistingInstance();
            var destinationFloor = Mathf.Abs(direction.y) > Mathf.Abs(direction.x)
                ? (ERestaurantFloorType)((int)host.Controller.CurrentFloor - (int)Mathf.Sign(direction.y)) : host.Controller.CurrentFloor;
            EnhancementFairyStage1Host.Set(sound, "_effectType", sound.GetHallEffectType(destinationFloor, host.Controller.CurrentRestaurant));
            if (direction.x != 0f)
            {
                // MoveCamera(floor, room) first sets the floor using the old room and
                // then changes the room. The second ambient transition is real, but its
                // coroutine cannot run on the deliberately inactive isolated services.
                // Expect exactly that one diagnostic; this is not a live mixer test.
                LogAssert.Expect(LogType.Error, "Coroutine couldn't be started because the the game object 'Isolated Stage1 fairy services' is inactive!");
            }
            PointerUp(host, touch, end);
            Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isMoveAction"), Is.True);
            host.CompleteCameraTween();
        }
    }

    private static void DismissActualItemCard(EnhancementFairyStage1Host host)
    {
        var root = (GameObject)EnhancementFairyStage1Host.Get(host.Habitat, "_cardCanvas");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = host.Camera;
        canvas.planeDistance = 1f; canvas.overrideSorting = true; canvas.sortingLayerName = "UI";
        int previousMask = host.Camera.cullingMask;
        try
        {
            // PreviewScenes do not submit overlay canvases. Render the actual card's
            // graphics through this camera before querying their native raycaster.
            host.Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
            host.Capture("input-card-dismiss.png");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(host.SceneComponents<EventSystem>().Single()) { position = new Vector2(5f, 5f), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var dismiss = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(dismiss.name, Is.EqualTo("Dismiss Background"));
            ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerClickHandler);
        }
        finally { host.Camera.cullingMask = previousMask; }
    }

    internal sealed class BirthState
    {
        internal readonly string Id;
        internal readonly object View;
        private readonly EnhancementFairyStage1Host _host;
        private readonly Transform _root;
        private readonly Vector2 _position, _start, _control;
        internal float Elapsed => (float)EnhancementFairyStage1Host.Get(View, "BirthElapsed");
        internal float Remaining => (float)EnhancementFairyStage1Host.Get(View, "ArrivalRemaining");
        internal BirthState(EnhancementFairyStage1Host host, string id)
        {
            _host = host; Id = id;
            View = ((IList)EnhancementFairyStage1Host.Get(host.Habitat, "_views")).Cast<object>()
                .Single(value => ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(value, "Brain")).ItemId == id);
            _root = (Transform)EnhancementFairyStage1Host.Get(View, "Root");
            _position = (Vector2)EnhancementFairyStage1Host.Get(View, "BirthPosition");
            _start = (Vector2)EnhancementFairyStage1Host.Get(View, "TrailStart");
            _control = (Vector2)EnhancementFairyStage1Host.Get(View, "TrailControl");
        }
        internal void AssertPath()
        {
            Assert.That(((IList)EnhancementFairyStage1Host.Get(_host.Habitat, "_views")).Contains(View), Is.True);
            Assert.That((Transform)EnhancementFairyStage1Host.Get(View, "Root"), Is.SameAs(_root));
            Assert.That((Vector2)EnhancementFairyStage1Host.Get(View, "BirthPosition"), Is.EqualTo(_position));
            Assert.That((Vector2)EnhancementFairyStage1Host.Get(View, "TrailStart"), Is.EqualTo(_start));
            Assert.That((Vector2)EnhancementFairyStage1Host.Get(View, "TrailControl"), Is.EqualTo(_control));
        }
    }

    private static void VerifyBackgroundTap(bool touch)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1);
            sound.StartFloor3();
            sound.Advance(.25f);
            Assert.That(host.Habitat.ActiveBirthCount, Is.EqualTo(1));
            Assert.That(host.Habitat.BirthElapsed, Is.GreaterThan(.2f));
            Assert.That(host.Bodies()[0].color.a, Is.Zero, "The item body is still hidden during the flight, so this is a background tap.");

            using (var background = new OwnedDragBackground(host))
            {
                var births = (IList)EnhancementFairyStage1Host.Get(host.Habitat, "_birthViews");
                object birth = births[0];
                float beforeElapsed = host.Habitat.BirthElapsed;
                Vector2 beforePosition = (Vector2)EnhancementFairyStage1Host.Get(birth, "BirthPosition");
                Vector2 beforeStart = (Vector2)EnhancementFairyStage1Host.Get(birth, "TrailStart");
                Vector2 beforeControl = (Vector2)EnhancementFairyStage1Host.Get(birth, "TrailControl");
                string[] beforeIdentity = host.Identity();
                Vector3 beforeCamera = host.Camera.transform.position;
                int cameraStarts = 0;
                Action onStart = () => cameraStarts++;
                host.Controller.OnStartMoveCameraHandler += onStart;
                try
                {
                    if (touch) Touch(host, background.ScreenPosition, TouchPhase.Began);
                    else Mouse(host, background.ScreenPosition, true, true, false);
                    Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDragging"), Is.True,
                        "Actual pointer-down and Physics.Raycast must arm drag detection; never set the flag in the fixture.");
                    Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.False);

                    if (touch) Touch(host, background.ScreenPosition, TouchPhase.Stationary);
                    else Mouse(host, background.ScreenPosition, false, true, false);
                    Assert.That(EnhancementFairyStage1Host.Get(host.Controller, "_isDraggingEnabled"), Is.False,
                        "A stationary press has not crossed the production drag threshold.");

                    if (touch) Touch(host, background.ScreenPosition, TouchPhase.Ended);
                    else Mouse(host, background.ScreenPosition, false, false, true);
                    float elapsedAtRelease = host.Habitat.BirthElapsed;
                    bool birthStillActiveAtRelease = births.Contains(birth);

                    // If the regression starts a spurious tween, finish that actual tween so
                    // this case also exposes the newly sampled trail when the birth restarts.
                    if ((bool)EnhancementFairyStage1Host.Get(host.Controller, "_isMoveAction")) host.CompleteCameraTween();
                    sound.Advance(1f / 60f);
                    TestContext.WriteLine("FAIRY_INPUT_TRACE input=" + (touch ? "touch" : "mouse")
                        + "; cameraStarts=" + cameraStarts
                        + "; birthActiveAtRelease=" + birthStillActiveAtRelease
                        + "; birthActiveNextFrame=" + births.Contains(birth)
                        + "; elapsedBefore=" + beforeElapsed.ToString("R")
                        + "; elapsedAtRelease=" + elapsedAtRelease.ToString("R")
                        + "; elapsedNextFrame=" + host.Habitat.BirthElapsed.ToString("R"));
                    TestContext.WriteLine("FAIRY_INPUT_PATH position=" + beforePosition.ToString("R") + " -> "
                        + ((Vector2)EnhancementFairyStage1Host.Get(birth, "BirthPosition")).ToString("R")
                        + "; trailStart=" + beforeStart.ToString("R") + " -> "
                        + ((Vector2)EnhancementFairyStage1Host.Get(birth, "TrailStart")).ToString("R")
                        + "; trailControl=" + beforeControl.ToString("R") + " -> "
                        + ((Vector2)EnhancementFairyStage1Host.Get(birth, "TrailControl")).ToString("R"));
                    TestContext.WriteLine("FAIRY_INPUT_IDENTITY before=" + string.Join(",", beforeIdentity)
                        + "; after=" + string.Join(",", host.Identity())
                        + "; camera=" + beforeCamera.ToString("R") + " -> " + host.Camera.transform.position.ToString("R")
                        + "; arrivalCount=" + host.Habitat.ArrivalPresentationCount
                        + "; pendingCount=" + EnhancementFairyAcquisitionEvents.Queue.PendingCount
                        + "; soundPlays=" + sound.Plays.Count);
                    Assert.That(cameraStarts, Is.Zero, "Background release without a drag must not request a camera move.");
                    Assert.That(birthStillActiveAtRelease, Is.True, "Release must not remove the existing in-flight birth.");
                    Assert.That(elapsedAtRelease, Is.EqualTo(beforeElapsed).Within(.00001f), "Release must not reset birth elapsed time.");
                    Assert.That(host.Habitat.BirthElapsed, Is.GreaterThan(beforeElapsed), "The existing birth should continue on the next frame.");
                    Assert.That(births.Contains(birth), Is.True);
                    Assert.That((Vector2)EnhancementFairyStage1Host.Get(birth, "BirthPosition"), Is.EqualTo(beforePosition));
                    Assert.That((Vector2)EnhancementFairyStage1Host.Get(birth, "TrailStart"), Is.EqualTo(beforeStart));
                    Assert.That((Vector2)EnhancementFairyStage1Host.Get(birth, "TrailControl"), Is.EqualTo(beforeControl));
                    CollectionAssert.AreEqual(beforeIdentity, host.Identity());
                    Assert.That(host.Camera.transform.position, Is.EqualTo(beforeCamera));
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
                    Assert.That(sound.Plays.Count, Is.Zero, "This early flight has not reached either sound cue.");
                    Assert.That(host.Habitat.IsItemCardOpen, Is.False);
                }
                finally { host.Controller.OnStartMoveCameraHandler -= onStart; }
            }
        }
    }

    private static void Mouse(EnhancementFairyStage1Host host, Vector2 position, bool down, bool held, bool up)
        => EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", position, down, held, up);

    private static void Touch(EnhancementFairyStage1Host host, Vector2 position, TouchPhase phase)
        => EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessTouchInputFrame", position, phase);

    /// <summary>
    /// Physics.Raycast queries default physics, while Stage1 is safely loaded in a PreviewScene.
    /// Supply an owned, temporary collider in the default physics scene; the Stage1 artwork
    /// remains in its PreviewScene. Only this collider is removed on disposal, and the host's
    /// scene/dirty/Undo witness verifies restoration. No scene is saved, discarded or closed.
    /// </summary>
    internal sealed class OwnedDragBackground : IDisposable
    {
        private Scene _scene;
        private GameObject _background;
        internal Vector2 ScreenPosition { get; }

        internal OwnedDragBackground(EnhancementFairyStage1Host host, Vector2? position = null)
        {
            ScreenPosition = position ?? (Vector2)host.Camera.ViewportToScreenPoint(new Vector3(.12f, .88f, 1f));
            try
            {
                _scene = SceneManager.GetActiveScene();
                Assert.That(_scene.GetPhysicsScene(), Is.EqualTo(Physics.defaultPhysicsScene));
                int detectionMask = ((LayerMask)EnhancementFairyStage1Host.Get(host.Controller, "_draggableLayerMask")).value
                    & ~((LayerMask)EnhancementFairyStage1Host.Get(host.Controller, "_ignoredLayer")).value;
                Assert.That(detectionMask, Is.Not.Zero);
                int layer = 0;
                while ((detectionMask & (1 << layer)) == 0) layer++;

                // Match the existing production conversion exactly, including its second
                // ScreenToWorldPoint conversion. Changing that behavior is outside this test.
                Vector2 world = host.Camera.ScreenToWorldPoint(ScreenPosition);
                Vector3 rayOrigin = host.Camera.ScreenToWorldPoint(new Vector3(world.x, world.y, host.Camera.nearClipPlane));
                Vector3 direction = host.Camera.transform.forward;
                _background = new GameObject("Owned draggable background collider");
                SceneManager.MoveGameObjectToScene(_background, _scene);
                _background.layer = layer;
                _background.transform.position = rayOrigin + direction;
                var collider = _background.AddComponent<BoxCollider>();
                collider.size = Vector3.one * .5f;
                Physics.SyncTransforms();
                Assert.That(Physics.Raycast(rayOrigin, direction, out RaycastHit hit, 30f, detectionMask), Is.True);
                Assert.That(hit.collider, Is.SameAs(collider), "The real default Physics query must hit this fixture's collider.");
                Assert.That(EnhancementFairyStage1Host.Invoke(host.Controller, "IsTouchingDraggableSprite", world), Is.True);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_background != null) Object.DestroyImmediate(_background);
            _background = null;
            _scene = default;
            Physics.SyncTransforms();
        }
    }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal void VerifyFairyInputShopOpenClose(Action whileOpen)
    {
        var shop = SceneComponents<UIRestaurantAdmin>().Single();
        var nav = shop.GetComponentInParent<MobileUINavigation>(true);
        var coordinator = SceneComponents<UINavigationCoordinator>().Single();
        var main = (MainScene)Get(shop, "_mainScene");
        var services = _services.AddComponent<GameManager>();
        Replace(typeof(GameManager), "_instance", services);
        var backgrounds = (ScrollingImage[])Get(shop, "_scrollImages");
        foreach (var background in backgrounds) background.Init();
        shop.gameObject.SetActive(false);
        var staff = (UIStaff)Get(shop, "_staffUI"); staff.gameObject.SetActive(false);
        shop.ConfigureEditorOfflineNavigation(staff);
        Set(shop, "_tabs", new[] { (UIRestaurantAdminTab)Get(shop, "_furnitureTab"), (UIRestaurantAdminTab)Get(shop, "_staffTab"),
            (UIRestaurantAdminTab)Get(shop, "_recipeTab"), (UIRestaurantAdminTab)Get(shop, "_kitchenTab") });
        Set(shop, "_tmpScale", ((GameObject)Get(shop, "_mainUI")).transform.localScale);
        using (var fonts = new StaffGachaOfflineFonts())
        {
            fonts.BindBeforeActivation(shop.gameObject); shop.ViewInit(nav);
            Set(nav, "_viewDic", ((MobileViewDicStruct[])Get(nav, "_uiViews")).ToDictionary(entry => entry.Name, entry => entry.UIView));
            Invoke(coordinator, "Init");
            Set(_sound, "_currentBackgroundClip", Get(shop, "_shopMusic"));
            nav.Push("RestaurantAdminUI"); CompleteTweens(shop.gameObject);
            Assert.That(shop.IsReadyForDetail, Is.True);
            Assert.That(coordinator.GetOpenViewCount(), Is.EqualTo(1));
            Assert.That(Habitat.IsVisible, Is.False);
            whileOpen?.Invoke();
            Set(_sound, "_currentBackgroundClip", Get(main, "_mainSceneMusic"));
            nav.Pop(); CompleteTweens(shop.gameObject);
            Assert.That(coordinator.GetOpenViewCount(), Is.Zero);
            var resume = (IEnumerator)Invoke(Habitat, "ResumeAfterClosingViews");
            int steps = 0; while (resume.MoveNext() && ++steps < 4) { }
            Assert.That(steps, Is.LessThan(4)); Assert.That(Habitat.IsVisible, Is.True);
            Assert.That(Controller.IsWorldInputBlocked, Is.False);
            foreach (var background in backgrounds)
            {
                var material = (Material)Get(background, "_material");
                if (material != null) Object.DestroyImmediate(material);
            }
        }
    }
}
#endif
