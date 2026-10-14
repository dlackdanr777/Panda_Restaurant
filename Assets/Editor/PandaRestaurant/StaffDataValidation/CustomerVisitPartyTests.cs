#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Muks.PathFinding;
using Muks.PathFinding.AStar;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;

// Owned PreviewScene and in-memory data only; invokes actual spawn/restore/guide code.
public sealed class CustomerVisitPartyTests
{
    private static List<NormalCustomer> Called(Flow s) => s.Controller.WaitingCustomers.ToList();
    private static List<NormalCustomer> Waiting(Flow s) => s.Controller.WaitingCustomers.ToList();
    private static object Visit(NormalCustomer c) => typeof(NormalCustomer).GetProperty("VisitIdentity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
    private static bool CreateModel(Flow s, NormalCustomer[] customers, out CustomerVisitParty party)
    {
        object[] args = { s.Controller, customers, null, null };
        bool result = (bool)typeof(CustomerVisitParty).GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        party = (CustomerVisitParty)args[2];
        return result;
    }
    private static NormalCustomer SpawnPrepared(Flow s)
    {
        var customer = ObjectPoolManager.Instance.SpawnNormalCustomer(Vector3.zero, Quaternion.identity);
        customer.SetData(s.Data, s.Controller, s.Manager);
        return customer;
    }
    private static void QueueOne(Flow s)
    {
        Assert.That(Invoke(s.Controller, "EnqueueVisit", s.Customer.VisitParty, true), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualAddCustomer_PreservesCountSkinWaitingAndFlatSave(bool extraOrder)
    {
        using (var s = new Flow(extraOrder: extraOrder))
        {
            var original = s.Customer;
            var old = original.VisitParty;
            ObjectPoolManager.Instance.DespawnNormalCustomer(original);
            s.Controller.AddCustomer();
            Assert.That(s.Controller.Count, Is.EqualTo(GameManager.Instance.AddPromotionCustomer));
            Assert.That(s.Controller.Count, Is.EqualTo(1));
            Assert.That(s.Controller.WaitCount, Is.Zero);
            s.Customer = Called(s).Single();
            Assert.That(s.Customer, Is.SameAs(original));
            Assert.That(old.IsActive, Is.False);
            Assert.That(s.Customer.VisitParty, Is.Not.SameAs(old));
            Assert.That(s.Customer.VisitParty.Members.Count, Is.EqualTo(1));
            Assert.That(s.Customer.VisitMember.Customer, Is.SameAs(s.Customer));
            Assert.That(typeof(CustomerVisitMember).GetProperty("VisitIdentity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s.Customer.VisitMember), Is.SameAs(Visit(s.Customer)));
            Assert.That(s.Customer.OrderCount, Is.EqualTo(extraOrder ? 2 : 1));
            var skin = UserInfo.GetEquipCustomerSkin(s.Data.Id);
            Assert.That(Get<SpriteRenderer>(s.Customer, "_spriteRenderer").sprite, Is.SameAs(skin == null ? s.Data.Sprite : skin.Sprite));
            Get<Action>(s.Customer, "_moveCompleted")();
            Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
            Assert.That(UserInfo.GetWaitingCustomerIds(EStage.Stage1), Is.EqualTo(new[] { s.Data.Id }));
        }
    }

    [Test]
    public void ActualRestore_PreservesRepeatedIdOrderSkinAndRandomState()
    {
        using (var s = new Flow(extraOrder: true))
        {
            Set(s.Controller, "_teamSpacing", 0f); // Explicit legacy grid spacing compatibility.
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            var b = s.Track(ScriptableObject.CreateInstance<NormalCustomerData>());
            Set(b, "_id", "PARTY-RESTORE-B"); Set(b, "_requiredDish", s.Food.Id); Set(b, "_wiatTime", 100);
            Set(b, "_sprite", s.NewSprite());
            Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[b.Id] = b;
            s.Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData>
            {
                [s.Data.Id] = new SaveCustomerData(s.Data.Id, "TABLE-USE-SKIN", 0),
                [b.Id] = new SaveCustomerData(b.Id, "", 0)
            });
            var ids = new List<string> { s.Data.Id, b.Id, s.Data.Id };
            UserInfo.SetWaitingCustomerIds(EStage.Stage1, ids);
            var random = UnityEngine.Random.state; int promotion = UserInfo.PromotionCount;
            Invoke(s.Controller, "LoadWaitingCustomers");
            Assert.That(Called(s).Select(c => c.NormalCustomerData.Id), Is.EqualTo(ids));
            Assert.That(Waiting(s), Is.EqualTo(Called(s)));
            Assert.That(Called(s).Select(c => c.VisitParty).Distinct().Count(), Is.EqualTo(3));
            Assert.That(Called(s).All(c => c.VisitParty.IsActive && c.VisitParty.Members.Count == 1), Is.True);
            Assert.That(Called(s).Select(c => c.OrderCount), Is.EqualTo(new[] { 2, 1, 2 }));
            Assert.That(Called(s).Select(c => Get<SpriteRenderer>(c, "_spriteRenderer").sprite),
                Is.EqualTo(new[] { UserInfo.GetEquipCustomerSkin(s.Data.Id).Sprite, b.Sprite, UserInfo.GetEquipCustomerSkin(s.Data.Id).Sprite }));
            Assert.That(Called(s).Select(c => c.transform.position.x), Is.EqualTo(new[] { 0f, 2 * AStar.Instance.NodeSize, 4 * AStar.Instance.NodeSize }));
            Assert.That(UserInfo.GetWaitingCustomerIds(EStage.Stage1), Is.EqualTo(ids));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(promotion));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }
    }

    [TestCase(3, 2)]
    [TestCase(3, 10)]
    public void ActualPromotionCountAndPersonalLimit_StillCountIndividuals(int promotion, int limit)
    {
        using (var s = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            Set(GameManager.Instance, "_addPromotionCustomer", promotion);
            Set(GameManager.Instance, "_maxWaitCustomerCount", limit);
            s.Controller.AddCustomer();
            int expected = Math.Min(promotion, limit);
            Assert.That(s.Controller.Count, Is.EqualTo(expected));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(expected));
            Assert.That(Called(s).Select(c => c.VisitParty).Distinct().Count(), Is.EqualTo(expected));
            Assert.That(Called(s).All(c => c.VisitParty.Members.Count == 1), Is.True);
            Assert.That(UserInfo.GetWaitingCustomerIds(EStage.Stage1), Is.EqualTo(Enumerable.Repeat(s.Data.Id, expected)));
        }
    }

    [Test]
    public void ActualEnteringCallback_CannotReinsertVisitAfterServiceEnd()
    {
        using (var s = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            s.Controller.AddCustomer(); s.Customer = Called(s).Single();
            Assert.That(s.Controller.WaitCount, Is.Zero);
            var arrival = Get<Action>(s.Customer, "_moveCompleted");
            var party = s.Customer.VisitParty;
            Invoke(s.Controller, "OnDisable");
            arrival(); arrival();
            Assert.That(s.Controller.WaitCount, Is.Zero);
            Assert.That(party.IsActive, Is.False); Assert.That(s.Customer.VisitParty, Is.Null);
        }
    }

    [Test]
    public void ActualDebugSpawn_UsesTheSameAuthoritativeTeamReservation()
    {
        using (var s = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            Set(s.Controller, "_uiCustomerTutorial", s.Create<UICustomerTutorial>());
            UserInfo.IsTutorialStart = true; // Do not enqueue the unrelated tutorial UI service.
            s.Controller.SpawnCustomer(s.Data.Id);
            Assert.That(s.Controller.Count, Is.EqualTo(1));
            Get<Action>(Called(s).Single(), "_moveCompleted")();
            Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(Waiting(s).Single().VisitParty.Members.Count, Is.EqualTo(1));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Guide_BindsExactMemberVisitUseAndSeat_AndRejectsSecondTable(int seat)
    {
        using (var s = new Flow())
        {
            QueueOne(s);
            var party = s.Customer.VisitParty;
            object visit = Visit(s.Customer);
            Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1, seat), Is.True);
            object use = s.Use;
            Assert.That(s.Table.CurrentParty, Is.SameAs(party));
            Assert.That(party.CurrentTable, Is.SameAs(s.Table));
            Assert.That(party.Members[0].SeatIndex, Is.EqualTo(seat));
            Assert.That(Visit(s.Customer), Is.SameAs(visit));
            Assert.That(s.Controller.WaitCount, Is.Zero);
            var second = s.Create<TableData>(); second.TableState = ETableState.Empty;
            Assert.That(Invoke(s.Controller, "EnqueueVisit", party, true), Is.False);
            var before = s.Snapshot();
            Assert.That(Invoke(s.Manager, "OnCustomerGuide", s.Customer, second, 0), Is.False);
            Assert.That(s.Controller.Count, Is.Zero);
            Assert.That(s.Controller.GuideCustomer(Vector3.one), Is.False);
            Assert.That(second.CurrentCustomer, Is.Null);
            Assert.That(s.Snapshot(), Is.EqualTo(before)); Assert.That(s.Use, Is.SameAs(use));
        }
    }

    [Test]
    public void ActualCreationAfterExecutionEnded_ReturnsSpawnedObjectWithoutOrphanOrPromotion()
    {
        using (var s = new Flow())
        {
            var old = s.Customer.VisitParty;
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            Invoke(s.Controller, "OnDisable");
            s.Controller.AddCustomer();
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(s.Controller.WaitCount, Is.Zero);
            Assert.That(UserInfo.PromotionCount, Is.Zero);
            Assert.That(old.IsActive, Is.False); Assert.That(s.Customer.VisitParty, Is.Null);
            Assert.That(s.Customer.gameObject.activeSelf, Is.False);
            Assert.That(Get<Queue<NormalCustomer>>(ObjectPoolManager.Instance, "_normalCustomerPool").Count, Is.EqualTo(1));
            Assert.That(Get<List<CustomerVisitParty>>(s.Controller, "_visitParties"), Is.Empty);
        }
    }

    [TestCase("empty")]
    [TestCase("null")]
    [TestCase("duplicate")]
    [TestCase("too-many")]
    [TestCase("inactive")]
    [TestCase("already-attached")]
    public void Model_RejectsIllegalMembersAtomically(string invalid)
    {
        using (var s = new Flow())
        {
            var first = SpawnPrepared(s);
            var second = SpawnPrepared(s);
            NormalCustomer[] members = { first, second };
            if (invalid == "empty") members = Array.Empty<NormalCustomer>();
            if (invalid == "null") members[1] = null;
            if (invalid == "duplicate") members[1] = first;
            if (invalid == "too-many") members = new[] { first, second, s.Customer };
            if (invalid == "inactive") ObjectPoolManager.Instance.DespawnNormalCustomer(second);
            if (invalid == "already-attached") members[1] = s.Customer;
            var existing = s.Customer.VisitParty;
            Assert.That(CreateModel(s, members, out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(first.VisitParty, Is.Null, "No partially attached first member");
            Assert.That(s.Customer.VisitParty, Is.SameAs(existing));
        }
    }

    [Test]
    public void TwoMemberModel_IsImmutable_AndActualSingleRoutesRejectBeforeMutation()
    {
        using (var s = new Flow())
        {
            // A two-member party is constructed only by this isolated model test.
            s.Customer.SetData(s.Data, s.Controller, s.Manager);
            var second = SpawnPrepared(s);
            Assert.That(CreateModel(s, new[] { s.Customer, second }, out var party), Is.True);
            Assert.That(party.Members.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<CustomerVisitMember>)party.Members).Clear());
            QueueOne(s);
            var random = UnityEngine.Random.state; var before = s.Snapshot(); var floor = s.Customer.VisitFloor;
            Assert.That(s.Controller.GuideCustomer(Vector3.one), Is.False);
            Assert.That(s.Snapshot(), Is.EqualTo(before));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(s.Customer.VisitFloor, Is.EqualTo(floor));
            Assert.That(s.Controller.Count, Is.EqualTo(1)); Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(party.CurrentTable, Is.Null); Assert.That(party.Members.All(m => m.SeatIndex == -1), Is.True);
            Assert.That(party.IsActive, Is.True);
        }
    }

    [TestCase("normal")]
    [TestCase("anger")]
    [TestCase("waiting")]
    [TestCase("pool")]
    public void EndingVisit_DetachesRelations_AndOldEndCannotClearReusedVisit(string end)
    {
        using (var s = new Flow())
        {
            var old = s.Customer.VisitParty; object visit = Visit(s.Customer); Action exit = null;
            if (end == "normal") { s.GuideAndSeat(); s.OrderAndCook(false); s.Eat(); s.Settle(); exit = Get<Action>(s.Customer, "_moveCompleted"); }
            if (end == "anger") { s.GuideAndSeat(); s.Manager.AngerExitCustomer(s.Table); exit = Get<Action>(s.Customer, "_moveCompleted"); }
            if (end == "waiting") { QueueOne(s); Invoke(s.Customer, "LeaveRestaurant"); exit = Get<Action>(s.Customer, "_moveCompleted"); }
            if (end == "pool") ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            Assert.That(old.IsActive, Is.False); Assert.That(s.Customer.VisitParty, Is.Null);
            Assert.That(old.CurrentTable, Is.Null); Assert.That(old.Members[0].SeatIndex, Is.EqualTo(-1));
            Assert.That(Invoke(s.Controller, "CreateSingleVisit", s.Customer), Is.False, "Ended visit requires SetData");
            s.NextCustomer(true);
            var current = s.Customer.VisitParty;
            Assert.That(current, Is.Not.Null); Assert.That(Visit(s.Customer), Is.Not.SameAs(visit));
            Invoke(old, "End"); exit?.Invoke(); Invoke(old, "End");
            Assert.That(s.Customer.VisitParty, Is.SameAs(current)); Assert.That(s.Customer.gameObject.activeSelf, Is.True);
        }
    }

    [TestCase("controller", false)]
    [TestCase("controller", true)]
    [TestCase("manager", false)]
    [TestCase("manager", true)]
    [TestCase("scene", false)]
    [TestCase("scene", true)]
    [TestCase("account", false)]
    [TestCase("account", true)]
    [TestCase("stage", false)]
    [TestCase("stage", true)]
    public void ExecutionEndsWaitingAndSeatedRelations_WithoutChangingSavedIds(string boundary, bool seated)
    {
        using (var s = new Flow())
        {
            var backend = boundary == "account" ? s.InstallAccount() : null;
            QueueOne(s);
            UserInfo.SetWaitingCustomerIds(EStage.Stage1, new List<string> { s.Data.Id, s.Data.Id });
            Action held;
            if (seated) held = s.Guide();
            else
            {
                // Sorting resets readiness when it starts a real movement request.
                Invoke(s.Controller, "SortCustomerLine"); held = Get<Action>(s.Customer, "_moveCompleted");
            }
            var party = s.Customer.VisitParty;
            var saved = UserInfo.GetWaitingCustomerIds(EStage.Stage1).ToArray();
            if (boundary == "controller") Invoke(s.Controller, "OnDisable");
            if (boundary == "manager") Invoke(s.Manager, "OnDisable");
            if (boundary == "scene")
                ((Action)typeof(LoadingSceneManager).GetField("OnLoadSceneHandler", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null))();
            if (boundary == "account") backend.InvalidateGameDataRestore();
            if (boundary == "stage") UserInfo.ChangeStage(EStage.Stage2);
            Invoke(s.Controller, "RefreshVisitParties");
            Assert.That(party.IsActive, Is.False); Assert.That(s.Customer.VisitMember, Is.Null);
            Assert.That(party.CurrentTable, Is.Null); Assert.That(s.Use, Is.Null);
            int count = s.Controller.WaitCount; var before = s.Snapshot(); held();
            Assert.That(s.Controller.WaitCount, Is.EqualTo(count)); Assert.That(s.Snapshot(), Is.EqualTo(before));
            Assert.That(UserInfo.GetWaitingCustomerIds(EStage.Stage1), Is.EqualTo(saved));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SpecialAndGatecrasher_ActualPathStopAndPoolReuseStillWork(bool gatecrasher)
    {
        using (var s = new Flow())
        {
            s.Swap(typeof(ObjectPoolManager), "_specialCustomerPool", new Queue<SpecialCustomer>());
            s.Swap(typeof(ObjectPoolManager), "_gatecrasherCustomerPool", new Queue<GatecrasherCustomer>());
            s.Swap(typeof(ObjectPoolManager), "_specialCustomerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/SpecialCustomer.prefab").GetComponent<SpecialCustomer>());
            s.Swap(typeof(ObjectPoolManager), "_gatecrasherCustomerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/GatecrasherCustomer.prefab").GetComponent<GatecrasherCustomer>());
            Customer Spawn() => gatecrasher ? (Customer)ObjectPoolManager.Instance.SpawnGatecrasherCustomer(Vector3.zero, Quaternion.identity)
                : ObjectPoolManager.Instance.SpawnSpecialCustomer(Vector3.zero, Quaternion.identity);
            var npc = Spawn(); Set(npc, "_tableManager", s.Manager);
            int completed = 0;
            var paths = Get<ConcurrentQueue<Action>>(PathfindingQueue.Instance, "_pathfindingQueue");
            npc.Move(Vector3.zero, 0, () => completed++);
            Assert.That(paths.TryDequeue(out var validPath), Is.True); validPath(); MainThreadDispatcher.Instance.Update();
            Assert.That(completed, Is.EqualTo(1));
            npc.Move(Vector3.one, 0, () => completed += 100);
            Assert.That(paths.TryDequeue(out var oldPath), Is.True); npc.StopMove();
            oldPath(); MainThreadDispatcher.Instance.Update(); Assert.That(Get<Coroutine>(npc, "_moveCoroutine"), Is.Null);
            if (gatecrasher) ObjectPoolManager.Instance.DespawnGatecrasherCustomer((GatecrasherCustomer)npc);
            else ObjectPoolManager.Instance.DespawnSpecialCustomer((SpecialCustomer)npc);
            Assert.That(Spawn(), Is.SameAs(npc));
            npc.Move(Vector3.zero, 0, () => completed++);
            oldPath(); MainThreadDispatcher.Instance.Update(); Assert.That(completed, Is.EqualTo(1));
            Assert.That(paths.TryDequeue(out validPath), Is.True); validPath(); MainThreadDispatcher.Instance.Update();
            Assert.That(completed, Is.EqualTo(2));
            Assert.That(npc, Is.Not.InstanceOf<NormalCustomer>());
        }
    }
}
#endif
