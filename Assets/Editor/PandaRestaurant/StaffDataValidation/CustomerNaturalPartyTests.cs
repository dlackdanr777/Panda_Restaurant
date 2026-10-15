#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;
using Object = UnityEngine.Object;

public sealed class CustomerNaturalPartyTests
{
    private static Dictionary<string, SaveCustomerData> Records => (Dictionary<string, SaveCustomerData>)typeof(UserInfo).GetField("_enabledCustomerDic", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
    internal static CustomerPartySettings Setup(Flow s, int visits = 25)
    {
        var config = s.Track(ScriptableObject.CreateInstance<CustomerPartySettings>());
        config.EnableNaturalParties = true; Set(s.Controller, "_naturalPartySettings", config);
        Records[s.Data.Id] = new SaveCustomerData(s.Data.Id, Records[s.Data.Id].SkinId, visits);
        s.Controller.NaturalPartyRoll = () => 0f; s.Controller.NaturalCompanionIndex = count => 0;
        return config;
    }
    private static NormalCustomerData Candidate(Flow s, string id, bool enabled = true, bool known = true, CustomerTendencyType tendency = CustomerTendencyType.Normal)
    {
        var data = s.Track(Object.Instantiate(s.Data)); Set(data, "_id", id); Set(data, "_tendencyType", tendency);
        Set(data, "_visitCountFoodDic", new Dictionary<int, string>());
        if (known) Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[id] = data;
        if (enabled && !string.IsNullOrWhiteSpace(id)) Records[id] = new SaveCustomerData(id, "", 0);
        Get<List<NormalCustomerData>>(CustomerDataManager.Instance, "_normalCustomerDataList").Add(data);
        return data;
    }
    private static string[] Select(Flow s, params NormalCustomerData[] candidates) => s.Controller.SelectNaturalPartyMembers(s.Data, candidates);
    internal static void RepresentativeSeed(int index, int count)
    {
        // Test-only seed search. The final Production attempt is made exactly once.
        for (int seed = 0; seed < 100; seed++)
        {
            UnityEngine.Random.InitState(seed); UnityEngine.Random.Range(0, 100);
            if (UnityEngine.Random.Range(0, count) != index) continue;
            UnityEngine.Random.InitState(seed); return;
        }
        Assert.Fail("No deterministic representative seed");
    }

    [TestCase(24, 1, 0)] [TestCase(25, 2, 1)] [TestCase(26, 2, 1)]
    public void CompletedVisits_AreReadBeforeSpawn_WithoutIncrement(int visits, int size, int draws)
    {
        using (var s = new Flow())
        {
            Setup(s, visits); var b = Candidate(s, "B"); int rolls = 0, choices = 0, visitEvents = 0;
            UserInfo.OnVisitedCustomerHandler += () => visitEvents++;
            s.Controller.NaturalPartyRoll = () => { rolls++; return 0; };
            s.Controller.NaturalCompanionIndex = count => { choices++; return 0; };
            Assert.That(Select(s, b).Length, Is.EqualTo(size)); Assert.That(rolls, Is.EqualTo(draws)); Assert.That(choices, Is.EqualTo(draws));
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(visits)); Assert.That(UserInfo.GetVisitedCustomerCount(b.Id), Is.Zero);
            Assert.That(visitEvents, Is.Zero);
        }
    }
    [TestCase(0f, 2)] [TestCase(.199999f, 2)] [TestCase(.2f, 1)] [TestCase(.200001f, 1)] [TestCase(1f, 1)]
    public void TwentyPercent_UsesOneStrictBoundaryDraw(float roll, int size)
    {
        using (var s = new Flow())
        {
            Setup(s); var b = Candidate(s, "B"); int draws = 0, choices = 0;
            s.Controller.NaturalPartyRoll = () => { draws++; return roll; };
            s.Controller.NaturalCompanionIndex = count => { choices++; Assert.That(count, Is.EqualTo(1)); return 0; };
            Assert.That(Select(s, b).Length, Is.EqualTo(size)); Assert.That(draws, Is.EqualTo(1)); Assert.That(choices, Is.EqualTo(size - 1));
        }
    }
    [TestCase("disabled")] [TestCase("tutorial")] [TestCase("no-candidate")] [TestCase("unloaded-record")]
    public void IneligibleOpportunities_DoNotConsumePartyRandom(string reason)
    {
        using (var s = new Flow())
        {
            var config = Setup(s); var b = Candidate(s, "B");
            if (reason == "disabled") config.EnableNaturalParties = false;
            if (reason == "tutorial") UserInfo.IsTutorialStart = true;
            if (reason == "unloaded-record") Records.Remove(s.Data.Id);
            s.Controller.NaturalPartyRoll = () => throw new Exception("Unexpected party RNG");
            s.Controller.NaturalCompanionIndex = count => throw new Exception("Unexpected companion RNG");
            var before = UnityEngine.Random.state;
            Assert.That(Select(s, reason == "no-candidate" ? s.Data : b), Is.EqualTo(new[] { s.Data.Id }));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(before));
        }
    }
    [TestCase("missing")] [TestCase("negative-visits")] [TestCase("negative-probability")] [TestCase("above-one")] [TestCase("nan")]
    public void InvalidSettings_WarnOnce_AndRetainSingleton(string reason)
    {
        using (var s = new Flow())
        {
            var config = Setup(s); var b = Candidate(s, "B");
            if (reason == "missing") Set(s.Controller, "_naturalPartySettings", null);
            if (reason == "negative-visits") config.MinimumNormalVisits = -1;
            if (reason == "negative-probability") config.CompanionProbability = -.1f;
            if (reason == "above-one") config.CompanionProbability = 20f;
            if (reason == "nan") config.CompanionProbability = float.NaN;
            s.Controller.NaturalPartyRoll = () => throw new Exception("Invalid settings consumed RNG");
            LogAssert.Expect(LogType.Warning, "[CustomerController] Natural party settings are missing or invalid; new natural visits remain single. Existing and saved parties are unaffected.");
            for (int i = 0; i < 3; i++) Assert.That(Select(s, b).Length, Is.EqualTo(1));
            Assert.That(s.Controller.LastNaturalPartyDiagnostic, Is.EqualTo("NaturalPartySettingsMissingOrInvalid"));
        }
    }
    [TestCase(0, "B")] [TestCase(1, "C")]
    public void UniqueIds_AreTheUniformDenominator_ZeroVisitCompanionAllowed(int index, string expected)
    {
        using (var s = new Flow())
        {
            Setup(s); var b = Candidate(s, "B"); var c = Candidate(s, "C");
            s.Controller.NaturalCompanionIndex = count => { Assert.That(count, Is.EqualTo(2)); return index; };
            Assert.That(Select(s, s.Data, b, b, c, c, c), Is.EqualTo(new[] { s.Data.Id, expected }));
            Assert.That(UserInfo.GetVisitedCustomerCount(expected), Is.Zero);
        }
    }
    [Test]
    public void InvalidLockedUnknownSpecialGateAndDisallowedCandidates_AreNeverRelaxed()
    {
        using (var s = new Flow())
        {
            Setup(s); var locked = Candidate(s, "LOCKED", false); var unknown = Candidate(s, "UNKNOWN", true, false);
            var sensitive = Candidate(s, "SENSITIVE", tendency: CustomerTendencyType.Sensitive);
            var highly = Candidate(s, "HIGH", tendency: CustomerTendencyType.HighlySensitive);
            var invalid = Candidate(s, "BAD-TENDENCY", tendency: (CustomerTendencyType)999);
            var empty = Candidate(s, ""); var special = Candidate(s, "SPECIAL"); var gate = Candidate(s, "GATE");
            var catalog = Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic");
            catalog[special.Id] = s.Track(ScriptableObject.CreateInstance<SpecialCustomerData>());
            catalog[gate.Id] = s.Track(ScriptableObject.CreateInstance<GatecrasherCustomer1Data>());
            Set(Get<SatisfactionSystem>(s.Controller, "_satisfactionSystem"), "_satisfaction", -30f);
            s.Controller.NaturalPartyRoll = () => throw new Exception("No eligible candidates must skip RNG");
            Assert.That(Select(s, null, locked, unknown, sensitive, highly, invalid, empty, special, gate, s.Data).Length, Is.EqualTo(1));
            Assert.That(CustomerDataManager.Instance.GetAppearNormalCustomerList(), Has.No.Member(unknown));
            Assert.That(CustomerDataManager.Instance.GetAppearNormalCustomerList(), Has.No.Member(special));
        }
    }
    [Test]
    public void RepresentativeFallback_RemainsSingle_WhenAllCompanionsFailTendency()
    {
        using (var s = new Flow())
        {
            Setup(s); Set(s.Data, "_tendencyType", CustomerTendencyType.HighlySensitive);
            Candidate(s, "B", tendency: CustomerTendencyType.HighlySensitive); Records["B"] = new SaveCustomerData("B", "", 25);
            s.Controller.NaturalPartyRoll = () => throw new Exception("Fallback must not supply a companion");
            Assert.That(s.Controller.TryAddCustomer(), Is.True); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(1));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void DisabledAndTutorial_NaturalSpawnRetainsOriginalRandomSequence(bool tutorial)
    {
        using (var s = new Flow())
        {
            var config = Setup(s); config.EnableNaturalParties = tutorial; UserInfo.IsTutorialStart = tutorial;
            var b = Candidate(s, "B"); s.Controller.NaturalPartyRoll = () => throw new Exception("Extra RNG");
            UnityEngine.Random.InitState(4001); UnityEngine.Random.Range(0, 100); int representative = UnityEngine.Random.Range(0, 2);
            var expected = UnityEngine.Random.state;
            UnityEngine.Random.InitState(4001); s.Controller.AddCustomer();
            Assert.That(s.Controller.WaitingParties.Single().Members.Single().Customer.NormalCustomerData.Id, Is.EqualTo(representative == 0 ? s.Data.Id : b.Id));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(expected));
        }
    }
    [TestCase(19, true)] [TestCase(20, false)]
    public void NaturalPair_ReservesOneTeamSlot_WhileStillEntering(int existing, bool success)
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Set(GameManager.Instance, "_maxWaitCustomerCount", 20);
            for (int i = 0; i < existing; i++) Assert.That(s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id }, out _), Is.True);
            RepresentativeSeed(0, 2); Assert.That(s.Controller.TryAddCustomer(), Is.EqualTo(success));
            Assert.That(s.Controller.Count, Is.EqualTo(20)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(success ? 21 : 20));
            Assert.That(s.Controller.WaitCount, Is.Zero); Assert.That(UserInfo.PromotionCount, Is.EqualTo(success ? 1 : 0));
        }
    }
    [Test]
    public void NaturalSecondAllocationFailure_ReleasesEverythingWithoutRetryOrCallCredit()
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); int draws = 0, changes = 0, called = 0;
            s.Controller.NaturalPartyRoll = () => { draws++; return 0; };
            s.Controller.OnChangeCustomerHandler += () => changes++; s.Controller.OnAddCustomerHandler += () => called++;
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer); s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            string saved = UserInfo.GetWaitingPartySave(EStage.Stage1).Serialize(); RepresentativeSeed(0, 2);
            for (int i = 0; i < GameManager.Instance.TotalTabCount; i++) s.Controller.AddTabCount();
            Assert.That(draws, Is.EqualTo(1)); Assert.That(s.Controller.Count, Is.Zero); Assert.That(UserInfo.PromotionCount, Is.Zero);
            Assert.That(changes, Is.Zero); Assert.That(called, Is.Zero); Assert.That(s.Customer.gameObject.activeSelf, Is.False);
            Assert.That(Get<Queue<NormalCustomer>>(ObjectPoolManager.Instance, "_normalCustomerPool").Count, Is.EqualTo(1));
            Assert.That(UserInfo.GetWaitingPartySave(EStage.Stage1).Serialize(), Is.EqualTo(saved));
        }
    }
    [Test]
    public void NaturalSave_ParamParseRestore_KeepsMembersDespiteDisabledChangedSettings()
    {
        using (var s = new Flow())
        {
            var config = Setup(s); Candidate(s, "B"); RepresentativeSeed(0, 2); s.Controller.AddCustomer();
            var original = s.Controller.WaitingParties.Single(); var ids = original.Members.Select(m => m.Customer.NormalCustomerData.Id).ToArray();
            var plain = JObject.Parse(s.Stage.SaveData().GetParam().GetJson());
            var saved = WaitingPartySaveData.ParsePlain(plain); Assert.That(saved.Teams.Single(), Is.EqualTo(ids));
            Invoke(s.Controller, "EndVisitParties"); Set(s.Stage, "_waitingParties", saved);
            config.EnableNaturalParties = false; config.MinimumNormalVisits = 999; config.CompanionProbability = 0;
            Records[s.Data.Id] = new SaveCustomerData(s.Data.Id, "", 0);
            s.Controller.NaturalPartyRoll = () => throw new Exception("Restore rerolled");
            Invoke(s.Controller, "LoadWaitingCustomers"); var restored = s.Controller.WaitingParties.Single();
            Assert.That(restored, Is.Not.SameAs(original)); Assert.That(restored.Members.Select(m => m.Customer.NormalCustomerData.Id), Is.EqualTo(ids));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void TwentyFifthNormalCompletion_ChangesOnlyTheNextVisitEligibility()
    {
        using (var s = new Flow())
        {
            Setup(s, 24); var b = Candidate(s, "B"); Assert.That(Select(s, b).Length, Is.EqualTo(1));
            s.GuideAndSeat(); s.OrderAndCook(false); s.Eat(); s.Settle();
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(25)); Assert.That(Select(s, b).Length, Is.EqualTo(2));
            Assert.That(s.Customer.VisitParty == null || s.Customer.VisitParty.Members.Count == 1, Is.True);
        }
    }
    [Test]
    public void DeliveredAsset_AndStageReference_AreDefaultOff25AndPoint20()
    {
        var config = AssetDatabase.LoadAssetAtPath<CustomerPartySettings>("Assets/Resources/CustomerPartySettings.asset");
        Assert.That(config, Is.Not.Null); Assert.That(config.EnableNaturalParties, Is.False);
        Assert.That(config.MinimumNormalVisits, Is.EqualTo(25)); Assert.That(config.CompanionProbability, Is.EqualTo(.2f));
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
        Assert.That(File.ReadAllText("Assets/Scenes/Stage1.unity"), Does.Contain("_naturalPartySettings: {fileID: 11400000, guid: " + guid + ", type: 2}"));
    }
    [Test]
    public void TempSettingsAsset_ReloadAffectsNewSpawnOnly_AndNeverChangesDelivery()
    {
        string path = "Assets/PartySettingsTest-" + Guid.NewGuid().ToString("N") + ".asset";
        try
        {
            using (var s = new Flow())
            {
                var config = ScriptableObject.CreateInstance<CustomerPartySettings>(); config.EnableNaturalParties = true;
                config.MinimumNormalVisits = 26; config.CompanionProbability = 1;
                AssetDatabase.CreateAsset(config, path); AssetDatabase.SaveAssetIfDirty(config);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<CustomerPartySettings>(path); Setup(s); Candidate(s, "B"); Set(s.Controller, "_naturalPartySettings", loaded);
                RepresentativeSeed(0, 2); s.Controller.AddCustomer(); var first = s.Controller.WaitingParties.Single();
                Assert.That(first.Members.Count, Is.EqualTo(1));
                loaded.MinimumNormalVisits = 25; EditorUtility.SetDirty(loaded); AssetDatabase.SaveAssetIfDirty(loaded); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                RepresentativeSeed(0, 2); s.Controller.AddCustomer(); var second = s.Controller.WaitingParties.Last();
                Assert.That(second.Members.Count, Is.EqualTo(2)); loaded.EnableNaturalParties = false;
                Assert.That(first.Members.Count, Is.EqualTo(1)); Assert.That(second.Members.Count, Is.EqualTo(2));
            }
        }
        finally { AssetDatabase.DeleteAsset(path); }
        DeliveredAsset_AndStageReference_AreDefaultOff25AndPoint20();
    }

    [TestCase(false)] [TestCase(true)]
    public void NaturalParty_UsesPersonalMenusSkinsAdditionalOrdersAndCompletion_WhenSettingsTurnOff(bool interrupt)
    {
        using (var p = new CustomerPartyOrderTests.Pair(ordersA: 1, ordersB: 2, natural: true))
        {
            var s = p.S; var original = p.Party; var use = s.Table.CurrentUse;
            Assert.That(p.Batch.Orders.Select(f => f.Food.Id), Is.EqualTo(new[] { s.Food.Id, p.SecondFood.Id }));
            Assert.That(original.Members.Select(m => m.Customer.OrderCount), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(25)); Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id), Is.Zero);
            Get<CustomerPartySettings>(s.Controller, "_naturalPartySettings").EnableNaturalParties = false;
            s.Controller.NaturalPartyRoll = () => throw new Exception("Existing party must not reroll");
            s.Table.RefreshPartyOrderButtons(); Invoke(s.Controller, "SortCustomerLine");
            CustomerPartyCompletionTests.CookRound(p, true);
            CustomerPartyCompletionTests.Eat(p, p.Batch.Orders[0]);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(26));
            if (interrupt)
            {
                Assert.That(s.Manager.AbortPartyService(use, "Natural test service failure"), Is.True);
                Assert.That(use.ServicePenaltyApplied, Is.True); Assert.That(s.Stage.Satisfaction, Is.EqualTo(-10));
                Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id), Is.Zero);
            }
            else
            {
                CustomerPartyCompletionTests.Eat(p, p.Batch.Orders[1]);
                Assert.That(p.Batch.Round, Is.EqualTo(2)); Assert.That(p.Batch.Orders.Single().Member.Customer.NormalCustomerData.Id, Is.EqualTo(p.SecondData.Id));
                CustomerPartyCompletionTests.CookRound(p); CustomerPartyCompletionTests.Eat(p, p.Batch.Orders.Single());
                Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id), Is.EqualTo(1));
                Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(2));
            }
            p.ArriveExit(0); p.ArriveExit(1);
            Assert.That(use.SettlementCompleted, Is.True); Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
            Assert.That(original.Members.All(m => !m.Customer.gameObject.activeSelf), Is.True);
        }
    }

    [TestCase("manual")] [TestCase("marketer")] [TestCase("auto-skill")]
    public void ExistingCallEntryPoints_CreateOneNaturalTeamForTheOriginalTapAmount(string path)
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); RepresentativeSeed(0, 2);
            int calls = 0; s.Controller.OnAddCustomerHandler += () => calls++;
            if (path == "manual")
            {
                var ui = s.Create<UIAddCutomerController>(); Set(ui, "_customerController", s.Controller);
                for (int i = 0; i < GameManager.Instance.TotalTabCount; i++) ui.OnAddCustomerButtonClicked(false);
            }
            else if (path == "marketer")
            {
                var data = s.Track(ScriptableObject.CreateInstance<MarketerData>()); Set(data, "_id", "MARKETER");
                Set(data, "_marketerLevelData", new[] { new MarketerLevelData() });
                Get<Dictionary<string, SaveStaffData>>(s.Stage, "_giveStaffDic")[data.Id] = new SaveStaffData(data.Id, 1);
                var staff = s.Create<Staff>(); Set(staff, "_staffData", data); var action = new MarketerAction(staff, s.Controller);
                for (int i = 0; i < GameManager.Instance.TotalTabCount; i++) action.PerformAction(staff);
            }
            else
            {
                var staff = s.Create<Staff>(); var token = staff.RuntimeSkillContext.BeginActivation("Natural auto-call test"); Set(staff, "_activeSkillToken", token);
                var skill = s.Track(ScriptableObject.CreateInstance<TouchAddCustomerButtonSkill>()); Set(skill, "_touchInterval", 1f);
                Set(staff.RuntimeSkillContext, "<CustomerCallElapsedTime>k__BackingField", (float)GameManager.Instance.TotalTabCount);
                skill.ActivateUpdate(staff, s.Manager, null, s.Controller);
                staff.RuntimeSkillContext.ResetLocalState();
            }
            Assert.That(s.Controller.Count, Is.EqualTo(1)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(2));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1)); Assert.That(calls, Is.EqualTo(1)); Assert.That(s.Controller.TabCount, Is.Zero);
        }
    }

    [Test]
    public void PromotionAmount_RemainsTeamOpportunities_AndCapacityKeeps10To30()
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Records["B"] = new SaveCustomerData("B", "", 25);
            Assert.That(GameManager.Instance.MaxWaitCustomerCount, Is.EqualTo(10));
            GameManager.Instance.AppendPromotionCustomer(2); Assert.That(GameManager.Instance.AddPromotionCustomer, Is.EqualTo(3));
            s.Controller.AddCustomer(); Assert.That(s.Controller.Count, Is.EqualTo(3)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(6));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(3));
            Set(GameManager.Instance, "_addGachaItemWaitCustomerMaxCount", 100);
            Assert.That(GameManager.Instance.MaxWaitCustomerCount, Is.EqualTo(30));
        }
    }

    [Test]
    public void FeverActualRoutine_KeepsEightTapOpportunitiesForOneNaturalTeam()
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Records["B"] = new SaveCustomerData("B", "", 25);
            var fever = Get<FeverSystem>(s.Manager, "_feverSystem"); var main = s.Create<MainScene>();
            Set(main, "_feverSystem", fever); Set(fever, "_mainScene", main); Set(fever, "_customerController", s.Controller);
            var clip = s.Track(AudioClip.Create("Offline same music", 100, 1, 44100, false));
            Set(main, "_mainSceneMusic", clip); Set(main, "_feverMusic", clip); Set(SoundManager.Instance, "_currentBackgroundClip", clip);
            var context = GameManager.Instance.FeverRuntimeContext; Assert.That(context.TryActivate(60, out var token), Is.True);
            Set(fever, "_feverRuntimeContext", context); Set(fever, "_activeFeverToken", token);
            var routine = (IEnumerator)Invoke(fever, "StartFeverRoutine", context, token);
            Assert.That(routine.MoveNext(), Is.True);
            for (int i = 0; i < GameManager.Instance.TotalTabCount; i++)
            {
                Set(context, "_autoCallAccumulatorSeconds", .5d);
                Assert.That(routine.MoveNext(), Is.True);
            }
            Assert.That(s.Controller.Count, Is.EqualTo(1)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(2));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(1)); Assert.That(s.Controller.TabCount, Is.Zero);
            ((IDisposable)routine).Dispose();
        }
    }

    [Test]
    public void PartialPromotionSuccess_ConsumesTapBundle_AndDoesNotGiveImmediateRetry()
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Records["B"] = new SaveCustomerData("B", "", 25);
            GameManager.Instance.AppendPromotionCustomer(1); int calls = 0;
            s.Controller.OnAddCustomerHandler += () => calls++;
            s.Controller.OnChangeCustomerHandler += () => s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            for (int i = 0; i < GameManager.Instance.TotalTabCount; i++) s.Controller.AddTabCount();
            Assert.That(s.Controller.Count, Is.EqualTo(1)); Assert.That(UserInfo.PromotionCount, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1)); Assert.That(s.Controller.TabCount, Is.Zero);
            s.Controller.AddTabCount(); Assert.That(s.Controller.TabCount, Is.EqualTo(1)); Assert.That(s.Controller.Count, Is.EqualTo(1));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void AdRewardFill_UsesRemainingTeams_AndStopsTechnicalFailure(bool fail)
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Records["B"] = new SaveCustomerData("B", "", 25);
            Set(GameManager.Instance, "_maxWaitCustomerCount", 20);
            for (int i = 0; i < 19; i++) s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id }, out _);
            var main = s.Create<UIMain>(); var watch = s.Create<WatchAdButton>();
            var text = s.Create<TMPro.TextMeshProUGUI>(); var image = s.Create<UIImageAndText>(); Set(image, "_text", s.Create<TMPro.TextMeshProUGUI>());
            Set(main, "_customerController", s.Controller); Set(main, "_watchAdButton", watch); Set(main, "_customerCountText", text);
            Set(main, "_customerCountImage", image); Set(main, "_customerMaxCountImage", s.Create<Transform>().gameObject);
            Invoke(main, "Awake"); // Binds the actual rewarded callback; WatchAdButton.Awake/SDK never runs.
            s.Swap(typeof(UserInfo), "_addCustomerAdCount", 0);
            int draws = 0; s.Controller.NaturalPartyRoll = () => { draws++; return 0; };
            if (fail) s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            main.gameObject.SetActive(true);
            Get<Action>(watch, "OnAdRewarded")(); // SDK-free fake reward; the real UIMain listener starts the first fill iteration.
            main.StopAllCoroutines();
            Assert.That(UserInfo.AddCustomerAdCount, Is.EqualTo(1));
            Assert.That(draws, Is.EqualTo(1));
            // Finish the yielded success path explicitly; a failed fill has already ended.
            var fill = (IEnumerator)Invoke(main, "AddCustomerRoutine");
            if (!fail) while (fill.MoveNext()) { }
            Assert.That(draws, Is.EqualTo(1)); Assert.That(s.Controller.Count, Is.EqualTo(fail ? 19 : 20));
            Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(fail ? 19 : 21));
            Assert.That(UserInfo.PromotionCount, Is.EqualTo(fail ? 0 : 1)); Assert.That(text.text, Is.EqualTo(fail ? "X 1" : "X 0"));
            Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        }
    }

    [Test]
    public void SelectionSample_RecordsEverySeededEligibleOpportunityWithoutOutcomeCorrection()
    {
        using (var s = new Flow())
        {
            Setup(s); var candidates = new[] { Candidate(s, "B"), Candidate(s, "C"), Candidate(s, "D") };
            const int seed = 4102026, samples = 2000; int draws = 0, choices = 0, hits = 0;
            var rng = new System.Random(seed); var counts = candidates.ToDictionary(c => c.Id, c => 0);
            s.Controller.NaturalPartyRoll = () => { draws++; return (float)rng.NextDouble(); };
            s.Controller.NaturalCompanionIndex = count => { choices++; Assert.That(count, Is.EqualTo(3)); return rng.Next(count); };
            for (int i = 0; i < samples; i++) { var ids = Select(s, candidates); if (ids.Length == 2) { hits++; counts[ids[1]]++; } }
            Assert.That(draws, Is.EqualTo(samples)); Assert.That(choices, Is.EqualTo(hits)); Assert.That(counts.Values.Sum(), Is.EqualTo(hits));
            string output = Environment.GetEnvironmentVariable("PANDA_CUSTOMER_USE_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "natural-probability-sample.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
                seed, samples, eligibleWithCandidates = samples, uniqueCompanionCandidates = 3, draws, hits, misses = samples - hits, counts,
                note = "All samples retained. This is conditional selection evidence, not the overall visit ratio or an economy validation. No scene customers spawned." }, Newtonsoft.Json.Formatting.Indented));
        }
    }

    [Test]
    public void FailedFillEnumerator_EndsImmediatelyInsteadOfYieldingAnotherDraw()
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); Records["B"] = new SaveCustomerData("B", "", 25);
            s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            var main = s.Create<UIMain>(); Set(main, "_customerController", s.Controller);
            Set(main, "_watchAdButton", s.Create<WatchAdButton>()); Set(main, "_customerCountText", s.Create<TMPro.TextMeshProUGUI>());
            int draws = 0; s.Controller.NaturalPartyRoll = () => { draws++; return 0; };
            var fill = (IEnumerator)Invoke(main, "AddCustomerRoutine");
            Assert.That(fill.MoveNext(), Is.False); Assert.That(fill.MoveNext(), Is.False);
            Assert.That(draws, Is.EqualTo(1)); Assert.That(s.Controller.Count, Is.Zero); Assert.That(UserInfo.PromotionCount, Is.Zero);
        }
    }

    [TestCase("account")] [TestCase("stage")]
    public void ContextReplacement_DoesNotMixOldTeamsOrPersonalEligibility(string boundary)
    {
        using (var s = new Flow())
        {
            Setup(s); Candidate(s, "B"); RepresentativeSeed(0, 2); s.Controller.AddCustomer(); var old = s.Controller.WaitingParties.Single();
            if (boundary == "account")
            {
                s.InstallAccount();
                s.Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData> {
                    [s.Data.Id] = new SaveCustomerData(s.Data.Id, "", 24), ["B"] = new SaveCustomerData("B", "", 0) });
            }
            else UserInfo.ChangeStage(EStage.Stage2);
            Assert.That(old.IsActive, Is.False); Assert.That(s.Controller.Count, Is.Zero);
            RepresentativeSeed(0, 2); s.Controller.AddCustomer(); var current = s.Controller.WaitingParties.Single();
            Assert.That(current, Is.Not.SameAs(old)); Assert.That(current.Members.Count, Is.EqualTo(boundary == "account" ? 1 : 2));
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(boundary == "account" ? 24 : 25));
            Assert.That(old.Members.Any(m => ReferenceEquals(m.Party, current)), Is.False);
            if (boundary == "stage") { UserInfo.ChangeStage(EStage.Stage1); Assert.That(current.IsActive, Is.False); Assert.That(old.IsActive, Is.False); }
        }
    }

    [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
    public void ExceptionalSpawn_StaysOutsideNaturalPartySelection(bool gate, bool naturalEnabled)
    {
        using (var s = new Flow())
        {
            var config = Setup(s); config.EnableNaturalParties = naturalEnabled;
            s.Controller.NaturalPartyRoll = () => throw new Exception("Exceptional branch entered natural selection");
            s.Controller.NaturalCompanionIndex = count => throw new Exception("Exceptional branch selected companion");
            CustomerData data = gate ? (CustomerData)s.Track(ScriptableObject.CreateInstance<GatecrasherCustomer2Data>())
                : s.Track(ScriptableObject.CreateInstance<SpecialCustomerData>());
            Set(data, "_id", "EXCEPTIONAL"); Set(data, "_sprite", s.NewSprite()); Set(data, "_spawnChance", 1f);
            Set(data, "_activeDuration", 20); Set(data, "_touchCount", 10);
            Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[data.Id] = data;
            Records[data.Id] = new SaveCustomerData(data.Id, "", 25);
            Set(s.Controller, "_breakCustomerEnabled", true); Set(s.Controller, "_uiCustomerTutorial", s.Create<UICustomerTutorial>());
            var commands = s.Create<SequentialCommandManager>();
            s.Swap(typeof(SequentialCommandManager), "_instance", commands);
            var queueField = typeof(SequentialCommandManager).GetField("_commandQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var comparerType = typeof(SequentialCommandManager).GetNestedType("CommandComparer", System.Reflection.BindingFlags.NonPublic);
            queueField.SetValue(commands, Activator.CreateInstance(queueField.FieldType, new[] { Activator.CreateInstance(comparerType, true) }));
            Set(commands, "_dontTouchCanvas", s.Create<Canvas>());
            Set(commands, "_isExecuting", true); // Keep the unrelated modal command pending in the disposable fixture.
            Set(s.Controller, "_specialCustomerTargetPosList", new List<Vector3> { Vector3.zero, Vector3.one });
            Set(s.Controller, "_gatecrasherCustomer2TargetPos", new[] { s.Create<Transform>(), s.Create<Transform>(), s.Create<Transform>() });
            if (gate)
            {
                Get<List<GatecrasherCustomerData>>(CustomerDataManager.Instance, "_gatecrasherCustomerDataList").Add((GatecrasherCustomerData)data);
                s.Swap(typeof(ObjectPoolManager), "_gatecrasherCustomerPool", new Queue<GatecrasherCustomer>());
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/GatecrasherCustomer.prefab").GetComponent<GatecrasherCustomer>();
                s.Swap(typeof(ObjectPoolManager), "_gatecrasherCustomerPrefab", prefab);
                Set(data, "_controller", Get<Animator>(prefab, "_animator").runtimeAnimatorController);
            }
            else
            {
                Get<List<SpecialCustomerData>>(CustomerDataManager.Instance, "_specialCustomerDataList").Add((SpecialCustomerData)data);
                s.Swap(typeof(ObjectPoolManager), "_specialCustomerPool", new Queue<SpecialCustomer>());
                s.Swap(typeof(ObjectPoolManager), "_specialCustomerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/SpecialCustomer.prefab").GetComponent<SpecialCustomer>());
                s.Swap(typeof(UserInfo), "_totalVisitSpecialCustomerCount", 0);
            }
            int seed = 0;
            for (; seed < 1000; seed++) { UnityEngine.Random.InitState(seed); if (UnityEngine.Random.Range(0, 100) < ConstValue.DEFAULT_EXCEPTIONAL_CUSTOMER_SPAWN_PERCENT) break; }
            Assert.That(seed, Is.LessThan(1000)); UnityEngine.Random.InitState(seed);
            Assert.That(s.Controller.TryAddCustomer(), Is.True);
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(UserInfo.PromotionCount, Is.Zero);
            Assert.That(UserInfo.GetVisitedCustomerCount(data.Id), Is.EqualTo(26));
            Assert.That(s.Stage.Satisfaction, Is.EqualTo(gate ? -5 : 5));
            Assert.That(s.Controller.LastNaturalPartyDiagnostic, Is.Null);
            Assert.That(TimeManager.Instance.GetTime("customerBreakTime"), Is.EqualTo(ConstValue.DEFAULT_CUSTOMER_BREAK_TIME));
            Assert.That(Get<bool>(s.Controller, "_breakCustomerEnabled"), Is.False);
        }
    }
}
#endif
