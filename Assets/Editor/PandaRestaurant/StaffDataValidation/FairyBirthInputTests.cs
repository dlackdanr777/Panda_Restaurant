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
    [TestCase(false)] [TestCase(true)]
    public void Stage1_FairyCardBackgroundClosesOnceWithOptionalButtonSoundAndSilentProgrammaticClose(bool muted)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            // The existing Stage1 memory host intentionally skips SoundManager.Init.
            // Bind the same authored clip used by that Init, without initializing any manager/account.
            var clips = new AudioClip[(int)SoundEffectType.Length];
            clips[(int)SoundEffectType.ButtonClickSound] = Resources.Load<AudioClip>("Audio/ButtonClickSound");
            EnhancementFairyStage1Host.Set(sound.Sound, "_clips", clips);
            var clickClip = sound.Sound.GetEffectClip(SoundEffectType.ButtonClickSound);
            Assert.That(clickClip, Is.Not.Null);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EnhancementFairyStage1Host.Set(sound.Sound, "_soundEffectVolume", 1f);
                Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.True);
                sound.Advance(host.Habitat.Settings.TouchDuration + .1f);
                Assert.That(host.Habitat.IsItemCardOpen, Is.True);
                var popup = (GachaResultCardPopup)EnhancementFairyStage1Host.Get(host.Habitat, "_itemPopup");
                var canvas = (GameObject)EnhancementFairyStage1Host.Get(host.Habitat, "_cardCanvas");
                var background = canvas.GetComponentsInChildren<Button>(true).Single(x => x.name == "Dismiss Background");
                var content = canvas.GetComponentsInChildren<Button>(true).Single(x => x.name == "Centered Card");
                int before = sound.Plays.Count;
                content.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.True); Assert.That(sound.Plays.Count, Is.EqualTo(before));
                EnhancementFairyStage1Host.Set(sound.Sound, "_soundEffectVolume", muted ? 0f : 1f);
                if (cycle == 1) host.Habitat.CloseItemCard();
                else background.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.False); Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
                int expected = before + (!muted && cycle != 1 ? 1 : 0);
                Assert.That(sound.Plays.Count, Is.EqualTo(expected));
                if (expected > before) Assert.That(sound.Plays.Last().Clip, Is.SameAs(clickClip));
                background.onClick.Invoke(); popup.Hide();
                Assert.That(sound.Plays.Count, Is.EqualTo(expected), "Hidden/automatic dismissals cannot replay the click.");
                sound.Advance(.6f);
            }
            // The shared popup has no sound policy unless its owner subscribes.
            var parent = (GameObject)EnhancementFairyStage1Host.Get(host.Habitat, "_cardCanvas");
            var template = (UIGachaCard)EnhancementFairyStage1Host.Get(host.Habitat, "_cardTemplate");
            using (var shared = new GachaResultCardPopup(parent.transform, template))
            {
                var catalog = (Dictionary<string, GachaItemData>)EnhancementFairyStage1Host.Get(host.Habitat, "_catalog");
                shared.ShowItem(catalog.Values.First());
                int before = sound.Plays.Count;
                parent.GetComponentsInChildren<Button>(false).Single(x => x.name == "Dismiss Background").onClick.Invoke();
                Assert.That(shared.IsOpen, Is.False); Assert.That(sound.Plays.Count, Is.EqualTo(before));
            }
        }
    }

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

    [Test]
    public void Stage1_MultitouchCancelsMovedBackgroundUntilAllFingersRelease()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.StartFloor3();
            using (var background = new OwnedDragBackground(host))
            {
                Vector3 origin = host.Camera.transform.position;
                var floor = host.Controller.CurrentFloor;
                var room = host.Controller.CurrentRestaurant;
                EnhancementFairyStage1Host.Set(host.Controller, "_moveThreshold", .01f);
                int starts = 0, ends = 0, taps = 0;
                host.Controller.OnStartMoveCameraHandler += () => starts++;
                host.Controller.OnEndMoveCameraHandler += (_, __) => ends++;
                host.Controller.OnWorldTapHandler += _ => { taps++; return false; };
                Vector2 point = background.ScreenPosition;
                Vector2 moved = point + Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f);
                TouchCountFrame(host, 1, 7, point, TouchPhase.Began);
                TouchCountFrame(host, 1, 7, moved, TouchPhase.Moved);
                Assert.That(host.Camera.transform.position, Is.Not.EqualTo(origin));
                TouchCountFrame(host, 2, 8, moved, TouchPhase.Began);
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(host.Controller.CurrentFloor, Is.EqualTo(floor));
                Assert.That(host.Controller.CurrentRestaurant, Is.EqualTo(room));
                TouchCountFrame(host, 1, 7, moved, TouchPhase.Ended);
                host.CompleteCameraTween();
                TouchCountFrame(host, 1, 8, point, TouchPhase.Began);
                TouchCountFrame(host, 1, 8, point, TouchPhase.Ended);
                TouchCountFrame(host, 0, 0, point, TouchPhase.Ended);
                TouchCountFrame(host, 1, 7, point, TouchPhase.Ended);
                Assert.That(taps, Is.Zero);
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(ends, Is.EqualTo(1));
                Assert.That(Vector3.Distance(host.Camera.transform.position, origin), Is.LessThan(.0001f));
                TouchCountFrame(host, 1, 9, point, TouchPhase.Began);
                TouchCountFrame(host, 1, 9, point, TouchPhase.Ended);
                Assert.That(taps, Is.EqualTo(1));
                Assert.That(starts, Is.EqualTo(1));
            }
        }
    }

    [TestCase("pause")]
    [TestCase("focus")]
    [TestCase("disable")]
    public void Stage1_InterruptedBackgroundRestoresOnlyAfterResume(string interruption)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.StartFloor3();
            using (var background = new OwnedDragBackground(host))
            {
                Vector3 origin = host.Camera.transform.position;
                int starts = 0;
                host.Controller.OnStartMoveCameraHandler += () => starts++;
                Vector2 point = background.ScreenPosition;
                Touch(host, point, TouchPhase.Began);
                Touch(host, point + Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f), TouchPhase.Moved);
                Assert.That(host.Camera.transform.position, Is.Not.EqualTo(origin));
                if (interruption == "disable") host.Controller.enabled = false;
                else EnhancementFairyStage1Host.Invoke(host.Controller,
                    interruption == "pause" ? "OnApplicationPause" : "OnApplicationFocus", interruption == "pause");
                EnhancementFairyStage1Host.Invoke(host.Controller, "RestoreCanceledCameraDrag");
                Assert.That(starts, Is.Zero);
                if (interruption == "disable") host.Controller.enabled = true;
                else EnhancementFairyStage1Host.Invoke(host.Controller,
                    interruption == "pause" ? "OnApplicationPause" : "OnApplicationFocus", interruption != "pause");
                EnhancementFairyStage1Host.Invoke(host.Controller, "RestoreCanceledCameraDrag");
                Assert.That(starts, Is.EqualTo(1));
                host.CompleteCameraTween();
                Assert.That(Vector3.Distance(host.Camera.transform.position, origin), Is.LessThan(.0001f));
            }
        }
    }

    [Test]
    public void Stage1_ExplicitFloorMoveSupersedesDeferredBackgroundRestore()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.StartFloor3();
            using (var background = new OwnedDragBackground(host))
            {
                int starts = 0;
                host.Controller.OnStartMoveCameraHandler += () => starts++;
                Vector2 point = background.ScreenPosition;
                Touch(host, point, TouchPhase.Began);
                Touch(host, point + Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f), TouchPhase.Moved);
                EnhancementFairyStage1Host.Invoke(host.Controller, "OnApplicationPause", true);
                host.Move(ERestaurantFloorType.Floor2);
                EnhancementFairyStage1Host.Invoke(host.Controller, "OnApplicationPause", false);
                EnhancementFairyStage1Host.Invoke(host.Controller, "RestoreCanceledCameraDrag");
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(host.Controller.CurrentFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
            }
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
                Assert.That(host.Habitat.IsItemCardPending, Is.True);
                Assert.That(host.Habitat.IsItemCardOpen, Is.False);
                sound.Advance(host.Habitat.Settings.TouchDuration + 1f / 60f);
                Assert.That(host.Habitat.IsItemCardOpen, Is.True);
                Assert.That(host.Controller.IsWorldInputBlocked, Is.True);
                before = birth.Elapsed; // The original birth pauses only when the card actually opens.
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

    [TestCase(30)]
    [TestCase(60)]
    public void Stage1_FairyTouchComposesSquishAtTheFeetThenOpensOneExistingCard(int fps)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            object view = TouchView(host, body);
            var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
            var ownership = UserInfo.GetGiveGachaItemCountDic().ToArray();
            int poolSize = sound.Sound.GetComponentsInChildren<AudioSource>(true).Length;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Vector2 point = host.Camera.WorldToScreenPoint(body.bounds.center);
                double acceptedAt = sound.Clock;
                Assert.That(host.Habitat.TryOpenItemCard(point), Is.True);
                Assert.That(host.Habitat.IsItemCardPending, Is.True);
                Assert.That(host.Habitat.IsItemCardOpen, Is.False);
                Assert.That(host.Controller.IsWorldInputBlocked, Is.True);
                Assert.That(host.Habitat.TryOpenItemCard(point), Is.False, "Repeated taps cannot create a second pending card or sound.");
                float maxX = 1f, minY = 1f, elapsed = 0f;
                while (!host.Habitat.IsItemCardOpen && elapsed < .4f)
                {
                    sound.Clock += 1d / fps;
                    host.Habitat.AdvancePreview(1f / fps);
                    elapsed += 1f / fps;
                    Vector2 multiplier = TouchMultiplier(view, body);
                    maxX = Mathf.Max(maxX, multiplier.x); minY = Mathf.Min(minY, multiplier.y);
                    Assert.That(multiplier.x, Is.InRange(1f - .0001f, 1.05f + .0001f));
                    Assert.That(multiplier.y, Is.InRange(.9f - .0001f, 1f + .0001f));
                    AssertMeshGroundContact(view, body);
                    Assert.That((Vector2)body.transform.parent.localPosition, Is.EqualTo(brain.GroundPosition),
                        "Touch feedback must not move the fairy's ground root away from the existing brain.");
                    if (elapsed + .0001f < host.Habitat.Settings.TouchDuration)
                        Assert.That(host.Habitat.IsItemCardOpen, Is.False, "The card must not cover the press/release feedback.");
                }
                Assert.That(maxX, Is.GreaterThan(1.045f));
                Assert.That(minY, Is.LessThan(.91f));
                Assert.That(elapsed, Is.InRange(host.Habitat.Settings.TouchDuration - .0001f,
                    host.Habitat.Settings.TouchDuration + 1f / fps + .0001f));
                Assert.That(host.Habitat.IsItemCardPending, Is.False);
                Assert.That(host.Habitat.IsItemCardOpen, Is.True);
                Assert.That(TouchMultiplier(view, body), Is.EqualTo(Vector2.one));
                Assert.That(sound.Plays.Count, Is.EqualTo(cycle + 1));
                var played = sound.Plays.Last();
                Assert.That(played.Clip, Is.SameAs(host.Habitat.Settings.BubbleClip));
                Assert.That(played.Volume, Is.EqualTo(host.Habitat.Settings.BubbleVolume).Within(.0001f));
                Assert.That(played.Pitch, Is.EqualTo(host.Habitat.Settings.BubblePitch).Within(.0001f));
                Assert.That(played.Time, Is.EqualTo(acceptedAt).Within(.00001d), "One bubble starts with the accepted squish, not a second sound when the card opens.");
                Assert.That(played.Clip.length / played.Pitch, Is.InRange(.15f, .30f), "The softened pitch keeps this a short touch response.");
                Assert.That(sound.Pops.Length + sound.Voices.Length, Is.Zero, "Ordinary touches must not request acquisition sounds.");
                Assert.That(host.Floor.GetComponentsInChildren<UIGachaCard>(true).Length, Is.EqualTo(1));
                host.Habitat.CloseItemCard();
                Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
                TestContext.WriteLine("FAIRY_TOUCH fps=" + fps + "; cycle=" + cycle + "; duration=" + elapsed
                    + "; maximumX=" + maxX + "; minimumY=" + minY + "; restored=" + TouchMultiplier(view, body));
            }
            CollectionAssert.AreEquivalent(ownership, UserInfo.GetGiveGachaItemCountDic());
            Assert.That(sound.Sound.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(poolSize));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_FairyPointerDragOrCancelDoesNotStartCardFeedbackOrPlaySound(bool canceledTouch)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            object view = TouchView(host, body);
            Vector2 point = host.Camera.WorldToScreenPoint(body.bounds.center);
            Vector3 camera = host.Camera.transform.position;
            string itemId = ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ItemId;
            Assert.That(host.Habitat.Settings.Physics.Enabled, Is.True, "This regression must exercise the enabled two-room physics path.");
            using (var background = new OwnedDragBackground(host, point))
            {
                PointerDown(host, canceledTouch, point);
                Assert.That(host.Habitat.PendingGestureItemId, Is.EqualTo(itemId));
                Assert.That(EnhancementFairyStage1Host.Get(host.Habitat, "_touchView"), Is.Null);
                if (canceledTouch) Touch(host, point, TouchPhase.Canceled);
                else
                {
                    Vector2 moved = point + Vector2.right * Mathf.Max(30f, DragThreshold(host) * 2f);
                    PointerMove(host, false, moved, true);
                    Assert.That(host.Habitat.DraggedFairyItemId, Is.EqualTo(itemId));
                    Assert.That(host.Habitat.TryGetPhysicsBody(itemId, out var physics), Is.True);
                    Assert.That(physics.Mode, Is.EqualTo(EnhancementFairyPhysicsSolver.BodyMode.Dragging));
                    Assert.That(EnhancementFairyStage1Host.Get(host.Habitat, "_touchView"), Is.Null,
                        "Dragging must not start the separate item-card press animation.");
                    PointerMove(host, false, point, true);
                    PointerUp(host, false, point);
                    Assert.That(physics.Mode, Is.EqualTo(EnhancementFairyPhysicsSolver.BodyMode.Physics));
                }
            }
            Assert.That(host.Habitat.PhysicalFairyCount, Is.EqualTo(canceledTouch ? 0 : 1));
            Assert.That(host.Camera.transform.position, Is.EqualTo(camera), "A fairy-owned press must never pan the camera.");
            sound.Advance(.3f);
            Assert.That(host.Habitat.IsItemCardPending || host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(EnhancementFairyStage1Host.Get(host.Habitat, "_touchView"), Is.Null);
            Assert.That(sound.Plays, Is.Empty);
            Assert.That(TouchMultiplier(view, body), Is.EqualTo(Vector2.one));
        }
    }

    [TestCase("close")]
    [TestCase("floor")]
    [TestCase("ui")]
    [TestCase("disable")]
    [TestCase("ownership")]
    public void Stage1_PendingFairyTouchRestoresBeforeCancellationAndNeverOpensLater(string reason)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            object view = TouchView(host, body);
            Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.True);
            sound.Advance(.05f);
            Assert.That(TouchMultiplier(view, body).y, Is.LessThan(1f));
            if (reason == "close") host.Habitat.CloseItemCard();
            if (reason == "floor") host.Move(ERestaurantFloorType.Floor2);
            if (reason == "ui") host.VerifyActualGachaOpenClose();
            if (reason == "disable") EnhancementFairyStage1Host.Invoke(host.Habitat, "OnDisable");
            if (reason == "ownership") host.Restore(Array.Empty<KeyValuePair<string, int>>());
            Assert.That(host.Habitat.IsItemCardPending, Is.False);
            Assert.That(host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
            if (reason != "ownership") Assert.That(TouchMultiplier(view, body), Is.EqualTo(Vector2.one));
            if (reason == "floor") host.Move(ERestaurantFloorType.Floor3);
            sound.Advance(.4f);
            Assert.That(host.Habitat.IsItemCardPending || host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(sound.Plays.Count, Is.EqualTo(1), "Cancellation must not replay the bubble or open another card.");
        }
    }

    [TestCase("_masterVolume")]
    [TestCase("_soundEffectVolume")]
    public void Stage1_MutedFairyTouchKeepsVisualAndCardWithoutAudio(string mutedField)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            EnhancementFairyStage1Host.Set(sound.Sound, mutedField, 0f);
            Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.True);
            sound.Advance(.05f);
            Assert.That(TouchMultiplier(TouchView(host, body), body).y, Is.LessThan(1f));
            sound.Advance(host.Habitat.Settings.TouchDuration);
            Assert.That(host.Habitat.IsItemCardOpen, Is.True);
            Assert.That(sound.Plays, Is.Empty);
            EnhancementFairyStage1Host.Set(sound.Sound, mutedField, 1f);
            sound.Advance(.2f);
            Assert.That(sound.Plays, Is.Empty, "Unmuting cannot replay a muted touch.");
        }
    }

    [Test]
    public void Stage1_FairyBubbleIntervalLimitsOnlyAudioAndNotTheNextValidTouch()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            var original = host.Habitat.Settings;
            var settings = Object.Instantiate(original);
            try
            {
                settings.MinimumBubbleInterval = .5f;
                EnhancementFairyStage1Host.Set(host.Habitat, "_settings", settings);
                for (int tap = 0; tap < 3; tap++)
                {
                    Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.True);
                    Assert.That(host.Habitat.IsItemCardPending, Is.True);
                    Assert.That(sound.Plays.Count, Is.EqualTo(tap < 2 ? 1 : 2));
                    host.Habitat.CloseItemCard();
                    sound.Advance(tap == 0 ? .1f : .5f);
                }
            }
            finally
            {
                EnhancementFairyStage1Host.Set(host.Habitat, "_settings", original);
                Object.DestroyImmediate(settings);
            }
        }
    }

    [Test]
    public void Stage1_VisibleAcquisitionBirthCannotBeInterruptedByFairyTouch()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3();
            sound.Advance(host.Habitat.Settings.TrailSeconds + host.Habitat.Settings.GatherSeconds + .2f);
            var body = host.Bodies().Single();
            Assert.That(body.color.a, Is.GreaterThan(.5f), "The candidate is visible but still completing acquisition.");
            int before = sound.Plays.Count;
            Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.False);
            Assert.That(host.Habitat.IsItemCardPending || host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(sound.Plays.Count, Is.EqualTo(before));
            sound.Advance(host.Habitat.Settings.BirthDuration);
            AssertSingleCompletion(host, sound);
        }
    }

    [TestCase("Emerging")]
    [TestCase("Returning")]
    [TestCase("Fading")]
    public void Stage1_HousePresentationStatesRejectFairyTouchWithoutAudio(string motion)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var body = PrepareTouchFairy(host, sound, fonts);
            object view = TouchView(host, body);
            var field = EnhancementFairyStage1Host.Field(view.GetType(), "HouseMotion");
            field.SetValue(view, Enum.Parse(field.FieldType, motion));
            Assert.That(host.Habitat.TryOpenItemCard(host.Camera.WorldToScreenPoint(body.bounds.center)), Is.False);
            Assert.That(host.Habitat.IsItemCardPending || host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(sound.Plays, Is.Empty);
        }
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void Stage1_ActualSideWallReflectionDispatchesOneSoftPooledSound(int side)
    {
        using (var f = new WallSoundFixture())
        {
            var ownership = UserInfo.GetGiveGachaItemCountDic().ToArray();
            var body = f.Arm(0, side, 10f);
            f.Sound.Advance(1f / 60f);
            Assert.That(body.Velocity.x * side, Is.LessThan(0f), "The real solver must reflect before presentation dispatch.");
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            var played = f.Sound.Plays.Single();
            Assert.That(played.Clip, Is.SameAs(f.Settings.WallCollisionClip));
            Assert.That(played.Volume, Is.InRange(.001f, f.Settings.SafeWallCollisionVolume));
            Assert.That(played.Pitch, Is.EqualTo(f.Settings.WallCollisionPitch));
            Assert.That(played.Owner, Is.SameAs(EnhancementFairyStage1Host.Get(f.Views[0], "WallSoundOwner")));
            Assert.That(body.TryConsumeWallImpact(out _), Is.False, "The habitat drains the contact exactly once.");
            f.Sound.Advance(.05f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1), "Moving away from the wall must not repeat the contact.");
            f.AssertPoolUnchanged();
            CollectionAssert.AreEquivalent(ownership, UserInfo.GetGiveGachaItemCountDic());
        }
    }

    [TestCase(30, false)] [TestCase(60, false)]
    [TestCase(30, true)] [TestCase(60, true)]
    public void Stage1_VerticalBoundaryDispatchesSamePooledClipAtRaisedVolume(int fps, bool ceiling)
    {
        using (var f = new WallSoundFixture())
        {
            var body = f.Arm(0, 1, 10f);
            Rect room = f.Settings.Physics.SafeBounds;
            body.Position = new Vector2(room.center.x, ceiling
                ? room.yMax - body.HalfExtents.y - .001f : room.yMin + body.HalfExtents.y + .001f);
            body.Velocity = Vector2.up * (ceiling ? 10f : -10f);
            f.Sound.Advance(1f / fps);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            var played = f.Sound.Plays.Single();
            Assert.That(played.Clip, Is.SameAs(f.Settings.WallCollisionClip));
            Assert.That(f.Settings.WallCollisionVolume, Is.EqualTo(.30f * 1.15f).Within(.000001f));
            Assert.That(played.Volume, Is.InRange(.345f * .65f, .345f));
            Assert.That(body.TryConsumeBoundaryImpact(out _), Is.False);
            f.Sound.Advance(.05f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            f.AssertPoolUnchanged();
        }
    }

    [TestCase(30)] [TestCase(60)]
    public void Stage1_RepeatedFloorBouncesRespectCooldownAndStopOnAiReturn(int fps)
    {
        using (var f = new WallSoundFixture())
        {
            var body = f.Arm(0, 1, 10f); Rect room = f.Settings.Physics.SafeBounds;
            body.Position = new Vector2(room.center.x, room.yMin + body.HalfExtents.y + .5f);
            body.Velocity = Vector2.down * 12f;
            for (int i = 0; i < fps * 5; i++) f.Sound.Advance(1f / fps);
            Assert.That(f.Sound.Plays.Count, Is.InRange(2, f.Settings.Physics.SafeMaximumBounceCount + 1));
            for (int i = 1; i < f.Sound.Plays.Count; i++)
                Assert.That(f.Sound.Plays[i].Time - f.Sound.Plays[i - 1].Time,
                    Is.GreaterThanOrEqualTo(f.Settings.SafePerFairyWallSoundCooldown - .0001f));
            Assert.That(body.Mode, Is.EqualTo(EnhancementFairyPhysicsSolver.BodyMode.Autonomous));
            int count = f.Sound.Plays.Count;
            f.Sound.Advance(.1f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(count), "Restored walking must remain silent.");
            f.AssertPoolUnchanged();
        }
    }

    [TestCase(false, "mute")] [TestCase(true, "mute")]
    [TestCase(false, "drag")] [TestCase(true, "drag")]
    [TestCase(false, "slow")] [TestCase(true, "slow")]
    public void Stage1_VerticalBoundaryHonorsMuteDragAndMinimumSpeed(bool ceiling, string gate)
    {
        using (var f = new WallSoundFixture())
        {
            var body = f.Arm(0, 1, 10f); Rect room = f.Settings.Physics.SafeBounds;
            body.Position = new Vector2(room.center.x, ceiling
                ? room.yMax - body.HalfExtents.y - .001f : room.yMin + body.HalfExtents.y + .001f);
            body.Velocity = Vector2.up * (ceiling ? 1f : -1f) * (gate == "slow" ? .5f : 10f);
            if (gate == "mute") EnhancementFairyStage1Host.Set(f.Sound.Sound, "_effectVolume", 0f);
            if (gate == "drag") body.Mode = EnhancementFairyPhysicsSolver.BodyMode.Dragging;
            f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays, Is.Empty);
            f.AssertPoolUnchanged();
        }
    }

    [TestCase("below-speed")]
    [TestCase("master-mute")]
    [TestCase("effects-mute")]
    [TestCase("sound-disabled")]
    public void Stage1_WallSoundGatesDoNotChangeReflectionOrQueueDeferredAudio(string gate)
    {
        using (var f = new WallSoundFixture())
        {
            if (gate == "master-mute") EnhancementFairyStage1Host.Set(f.Sound.Sound, "_masterVolume", 0f);
            if (gate == "effects-mute") EnhancementFairyStage1Host.Set(f.Sound.Sound, "_soundEffectVolume", 0f);
            if (gate == "sound-disabled") f.Settings.WallCollisionSoundEnabled = false;
            var body = f.Arm(0, -1, gate == "below-speed" ? 1f : 10f);
            f.Sound.Advance(1f / 60f);
            Assert.That(body.Velocity.x, Is.GreaterThan(0f), "Mute/threshold must never suppress the physical reflection.");
            Assert.That(f.Sound.Plays, Is.Empty);
            Assert.That(body.TryConsumeWallImpact(out _), Is.False);
            EnhancementFairyStage1Host.Set(f.Sound.Sound, "_masterVolume", 1f);
            EnhancementFairyStage1Host.Set(f.Sound.Sound, "_soundEffectVolume", 1f);
            f.Settings.WallCollisionSoundEnabled = true;
            f.Sound.Advance(.05f);
            Assert.That(f.Sound.Plays, Is.Empty, "Opening a gate cannot replay a previously suppressed collision.");
            f.AssertPoolUnchanged();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stage1_WallCooldownSeparatelyLimitsSameFairyAndOtherFairies(bool global)
    {
        using (var f = new WallSoundFixture(2))
        {
            f.Settings.GlobalWallSoundCooldown = global ? 1f : 0f;
            f.Settings.PerFairyWallSoundCooldown = global ? 0f : 1f;
            f.Arm(0, -1, 10f); f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            f.Sound.Advance(.25f); // The WAV has ended, so the next rejection is the cooldown rather than pool occupancy.
            int next = global ? 1 : 0;
            f.Arm(next, 1, 10f); f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            f.Sound.Advance(.8f);
            f.Arm(next, 1, 10f); f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(2));
            Assert.That(f.Sound.Plays[1].Time - f.Sound.Plays[0].Time, Is.GreaterThanOrEqualTo(1d));
            f.AssertPoolUnchanged();
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void Stage1_FivePhysicalFairiesKeepTwoWallVoicesAndCancelOnlyTheirOwnSource(bool mixedSurfaces)
    {
        using (var f = new WallSoundFixture(5))
        {
            f.Settings.GlobalWallSoundCooldown = 0f; // Exercise the concurrent cap independently of the normal global throttle.
            for (int i = 0; i < 5; i++)
            {
                var body = f.Arm(i, i % 2 == 0 ? -1 : 1, 10f);
                if (mixedSurfaces && i > 0)
                {
                    Rect room = f.Settings.Physics.SafeBounds; bool ceiling = i % 2 == 1;
                    body.Position = new Vector2(room.center.x + i * 3f, ceiling
                        ? room.yMax - body.HalfExtents.y - .001f : room.yMin + body.HalfExtents.y + .001f);
                    body.Velocity = Vector2.up * (ceiling ? 10f : -10f);
                }
            }
            f.Sound.Advance(1f / 60f);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(5));
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(2));
            var first = f.Sound.Plays[0]; var second = f.Sound.Plays[1];
            Assert.That(first.Source, Is.Not.SameAs(second.Source));
            Assert.That(((IDictionary)EnhancementFairyStage1Host.Get(f.Sound.Sound, "_effectVoices")).Count, Is.EqualTo(2));
            EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "CancelPhysicsView", f.Views[0]);
            CollectionAssert.Contains(f.Sound.Stops, first.Source);
            CollectionAssert.DoesNotContain(f.Sound.Stops, second.Source);
            f.Arm(4, -1, 10f); f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(3), "A new real impact may reuse the newly released voice; dropped contacts are not queued.");
            Assert.That(f.Sound.Plays.Last().Source, Is.SameAs(first.Source));
            Assert.That(((IDictionary)EnhancementFairyStage1Host.Get(f.Sound.Sound, "_effectVoices")).Count, Is.EqualTo(2));
            f.AssertPoolUnchanged();
        }
    }

    [TestCase("cancel")]
    [TestCase("disable")]
    [TestCase("physics-disabled")]
    [TestCase("ownership-removed")]
    public void Stage1_WallAudioStopsWithItsPhysicalPresentation(string reason)
    {
        using (var f = new WallSoundFixture())
        {
            var body = f.Arm(0, 1, 10f); f.Sound.Advance(1f / 60f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            var source = f.Sound.Plays[0].Source;
            if (reason == "cancel") f.Host.Habitat.Cancel();
            if (reason == "disable") EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "OnDisable");
            if (reason == "physics-disabled") { f.Settings.Physics.Enabled = false; f.Sound.Advance(1f / 60f); }
            if (reason == "ownership-removed") f.Host.Restore(Array.Empty<KeyValuePair<string, int>>());
            CollectionAssert.Contains(f.Sound.Stops, source);
            Assert.That(((IDictionary)EnhancementFairyStage1Host.Get(f.Sound.Sound, "_effectVoices")).Count, Is.Zero);
            Assert.That(body.TryConsumeWallImpact(out _), Is.False);
            f.Sound.Advance(.05f);
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1));
            f.AssertPoolUnchanged();
        }
    }

    [Test]
    public void OfflineSessionResetDoesNotCarryWallSoundCooldownIntoItsRestartedClock()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            var root = new GameObject("Owned offline wall sound reset host");
            var settings = Object.Instantiate(host.Habitat.Settings);
            try
            {
                settings.GlobalWallSoundCooldown = 10f;
                var habitat = root.AddComponent<EnhancementFairyHabitat>();
                string[] owned = { sound.Items[0].Id };
                habitat.ConfigureOffline(sound.Items, owned, settings);
                sound.Clock += .5d; habitat.AdvancePreview(.1f);
                object view = ((IList)EnhancementFairyStage1Host.Get(habitat, "_views"))[0];
                ArmWallSoundImpact(habitat, view, 0, -1, 10f);
                sound.Clock += 1d / 60d; habitat.AdvancePreview(1f / 60f);
                Assert.That(sound.Plays.Count, Is.EqualTo(1));
                habitat.ResetOfflineSession(owned);
                CollectionAssert.Contains(sound.Stops, sound.Plays[0].Source);
                view = ((IList)EnhancementFairyStage1Host.Get(habitat, "_views"))[0];
                ArmWallSoundImpact(habitat, view, 0, 1, 10f);
                sound.Clock += 1d / 60d; habitat.AdvancePreview(1f / 60f);
                Assert.That(sound.Plays.Count, Is.EqualTo(2), "The new session cannot inherit a deadline from the former clock.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(settings); }
        }
    }

    private static EnhancementFairyPhysicsSolver.Body ArmWallSoundImpact(EnhancementFairyHabitat habitat, object view,
        int heightIndex, int side, float speed)
    {
        var body = (EnhancementFairyPhysicsSolver.Body)EnhancementFairyStage1Host.Get(view, "PhysicsBody");
        Rect room = habitat.Settings.Physics.SafeBounds;
        body.Mode = EnhancementFairyPhysicsSolver.BodyMode.Physics;
        body.Position = new Vector2(side < 0 ? room.xMin + body.HalfExtents.x + .001f : room.xMax - body.HalfExtents.x - .001f,
            room.yMin + 2.5f + heightIndex * 2.1f);
        body.Velocity = new Vector2(side * speed, 0f);
        body.Enabled = body.Eligible = true;
        body.Settling = body.Finished = false; body.Age = 0f; body.ClearWallImpact();
        EnhancementFairyStage1Host.Set(body, "BoundaryEntryWeight", 0f);
        EnhancementFairyStage1Host.Set(view, "PhysicsSessionStartedAt", (float)EnhancementFairyStage1Host.Get(habitat, "_clock"));
        return body;
    }

    private sealed class WallSoundFixture : IDisposable
    {
        internal readonly EnhancementFairyStage1Host Host;
        internal readonly FairyBirthSoundScope Sound;
        internal readonly EnhancementFairySettings Settings;
        internal readonly object[] Views;
        private readonly EnhancementFairySettings _original;
        private readonly int _sourceCount;
        internal WallSoundFixture(int count = 1)
        {
            Host = new EnhancementFairyStage1Host();
            try
            {
                Sound = new FairyBirthSoundScope(Host);
                _original = Host.Habitat.Settings; Settings = Object.Instantiate(_original);
                EnhancementFairyStage1Host.Set(Host.Habitat, "_settings", Settings);
                Host.Restore(Sound.Items.Take(count).Select(item => new KeyValuePair<string, int>(item.Id, 1)));
                Sound.StartFloor3(); Sound.Advance(.5f);
                Views = ((IList)EnhancementFairyStage1Host.Get(Host.Habitat, "_views")).Cast<object>().ToArray();
                Assert.That(Views.Length, Is.EqualTo(count));
                Assert.That(Settings.WallCollisionClip, Is.Not.Null);
                Assert.That(Sound.Plays, Is.Empty);
                _sourceCount = Sound.Sound.GetComponentsInChildren<AudioSource>(true).Length;
            }
            catch { Dispose(); throw; }
        }
        internal EnhancementFairyPhysicsSolver.Body Arm(int index, int side, float speed)
            => ArmWallSoundImpact(Host.Habitat, Views[index], index, side, speed);
        internal void AssertPoolUnchanged()
            => Assert.That(Sound.Sound.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(_sourceCount));
        public void Dispose()
        {
            try { Sound?.Dispose(); }
            finally
            {
                if (Host?.Habitat != null && _original != null) EnhancementFairyStage1Host.Set(Host.Habitat, "_settings", _original);
                if (Settings != null) Object.DestroyImmediate(Settings);
                Host?.Dispose();
            }
        }
    }

    private static SpriteRenderer PrepareTouchFairy(EnhancementFairyStage1Host host, FairyBirthSoundScope sound, StaffGachaOfflineFonts fonts)
    {
        foreach (var itemUi in host.SceneComponents<UIItemGacha>())
        { itemUi.gameObject.SetActive(false); fonts.BindBeforeActivation(itemUi.gameObject); }
        host.Restore(new[] { new KeyValuePair<string, int>(sound.Items[0].Id, 1) });
        sound.StartFloor3();
        var brain = host.Habitat.GetActiveBrain(0);
        brain.PlaceForBirth(new Vector2(host.Floor.transform.InverseTransformPoint(host.Camera.transform.position).x,
            host.Habitat.Settings.GroundArea.y));
        sound.Advance(.05f);
        return host.Bodies().Single();
    }

    private static object TouchView(EnhancementFairyStage1Host host, SpriteRenderer body)
        => ((IList)EnhancementFairyStage1Host.Get(host.Habitat, "_views")).Cast<object>()
            .Single(view => ReferenceEquals(EnhancementFairyStage1Host.Get(view, "Body"), body));

    private static Vector2 TouchMultiplier(object view, SpriteRenderer body)
    {
        var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
        float scale = (float)EnhancementFairyStage1Host.Get(view, "Scale") * (float)EnhancementFairyStage1Host.Get(view, "HouseScale");
        var physics = (EnhancementFairyPhysicsSolver.Body)EnhancementFairyStage1Host.Get(view, "PhysicsBody");
        // Isolate the card-only multiplier from both normal AI and collision squash,
        // including the collision reaction that can decay after physics returns to AI.
        float baseSquash = physics != null && physics.Mode != EnhancementFairyPhysicsSolver.BodyMode.Autonomous
            ? physics.Squash : brain.Squash + (physics?.Squash ?? 0f);
        return new Vector2(body.transform.localScale.x / (scale * (1f + baseSquash)),
            body.transform.localScale.y / (scale * (1f - baseSquash)));
    }

    private static void AssertMeshGroundContact(object view, SpriteRenderer body)
    {
        var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
        float lowest = float.PositiveInfinity;
        foreach (var vertex in body.sprite.vertices)
        {
            Vector3 local = new Vector3(body.flipX ? -vertex.x : vertex.x, body.flipY ? -vertex.y : vertex.y, 0f);
            lowest = Mathf.Min(lowest, body.transform.parent.InverseTransformPoint(body.transform.TransformPoint(local)).y);
        }
        Assert.That(lowest, Is.EqualTo(brain.VisualHeight + (float)EnhancementFairyStage1Host.Get(view, "HouseLift")).Within(.0001f),
            "Squish/tilt/flip composition must preserve the lowest visible mesh point at its original ground height.");
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
        bool cameraOwnsPointer = (bool)EnhancementFairyStage1Host.Get(host.Controller, "_isDragging");
        bool fairyOwnsPointer = host.Controller.IsWorldPointerGestureActive;
        Assert.That(cameraOwnsPointer ^ fairyOwnsPointer, Is.True,
            "A production press must have exactly one owner: a pending fairy gesture or the owned default-physics background.");
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

    internal static void TouchCountFrame(EnhancementFairyStage1Host host, int count, int id, Vector2 position, TouchPhase phase)
    {
        if ((bool)EnhancementFairyStage1Host.Invoke(host.Controller, "UpdateTouchInputState", count))
            host.Controller.ProcessPointerInputFrame(id, position, phase, Time.unscaledTime);
    }

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
