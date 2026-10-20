using System.Collections.Generic;
using LitJson;
using NUnit.Framework;

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
    public void T12_CatalogContainsOnlyTheThreeApprovedCustomersAndExactLines()
    {
        string[][] expected =
        {
            new[] { "어, 옆집에 불이 켜졌네! 난 파니야. 잘 부탁해!", "죽순은 잘 팔렸는데, 점심을 놓쳤지 뭐야.", "가게가 커지는 게 보여. 이웃으로서 뿌듯해!", "오늘은 쉬는 날! 남의 식당에서 쉬는 게 최고야.", "장사하다 막히면 말해. 손님 상대는 좀 알거든.", "길 건너에서도 여기 냄새가 나. 아주 좋은 냄새!", "바빠도 밥은 꼭 챙겨. 나처럼 놓치지 말고.", "처음 먼지 쌓였던 때 기억나? 이젠 참 북적거린다." },
            new[] { "나 호랑이야. 고양이는 아니고.", "여기선 나를 제대로 알아보는군.", "키는 아직 더 클 거야. 정말이야.", "고양이? 아까 밖에서 들은 얘기야. 신경 안 써.", "큰 목소리를 내야 커 보이는 건 아니더라.", "네가 웃으면 놀리는 건지 헷갈려.", "이젠 먼저 정정하지 않아도 알아주겠지?", "여기 오는 길은 눈 감고도 찾아." },
            new[] { "수련 중이야. 잠깐 쉬러 왔어.", "오늘도 마늘 냄새가 나려나.", "쑥만 먹은 지 몇 주째지? 세는 걸 잊었어.", "선생님은 참는 것도 수행이라셨지.", "구미호가 되면 뭘 먼저 할까? 아직 비밀.", "가끔은 다른 향이 그리워.", "쉬어 가는 것도 수련일 수 있겠지.", "꼬리가 늘었는지는 묻지 마. 아직 확인 중이야." }
        };
        string[] ids = { "CUSTOMER01", "CUSTOMER02", "CUSTOMER03" };
        Assert.That(CustomerEncounterCatalog.CustomerIds, Is.EquivalentTo(ids));
        for (int customer = 0; customer < ids.Length; customer++)
        {
            Assert.That(CustomerEncounterCatalog.TryGetMessage(ids[customer], CustomerEncounterEventKind.FirstAppearance, 0, out var first), Is.True);
            Assert.That(first, Is.EqualTo(expected[customer][0]));
            for (int milestone = 0; milestone < CustomerEncounterCatalog.VisitMilestones.Count; milestone++)
            {
                int visits = CustomerEncounterCatalog.VisitMilestones[milestone];
                Assert.That(CustomerEncounterCatalog.TryGetMessage(ids[customer], CustomerEncounterEventKind.VisitMilestone, visits, out var message), Is.True);
                Assert.That(message, Is.EqualTo(expected[customer][milestone + 1]));
            }
        }
        Assert.That(CustomerEncounterCatalog.IsSupportedCustomer("CUSTOMER04"), Is.False);
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
