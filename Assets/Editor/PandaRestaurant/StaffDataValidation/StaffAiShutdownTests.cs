#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;
using Object = UnityEngine.Object;

// Isolated production teardown calls use real delegates and real destroyed Unity
// sources. The first six tests seed subscriptions without initializing UI/assets.
// The last two exercise representative normal Init paths after a previous source
// was destroyed. These tests do not claim real PlayerLoop or shutdown-order coverage.
public sealed class StaffAiShutdownTests
{
    private sealed class Subscriber
    {
        public readonly Type Type;
        public readonly string[] Events;
        public readonly string[] Methods;

        public Subscriber(Type type, string[] events, string[] methods)
        {
            Type = type;
            Events = events;
            Methods = methods;
        }
    }

    private enum SourceState { Absent, Destroyed, Replaced }

    private static Subscriber ScoreSubscriber<T>(string method) where T : Component
        => new Subscriber(typeof(T), new[] { "OnChangeScoreHandler" }, new[] { method });

    private static readonly Subscriber[] GameSubscribers =
    {
        ScoreSubscriber<UIScore>("OnChangeScoreEvent"),
        ScoreSubscriber<UIScoreImage>("OnScoreChangeEvent"),
        ScoreSubscriber<UIFurniture>("UpdateUIOptimized"),
        ScoreSubscriber<UIStaff>("UpdateResourceUI"),
        ScoreSubscriber<UIRecipeTab>("OnDataChanged"),
        ScoreSubscriber<UIPictorialBook>("CustomerUpdateUI"),
        ScoreSubscriber<UIKitchen>("UpdateUI"),
        new Subscriber(typeof(UImanagementManagementGroup),
            new[] { "OnChangeTipPerMinuteHandler", "OnChangeScoreHandler" },
            new[] { "OnChangeTipPerMinuteEvent", "OnChangeCurrentScoreEvent" })
    };

    private static readonly Subscriber[] ChallengeSubscribers =
    {
        new Subscriber(typeof(UIChallenge),
            new[] { "OnDailyChallengeUpdateHandler", "OnAllTimeChallengeUpdateHandler", "OnWeeklyChallengeUpdateHandler" },
            new[] { "OnDailyUpdateUI", "OnAllTimeUpdateUI", "OnWeeklyUpdateUI" }),
        new Subscriber(typeof(UIChallengeTab), new[] { "OnAddChallengeHandler" }, new[] { "OnUpdateEvent" }),
        new Subscriber(typeof(UIChallengeNotification), new[] { "OnDailyChallengeUpdateHandler" }, new[] { "RefreshNotificationMessage" }),
        new Subscriber(typeof(UIMainChallengeNotification), new[] { "OnMainChallengeUpdateHandler" }, new[] { "RefreshNotificationMessage" }),
        new Subscriber(typeof(UIMainChallenge), new[] { "OnMainChallengeUpdateHandler" }, new[] { "UpdateData" })
    };

    [Test]
    public void GameUiTeardownWithoutSource_DoesNotCreateGameManager()
        => CheckGroup(typeof(GameManager), "_subscribedGameManager", GameSubscribers, SourceState.Absent);

    [Test]
    public void GameUiTeardownAfterSourceDestroyed_DoesNotRecreateGameManager()
        => CheckGroup(typeof(GameManager), "_subscribedGameManager", GameSubscribers, SourceState.Destroyed);

    [Test]
    public void GameUiTeardownAfterReplacement_UnsubscribesOnlyOriginalSource()
        => CheckGroup(typeof(GameManager), "_subscribedGameManager", GameSubscribers, SourceState.Replaced);

    [Test]
    public void ChallengeUiTeardownWithoutSource_DoesNotCreateChallengeManager()
        => CheckGroup(typeof(ChallengeManager), "_subscribedChallengeManager", ChallengeSubscribers, SourceState.Absent);

    [Test]
    public void ChallengeUiTeardownAfterSourceDestroyed_DoesNotRecreateChallengeManager()
        => CheckGroup(typeof(ChallengeManager), "_subscribedChallengeManager", ChallengeSubscribers, SourceState.Destroyed);

    [Test]
    public void ChallengeUiTeardownAfterReplacement_UnsubscribesOnlyOriginalSource()
        => CheckGroup(typeof(ChallengeManager), "_subscribedChallengeManager", ChallengeSubscribers, SourceState.Replaced);

    [Test]
    public void ScoreInitAfterPreviousSourceDestroyed_SubscribesAndCleansUpAgain()
        => CheckNormalInitialization(typeof(GameManager), "_subscribedGameManager", GameSubscribers[0]);

    [Test]
    public void ChallengeTabInitAfterPreviousSourceDestroyed_SubscribesAndCleansUpAgain()
        => CheckNormalInitialization(typeof(ChallengeManager), "_subscribedChallengeManager", ChallengeSubscribers[1]);

