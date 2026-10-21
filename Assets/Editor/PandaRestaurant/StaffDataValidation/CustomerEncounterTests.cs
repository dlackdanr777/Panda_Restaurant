using System.Collections.Generic;
using LitJson;
using NUnit.Framework;
using UnityEngine;

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
