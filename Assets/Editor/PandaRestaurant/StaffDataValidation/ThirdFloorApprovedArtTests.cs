#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using Muks.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ThirdFloorApprovedArtTests
{
    [Test]
    public void ActualStage1PrefabHasApprovedSlicesAlignedSeamAndOriginalMapReferences()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Actual Stage1 asset verification requires SDK-free Edit Mode.");
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            var art = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdFloorApprovedArt>(true)).Single();
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(art.gameObject),
                Is.EqualTo("Assets/Prefabs/Floor3/ThirdFloorApprovedArt.prefab"));
            Assert.That(art.UseApprovedArt, Is.True);
            var floor = art.GetComponentInParent<Floor3Controller>();
            Assert.That(floor, Is.Not.Null);
            var sprites = art.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(sprite => sprite.name == "LeftFairyPlayroom" || sprite.name == "RightMapRoom").ToArray();
            Assert.That(sprites.Length, Is.EqualTo(2));
            var left = sprites.Single(sprite => sprite.name == "LeftFairyPlayroom");
            var right = sprites.Single(sprite => sprite.name == "RightMapRoom");
            Assert.That(left.sprite, Is.Not.Null);
            Assert.That(right.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(left.sprite), Is.EqualTo("Assets/Sprites/3층/PlayArea/3F_Left_Fairy_Playroom_TouchProps.png"));
            Assert.That(AssetDatabase.GetAssetPath(right.sprite), Is.EqualTo("Assets/Sprites/3층/Approved/3F_Right_Map_Room_WithoutFrame.png"));
            Assert.That(left.sprite.rect, Is.EqualTo(new Rect(1f, 164f, 1647f, 777f)));
            Assert.That(right.sprite.rect, Is.EqualTo(new Rect(24f, 159f, 1646f, 777f)));
            var buildingFrames = scene.GetRootGameObjects().Single(root => root.name == "Backgrounds")
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(sprite => sprite.name == "3층 식당" || sprite.name == "3층 주방").ToArray();
            Assert.That(buildingFrames.Length, Is.EqualTo(2), "Verify against both original third-floor building frames.");
            foreach (var sprite in sprites)
            {
                Assert.That(sprite.transform.lossyScale.x, Is.EqualTo(sprite.transform.lossyScale.y).Within(.0001f));
                Assert.That(sprite.sortingLayerID, Is.EqualTo(left.sortingLayerID));
                foreach (var frame in buildingFrames)
                {
                    Assert.That(sprite.sortingLayerID, Is.EqualTo(frame.sortingLayerID));
                    Assert.That(sprite.sortingOrder, Is.LessThan(frame.sortingOrder),
                        "Approved art must stay behind the original wooden building frame: " + frame.name);
                }
                Assert.That(sprite.GetComponents<Collider2D>(), Is.Empty, "The room art must not own a full-room input surface.");
                var originals = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(sprite.sprite)).OfType<Sprite>();
                Assert.That(originals.Any(original => original.name == "ApprovedOriginal" && original.rect.height == 941f), Is.True);
            }
            art.Refresh();
            Assert.That(left.bounds.max.x, Is.EqualTo(right.bounds.min.x).Within(.001f), "The room slices share one seam.");
            Assert.That(left.bounds.min.y, Is.EqualTo(right.bounds.min.y).Within(.001f));
            Assert.That(left.bounds.min.y, Is.EqualTo(53.2f).Within(.001f));
            var fairySettings = Resources.Load<EnhancementFairySettings>("Fairies/EnhancementFairySettings");
            Assert.That(fairySettings, Is.Not.Null);
            Assert.That(SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(fairySettings.SortingLayer)),
                Is.GreaterThan(SortingLayer.GetLayerValueFromID(left.sortingLayerID)), "Existing fairies must render in front of room backgrounds.");

            var button = art.GetComponentInChildren<ThirdFloorApprovedMapButton>(true);
            Assert.That(button, Is.Not.Null);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_enableClickSound").GetValue(button), Is.True);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_clickSoundVolume").GetValue(button), Is.EqualTo(1f));
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_clickSoundPitch").GetValue(button), Is.EqualTo(1f));
            Assert.That(button.transform.parent, Is.SameAs(right.transform));
            var legacyTouch = (SpriteTouchEvent)Field(typeof(ThirdFloorApprovedMapButton), "_legacyMapTouch").GetValue(button);
            Assert.That(legacyTouch, Is.SameAs(Field(typeof(Floor3Controller), "_touchEvent").GetValue(floor)));
            var camera = (CameraController)Field(typeof(ThirdFloorApprovedMapButton), "_cameraController").GetValue(button);
            var navigation = (MobileUINavigation)Field(typeof(ThirdFloorApprovedMapButton), "_navigation").GetValue(button);
            Assert.That(camera, Is.Not.Null);
            Assert.That(navigation, Is.Not.Null);
            Assert.That(camera.gameObject.scene, Is.EqualTo(scene));
            Assert.That(navigation.gameObject.scene, Is.EqualTo(scene));
            Assert.That(navigation.GetComponent<UIMainCanvas>(), Is.Not.Null);
            int buttonLayer = 1 << button.gameObject.layer;
            Assert.That(((LayerMask)Field(typeof(CameraController), "_ignoredLayer").GetValue(camera)).value & buttonLayer, Is.Not.Zero);
            Assert.That(camera.GetComponent<Physics2DRaycaster>().eventMask.value & buttonLayer, Is.Not.Zero);
            var hitArea = button.GetComponent<BoxCollider2D>();
            Assert.That(hitArea.size, Is.EqualTo(new Vector2(2.9f, 2.32f)));
            Assert.That(hitArea.enabled, Is.True);
            Assert.That(hitArea.isTrigger, Is.True);
            Assert.That(button.GetComponent<SpriteRenderer>(), Is.Null, "Only the visual children move; the original input surface stays fixed.");
            var idlePivot = (Transform)Field(typeof(ThirdFloorApprovedMapButton), "_idlePivot").GetValue(button);
            var pressVisual = (Transform)Field(typeof(ThirdFloorApprovedMapButton), "_pressVisual").GetValue(button);
            var frameSprite = (SpriteRenderer)Field(typeof(ThirdFloorApprovedMapButton), "_frameSprite").GetValue(button);
            Assert.That(idlePivot, Is.SameAs(button.transform.Find("IdleSwingPivot")));
            Assert.That(pressVisual, Is.SameAs(idlePivot.Find("PressVisual")));
            Assert.That(frameSprite, Is.SameAs(pressVisual.Find("MapFrameSprite").GetComponent<SpriteRenderer>()));
            Assert.That(frameSprite.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(frameSprite.sprite), Is.EqualTo("Assets/Sprites/3층/Approved/3F_Small_Map_Frame.png"));
            Assert.That(frameSprite.sprite.rect.size, Is.EqualTo(new Vector2(296f, 250f)));
            Assert.That(frameSprite.sprite.pixelsPerUnit, Is.EqualTo(100f));
            Assert.That(Vector3.Distance(right.transform.InverseTransformPoint(frameSprite.transform.position),
                new Vector3(-.11f, -.035f, -.01f)), Is.LessThan(.0001f), "Keep the source pixel center and the existing button depth unchanged.");
            Assert.That(frameSprite.sortingLayerID, Is.EqualTo(right.sortingLayerID));
            Assert.That(frameSprite.sortingOrder, Is.GreaterThan(right.sortingOrder));
            Assert.That(SortingLayer.GetLayerValueFromID(frameSprite.sortingLayerID),
                Is.LessThan(SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(fairySettings.SortingLayer))));
            Assert.That(button.GetComponentsInChildren<Collider2D>(true).Length, Is.EqualTo(1));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frameSprite.sprite))).alphaIsTransparency, Is.True);

            var legacySprites = (SpriteRenderer[])Field(typeof(ThirdFloorApprovedArt), "_legacySprites").GetValue(art);
            var legacyColliders = (Collider2D[])Field(typeof(ThirdFloorApprovedArt), "_legacyColliders").GetValue(art);
            Assert.That(legacySprites.Length, Is.EqualTo(9));
            Assert.That(legacySprites.All(sprite => sprite != null), Is.True);
            var outlines = floor.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(sprite => sprite.sprite != null
                    && AssetDatabase.GetAssetPath(sprite.sprite) == "Assets/Sprites/배경/Stage1/외각선.png").ToArray();
            Assert.That(outlines.Length, Is.EqualTo(2));
            Assert.That(outlines.All(outline => outline.enabled && !legacySprites.Contains(outline)), Is.True,
                "Keep both original continuous wooden outlines visible to cover the building Sprite seam.");
            CollectionAssert.AreEqual(new[] { legacyTouch.GetComponent<Collider2D>() }, legacyColliders);
            art.UseApprovedArt = false;
            Assert.That(frameSprite.gameObject.activeInHierarchy, Is.False);
            Assert.That(button.gameObject.activeInHierarchy, Is.False);
            var beforeSprites = legacySprites.Select(sprite => sprite.enabled).ToArray();
            var beforeColliders = legacyColliders.Select(collider => collider.enabled).ToArray();
            var otherSprites = floor.GetComponentsInChildren<SpriteRenderer>(true)
                .Except(legacySprites).Except(sprites).ToDictionary(sprite => sprite, sprite => sprite.enabled);
            art.UseApprovedArt = true;
            Assert.That(legacySprites.All(sprite => !sprite.enabled), Is.True);
            Assert.That(outlines.All(outline => outline.enabled), Is.True);
            Assert.That(legacyColliders.All(collider => !collider.enabled), Is.True);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            art.gameObject.SetActive(false);
            CollectionAssert.AreEqual(beforeSprites, legacySprites.Select(sprite => sprite.enabled));
            CollectionAssert.AreEqual(beforeColliders, legacyColliders.Select(collider => collider.enabled));
            foreach (var pair in otherSprites) Assert.That(pair.Key.enabled, Is.EqualTo(pair.Value), pair.Key.name);
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            witness.AssertUnchanged("approved third-floor authored Stage1 verification");
        }
    }

    [Test]
    public void ApprovedToggleRestoresMixedEnabledStatesAndRecapturesEditsMadeWhileOff()
    {
        using (var host = new ArtHost())
        {
            Assert.That(host.Content.activeSelf, Is.True);
            host.AssertLegacy(false, false);
            host.Art.Refresh();
            host.Art.UseApprovedArt = false;
            host.AssertLegacy(true, false);
            Assert.That(host.Content.activeSelf, Is.False);

            host.Sprites[0].enabled = false;
            host.Sprites[1].enabled = true;
            host.Colliders[0].enabled = false;
            host.Colliders[1].enabled = true;
            host.Art.UseApprovedArt = true;
            host.AssertLegacy(false, false);
            host.Art.UseApprovedArt = false;
            host.AssertLegacy(false, true);
        }
    }

    [Test]
    public void RootAndComponentDisableOrRemovalRestoreLegacyWithoutEnablingOriginallyHiddenArt()
    {
        using (var host = new ArtHost())
        {
            host.Art.gameObject.SetActive(false);
            host.AssertLegacy(true, false);
            host.Art.gameObject.SetActive(true);
            host.AssertLegacy(false, false);
            host.Art.enabled = false;
            host.AssertLegacy(true, false);
            Assert.That(host.Content.activeSelf, Is.False);
            host.Art.enabled = true;
            host.AssertLegacy(false, false);
            Object.DestroyImmediate(host.Art);
            host.AssertLegacy(true, false);
            Assert.That(host.Content.activeSelf, Is.False);
        }
    }

    [Test]
    public void ActualStage1PrefabRecordsRestoreSnapshotAndOffStateAsOverrides()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Actual Stage1 asset verification requires SDK-free Edit Mode.");
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            var art = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdFloorApprovedArt>(true)).Single();
            var legacySprites = (SpriteRenderer[])Field(typeof(ThirdFloorApprovedArt), "_legacySprites").GetValue(art);
            var legacyColliders = (Collider2D[])Field(typeof(ThirdFloorApprovedArt), "_legacyColliders").GetValue(art);
            var content = (GameObject)Field(typeof(ThirdFloorApprovedArt), "_approvedContent").GetValue(art);
            art.UseApprovedArt = false;
            var beforeSprites = legacySprites.Select(sprite => sprite.enabled).ToArray();
            var beforeColliders = legacyColliders.Select(collider => collider.enabled).ToArray();
            art.UseApprovedArt = true;

            // These are the actual records Unity writes with a prefab instance when the user saves.
            // No Scene save, asset save, instance copy, or JSON replacement is performed here.
            AssertOverride(art, art, "_hasLegacySnapshot", "1");
            AssertOverride(art, art, "_spriteStates.Array.size", legacySprites.Length.ToString());
            AssertOverride(art, art, "_colliderStates.Array.size", legacyColliders.Length.ToString());
            for (int i = 0; i < legacySprites.Length; i++)
            {
                AssertOverride(art, art, "_spriteStates.Array.data[" + i + "].Target", legacySprites[i]);
                AssertOverride(art, art, "_spriteStates.Array.data[" + i + "].Enabled", beforeSprites[i] ? "1" : "0");
            }
            for (int i = 0; i < legacyColliders.Length; i++)
            {
                AssertOverride(art, art, "_colliderStates.Array.data[" + i + "].Target", legacyColliders[i]);
                AssertOverride(art, art, "_colliderStates.Array.data[" + i + "].Enabled", beforeColliders[i] ? "1" : "0");
            }

            var validate = typeof(ThirdFloorApprovedArt).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            var update = typeof(ThirdFloorApprovedArt).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            validate.Invoke(art, null);
            update.Invoke(art, null);
            Assert.That(Field(typeof(ThirdFloorApprovedArt), "_refreshRequested").GetValue(art), Is.False,
                "Recording an unchanged prefab state must not create an OnValidate/Update loop.");
            AssertOverride(art, art, "_hasLegacySnapshot", "1");

            art.UseApprovedArt = false;
            AssertOverride(art, art, "_useApprovedArt", "0");
            AssertOverride(art, content, "m_IsActive", "0");
            AssertOverride(art, art, "_hasLegacySnapshot", "0");
            AssertOverride(art, art, "_spriteStates.Array.size", "0");
            AssertOverride(art, art, "_colliderStates.Array.size", "0");
            CollectionAssert.AreEqual(beforeSprites, legacySprites.Select(sprite => sprite.enabled));
            CollectionAssert.AreEqual(beforeColliders, legacyColliders.Select(collider => collider.enabled));

            art.UseApprovedArt = true;
            art.gameObject.SetActive(false);
            AssertOverride(art, content, "m_IsActive", "0");
            AssertOverride(art, art, "_hasLegacySnapshot", "0");
            CollectionAssert.AreEqual(beforeSprites, legacySprites.Select(sprite => sprite.enabled));
            CollectionAssert.AreEqual(beforeColliders, legacyColliders.Select(collider => collider.enabled));
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            witness.AssertUnchanged("approved third-floor prefab snapshot overrides");
        }
    }

    [Test]
    public void CompletedTapUsesExistingFloor3ControllerAndMapNavigationOnceThenCanReenter()
    {
        using (var host = new MapHost(withSound: true))
        {
            Vector3 roomScale = host.Room.transform.localScale;
            var pointer = host.Pointer();
            host.Button.OnPointerDown(pointer);
            host.Button.OnPointerUp(pointer);
            Assert.That(host.Navigation.Count, Is.Zero, "The popup opens only on a completed click.");
            host.Button.OnPointerClick(pointer);
            host.Button.OnPointerClick(pointer);
            Assert.That(host.Navigation.Count, Is.Zero, "The approved frame gets its own visible press/release before opening.");
            Assert.That(host.SoundPlays, Is.Empty, "Audio waits for the final input recheck and map dispatch.");
            host.Advance(.25f);
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Floor.VisibleState, Is.EqualTo(VisibleState.Appearing));
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1), "The real legacy map callbacks must not add a second sound.");
            Assert.That(host.SoundPlays[0].Clip, Is.SameAs(Resources.Load<AudioClip>("Audio/ButtonClickSound")));
            Assert.That(host.SoundPlays[0].Owner, Is.SameAs(host.Button));
            Assert.That(host.SoundPlays[0].Type, Is.EqualTo(EffectType.None));
            host.Tap(host.Pointer());
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1), "An already open map rejects both input and sound.");
            Assert.That(host.LegacyFrame.GetComponent<TweenTransformScale>(), Is.Not.Null);
            Assert.That(host.Room.GetComponent<TweenData>(), Is.Null);
            Assert.That(host.Room.transform.localScale, Is.EqualTo(roomScale));
            host.AssertVisualsRestored();

            CompleteTweens(host.Floor.gameObject);
            host.Navigation.Pop("UIFloor3");
            CompleteTweens(host.Floor.gameObject);
            Assert.That(host.Floor.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(host.Navigation.Count, Is.Zero);
            host.Tap(host.Pointer());
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Opened, Is.EqualTo(2));
            Assert.That(host.SoundPlays.Count, Is.EqualTo(2), "Closing and reopening restores one sound per map entry.");
        }
    }

    [TestCase("distance")]
    [TestCase("drag-return")]
    [TestCase("exit-return")]
    [TestCase("cancel")]
    [TestCase("second-pointer")]
    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("navigation-hidden")]
    [TestCase("view-transition")]
    [TestCase("disabled")]
    public void InterruptedGesturesDoNotOpenMapAndNextValidTapRecovers(string interruption)
    {
        using (var host = new MapHost(withSound: true))
        {
            var pointer = host.Pointer();
            host.Button.OnPointerDown(pointer);
            switch (interruption)
            {
                case "distance": pointer.position += Vector2.right * 100f; break;
                case "drag-return": host.Button.OnBeginDrag(pointer); host.Button.OnDrag(pointer); break;
                case "exit-return": host.Button.OnPointerExit(pointer); break;
                case "cancel": host.Button.OnCancel(pointer); break;
                case "second-pointer": host.Button.OnPointerDown(host.Pointer(1)); break;
                case "camera-moving": Set(host.Camera, "_isMoveAction", true); break;
                case "world-block": host.Camera.SetWorldInputBlocked(host, true); break;
                case "navigation-hidden": Set(host.Navigation, "_isViewsInactive", true); break;
                case "view-transition": host.Floor.VisibleState = VisibleState.Disappearing; break;
                case "disabled": host.SetButtonEnabled(false); break;
            }
            host.Button.OnPointerUp(pointer);
            host.Button.OnPointerClick(pointer);
            host.Advance(1f);
            Assert.That(host.Navigation.Count, Is.Zero, interruption);
            Assert.That(host.Opened, Is.Zero, interruption);
            Assert.That(host.SoundPlays, Is.Empty, "Rejected or cancelled input must stay silent: " + interruption);
            Assert.That(host.LegacyFrame.GetComponent<TweenData>(), Is.Null, "Cancelled input must never reach the old press callbacks.");
            host.AssertVisualsRestored();

            Set(host.Camera, "_isMoveAction", false);
            host.Camera.SetWorldInputBlocked(host, false);
            Set(host.Navigation, "_isViewsInactive", false);
            host.Floor.VisibleState = VisibleState.Disappeared;
            host.SetButtonEnabled(true);
            host.Tap(host.Pointer());
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1));
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void PressAndReleaseAnimateOnlyFrameThenReuseExistingMapNavigation(int hz)
    {
        using (var host = new MapHost())
        {
            Vector3 roomScale = host.Room.transform.localScale;
            Vector3 hitPosition = host.Button.transform.position;
            Quaternion hitRotation = host.Button.transform.rotation;
            Vector2 colliderSize = host.Button.GetComponent<BoxCollider2D>().size;
            var pointer = host.Pointer();
            host.Button.OnPointerDown(pointer);
            host.Advance(.09f, 1f / hz);
            Assert.That(Vector3.Distance(host.PressVisual.localScale, host.PressRestScale * .95f), Is.LessThan(.00001f));
            Assert.That(host.Navigation.Count, Is.Zero);
            host.Button.OnPointerUp(pointer);
            host.Button.OnPointerClick(pointer);
            host.Advance(.1f, 1f / hz);
            Assert.That(host.Navigation.Count, Is.Zero, "The release must remain visible before the existing map opens.");
            Assert.That(host.PressVisual.localScale.x, Is.GreaterThan(host.PressRestScale.x * .95f));
            host.Advance(.15f, 1f / hz);
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            host.AssertVisualsRestored();
            Assert.That(host.Room.transform.localScale, Is.EqualTo(roomScale));
            Assert.That(host.Button.transform.position, Is.EqualTo(hitPosition));
            Assert.That(host.Button.transform.rotation, Is.EqualTo(hitRotation));
            Assert.That(host.Button.GetComponent<BoxCollider2D>().size, Is.EqualTo(colliderSize));
        }
    }

    [Test]
    public void InstantCompletedClickStillShowsFeedbackAndRapidClicksOpenOnlyOnce()
    {
        using (var host = new MapHost(withSound: true))
        {
            host.Tap(host.Pointer(), false);
            for (int i = 0; i < 10; i++) host.Tap(host.Pointer(), false);
            host.Advance(.04f);
            Assert.That(host.PressVisual.localScale.x, Is.LessThan(host.PressRestScale.x));
            Assert.That(host.Opened, Is.Zero);
            host.Advance(.21f);
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            host.AssertVisualsRestored();
        }
    }

    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("navigation-hidden")]
    [TestCase("other-floor")]
    [TestCase("disabled")]
    [TestCase("cancel")]
    public void DelayedOpeningRevalidatesInputAndScreenState(string interruption)
    {
        using (var host = new MapHost(withSound: true))
        {
            host.Tap(host.Pointer(), false);
            host.Advance(.04f);
            switch (interruption)
            {
                case "camera-moving": Set(host.Camera, "_isMoveAction", true); break;
                case "world-block": host.Camera.SetWorldInputBlocked(host, true); break;
                case "navigation-hidden": Set(host.Navigation, "_isViewsInactive", true); break;
                case "other-floor": Set(host.Main, "_currentFloor", ERestaurantFloorType.Floor1); break;
                case "disabled": host.SetButtonEnabled(false); break;
                case "cancel": host.Button.OnCancel(host.Pointer()); break;
            }
            host.Advance(.4f);
            Assert.That(host.Opened, Is.Zero, interruption);
            Assert.That(host.SoundPlays, Is.Empty, "A transition during feedback must cancel its pending sound.");
            Assert.That(host.Navigation.Count, Is.Zero);
            Assert.That(host.LegacyFrame.GetComponent<TweenData>(), Is.Null);
            host.AssertVisualsRestored();
            Set(host.Camera, "_isMoveAction", false);
            host.Camera.SetWorldInputBlocked(host, false);
            Set(host.Navigation, "_isViewsInactive", false);
            Set(host.Main, "_currentFloor", ERestaurantFloorType.Floor3);
            host.SetButtonEnabled(true);
            host.Tap(host.Pointer());
            Assert.That(host.Opened, Is.EqualTo(1), "A cancellation must not leave the button locked.");
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void IdleHintWaitsSwingsBrieflyRestoresExactlyAndSchedulesRandomInterval()
    {
        using (var host = new MapHost(idleHints: true, withSound: true))
        {
            host.Advance(.49f);
            host.AssertVisualsRestored();
            host.Advance(.02f);
            float maximumAngle = 0f;
            for (int i = 0; i < 50; i++)
            {
                host.Advance(.02f);
                maximumAngle = Mathf.Max(maximumAngle, Quaternion.Angle(host.IdleRestRotation, host.IdlePivot.localRotation));
            }
            Assert.That(maximumAngle, Is.GreaterThan(.1f));
            Assert.That(maximumAngle, Is.LessThanOrEqualTo(2.01f));
            host.AssertVisualsRestored();
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_hintSwinging").GetValue(host.Button), Is.False);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintRemaining").GetValue(host.Button), Is.InRange(8f, 15f));
            Assert.That(host.Opened, Is.Zero);
            Assert.That(host.SoundPlays, Is.Empty, "Automatic map hints must never request click audio.");
            Assert.That(host.Room.GetComponent<TweenData>(), Is.Null);
        }
    }

    [TestCase("master")]
    [TestCase("effects")]
    [TestCase("button-disabled")]
    [TestCase("volume-zero")]
    [TestCase("missing-clip")]
    public void MapAudioHonoursMuteAndOptionalSoundSettingsWithoutBlockingMap(string silence)
    {
        using (var host = new MapHost(withSound: true))
        {
            if (silence == "master") Set(host.Sound, "_masterVolume", 0f);
            if (silence == "effects") Set(host.Sound, "_soundEffectVolume", 0f);
            if (silence == "button-disabled") Set(host.Button, "_enableClickSound", false);
            if (silence == "volume-zero") Set(host.Button, "_clickSoundVolume", 0f);
            if (silence == "missing-clip") Set(host.Sound, "_clips", null);
            host.Tap(host.Pointer());
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(host.SoundPlays, Is.Empty, silence);
        }
    }

    [Test]
    public void MapAudioUsesTheExistingPoolAndInspectorVolumeAndPitch()
    {
        using (var host = new MapHost(withSound: true))
        {
            int sourcesBefore = host.Sound.GetComponentsInChildren<AudioSource>(true).Length;
            Set(host.Button, "_clickSoundVolume", .6f);
            Set(host.Button, "_clickSoundPitch", 1.2f);
            host.Tap(host.Pointer());
            Assert.That(host.SoundPlays.Count, Is.EqualTo(1));
            Assert.That(host.SoundPlays[0].Volume, Is.EqualTo(.6f).Within(.0001f));
            Assert.That(host.SoundPlays[0].Pitch, Is.EqualTo(1.2f).Within(.0001f));
            Assert.That(host.Sound.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourcesBefore));
            Assert.That(host.Button.GetComponentsInChildren<AudioSource>(true), Is.Empty);
        }
    }

    [Test]
    public void MapOpeningWithoutASoundManagerDoesNotCreateOne()
    {
        using (var host = new MapHost())
        {
            Assert.That(SoundManager.TryGetExistingInstance(), Is.Null);
            host.Tap(host.Pointer());
            Assert.That(host.Opened, Is.EqualTo(1));
            Assert.That(SoundManager.TryGetExistingInstance(), Is.Null);
        }
    }

    [Test]
    public void PopupCloseRetainsTheFloorSessionAndUsesTheNormalHintInterval()
    {
        using (var host = new MapHost(idleHints: true))
        {
            host.Advance(0f);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintRemaining").GetValue(host.Button), Is.EqualTo(.5f));
            host.Advance(1.4f);
            host.Tap(host.Pointer());
            host.Advance(30f);
            host.AssertVisualsRestored();
            CompleteTweens(host.Floor.gameObject);
            host.Navigation.Pop("UIFloor3");
            CompleteTweens(host.Floor.gameObject);
            host.Advance(0f);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintRemaining").GetValue(host.Button), Is.InRange(9f, 15f),
                "Closing a popup does not create another first-visible hint in the same third-floor visit.");
        }
    }

    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("navigation-hidden")]
    [TestCase("other-floor")]
    [TestCase("offscreen")]
    [TestCase("disabled")]
    public void IdleHintStopsAndRestoresWhenItsScreenIsUnavailable(string interruption)
    {
        using (var host = new MapHost(idleHints: true))
        {
            Set(host.Button, "_firstVisibleHintDelay", .1f);
            host.Advance(.24f);
            Assert.That(Quaternion.Angle(host.IdleRestRotation, host.IdlePivot.localRotation), Is.GreaterThan(.01f));
            switch (interruption)
            {
                case "camera-moving": Set(host.Camera, "_isMoveAction", true); break;
                case "world-block": host.Camera.SetWorldInputBlocked(host, true); break;
                case "navigation-hidden": Set(host.Navigation, "_isViewsInactive", true); break;
                case "other-floor": Set(host.Main, "_currentFloor", ERestaurantFloorType.Floor1); break;
                case "offscreen": host.Camera.transform.position += Vector3.right * 100f; break;
                case "disabled": host.SetButtonEnabled(false); break;
            }
            host.Advance(2f);
            host.AssertVisualsRestored();
            Assert.That(host.Opened, Is.Zero);
        }
    }

    [Test]
    public void PressInterruptsHintAndDragRestoresTheVisualWithoutOpening()
    {
        using (var host = new MapHost(idleHints: true))
        {
            Set(host.Button, "_firstVisibleHintDelay", .1f);
            host.Advance(.24f);
            Assert.That(Quaternion.Angle(host.IdleRestRotation, host.IdlePivot.localRotation), Is.GreaterThan(.01f));
            var pointer = host.Pointer();
            host.Button.OnPointerDown(pointer);
            host.Advance(.09f);
            Assert.That(host.IdlePivot.localRotation, Is.EqualTo(host.IdleRestRotation));
            Assert.That(Vector3.Distance(host.PressVisual.localScale, host.PressRestScale * .95f), Is.LessThan(.00001f));
            host.Button.OnBeginDrag(pointer);
            host.Button.OnPointerUp(pointer);
            host.Button.OnPointerClick(pointer);
            host.Advance(.05f);
            Assert.That(host.Opened, Is.Zero);
            host.AssertVisualsRestored();
        }
    }

    [Test]
    public void InspectorInvalidAnimationRangesAreClampedToFiniteSafeValues()
    {
        using (var host = new MapHost())
        {
            Set(host.Button, "_firstVisibleHintDelay", float.NaN);
            Set(host.Button, "_hintIntervalMin", float.NaN);
            Set(host.Button, "_hintIntervalMax", float.PositiveInfinity);
            Set(host.Button, "_hintDuration", -1f);
            Set(host.Button, "_hintRotationDegrees", 200f);
            Set(host.Button, "_pressScale", -4f);
            Set(host.Button, "_pressDuration", -1f);
            Set(host.Button, "_releaseDuration", float.NaN);
            Set(host.Button, "_mapOpenDelay", -1f);
            Set(host.Button, "_clickSoundVolume", float.NaN);
            Set(host.Button, "_clickSoundPitch", float.PositiveInfinity);
            typeof(ThirdFloorApprovedMapButton).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host.Button, null);
            foreach (string name in new[] { "_firstVisibleHintDelay", "_hintIntervalMin", "_hintIntervalMax",
                "_hintDuration", "_hintRotationDegrees", "_pressScale", "_pressDuration", "_releaseDuration", "_mapOpenDelay",
                "_clickSoundVolume", "_clickSoundPitch" })
            {
                float value = (float)Field(typeof(ThirdFloorApprovedMapButton), name).GetValue(host.Button);
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, name);
                Assert.That(value, Is.GreaterThanOrEqualTo(0f), name);
            }
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintIntervalMax").GetValue(host.Button),
                Is.GreaterThanOrEqualTo((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintIntervalMin").GetValue(host.Button)));
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_pressScale").GetValue(host.Button), Is.InRange(.5f, 1f));
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void FirstVisibleHintWaitsHalfASecondAfterTheCameraSettlesAndIsNotConsumedOffscreen(int hz)
    {
        using (var host = new MapHost(idleHints: true))
        {
            host.Camera.transform.position += Vector3.right * 100f;
            host.Advance(20f, 1f / hz);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.False);
            host.Camera.transform.position -= Vector3.right * 100f;
            Set(host.Camera, "_isMoveAction", true);
            host.Advance(2f, 1f / hz);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.False);
            Set(host.Camera, "_isMoveAction", false);
            host.Advance(.49f, 1f / hz);
            host.AssertVisualsRestored();
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.False);
            host.Advance(.01f + 1f / hz, 1f / hz);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_hintSwinging").GetValue(host.Button), Is.True);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.True);
        }
    }

    [TestCase("offscreen")]
    [TestCase("camera-moving")]
    [TestCase("disabled")]
    public void SameFloorReappearanceDoesNotRepeatTheFirstHintButLeavingTheFloorDoes(string interruption)
    {
        using (var host = new MapHost(idleHints: true))
        {
            host.Advance(1.4f);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.True);
            if (interruption == "offscreen") host.Camera.transform.position += Vector3.right * 100f;
            if (interruption == "camera-moving") Set(host.Camera, "_isMoveAction", true);
            if (interruption == "disabled") host.SetButtonEnabled(false);
            host.Advance(3f);
            if (interruption == "offscreen") host.Camera.transform.position -= Vector3.right * 100f;
            if (interruption == "camera-moving") Set(host.Camera, "_isMoveAction", false);
            if (interruption == "disabled") host.SetButtonEnabled(true);
            host.Advance(0f);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintRemaining").GetValue(host.Button), Is.InRange(9f, 15f));
            host.Advance(.6f);
            host.AssertVisualsRestored();
            Set(host.Main, "_currentFloor", ERestaurantFloorType.Floor1);
            host.Advance(.02f);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_firstHintConsumed").GetValue(host.Button), Is.False);
            Set(host.Main, "_currentFloor", ERestaurantFloorType.Floor3);
            host.Advance(0f);
            Assert.That((float)Field(typeof(ThirdFloorApprovedMapButton), "_hintRemaining").GetValue(host.Button), Is.EqualTo(.5f));
            host.Advance(.52f);
            Assert.That((bool)Field(typeof(ThirdFloorApprovedMapButton), "_hintSwinging").GetValue(host.Button), Is.True);
        }
    }

    [Test]
    public void ApprovedRootSpacesAmbientHintsByTwoSecondsWithoutBlockingAnotherSceneRoot()
    {
        using (var first = new ArtHost())
        using (var second = new ArtHost())
        {
            Assert.That(first.Art.HintMinimumGap, Is.EqualTo(2f));
            Assert.That(first.Art.TryBeginIdleHint(10f, .8f), Is.True);
            Assert.That(first.Art.TryBeginIdleHint(10.1f, .6f), Is.False);
            Assert.That(first.Art.TryBeginIdleHint(12.79f, .6f), Is.False);
            Assert.That(first.Art.TryBeginIdleHint(12.81f, .6f), Is.True);
            Assert.That(second.Art.TryBeginIdleHint(10.1f, .6f), Is.True,
                "Independent preview/scene roots must not share a static global hint lock.");
        }
    }

    [Test]
    public void FirstVisibleMapHintCanTakePriorityOverThePreviousRoomsCancelledHintGap()
    {
        using (var host = new ArtHost())
        {
            Assert.That(host.Art.TryBeginIdleHint(10f, .6f), Is.True);
            Assert.That(host.Art.TryBeginIdleHint(10.5f, .8f), Is.False);
            Assert.That(host.Art.TryBeginIdleHint(10.5f, .8f, true), Is.True);
            Assert.That(host.Art.TryBeginIdleHint(13.29f, .6f), Is.False);
            Assert.That(host.Art.TryBeginIdleHint(13.31f, .6f), Is.True);
        }
    }

    private class Host : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();

        protected GameObject Create(string name, bool active = false)
        {
            var result = new GameObject(name);
            result.SetActive(false);
            SceneManager.MoveGameObjectToScene(result, _scene);
            if (active) result.SetActive(true);
            return result;
        }

        public virtual void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }
    }

    private sealed class ArtHost : Host
    {
        internal readonly ThirdFloorApprovedArt Art;
        internal readonly GameObject Content;
        internal readonly SpriteRenderer[] Sprites = new SpriteRenderer[2];
        internal readonly Collider2D[] Colliders = new Collider2D[2];

        internal ArtHost()
        {
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var legacy = Create("legacy-" + i, true);
                    Sprites[i] = legacy.AddComponent<SpriteRenderer>();
                    Colliders[i] = legacy.AddComponent<BoxCollider2D>();
                    Sprites[i].enabled = Colliders[i].enabled = i == 0;
                }
                var root = Create("ThirdFloorApprovedArtRoot");
                Content = Create("ApprovedContent");
                Content.transform.SetParent(root.transform, false);
                Art = root.AddComponent<ThirdFloorApprovedArt>();
                Set(Art, "_approvedContent", Content);
                Set(Art, "_legacySprites", new[] { Sprites[0], Sprites[1], Sprites[0] });
                Set(Art, "_legacyColliders", Colliders);
                root.SetActive(true);
                Art.Refresh();
            }
            catch { Dispose(); throw; }
        }

        internal void AssertLegacy(bool first, bool second)
        {
            Assert.That(Sprites[0].enabled, Is.EqualTo(first));
            Assert.That(Sprites[1].enabled, Is.EqualTo(second));
            Assert.That(Colliders[0].enabled, Is.EqualTo(first));
            Assert.That(Colliders[1].enabled, Is.EqualTo(second));
        }
    }

    private sealed class MapHost : Host
    {
        private readonly object _previousActions;
        private readonly object _previousSoundManager;
        private double _soundClock;
        private readonly UnityEngine.Random.State _previousRandom = UnityEngine.Random.state;
        private readonly EventSystem _events;
        private readonly Texture2D _frameTexture;
        private readonly Sprite _fixtureSprite;
        internal readonly MainScene Main;
        internal readonly CameraController Camera;
        internal readonly MobileUINavigation Navigation;
        internal readonly UIFloor3 Floor;
        internal readonly ThirdFloorApprovedMapButton Button;
        internal readonly GameObject LegacyFrame, Room;
        internal readonly Transform IdlePivot, PressVisual;
        internal readonly Quaternion IdleRestRotation;
        internal readonly Vector3 PressRestScale;
        internal readonly SoundManager Sound;
        internal readonly List<SoundManager.EffectPlaybackDiagnostic> SoundPlays = new List<SoundManager.EffectPlaybackDiagnostic>();
        internal int Opened;

        internal MapHost(bool idleHints = false, bool withSound = false)
        {
            _previousActions = Field(typeof(DataBind), "_unityActionBindDic").GetValue(null);
            _previousSoundManager = Field(typeof(SoundManager), "_instance").GetValue(null);
            try
            {
                Assert.That(UserInfo.IsTutorialStart, Is.False, "Run in the approved isolated Editor context.");
                Assert.That(PopupManager.IsCustomerEncounterPresentationBlocked, Is.False);
                Field(typeof(DataBind), "_unityActionBindDic").SetValue(null, new DataBindContainer<UnityAction>());
                // Keep the actual SoundManager pool dispatch while avoiding Awake, account settings and native audio.
                Field(typeof(SoundManager), "_instance").SetValue(null, null);
                if (withSound)
                {
                    Sound = Create("inactive-existing-map-sound-pool").AddComponent<SoundManager>();
                    Sound.SuppressNativePlaybackForTests = true;
                    Sound.EffectClockForTests = () => _soundClock;
                    Sound.EffectPlaybackRequested += play => SoundPlays.Add(play);
                    var clip = Resources.Load<AudioClip>("Audio/ButtonClickSound");
                    Assert.That(clip, Is.Not.Null);
                    var clips = new AudioClip[(int)SoundEffectType.Length];
                    clips[(int)SoundEffectType.ButtonClickSound] = clip;
                    Set(Sound, "_clips", clips);
                    var pool = new List<AudioSource>();
                    for (int i = 0; i < 2; i++)
                    {
                        var source = Create("existing-map-pool-voice-" + i).AddComponent<AudioSource>();
                        source.transform.SetParent(Sound.transform, false);
                        source.playOnAwake = false;
                        pool.Add(source);
                    }
                    Set(Sound, "_effectAudioDic", new Dictionary<EffectType, List<AudioSource>> { { EffectType.None, pool } });
                    Field(typeof(SoundManager), "_instance").SetValue(null, Sound);
                }
                Camera = Create("inactive-camera").AddComponent<CameraController>();
                Main = Create("inactive-main-scene").AddComponent<MainScene>();
                Set(Main, "_currentFloor", ERestaurantFloorType.Floor3);
                Set(Camera, "_mainScene", Main);
                var renderCamera = Camera.GetComponent<Camera>();
                renderCamera.orthographic = true;
                renderCamera.orthographicSize = 5f;
                renderCamera.aspect = 16f / 9f;
                Camera.transform.position = new Vector3(0f, 0f, -10f);
                Set(Camera, "_cam", renderCamera);
                Navigation = Create("inactive-navigation").AddComponent<MobileUINavigation>();
                _events = Create("inactive-event-system").AddComponent<EventSystem>();
                Floor = Create("existing-map-view").AddComponent<UIFloor3>();
                var anime = Create("map-image");
                anime.transform.SetParent(Floor.transform, false);
                var dontTouch = Create("map-transition-blocker");
                dontTouch.transform.SetParent(Floor.transform, false);
                Set(Floor, "_animeUI", anime);
                Set(Floor, "_dontTouchArea", dontTouch);
                Set(Floor, "_showDuration", .1f);
                Set(Floor, "_hideDuration", .1f);
                Set(Floor, "_buttons", Array.Empty<UnityEngine.UI.Button>());
                Floor.ViewInit(Navigation);
                Floor.VisibleState = VisibleState.Disappeared;
                Set(Navigation, "_viewDic", new Dictionary<string, MobileUIView> { { "UIFloor3", Floor } });
                var main = Create("inactive-main-canvas").AddComponent<UIMainCanvas>();
                Set(main, "_uiNav", Navigation);
                DataBind.SetUnityActionValue("ShowFloor3UI", main.OnShowFloor3UI);
                Navigation.OnShowUIHandler += () => Opened++;

                LegacyFrame = Create("hidden-legacy-map", true);
                var legacyTouch = LegacyFrame.AddComponent<SpriteTouchEvent>();
                var controller = Create("inactive-original-floor-controller").AddComponent<Floor3Controller>();
                Set(controller, "_touchEvent", legacyTouch);
                Set(controller, "_tweenTarget", LegacyFrame);
                // Bind the exact production callbacks without Start's unrelated fairy-habitat attachment.
                legacyTouch.AddDownEvent((Action)Delegate.CreateDelegate(typeof(Action), controller,
                    typeof(Floor3Controller).GetMethod("OnTouchStart", BindingFlags.Instance | BindingFlags.NonPublic)));
                legacyTouch.AddUpEvent((Action)Delegate.CreateDelegate(typeof(Action), controller,
                    typeof(Floor3Controller).GetMethod("OnTouchEnd", BindingFlags.Instance | BindingFlags.NonPublic)));
                Room = Create("approved-room", true);
                Room.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
                var hitArea = Create("small-map-hit-area");
                hitArea.transform.SetParent(Room.transform, false);
                IdlePivot = Create("IdleSwingPivot", true).transform;
                IdlePivot.SetParent(hitArea.transform, false);
                IdlePivot.localPosition = new Vector3(.1f, 1f, 0f);
                IdleRestRotation = Quaternion.Euler(0f, 0f, 3f);
                IdlePivot.localRotation = IdleRestRotation;
                PressVisual = Create("PressVisual", true).transform;
                PressVisual.SetParent(IdlePivot, false);
                PressVisual.localPosition = new Vector3(0f, -.5f, 0f);
                PressRestScale = new Vector3(1.3f, .7f, 1f);
                PressVisual.localScale = PressRestScale;
                var frame = Create("MapFrameSprite", true);
                frame.transform.SetParent(PressVisual, false);
                _frameTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _frameTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                _frameTexture.Apply();
                _fixtureSprite = Sprite.Create(_frameTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f), 2f);
                var renderer = frame.AddComponent<SpriteRenderer>();
                renderer.sprite = _fixtureSprite;
                Button = hitArea.AddComponent<ThirdFloorApprovedMapButton>();
                Set(Button, "_legacyMapTouch", legacyTouch);
                Set(Button, "_cameraController", Camera);
                Set(Button, "_navigation", Navigation);
                Set(Button, "_idlePivot", IdlePivot);
                Set(Button, "_pressVisual", PressVisual);
                Set(Button, "_frameSprite", renderer);
                Set(Button, "_enableIdleHint", idleHints);
                hitArea.SetActive(true);
            }
            catch { Dispose(); throw; }
        }

        internal PointerEventData Pointer(int id = -1) => new PointerEventData(_events)
        {
            pointerId = id,
            button = PointerEventData.InputButton.Left,
            position = new Vector2(300f, 300f),
            pressPosition = new Vector2(300f, 300f),
            eligibleForClick = true
        };

        internal void Tap(PointerEventData pointer, bool completeAnimation = true)
        {
            ExecuteEvents.Execute(Button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(Button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            if (completeAnimation) Advance(.25f);
        }

        internal void Advance(float seconds, float step = .02f)
        {
            var tick = typeof(ThirdFloorApprovedMapButton).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tick, Is.Not.Null, "Tests advance the production animation code, not a duplicate timing model.");
            if (seconds <= 0f) { tick.Invoke(Button, new object[] { 0f }); return; }
            for (float elapsed = 0f; elapsed < seconds;)
            {
                float dt = Mathf.Min(step, seconds - elapsed);
                _soundClock += dt;
                tick.Invoke(Button, new object[] { dt });
                elapsed += dt;
            }
        }

        internal void SetButtonEnabled(bool enabled)
        {
            Button.enabled = enabled;
            // Detached Edit Mode does not guarantee Play lifecycle callbacks. Invoke the production callback.
            typeof(ThirdFloorApprovedMapButton).GetMethod(enabled ? "OnEnable" : "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Button, null);
        }

        internal void AssertVisualsRestored()
        {
            Assert.That(IdlePivot.localRotation, Is.EqualTo(IdleRestRotation));
            Assert.That(PressVisual.localScale, Is.EqualTo(PressRestScale));
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_fixtureSprite != null) Object.DestroyImmediate(_fixtureSprite);
            if (_frameTexture != null) Object.DestroyImmediate(_frameTexture);
            Field(typeof(DataBind), "_unityActionBindDic").SetValue(null, _previousActions);
            Field(typeof(SoundManager), "_instance").SetValue(null, _previousSoundManager);
            UnityEngine.Random.state = _previousRandom;
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var result = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (result != null) return result;
        }
        throw new MissingFieldException(type.Name, name);
    }

    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

    private static void AssertOverride(ThirdFloorApprovedArt root, Object target, string path, object expected)
    {
        Object source = PrefabUtility.GetCorrespondingObjectFromSource(target);
        var modification = PrefabUtility.GetPropertyModifications(root)
            .SingleOrDefault(entry => entry.target == source && entry.propertyPath == path);
        if (modification == null && expected is string defaultValue)
        {
            // Unity may remove an override when OFF restores the source prefab's default value.
            var property = new SerializedObject(source).FindProperty(path);
            if (property != null)
            {
                string actual = property.propertyType == SerializedPropertyType.Boolean
                    ? (property.boolValue ? "1" : "0") : property.intValue.ToString();
                Assert.That(actual, Is.EqualTo(defaultValue), path + " (source default)");
                return;
            }
        }
        Assert.That(modification, Is.Not.Null, path + " must be persisted as a prefab override");
        if (expected is Object reference) Assert.That(modification.objectReference, Is.SameAs(reference), path);
        else Assert.That(modification.value, Is.EqualTo(expected), path);
    }

    private static void CompleteTweens(GameObject root)
    {
        foreach (var tween in root.GetComponentsInChildren<TweenData>(true))
        {
            if (!tween.enabled) continue;
            if (Field(typeof(TweenData), "_percentHandler").GetValue(tween) == null)
                typeof(TweenData).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tween, null);
            var update = tween.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(tween, null);
            Set(tween, "ElapsedDuration", float.MaxValue);
            update.Invoke(tween, null);
        }
    }
}
#endif