    private static void CheckGroup(Type managerType, string cacheField, Subscriber[] subscribers, SourceState state)
    {
        foreach (var spec in subscribers)
        {
            using (var s = new Flow())
            {
                var instanceField = Field(managerType, "_instance");
                var fixtureManager = instanceField.GetValue(null);
                var source = state == SourceState.Absent ? null : Create(s, managerType);
                Component replacement = null;
                s.Swap(managerType, "_instance", source);
                var ui = Create(s, spec.Type);
                Set(ui, cacheField, source);
                var handlers = RegisterCallbacks(source, ui, spec);
                try
                {
                    if (state == SourceState.Destroyed)
                    {
                        Object.DestroyImmediate(source.gameObject);
                        Assert.That(source == null, Is.True, spec.Type.Name + " source must really be destroyed.");
                    }
                    else if (state == SourceState.Replaced)
                    {
                        replacement = Create(s, managerType);
                        instanceField.SetValue(null, replacement);
                        RegisterCallbacks(replacement, ui, spec);
                    }

                    var expectedInstance = state == SourceState.Replaced ? replacement : source;
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        Invoke(ui, "OnDestroy");
                        Assert.That(ReferenceEquals(instanceField.GetValue(null), expectedInstance), Is.True,
                            spec.Type.Name + " teardown must not create or replace its manager.");
                        Assert.That(ReferenceEquals(Field(ui.GetType(), cacheField).GetValue(ui), null), Is.True,
                            spec.Type.Name + " must release its cached source.");
                        if (state == SourceState.Replaced)
                        {
                            CheckCallbacks(source, spec, handlers, false);
                            CheckCallbacks(replacement, spec, handlers, true);
                        }
                    }

                    if (ui is UIMainChallenge)
                    {
                        // A queued scene callback after teardown must also avoid
                        // finding/creating a manager or removing replacement work.
                        Invoke(ui, "OnChangeSceneEvent");
                        Assert.That(ReferenceEquals(instanceField.GetValue(null), expectedInstance), Is.True);
                        if (state == SourceState.Replaced)
                            CheckCallbacks(replacement, spec, handlers, true);
                    }
                }
                finally
                {
                    RestoreFixture(instanceField, fixtureManager, source, replacement);
                }
            }
        }
    }

    private static void CheckNormalInitialization(Type managerType, string cacheField, Subscriber spec)
    {
        using (var s = new Flow())
        {
            var instanceField = Field(managerType, "_instance");
            var fixtureManager = instanceField.GetValue(null);
            s.Swap(managerType, "_instance", null);
            Component source = null;
            try
            {
                for (int run = 0; run < 2; run++)
                {
                    source = Create(s, managerType);
                    instanceField.SetValue(null, source);
                    var ui = Create(s, spec.Type);
                    if (ui is UIScore)
                    {
                        var rectObject = s.Track(new GameObject("Shutdown score rect", typeof(RectTransform)));
                        rectObject.SetActive(false);
                        Set(ui, "_rt", rectObject.GetComponent<RectTransform>());
                        Invoke(ui, "Init");
                    }
                    else
                    {
                        ((UIChallengeTab)ui).Init(new List<ChallengeData>());
                    }

                    Assert.That(ReferenceEquals(Field(ui.GetType(), cacheField).GetValue(ui), source), Is.True);
                    var handler = CreateCallback(ui, spec.Methods[0], managerType.GetEvent(spec.Events[0]).EventHandlerType);
                    CheckCallbacks(source, spec, new[] { handler }, true);
                    Invoke(ui, "OnDestroy");
                    CheckCallbacks(source, spec, new[] { handler }, false);
                    Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True);
                }
            }
            finally
            {
                RestoreFixture(instanceField, fixtureManager, source, null);
            }
        }
    }

    private static Component Create(Flow s, Type type)
    {
        var go = s.Track(new GameObject("Shutdown boundary " + type.Name));
        go.SetActive(false);
        return go.AddComponent(type);
    }

    private static Delegate[] RegisterCallbacks(Component source, Component ui, Subscriber spec)
    {
        if (source == null) return new Delegate[0];
        var handlers = new Delegate[spec.Events.Length];
        for (int i = 0; i < spec.Events.Length; i++)
        {
            var eventInfo = source.GetType().GetEvent(spec.Events[i]);
            Assert.That(eventInfo, Is.Not.Null, spec.Events[i]);
            handlers[i] = CreateCallback(ui, spec.Methods[i], eventInfo.EventHandlerType);
            eventInfo.AddEventHandler(source, handlers[i]);
        }
        return handlers;
    }

    private static Delegate CreateCallback(Component target, string method, Type delegateType)
    {
        var info = target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        Assert.That(info, Is.Not.Null, target.GetType().Name + "." + method);
        return Delegate.CreateDelegate(delegateType, target, info);
    }

    private static void CheckCallbacks(Component source, Subscriber spec, Delegate[] handlers, bool expected)
    {
        for (int i = 0; i < spec.Events.Length; i++)
        {
            var current = Field(source.GetType(), spec.Events[i]).GetValue(source) as Delegate;
            bool subscribed = current != null && current.GetInvocationList().Contains(handlers[i]);
            Assert.That(subscribed, Is.EqualTo(expected), spec.Type.Name + "." + spec.Events[i]);
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }

    private static void RestoreFixture(FieldInfo instanceField, object fixtureManager, Component source, Component replacement)
    {
        var unexpected = instanceField.GetValue(null) as Component;
        if (unexpected != null && !ReferenceEquals(unexpected, source) &&
            !ReferenceEquals(unexpected, replacement) && !ReferenceEquals(unexpected, fixtureManager))
            Object.DestroyImmediate(unexpected.gameObject);
        instanceField.SetValue(null, fixtureManager);
    }
}
#endif
