using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LitJson;
using Muks.MobileUI;
using Muks.Tween;
using Muks.UI;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;

public sealed class CustomerEncounterTests
{
    [Test]
    public void T01_FirstAppearance_QueuesExactPaniLineOnce()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.True);
        Assert.That(progress.TryGetNextPending("CUSTOMER01", out var record, out string message), Is.True);
        Assert.That(record.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
        Assert.That(message, Is.EqualTo("어, 옆집에 불이 켜졌네! 난 파니야. 잘 부탁해!"));
    }

    [Test]
    public void T02_ReappearanceAndPoolReuse_DoNotQueueFirstLineAgain()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.True);
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.False);
        Assert.That(progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 1), Is.False);
        Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
    }

    [TestCase(24, 25, 25)]
    [TestCase(49, 50, 50)]
    [TestCase(99, 100, 100)]
    [TestCase(199, 200, 200)]
    [TestCase(299, 300, 300)]
    [TestCase(399, 400, 400)]
    [TestCase(499, 500, 500)]
    public void T03_T05_OfficialVisitCrossingQueuesOnlyReachedMilestone(int previous, int current, int expected)
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(progress.RecordCompletedVisit("CUSTOMER01", previous, current), Is.True);
        Assert.That(progress.TryGetNextPending("CUSTOMER01", out var record, out _), Is.True);
        Assert.That(record.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.VisitMilestone));
        Assert.That(record.VisitMilestone, Is.EqualTo(expected));
        Assert.That(progress.RecordCompletedVisit("CUSTOMER01", previous, current), Is.False);
    }

    [Test]
    public void T06_T07_ClosedShopDoesNotDisplayAndDoesNotMarkPendingAsSeen()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        progress.RecordValidAppearance("CUSTOMER01", 0);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Hall, 1, false, false, true), Is.False);
        Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.False);
    }

    [Test]
    public void T08_NextValidAppearanceCanResumeDeferredEvent()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        progress.RecordValidAppearance("CUSTOMER01", 0);
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.False);
        Assert.That(progress.TryGetNextPending("CUSTOMER01", out var record, out string message), Is.True);
        Assert.That(record.CustomerId, Is.EqualTo("CUSTOMER01"));
        Assert.That(message, Is.Not.Empty);
    }

    [Test]
    public void T09_TutorialAndOtherBlockedContextsDoNotPassGate()
    {
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, true, true,
            RestaurantType.Hall, 0, false, false, true), Is.False);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(false, false, true,
            RestaurantType.Hall, 0, false, false, true), Is.False);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Kitchen, 0, true, false, true), Is.False);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Hall, 0, false, true, true), Is.False);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Hall, 0, false, false, false), Is.False);
    }

    [Test]
    public void RestaurantAdminOpeningOrOpenDefersPendingEncounter()
    {
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Hall, 0, false, false, true, shopOpenOrOpening: true), Is.False);
        Assert.That(CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
            RestaurantType.Hall, 0, false, false, true), Is.True);
    }

    [Test]
    public void T10_SaveRestore_RetainsSeenAndDeferredKeys()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        progress.RecordValidAppearance("CUSTOMER01", 0);
        progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0);
        progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
        string recordsJson = JsonMapper.ToJson(progress.ToSaveRecords());
        string payload = "[{\"CustomerEncounterStateVersion\":1,\"CustomerEncounterStateList\":" + recordsJson + "}]";
        var loaded = new LoadUserData(JsonMapper.ToObject<JsonData>(payload));
        Assert.That(loaded.IsValid, Is.True);
        Assert.That(loaded.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.Valid));
        Assert.That(loaded.CustomerEncounterRecords, Has.Count.EqualTo(2));
        Assert.That(loaded.CustomerEncounterRecords[0].EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
        Assert.That(loaded.CustomerEncounterRecords[0].VisitMilestone, Is.Zero);
        Assert.That(loaded.CustomerEncounterRecords[0].State, Is.EqualTo((int)CustomerEncounterEventState.Seen));
        Assert.That(loaded.CustomerEncounterRecords[1].EventKind, Is.EqualTo((int)CustomerEncounterEventKind.VisitMilestone));
        Assert.That(loaded.CustomerEncounterRecords[1].VisitMilestone, Is.EqualTo(25));
        Assert.That(loaded.CustomerEncounterRecords[1].State, Is.EqualTo((int)CustomerEncounterEventState.Pending));
        Assert.That(CustomerEncounterProgressData.TryRestore(loaded.CustomerEncounterRecords, out var restored, out _), Is.True);
        Assert.That(restored.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(restored.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
    }

    [Test]
    public void T11_APlusAAndTwoCustomerIds_UseIndependentStableKeys()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.True);
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.False);
        Assert.That(progress.RecordValidAppearance("CUSTOMER02", 0), Is.True);
        Assert.That(progress.RecordCompletedVisit("CUSTOMER01", 24, 25), Is.True);
        Assert.That(progress.RecordCompletedVisit("CUSTOMER01", 25, 26), Is.False);
        Assert.That(progress.TryGetNextPending("CUSTOMER01", out var first, out _), Is.True);
        Assert.That(first.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
        progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0);
        Assert.That(progress.TryGetNextPending("CUSTOMER01", out var milestone, out _), Is.True);
        Assert.That(milestone.VisitMilestone, Is.EqualTo(25));
        Assert.That(progress.TryGetNextPending("CUSTOMER02", out var secondCustomer, out _), Is.True);
        Assert.That(secondCustomer.CustomerId, Is.EqualTo("CUSTOMER02"));
    }

    [Test]
    public void T12_CatalogSupportsElevenCustomersWithExactA1LinesAndPerCustomerMilestones()
    {
        string[][] expected =
        {
            new[] { "어, 옆집에 불이 켜졌네! 난 파니야. 잘 부탁해!", "죽순은 잘 팔렸는데, 점심을 놓쳤지 뭐야.", "가게가 커지는 게 보여. 이웃으로서 뿌듯해!", "오늘은 쉬는 날! 남의 식당에서 쉬는 게 최고야.", "장사하다 막히면 말해. 손님 상대는 좀 알거든.", "길 건너에서도 여기 냄새가 나. 아주 좋은 냄새!", "바빠도 밥은 꼭 챙겨. 나처럼 놓치지 말고.", "처음 먼지 쌓였던 때 기억나? 이젠 참 북적거린다." },
            new[] { "나 호랑이야. 고양이는 아니고.", "여기선 나를 제대로 알아보는군.", "키는 아직 더 클 거야. 정말이야.", "고양이? 아까 밖에서 들은 얘기야. 신경 안 써.", "큰 목소리를 내야 커 보이는 건 아니더라.", "네가 웃으면 놀리는 건지 헷갈려.", "이젠 먼저 정정하지 않아도 알아주겠지?", "여기 오는 길은 눈 감고도 찾아." },
            new[] { "수련 중이야. 잠깐 쉬러 왔어.", "오늘도 마늘 냄새가 나려나.", "쑥만 먹은 지 몇 주째지? 세는 걸 잊었어.", "선생님은 참는 것도 수행이라셨지.", "구미호가 되면 뭘 먼저 할까? 아직 비밀.", "가끔은 다른 향이 그리워.", "쉬어 가는 것도 수련일 수 있겠지.", "꼬리가 늘었는지는 묻지 마. 아직 확인 중이야." },
            new[] { "먹기 위해 뛰고, 뛰기 위해 먹어!", "운동하고 나서 쉬는 시간이 최고야.", "아침에 멀리 한 바퀴 돌고 왔어.", "달콤한 건 마음까지 가볍게 해.", "운동이 습관이 됐어. 신기하지?", "카우리한테도 잠깐 쉬라고 했는데.", "쉬는 날엔 더 많이 걷게 되더라.", "건강하게 오래 즐기는 게 내 목표야." },
            new[] { "안녕. 내 털, 오늘은 좀 윤기가 나지?", "채소는 여전히 별로야. 그래도 먹어.", "빗질은 하루도 빼먹지 않아.", "비 오는 날엔 털이 정말 골치야.", "완벽한 털결은 작은 습관에서 나와.", "나도 가끔은 대충 살고 싶어.", "오늘은 빗 하나를 새로 샀어.", "네가 보는 내 모습만큼, 나도 나를 아껴." },
            new[] { "자리 하나면 충분해.", "필요 없는 건 두고 왔어.", "물건이 적으면 마음도 조용하지.", "또 그런 소문이 돌았어? ……그래.", "오늘은 짐이 더 가벼워졌어.", "간단한 게 좋아. 설명도.", "무언가를 소유하면 지켜야 할 것도 생기지.", "여긴 오래 앉아도 부담스럽지 않네." },
            new[] { "수업 끝났어! ……아마도.", "선생님이 나 찾으시면 못 봤다고 해줘.", "오늘은 공부보다 더 중요한 일이 있어.", "서당 가는 길을 또 잘못 들었네.", "나 사실 공부도 아주 못하진 않아.", "공책엔 너희 가게 가는 길이 제일 잘 그려져.", "오늘은 정말 수업 끝나고 온 거야!", "언젠가 선생님도 데려올게. 그땐 도망 안 가." },
            new[] { "여기가 따뜻한 곳이구나. 좋아.", "극지방에선 이런 냄새가 안 나.", "온천을 찾다가 여기까지 왔어.", "내 고향에도 네가 올 수 있으면 좋겠네.", "덥지 않냐고? 이 정도는 포근해.", "여행길에서 만난 친구들을 기록해 둬.", "이곳 풍경은 눈이 없어도 참 좋다.", "돌아가도 여긴 꼭 기억할 거야." },
            new[] { "안녕! 오늘 뿔 상태가 꽤 좋아.", "올해 대회도 준비해야겠어.", "사진은 어느 쪽에서 찍어야 잘 나올까?", "상을 받은 뒤로 신경 쓸 게 늘었어.", "칭찬해 줘도 돼. 꽤 노력했거든.", "뿔을 손질하는 시간이 생각보다 길어.", "누구에게나 자기만의 멋이 있는 거겠지.", "다음 대회 결과보다 오늘이 더 즐거워." },
            new[] { "면접 다녀오는 길이야. 결과는 나중에.", "안정적이라는 말, 참 듣기 좋지.", "이력서는 계속 고쳐 쓰게 되네.", "왕이라 불리던 때가 언제였더라.", "아직은 포기 안 했어. 걱정 마.", "손님으로 앉아 있는 게 조금 어색해.", "일이 전부는 아니라는데, 글쎄.", "언젠가 좋은 소식 들려줄게. 조용히." },
            new[] { "혹시 의자에 털 좀 묻었니?", "오늘은 빗에 털이 적게 남았어.", "바람 부는 날은 조금 무서워.", "여기선 마음 놓고 앉아도 될까?", "걱정만 하면 더 빠진다는 말이 있더라." }
        };
        int[][] expectedMilestones =
        {
            new[] { 25, 50, 100, 200, 300, 400, 500 }, new[] { 25, 50, 100, 200, 300, 400, 500 },
            new[] { 25, 50, 100, 200, 300, 400, 500 }, new[] { 25, 50, 100, 200, 300, 400, 500 },
            new[] { 25, 50, 100, 200, 300, 400, 500 }, new[] { 25, 50, 100, 200, 300, 400, 500 },
            new[] { 25, 50, 100, 200, 300, 400, 500 }, new[] { 25, 50, 100, 200, 300, 400, 500 },
            new[] { 25, 50, 100, 200, 300, 400, 500 }, new[] { 25, 50, 100, 200, 300, 400, 500 },
            new[] { 25, 50, 100, 200 }
        };
        string[] ids = { "CUSTOMER01", "CUSTOMER02", "CUSTOMER03", "CUSTOMER04", "CUSTOMER05", "CUSTOMER06", "CUSTOMER07", "CUSTOMER08", "CUSTOMER09", "CUSTOMER10", "CUSTOMER11" };
        Assert.That(CustomerEncounterCatalog.CustomerIds, Is.EquivalentTo(ids));
        int totalMessageCount = 0;
        for (int customer = 0; customer < ids.Length; customer++)
        {
            totalMessageCount += expected[customer].Length;
            Assert.That(expected[customer].Length, Is.EqualTo(expectedMilestones[customer].Length + 1), ids[customer]);
            Assert.That(CustomerEncounterCatalog.TryGetMessage(ids[customer], CustomerEncounterEventKind.FirstAppearance, 0, out var first), Is.True);
            Assert.That(first, Is.EqualTo(expected[customer][0]));
            Assert.That(CustomerEncounterCatalog.IsValidEventKey(ids[customer], (int)CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(CustomerEncounterCatalog.GetCustomerVisitMilestones(ids[customer]), Is.EqualTo(expectedMilestones[customer]));
            for (int milestone = 0; milestone < expectedMilestones[customer].Length; milestone++)
            {
                int visits = expectedMilestones[customer][milestone];
                Assert.That(CustomerEncounterCatalog.IsValidEventKey(ids[customer], (int)CustomerEncounterEventKind.VisitMilestone, visits), Is.True);
                Assert.That(CustomerEncounterCatalog.TryGetMessage(ids[customer], CustomerEncounterEventKind.VisitMilestone, visits, out var message), Is.True);
                Assert.That(message, Is.EqualTo(expected[customer][milestone + 1]));
            }
        }
        Assert.That(totalMessageCount, Is.EqualTo(85));
        Assert.That(CustomerEncounterCatalog.IsSupportedCustomer("CUSTOMER12"), Is.False);
        Assert.That(CustomerEncounterCatalog.IsSupportedCustomer("CUSTOMER77"), Is.False);
    }

    [Test]
    public void T18_RegisteredMilestonesQueueTheMatchingCustomerMessageOnlyOnce()
    {
        for (int customerIndex = 3; customerIndex < 11; customerIndex++)
        {
            string customerId = "CUSTOMER" + (customerIndex + 1).ToString("D2");
            var firstProgress = CustomerEncounterProgressData.CreateEmpty();
            Assert.That(firstProgress.RecordValidAppearance(customerId, 0), Is.True, customerId + "/first");
            Assert.That(firstProgress.TryGetNextPending(customerId, out var firstRecord, out var firstMessage), Is.True, customerId + "/first");
            Assert.That(firstRecord.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
            Assert.That(CustomerEncounterCatalog.TryGetMessage(customerId, CustomerEncounterEventKind.FirstAppearance, 0, out var expectedFirst), Is.True);
            Assert.That(firstMessage, Is.EqualTo(expectedFirst));
            Assert.That(firstProgress.MarkSeen(customerId, CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(firstProgress.RecordValidAppearance(customerId, 0), Is.False, customerId + "/repeat first");
            Assert.That(firstProgress.TryGetNextPending(customerId, out _, out _), Is.False, customerId + "/repeat pending");

            foreach (int milestone in CustomerEncounterCatalog.GetCustomerVisitMilestones(customerId))
            {
                var progress = CustomerEncounterProgressData.CreateEmpty();
                Assert.That(progress.RecordCompletedVisit(customerId, milestone - 1, milestone), Is.True, customerId + "/" + milestone);
                Assert.That(progress.TryGetNextPending(customerId, out var pending, out var message), Is.True, customerId + "/" + milestone);
                Assert.That(pending.CustomerId, Is.EqualTo(customerId));
                Assert.That(pending.VisitMilestone, Is.EqualTo(milestone));
                Assert.That(CustomerEncounterCatalog.TryGetMessage(customerId, CustomerEncounterEventKind.VisitMilestone, milestone, out var expected), Is.True);
                Assert.That(message, Is.EqualTo(expected));
                Assert.That(progress.RecordCompletedVisit(customerId, milestone - 1, milestone), Is.False, customerId + "/" + milestone);
            }
        }
    }

    [Test]
    public void T19_UlliMissing300DialogueDoesNotQueueAndOtherCustomersKeepTheir300Dialogue()
    {
        Assert.That(CustomerEncounterCatalog.IsValidEventKey("CUSTOMER11", (int)CustomerEncounterEventKind.VisitMilestone, 300), Is.False);
        Assert.That(CustomerEncounterCatalog.TryGetMessage("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 300, out var missingMessage), Is.False);
        Assert.That(missingMessage, Is.Null);
        var ulliProgress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(ulliProgress.RecordCompletedVisit("CUSTOMER11", 299, 300), Is.False);
        Assert.That(ulliProgress.TryGetNextPending("CUSTOMER11", out _, out _), Is.False);

        var otherProgress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(otherProgress.RecordCompletedVisit("CUSTOMER10", 299, 300), Is.True);
        Assert.That(otherProgress.TryGetNextPending("CUSTOMER10", out var pending, out var message), Is.True);
        Assert.That(pending.VisitMilestone, Is.EqualTo(300));
        Assert.That(message, Is.Not.Empty);

        TextAsset normalCustomerCsv = Resources.Load<TextAsset>("CustomerData/CSVData/NormalCustomerData");
        Assert.That(normalCustomerCsv, Is.Not.Null, "The existing food unlock source must remain loadable.");
        string[] ulliFields = null;
        foreach (string row in normalCustomerCsv.text.Split('\n'))
            if (row.StartsWith("CUSTOMER11,")) { ulliFields = row.TrimEnd('\r').Split(','); break; }
        Assert.That(ulliFields, Is.Not.Null);
        Assert.That(ulliFields.Length, Is.GreaterThan(13));
        Assert.That(ulliFields[13].Trim(), Is.EqualTo("FOOD43"), "FOOD43 remains the existing 300-visit food unlock.");
    }

    [Test]
    public void T20_ExpandedLegacyMigrationSuppressesPastEventsButAllowsFutureMilestones()
    {
        var progress = CustomerEncounterProgressData.MigrateLegacy(new List<SaveCustomerData>
        {
            new SaveCustomerData("CUSTOMER04", string.Empty, 24),
            new SaveCustomerData("CUSTOMER11", string.Empty, 300)
        });
        Assert.That(progress.IsSeen("CUSTOMER04", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(progress.IsPending("CUSTOMER04", CustomerEncounterEventKind.VisitMilestone, 25), Is.False);
        Assert.That(progress.TryGetNextPending("CUSTOMER04", out _, out _), Is.False);
        Assert.That(progress.RecordCompletedVisit("CUSTOMER04", 24, 25), Is.True);
        Assert.That(progress.TryGetNextPending("CUSTOMER04", out var future, out var message), Is.True);
        Assert.That(future.VisitMilestone, Is.EqualTo(25));
        Assert.That(message, Is.EqualTo("운동하고 나서 쉬는 시간이 최고야."));
        Assert.That(progress.IsSeen("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 200), Is.True);
        Assert.That(progress.IsSeen("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 300), Is.False);
        Assert.That(progress.IsPending("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 300), Is.False);
        string recordsJson = JsonMapper.ToJson(progress.ToSaveRecords());
        var loaded = new LoadUserData(JsonMapper.ToObject<JsonData>(
            "[{\"CustomerEncounterStateVersion\":1,\"CustomerEncounterStateList\":" + recordsJson + "}]"));
        Assert.That(loaded.IsValid, Is.True);
        Assert.That(loaded.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.Valid));
        Assert.That(CustomerEncounterProgressData.TryRestore(loaded.CustomerEncounterRecords, out var restored, out _), Is.True);
        Assert.That(restored.IsPending("CUSTOMER04", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
        Assert.That(restored.IsSeen("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 200), Is.True);
        Assert.That(restored.IsPending("CUSTOMER11", CustomerEncounterEventKind.VisitMilestone, 300), Is.False);
    }

    [Test]
    public void T21_Ec01VersionOneStatesSurviveExpansionWithoutBackfillingNewCustomers()
    {
        var ec01Progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(ec01Progress.RecordValidAppearance("CUSTOMER01", 0), Is.True);
        Assert.That(ec01Progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(ec01Progress.RecordCompletedVisit("CUSTOMER02", 24, 25), Is.True);
        string recordsJson = JsonMapper.ToJson(ec01Progress.ToSaveRecords());
        var loaded = new LoadUserData(JsonMapper.ToObject<JsonData>(
            "[{\"CustomerEncounterStateVersion\":1,\"CustomerEncounterStateList\":" + recordsJson + "}]"));
        Assert.That(loaded.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.Valid));
        Assert.That(CustomerEncounterProgressData.TryRestore(loaded.CustomerEncounterRecords, out var expanded, out _), Is.True);
        Assert.That(expanded.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(expanded.IsPending("CUSTOMER02", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);

        Assert.That(expanded.RecordValidAppearance("CUSTOMER04", 49), Is.False);
        Assert.That(expanded.IsSeen("CUSTOMER04", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(expanded.TryGetNextPending("CUSTOMER04", out _, out _), Is.False);
        Assert.That(expanded.RecordCompletedVisit("CUSTOMER04", 49, 50), Is.True);
        Assert.That(expanded.TryGetNextPending("CUSTOMER04", out var next, out var nextMessage), Is.True);
        Assert.That(next.VisitMilestone, Is.EqualTo(50));
        Assert.That(nextMessage, Is.EqualTo("아침에 멀리 한 바퀴 돌고 왔어."));
        Assert.That(expanded.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(expanded.IsPending("CUSTOMER02", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
    }

    [Test]
    public void T13_LegacyAccountMigrationSuppressesPastFirstAndMilestones()
    {
        var progress = CustomerEncounterProgressData.MigrateLegacy(new List<SaveCustomerData>
        {
            new SaveCustomerData("CUSTOMER01", string.Empty, 49),
            new SaveCustomerData("CUSTOMER02", string.Empty, 0)
        });
        Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
        Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.False);
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 49), Is.False);
        Assert.That(progress.RecordValidAppearance("CUSTOMER02", 0), Is.True);
    }

    [Test]
    public void T14_OldSchemaAndAccountStatesRemainSeparate()
    {
        var oldRow = new LoadUserData(JsonMapper.ToObject<JsonData>("[{}]"));
        Assert.That(oldRow.IsValid, Is.True);
        Assert.That(oldRow.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.LegacyMissing));

        var accountA = CustomerEncounterProgressData.CreateEmpty();
        var accountB = CustomerEncounterProgressData.CreateEmpty();
        accountA.RecordValidAppearance("CUSTOMER01", 0);
        Assert.That(accountB.TryGetNextPending("CUSTOMER01", out _, out _), Is.False);
    }

    [Test]
    public void UnknownVersionOrMalformedRecordsAreProtectedInsteadOfEmptied()
    {
        var unsupported = new LoadUserData(JsonMapper.ToObject<JsonData>(
            "[{\"CustomerEncounterStateVersion\":2,\"CustomerEncounterStateList\":[]}]"));
        Assert.That(unsupported.IsValid, Is.True);
        Assert.That(unsupported.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.ProtectedInvalid));

        var invalidRecord = new CustomerEncounterSaveData("CUSTOMER77", CustomerEncounterEventKind.FirstAppearance, 0,
            CustomerEncounterEventState.Pending);
        Assert.That(CustomerEncounterProgressData.TryRestore(new[] { invalidRecord }, out _, out _), Is.False);
    }

    [Test]
    public void T15_LoadUserDataRejectsUndefinedEventKindAndProtectsState()
    {
        string payload = ReplaceFirstRecordValue(CreateValidPendingPayload(), "EventKind", 0, 77);
        AssertProtectedEncounterPayload(payload);
    }

    [Test]
    public void T16_LoadUserDataRejectsUndefinedEventStateAndProtectsState()
    {
        string payload = ReplaceFirstRecordValue(CreateValidPendingPayload(), "State", 0, 77);
        AssertProtectedEncounterPayload(payload);
    }

    [Test]
    public void T17_LoadUserDataRejectsFirstAppearanceWithMilestoneAndProtectsState()
    {
        string payload = ReplaceFirstRecordValue(CreateValidPendingPayload(), "VisitMilestone", 0, 25);
        AssertProtectedEncounterPayload(payload);
    }

    [TestCase("CUSTOMER01", null)]
    [TestCase("CUSTOMER01", "CUSTOMER01")]
    [TestCase("CUSTOMER01", "CUSTOMER02")]
    public void Arrival_ActualQueueConsumesEachMemberOnceAcrossReordering(string firstId, string secondId)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s);
            var candidates = ObserveEncounterCandidates(s);
            var party = SpawnEncounterParty(s, secondId == null ? new[] { firstId } : new[] { firstId, secondId });
            var members = party.Members.ToArray();
            var originalCallbacks = members.Select(member => Get<Action>(member.Customer, "_moveCompleted")).ToArray();
            Assert.That(members.All(member => !Get<bool>(member, "_encounterArrivalConsumed")), Is.True);

            ArriveEncounterParty(party);
            Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(candidates.Count, Is.EqualTo(members.Length), "Each actual queue arrival still prepares its first-appearance candidate.");
            Assert.That(candidates.All(record => record.EventKind == (int)CustomerEncounterEventKind.FirstAppearance), Is.True);
            foreach (var member in members)
            {
                Assert.That(Get<bool>(member, "_encounterArrivalConsumed"), Is.True);
                Assert.That(Get<bool>(member, "_encounterEntryConsumed"), Is.False, "Queue arrival cannot consume milestone entry eligibility.");
                Assert.That(TryPrepareArrival(member, out var repeat, out _), Is.False);
                Assert.That(repeat, Is.Null);
                Assert.That(progress.IsPending(member.Customer.NormalCustomerData.Id,
                    CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            }
            string before = JsonMapper.ToJson(progress.ToSaveRecords());

            Get<Transform>(s.Controller, "_startLine").position += Vector3.right;
            Invoke(s.Controller, "SortCustomerLine");
            ArriveEncounterParty(party);
            foreach (var callback in originalCallbacks) callback();

            Assert.That(party.Members, Is.EqualTo(members));
            Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(members.All(member => Get<bool>(member, "_encounterArrivalConsumed")), Is.True);
            Assert.That(JsonMapper.ToJson(progress.ToSaveRecords()), Is.EqualTo(before));
            Assert.That(candidates.Count, Is.EqualTo(members.Length), "Reordering and old callbacks cannot prepare another first-appearance candidate.");
            foreach (var member in members)
                Assert.That(TryPrepareArrival(member, out _, out _), Is.False, "A position-only arrival cannot retry Pending.");
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Arrival_ActualRestoreAndReorderNeverPrepareAnEncounter(bool hasPending, bool pairFirst)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: hasPending ? 25 : 0);
            var candidates = ObserveEncounterCandidates(s);
            if (hasPending)
            {
                progress.RecordValidAppearance("CUSTOMER01", 0);
                progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
                progress.RecordValidAppearance("CUSTOMER02", 0);
            }
            string before = JsonMapper.ToJson(progress.ToSaveRecords());
            string[][] savedParties = pairFirst
                ? new[] { new[] { "CUSTOMER01", "CUSTOMER02" }, new[] { "CUSTOMER01" } }
                : new[] { new[] { "CUSTOMER01" }, new[] { "CUSTOMER01", "CUSTOMER02" } };
            Assert.That(UserInfo.SetWaitingParties(EStage.Stage1,
                savedParties), Is.True);
            Invoke(s.Controller, "LoadWaitingCustomers");
            var parties = s.Controller.WaitingParties.ToArray();
            Assert.That(parties.Select(party => party.Members.Count), Is.EqualTo(pairFirst ? new[] { 2, 1 } : new[] { 1, 2 }));
            foreach (var member in parties.SelectMany(party => party.Members))
            {
                Assert.That(Get<bool>(member, "_encounterArrivalConsumed"), Is.True);
                Assert.That(Get<bool>(member, "_encounterEntryConsumed"), Is.True);
                Assert.That(TryPrepareArrival(member, out _, out _), Is.False);
            }

            Get<Transform>(s.Controller, "_startLine").position += Vector3.right;
            Invoke(s.Controller, "SortCustomerLine");
            foreach (var party in parties) ArriveEncounterParty(party);

            Assert.That(s.Controller.WaitCount, Is.EqualTo(2));
            Assert.That(UserInfo.PromotionCount, Is.Zero);
            Assert.That(JsonMapper.ToJson(progress.ToSaveRecords()), Is.EqualTo(before));
            foreach (var member in parties.SelectMany(party => party.Members))
                Assert.That(TryPrepareArrival(member, out _, out _), Is.False);
            CompleteEncounterEntry(s, parties[0]);
            Assert.That(candidates, Is.Empty, "Restored waiting customers also cannot prepare a candidate when guided.");
            Assert.That(JsonMapper.ToJson(progress.ToSaveRecords()), Is.EqualTo(before));
        }
    }

    [Test]
    public void Arrival_FirstAppearanceSelectionNeverDrainsMilestonesAndKeepsCustomerIdsIndependent()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            progress.RecordValidAppearance("CUSTOMER01", 0);
            progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            progress.RecordValidAppearance("CUSTOMER02", 0);
            var sameIdParty = SpawnEncounterParty(s, "CUSTOMER01", "CUSTOMER01");
            var otherIdParty = SpawnEncounterParty(s, "CUSTOMER02");

            Assert.That(TryPrepareArrival(sameIdParty.Members[0], out var first, out var firstMessage), Is.True);
            Assert.That(first.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
            Assert.That(firstMessage, Is.Not.Empty);
            Assert.That(UserInfo.MarkCustomerEncounterSeen(first.CustomerId, (CustomerEncounterEventKind)first.EventKind,
                first.VisitMilestone, UserInfo.CustomerEncounterContextGeneration), Is.True);
            Assert.That(TryPrepareArrival(sameIdParty.Members[0], out var repeat, out _), Is.False);
            Assert.That(repeat, Is.Null, "The same member cannot drain another Pending card.");

            Assert.That(TryPrepareArrival(sameIdParty.Members[1], out var second, out _), Is.False);
            Assert.That(second, Is.Null, "The second same-ID arrival cannot substitute a milestone for the Seen first appearance.");
            Assert.That(TryPrepareArrival(sameIdParty.Members[1], out _, out _), Is.False);
            Assert.That(TryPrepareArrival(otherIdParty.Members[0], out var other, out _), Is.True);
            Assert.That(other.CustomerId, Is.EqualTo("CUSTOMER02"));
            Assert.That(other.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(progress.IsPending("CUSTOMER02", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        }
    }

    [Test]
    public void Arrival_PoolReuseHasFreshMemberAndOldCallbackCannotConsumeItsArrival()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s);
            var oldParty = SpawnEncounterParty(s, "CUSTOMER01");
            var oldMember = oldParty.Members[0];
            var customer = oldMember.Customer;
            var oldArrival = Get<Action>(customer, "_moveCompleted");
            ArriveEncounterParty(oldParty);
            Assert.That(s.Controller.CancelWaitingParty(oldParty), Is.True);
            var freshParty = SpawnEncounterParty(s, "CUSTOMER01");
            var freshMember = freshParty.Members[0];
            Assert.That(freshMember.Customer, Is.SameAs(customer));
            Assert.That(freshMember, Is.Not.SameAs(oldMember));

            oldArrival();
            Assert.That(TryPrepareArrival(oldMember, out _, out _), Is.False);
            Assert.That(Get<bool>(freshMember, "_encounterArrivalConsumed"), Is.False);
            Assert.That(TryPrepareArrival(freshMember, out var resumed, out _), Is.True);
            Assert.That(resumed.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
            Assert.That(TryPrepareArrival(freshMember, out _, out _), Is.False);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(progress.ToSaveRecords(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void ArrivalAndGuidedEntry_NeverSelectSeenEvents()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 25);
            progress.RecordValidAppearance("CUSTOMER01", 0);
            progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
            progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0);
            progress.MarkSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25);
            var candidates = ObserveEncounterCandidates(s);
            var party = SpawnEncounterParty(s, "CUSTOMER01");

            Assert.That(TryPrepareArrival(party.Members[0], out var arrival, out _), Is.False);
            Assert.That(arrival, Is.Null);
            Assert.That(Get<bool>(party.Members[0], "_encounterArrivalConsumed"), Is.True);
            ArriveEncounterParty(party);
            CompleteEncounterEntry(s, party);
            Assert.That(UserInfo.TryGetCustomerEncounterForEntry("CUSTOMER01", 25, out var entry, out _), Is.False);
            Assert.That(entry, Is.Null);
            Assert.That(candidates, Is.Empty);
            Assert.That(progress.ToSaveRecords().All(record => record.State == (int)CustomerEncounterEventState.Seen), Is.True);
        }
    }

    [TestCase(24, 0)]
    [TestCase(25, 25)]
    [TestCase(49, 25)]
    [TestCase(50, 25)]
    [TestCase(51, 25)]
    [TestCase(0, 0)]
    public void Entry_SelectsOldestPendingMilestoneAllowedByVisitStart(int visitsBeforeThisVisit, int expected)
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        progress.RecordValidAppearance("CUSTOMER01", 0);
        progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
        progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
        progress.RecordCompletedVisit("CUSTOMER02", 24, 25);
        string before = JsonMapper.ToJson(progress.ToSaveRecords());

        Assert.That(progress.TryGetPendingVisitMilestone("CUSTOMER01", visitsBeforeThisVisit, out var record, out var message),
            Is.EqualTo(expected != 0));
        if (expected == 0)
        {
            Assert.That(record, Is.Null);
            Assert.That(message, Is.Null);
        }
        else
        {
            Assert.That(record.CustomerId, Is.EqualTo("CUSTOMER01"));
            Assert.That(record.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.VisitMilestone));
            Assert.That(record.VisitMilestone, Is.EqualTo(expected));
            Assert.That(message, Is.Not.Empty);
        }
        Assert.That(JsonMapper.ToJson(progress.ToSaveRecords()), Is.EqualTo(before), "Selection never consumes deferred events.");
    }

    [Test]
    public void CompletedVisit_ActualUserInfoCountsAndEventRangePreservePendingSaveRoundTrip()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 24);
            progress.RecordValidAppearance("CUSTOMER01", 0);
            var ranges = new List<string>();
            var candidates = ObserveEncounterCandidates(s);
            UserInfo.OnCustomerVisitCompletedHandler += (data, previous, current) =>
            {
                ranges.Add(data.Id + ":" + previous + "->" + current);
            };

            UserInfo.CustomerVisits(s.Data);
            UserInfo.CustomerVisits(s.Data);

            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(26));
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER02"), Is.Zero);
            Assert.That(ranges, Is.EqualTo(new[] { "CUSTOMER01:24->25", "CUSTOMER01:25->26" }));
            Assert.That(candidates, Is.Empty, "Official visit completion arms Pending without preparing a display candidate.");
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
            Assert.That(UserInfo.MarkCustomerEncounterSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25,
                UserInfo.CustomerEncounterContextGeneration + 1), Is.False);
            Assert.That(UserInfo.MarkCustomerEncounterSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25,
                UserInfo.CustomerEncounterContextGeneration), Is.True);

            string recordsJson = JsonMapper.ToJson(progress.ToSaveRecords());
            var loaded = new LoadUserData(JsonMapper.ToObject<JsonData>(
                "[{\"CustomerEncounterStateVersion\":1,\"CustomerEncounterStateList\":" + recordsJson + "}]"));
            Assert.That(loaded.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.Valid));
            Assert.That(CustomerEncounterProgressData.TryRestore(loaded.CustomerEncounterRecords, out var restored, out _), Is.True);
            Assert.That(JsonMapper.ToJson(restored.ToSaveRecords()), Is.EqualTo(recordsJson));
            Assert.That(restored.IsPending("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(restored.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
        }
    }

    [TestCase(24, 25)]
    [TestCase(49, 50)]
    public void CompletedMeal_ArmsPendingWithoutCandidateUntilNextVisitActuallySeats(int previous, int milestone)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: previous);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            var candidates = ObserveEncounterCandidates(s);
            var finishingVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(finishingVisit);
            CompleteEncounterEntry(s, finishingVisit);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, milestone), Is.False);
            Assert.That(candidates, Is.Empty, "The visit that will reach the milestone has no milestone candidate at entry.");

            s.OrderAndCook(false);
            s.Eat();
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(milestone));
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, milestone), Is.True);
            Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, milestone), Is.False);
            Assert.That(candidates, Is.Empty, "The actual serving/eating completion path must only arm Pending.");
            s.Settle();
            s.CleanAndExit();
            Assert.That(candidates, Is.Empty, "Settlement and departure cannot prepare the milestone either.");

            var laterVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(laterVisit);
            Assert.That(candidates, Is.Empty, "The later visit's queue arrival cannot prepare a milestone.");
            Action repeatCallbacks = CompleteEncounterEntry(s, laterVisit);
            Assert.That(candidates, Has.Count.EqualTo(1));
            AssertMilestoneCandidate(candidates[0], "CUSTOMER01", milestone);
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(milestone), "Entry does not count a completed visit.");
            repeatCallbacks();
            InvokeGuidedEntry(s.Use);
            Assert.That(candidates, Has.Count.EqualTo(1), "Repeated production movement/seating callbacks cannot prepare another candidate.");
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, milestone), Is.True,
                "A prepared candidate is not a visible or confirmed popup in this offline Flow.");
        }
    }

    [TestCase(24, 25)]
    [TestCase(49, 50)]
    public void Entry_VisitStartSnapshotRejectsMilestoneReachedWhileAlreadyWaiting(int previous, int milestone)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: previous);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            var candidates = ObserveEncounterCandidates(s);
            var alreadyWaiting = SpawnEncounterParty(s, "CUSTOMER01");
            Assert.That(Get<int>(alreadyWaiting.Members[0], "<EncounterVisitsBeforeVisit>k__BackingField"), Is.EqualTo(previous));
            ArriveEncounterParty(alreadyWaiting);
            UserInfo.CustomerVisits(s.Data);
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(milestone));
            CompleteEncounterEntry(s, alreadyWaiting);
            Assert.That(candidates, Is.Empty, "Reaching a milestone elsewhere cannot make an already-started visit eligible.");
            Assert.That(Get<bool>(alreadyWaiting.Members[0], "_encounterEntryConsumed"), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, milestone), Is.True);

            s.Manager.AngerExitCustomer(s.Table);
            var freshVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(freshVisit);
            CompleteEncounterEntry(s, freshVisit);
            Assert.That(candidates, Has.Count.EqualTo(1));
            AssertMilestoneCandidate(candidates[0], "CUSTOMER01", milestone);
        }
    }

    [TestCase(null)]
    [TestCase("CUSTOMER01")]
    [TestCase("CUSTOMER02")]
    public void Entry_OneCandidateForWholePartyConsumesEveryMember(string companionId)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01", "CUSTOMER02");
            UserInfo.CustomerVisits(CustomerDataManager.Instance.GetCustomerData("CUSTOMER02"), 50);
            progress.RecordCompletedVisit("CUSTOMER01", 24, 25);
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            progress.RecordCompletedVisit("CUSTOMER02", 24, 25);
            var candidates = ObserveEncounterCandidates(s);
            var party = SpawnEncounterParty(s, companionId == null ? new[] { "CUSTOMER01" } : new[] { "CUSTOMER01", companionId });
            ArriveEncounterParty(party);
            Assert.That(candidates, Is.Empty);
            var arrivals = BeginEncounterGuide(s, party);
            var seating = new List<Action>();
            for (int index = 0; index < party.Members.Count; index++)
            {
                ReachEncounterTarget(party.Members[index], arrivals[index]);
                Assert.That(candidates, Is.Empty, "Movement arrival alone must not prepare an entry candidate.");
                seating.Add(s.TakeWait(1.5f));
                seating[index]();
                Assert.That(candidates.Count, Is.EqualTo(index == party.Members.Count - 1 ? 1 : 0),
                    "A pair becomes eligible only when both actual seating callbacks succeed.");
            }
            Assert.That(s.Table.TableState, Is.EqualTo(party.Members.Count == 1
                ? ETableState.Seating : ETableState.PartySeatedAwaitingOrders),
                "A candidate alone must not hide a failed order-preparation fixture.");
            AssertMilestoneCandidate(candidates.Single(), "CUSTOMER01", 25);
            Assert.That(party.Members.All(member => Get<bool>(member, "_encounterEntryConsumed")), Is.True,
                "Even the member that did not win the candidate consumes this entry opportunity.");
            Assert.That(UserInfo.MarkCustomerEncounterSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone,
                25, UserInfo.CustomerEncounterContextGeneration), Is.True);
            foreach (var arrival in arrivals) arrival();
            foreach (var seated in seating) seated();
            InvokeGuidedEntry(s.Use);
            Assert.That(candidates, Has.Count.EqualTo(1), "Confirming one card cannot drain another member or milestone in the same entry.");
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(progress.IsPending("CUSTOMER02", CustomerEncounterEventKind.VisitMilestone, 25), Is.True);
        }
    }

    [Test]
    public void Entry_OtherCustomerCannotSelectPanisPendingMilestone()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01", "CUSTOMER02");
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            var otherVisit = SpawnEncounterParty(s, "CUSTOMER02");
            ArriveEncounterParty(otherVisit);
            CompleteEncounterEntry(s, otherVisit);
            Assert.That(candidates, Is.Empty);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.False);
        }
    }

    [Test]
    public void Entry_FirstAppearanceAndMilestoneKeepSeparateEligibilityAndKeys()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            progress.RecordValidAppearance("CUSTOMER01", 0);
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            var party = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(party);
            Assert.That(candidates, Has.Count.EqualTo(1));
            Assert.That(candidates[0].EventKind, Is.EqualTo((int)CustomerEncounterEventKind.FirstAppearance));
            Assert.That(candidates[0].VisitMilestone, Is.Zero);
            Assert.That(Get<bool>(party.Members[0], "_encounterArrivalConsumed"), Is.True);
            Assert.That(Get<bool>(party.Members[0], "_encounterEntryConsumed"), Is.False);
            CompleteEncounterEntry(s, party);
            Assert.That(candidates, Has.Count.EqualTo(2));
            AssertMilestoneCandidate(candidates[1], "CUSTOMER01", 50);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
        }
    }

    [Test]
    public void Entry_RegisteredMilestonesRespectEachCustomerCatalog()
    {
        foreach (string customerId in CustomerEncounterCatalog.CustomerIds)
            foreach (int milestone in CustomerEncounterCatalog.GetCustomerVisitMilestones(customerId))
            {
                var progress = CustomerEncounterProgressData.CreateEmpty();
                Assert.That(progress.RecordCompletedVisit(customerId, milestone - 1, milestone), Is.True);
                Assert.That(progress.TryGetPendingVisitMilestone(customerId, milestone - 1, out _, out _), Is.False);
                Assert.That(progress.TryGetPendingVisitMilestone(customerId, milestone, out var record, out var message), Is.True);
                AssertMilestoneCandidate(record, customerId, milestone);
                Assert.That(CustomerEncounterCatalog.TryGetMessage(customerId, CustomerEncounterEventKind.VisitMilestone,
                    milestone, out string expected), Is.True);
                Assert.That(message, Is.EqualTo(expected));
                Assert.That(progress.MarkSeen(customerId, CustomerEncounterEventKind.VisitMilestone, milestone), Is.True);
                Assert.That(progress.TryGetPendingVisitMilestone(customerId, milestone + 2, out _, out _), Is.False);
            }
        var ulli = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(CustomerEncounterCatalog.GetCustomerVisitMilestones("CUSTOMER11"), Is.EqualTo(new[] { 25, 50, 100, 200 }));
        Assert.That(ulli.RecordCompletedVisit("CUSTOMER11", 299, 300), Is.False);
        Assert.That(ulli.TryGetPendingVisitMilestone("CUSTOMER11", 301, out _, out _), Is.False);
    }

    [TestCase("not-ready")]
    [TestCase("table-unavailable")]
    [TestCase("single-path-failed")]
    [TestCase("second-member-path-failed")]
    [TestCase("cancelled-pair")]
    public void Entry_FailedGuidanceOrPathNeverPreparesMilestone(string failure)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            bool pair = failure == "second-member-path-failed" || failure == "cancelled-pair";
            var party = SpawnEncounterParty(s, pair ? new[] { "CUSTOMER01", "CUSTOMER01" } : new[] { "CUSTOMER01" });
            if (failure == "not-ready")
            {
                Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1, 0), Is.False);
                Assert.That(s.Use, Is.Null);
            }
            else
            {
                ArriveEncounterParty(party);
                if (failure == "table-unavailable")
                {
                    // Flow contains only Floor1; do not advertise an absent Floor2 to UpdateTable.
                    Set(s.Stage, "_unlockFloor", ERestaurantFloorType.Floor1);
                    s.Table.TableState = ETableState.NeedCleaning;
                    Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1, 0), Is.False);
                    Assert.That(s.Use, Is.Null);
                }
                else
                {
                    var arrivals = BeginEncounterGuide(s, party);
                    if (failure == "cancelled-pair")
                    {
                        Assert.That(s.Manager.CancelPartyGuide(s.Table), Is.True);
                        foreach (var arrival in arrivals) arrival();
                        Assert.That(s.Use, Is.Null);
                    }
                    else
                    {
                        if (pair)
                        {
                            ReachEncounterTarget(party.Members[0], arrivals[0]);
                            s.TakeWait(1.5f)();
                            Assert.That(candidates, Is.Empty, "The first seated member cannot present before its companion arrives.");
                        }
                        var failing = party.Members[pair ? 1 : 0];
                        failing.Customer.transform.position = (Vector3)failing.Customer.TargetPos + Vector3.right * 100f;
                        arrivals[pair ? 1 : 0]();
                        if (!pair) s.TakeWait(1.5f)(); // Preserve legacy singleton seating, but reject encounter eligibility.
                        Assert.That(failing.Customer.HasPathFailure, Is.True);
                    }
                }
            }
            Assert.That(candidates, Is.Empty, "Only a successful actual entry can prepare a milestone candidate.");
            Assert.That(party.Members.All(member => !Get<bool>(member, "_encounterEntryConsumed")), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.False);
        }
    }

    [Test]
    public void Entry_ContextGenerationChangeRejectsHeldSeatingCallback()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            var party = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(party);
            var arrivals = BeginEncounterGuide(s, party);
            ReachEncounterTarget(party.Members[0], arrivals[0]);
            Action seated = s.TakeWait(1.5f);
            s.Swap(typeof(UserInfo), "_customerEncounterContextGeneration", UserInfo.CustomerEncounterContextGeneration + 1);
            seated();
            InvokeGuidedEntry(s.Use);
            Assert.That(candidates, Is.Empty);
            Assert.That(Get<bool>(party.Members[0], "_encounterEntryConsumed"), Is.False);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
        }
    }

    [Test]
    public void Entry_PoolReuseRejectsOldGuideAndSeatingCallbacks()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            var oldParty = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(oldParty);
            var oldArrivals = BeginEncounterGuide(s, oldParty);
            ReachEncounterTarget(oldParty.Members[0], oldArrivals[0]);
            Action oldSeated = s.TakeWait(1.5f);
            var reusedCustomer = oldParty.Members[0].Customer;
            s.Manager.AngerExitCustomer(s.Table);
            ObjectPoolManager.Instance.DespawnNormalCustomer(reusedCustomer);
            var nextParty = SpawnEncounterParty(s, "CUSTOMER01");
            Assert.That(nextParty.Members[0].Customer, Is.SameAs(reusedCustomer));
            Assert.That(nextParty.Members[0], Is.Not.SameAs(oldParty.Members[0]));
            oldArrivals[0]();
            oldSeated();
            Assert.That(candidates, Is.Empty);
            Assert.That(Get<bool>(nextParty.Members[0], "_encounterEntryConsumed"), Is.False);
            ArriveEncounterParty(nextParty);
            CompleteEncounterEntry(s, nextParty);
            Assert.That(candidates, Has.Count.EqualTo(1));
            AssertMilestoneCandidate(candidates[0], "CUSTOMER01", 50);
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void EncounterGate_WaitsForActualViewDisappearance(bool additionalNavigation, bool allPop)
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 49);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            var candidates = ObserveEncounterCandidates(s);
            var scene = s.Create<MainScene>();
            var coordinator = s.Create<UINavigationCoordinator>();
            coordinator.gameObject.SetActive(true);
            var main = EncounterNavigation(s, coordinator);
            var tutorial = EncounterNavigation(s, coordinator);
            var extra = EncounterNavigation(s, coordinator);
            Set(scene, "_uiNavCoordinator", coordinator);
            Set(scene, "_uiMainNav", main);
            Set(scene, "_uiTutorialNav", tutorial);
            var registrations = new NavigationData[3];
            var navigations = new[] { main, tutorial, extra };
            for (int i = 0; i < registrations.Length; i++)
            {
                registrations[i] = new NavigationData();
                Set(registrations[i], "_uiNav", navigations[i]);
                Set(registrations[i], "_focusEnabled", false);
            }
            Set(coordinator, "_navDatas", registrations);
            Invoke(coordinator, "Init");
            var nav = additionalNavigation ? extra : main;
            var view = s.Create<StaffShopTestView>();
            view.transform.SetParent(nav.transform);
            Set(nav, "_viewDic", new Dictionary<string, MobileUIView> { ["BlockingView"] = view });
            Func<bool> allowed = () => CustomerEncounterPresentation.IsDisplayContextAllowed(
                true, false, true, RestaurantType.Hall, scene.CustomerEncounterOpenViewCount,
                false, false, true, navigationSettled: scene.CustomerEncounterViewsSettled);
            Assert.That(allowed(), Is.True);

            nav.Push("BlockingView");
            // The existing view double models state, so model visual lifetime explicitly.
            view.gameObject.SetActive(true);
            Assert.That(allowed(), Is.False);
            UserInfo.CustomerVisits(s.Data);
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(50));
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(candidates, Is.Empty, "Completion cannot prepare a candidate while another view is open.");
            var blockedVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(blockedVisit);
            Action repeatEntry = CompleteEncounterEntry(s, blockedVisit);
            Assert.That(candidates, Has.Count.EqualTo(1), "The production entry prepares one candidate before the UI eligibility gate.");
            AssertMilestoneCandidate(candidates[0], "CUSTOMER01", 50);
            Assert.That(allowed(), Is.False);
            Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.False);

            if (allPop) nav.AllPop();
            else nav.Pop("BlockingView");
            Assert.That(view.Hides, Is.EqualTo(1));
            Assert.That(nav.Count, Is.Zero);
            Assert.That(view.VisibleState, Is.EqualTo(allPop ? VisibleState.Disappeared : VisibleState.Disappearing));
            Assert.That(scene.CustomerEncounterViewsSettled, Is.False);
            Assert.That(allowed(), Is.False);

            view.VisibleState = VisibleState.Disappeared;
            view.gameObject.SetActive(false);
            Assert.That(allowed(), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True,
                "Returning to play does not consume the Pending card.");
            Assert.That(candidates, Has.Count.EqualTo(1), "Closing the view alone cannot prepare a new candidate.");
            repeatEntry();
            InvokeGuidedEntry(s.Use);
            Assert.That(candidates, Has.Count.EqualTo(1), "The blocked entry cannot retry after navigation settles.");
            UserInfo.CustomerVisits(s.Data);
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(51));
            Assert.That(candidates, Has.Count.EqualTo(1), "Completing a later visit still cannot present an older Pending.");
            s.Manager.AngerExitCustomer(s.Table);
            var nextVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(nextVisit);
            Assert.That(candidates, Has.Count.EqualTo(1));
            CompleteEncounterEntry(s, nextVisit);
            Assert.That(candidates, Has.Count.EqualTo(2));
            AssertMilestoneCandidate(candidates[1], "CUSTOMER01", 50);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
        }
    }

    [Test]
    public void Entry_ShopGatePreservesPendingAndClosingAloneCannotRetry()
    {
        using (var s = new Flow())
        {
            var progress = PrepareEncounterFlow(s, visitCount: 50);
            MarkFirstAppearanceSeen(progress, "CUSTOMER01");
            progress.RecordCompletedVisit("CUSTOMER01", 49, 50);
            var candidates = ObserveEncounterCandidates(s);
            bool shopOpenOrOpening = true;
            Func<bool> allowed = () => CustomerEncounterPresentation.IsDisplayContextAllowed(true, false, true,
                RestaurantType.Hall, 0, false, false, true, shopOpenOrOpening);
            Assert.That(allowed(), Is.False);
            var blockedVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(blockedVisit);
            Action repeatEntry = CompleteEncounterEntry(s, blockedVisit);
            Assert.That(candidates, Has.Count.EqualTo(1), "This observer counts prepared candidates, not visible popups.");
            AssertMilestoneCandidate(candidates[0], "CUSTOMER01", 50);
            Assert.That(Get<bool>(blockedVisit.Members[0], "_encounterEntryConsumed"), Is.True);
            Assert.That(progress.IsPending("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.True);
            Assert.That(progress.IsSeen("CUSTOMER01", CustomerEncounterEventKind.VisitMilestone, 50), Is.False);

            shopOpenOrOpening = false;
            Assert.That(allowed(), Is.True);
            Assert.That(candidates, Has.Count.EqualTo(1));
            repeatEntry();
            InvokeGuidedEntry(s.Use);
            Assert.That(candidates, Has.Count.EqualTo(1), "Closing the shop and repeating callbacks cannot retry the same entry.");
            UserInfo.CustomerVisits(s.Data);
            s.Manager.AngerExitCustomer(s.Table);
            var laterVisit = SpawnEncounterParty(s, "CUSTOMER01");
            ArriveEncounterParty(laterVisit);
            Assert.That(candidates, Has.Count.EqualTo(1));
            CompleteEncounterEntry(s, laterVisit);
            Assert.That(candidates, Has.Count.EqualTo(2));
            AssertMilestoneCandidate(candidates[1], "CUSTOMER01", 50);
            Assert.That(UserInfo.GetVisitedCustomerCount("CUSTOMER01"), Is.EqualTo(51));
        }
    }

    [Test]
    public void EncounterGate_BlocksPopupUntilNativeHideCompletes()
    {
        using (var s = new Flow())
        {
            var popup = s.Create<UIPopup>();
            var anime = s.Create<Transform>();
            anime.SetParent(popup.transform);
            anime.gameObject.SetActive(true);
            Set(popup, "_animeUI", anime.gameObject);
            Set(popup, "_canvasGroup", popup.gameObject.AddComponent<CanvasGroup>());
            Set(popup, "_hideDuration", 0.2f);
            s.Swap(typeof(PopupManager), "_uiPopup", popup);
            s.Swap(typeof(PopupManager), "_isShowPopup", false);
            popup.gameObject.SetActive(true);
            popup.Hide();
            Assert.That(PopupManager.IsShowPopup, Is.False);
            Assert.That(PopupManager.IsCustomerEncounterPresentationBlocked, Is.True);
            var tween = anime.GetComponent<TweenTransformScale>();
            var completed = Get<Dictionary<int, Action>>(tween, "_onCompletedDic").Values.Single();
            tween.Clear();
            completed();
            Assert.That(popup.gameObject.activeInHierarchy, Is.False);
            Assert.That(PopupManager.IsCustomerEncounterPresentationBlocked, Is.False);
            s.Swap(typeof(PopupManager), "_isShowPopup", true);
            Assert.That(PopupManager.IsCustomerEncounterPresentationBlocked, Is.True);
        }
    }

    private static MobileUINavigation EncounterNavigation(Flow flow, UINavigationCoordinator coordinator)
    {
        var nav = flow.Create<MobileUINavigation>();
        nav.transform.SetParent(coordinator.transform);
        nav.gameObject.SetActive(true);
        return nav;
    }

    private static CustomerEncounterProgressData PrepareEncounterFlow(Flow flow, int visitCount = 0)
    {
        ObjectPoolManager.Instance.DespawnNormalCustomer(flow.Customer);
        var progress = CustomerEncounterProgressData.CreateEmpty();
        flow.Swap(typeof(UserInfo), "_customerEncounterProgress", progress);
        flow.Swap(typeof(UserInfo), "_customerEncounterStateWritable", true);
        flow.Swap(typeof(UserInfo), "_customerEncounterContextGeneration", 91001L);
        Set(flow.Data, "_id", "CUSTOMER01");
        var other = flow.Track(UnityEngine.Object.Instantiate(flow.Data));
        Set(other, "_id", "CUSTOMER02");
        // Unity does not clone this runtime dictionary with the serialized customer fields.
        Set(other, "_visitCountFoodDic", new Dictionary<int, string>(flow.Data.GetVisitCountFoodDic()));
        flow.Swap(typeof(CustomerDataManager), "_customerDataDic", new Dictionary<string, CustomerData>
        {
            [flow.Data.Id] = flow.Data,
            [other.Id] = other
        });
        flow.Swap(typeof(CustomerDataManager), "_normalCustomerDataList", new List<NormalCustomerData> { flow.Data, other });
        flow.Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData>
        {
            [flow.Data.Id] = new SaveCustomerData(flow.Data.Id, "", visitCount),
            [other.Id] = new SaveCustomerData(other.Id, "", 0)
        });
        return progress;
    }

    private static CustomerVisitParty SpawnEncounterParty(Flow flow, params string[] ids)
    {
        Assert.That(flow.Controller.TrySpawnWaitingParty(ids, out var party), Is.True, flow.Controller.LastVisitDiagnostic);
        return party;
    }

    // Observes the production trigger before UI gating. Flow deliberately has no logged-in
    // backend, so these records are evidence of candidate preparation, never of rendering.
    private static List<CustomerEncounterSaveData> ObserveEncounterCandidates(Flow flow)
    {
        var candidates = new List<CustomerEncounterSaveData>();
        flow.Swap(typeof(CustomerEncounterPresentation), "CandidatePreparedForTesting",
            (Action<CustomerEncounterSaveData>)(record => candidates.Add(record)));
        return candidates;
    }

    private static void MarkFirstAppearanceSeen(CustomerEncounterProgressData progress, params string[] customerIds)
    {
        foreach (string customerId in customerIds)
        {
            Assert.That(progress.RecordValidAppearance(customerId, 0), Is.True);
            Assert.That(progress.MarkSeen(customerId, CustomerEncounterEventKind.FirstAppearance, 0), Is.True);
        }
    }

    private static void AssertMilestoneCandidate(CustomerEncounterSaveData record, string customerId, int milestone)
    {
        Assert.That(record, Is.Not.Null);
        Assert.That(record.CustomerId, Is.EqualTo(customerId));
        Assert.That(record.EventKind, Is.EqualTo((int)CustomerEncounterEventKind.VisitMilestone));
        Assert.That(record.VisitMilestone, Is.EqualTo(milestone));
        Assert.That(record.State, Is.EqualTo((int)CustomerEncounterEventState.Pending));
    }

    private static Action[] BeginEncounterGuide(Flow flow, CustomerVisitParty party)
    {
        flow.Customer = party.Members[0].Customer;
        if (party.Members.Count == 2 && flow.Table.CompanionOrderButton == null)
        {
            flow.Table.CompanionOrderButton = flow.Track(UnityEngine.Object.Instantiate(flow.Table.OrderButton.gameObject)).GetComponent<TableButton>();
            flow.Table.CompanionOrderButton.Init();
        }
        Assert.That(flow.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1, 0), Is.True);
        Assert.That(flow.Table.TableState, Is.EqualTo(ETableState.Move));
        Assert.That(flow.Table.CurrentParty, Is.SameAs(party));
        return party.Members.Select(member => Get<Action>(member.Customer, "_moveCompleted")).ToArray();
    }

    private static void ReachEncounterTarget(CustomerVisitMember member, Action arrival)
    {
        member.Customer.transform.position = member.Customer.TargetPos;
        arrival();
    }

    private static Action CompleteEncounterEntry(Flow flow, CustomerVisitParty party)
    {
        Action[] arrivals = BeginEncounterGuide(flow, party);
        var seating = new List<Action>();
        for (int index = 0; index < party.Members.Count; index++)
        {
            ReachEncounterTarget(party.Members[index], arrivals[index]);
            seating.Add(flow.TakeWait(1.5f));
            seating[index]();
        }
        Assert.That(flow.Table.TableState, Is.EqualTo(party.Members.Count == 1
            ? ETableState.Seating : ETableState.PartySeatedAwaitingOrders));
        return () =>
        {
            foreach (var arrival in arrivals) arrival();
            foreach (var seated in seating) seated();
        };
    }

    private static void InvokeGuidedEntry(object use)
    {
        var method = typeof(CustomerEncounterPresentation).GetMethod("OnGuidedEntryCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(null, new[] { use });
    }

    private static void ArriveEncounterParty(CustomerVisitParty party)
    {
        foreach (var member in party.Members)
        {
            member.Customer.transform.position = member.Customer.TargetPos;
            Get<Action>(member.Customer, "_moveCompleted")();
        }
    }

    private static bool TryPrepareArrival(CustomerVisitMember member, out CustomerEncounterSaveData record, out string message)
    {
        object[] arguments = { member, null, null };
        var method = typeof(CustomerEncounterPresentation).GetMethod("TryPrepareWaitingArrival", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        bool prepared = (bool)method.Invoke(null, arguments);
        record = (CustomerEncounterSaveData)arguments[1];
        message = (string)arguments[2];
        return prepared;
    }

    private static string CreateValidPendingPayload()
    {
        var progress = CustomerEncounterProgressData.CreateEmpty();
        Assert.That(progress.RecordValidAppearance("CUSTOMER01", 0), Is.True);
        string recordsJson = JsonMapper.ToJson(progress.ToSaveRecords());
        return "[{\"CustomerEncounterStateVersion\":1,\"CustomerEncounterStateList\":" + recordsJson + "}]";
    }

    private static string ReplaceFirstRecordValue(string payload, string field, int originalValue, int replacementValue)
    {
        string original = "\"" + field + "\":" + originalValue;
        Assert.That(payload, Does.Contain(original), "The valid fixture must contain exactly the target field value.");
        return payload.Replace(original, "\"" + field + "\":" + replacementValue);
    }

    private static void AssertProtectedEncounterPayload(string payload)
    {
        var loaded = new LoadUserData(JsonMapper.ToObject<JsonData>(payload));
        Assert.That(loaded.IsValid, Is.True, "The unrelated account payload remains parseable.");
        Assert.That(loaded.EncounterLoadStatus, Is.EqualTo(CustomerEncounterLoadStatus.ProtectedInvalid));
        Assert.That(loaded.CustomerEncounterRecords, Is.Empty,
            "Damaged encounter input must not be accepted as a normal empty encounter state.");
    }
}
