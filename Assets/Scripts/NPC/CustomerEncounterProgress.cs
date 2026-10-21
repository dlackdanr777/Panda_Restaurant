using System;
using System.Collections.Generic;
using System.Linq;

public enum CustomerEncounterEventKind
{
    FirstAppearance = 0,
    VisitMilestone = 1
}

public enum CustomerEncounterEventState
{
    Pending = 0,
    Seen = 1
}

[Serializable]
public sealed class CustomerEncounterSaveData
{
    public string CustomerId;
    public int EventKind;
    public int VisitMilestone;
    public int State;

    public CustomerEncounterSaveData() { }

    public CustomerEncounterSaveData(string customerId, CustomerEncounterEventKind eventKind,
        int visitMilestone, CustomerEncounterEventState state)
    {
        CustomerId = customerId;
        EventKind = (int)eventKind;
        VisitMilestone = visitMilestone;
        State = (int)state;
    }
}

public static class CustomerEncounterCatalog
{
    public const int StateVersion = 1;
    public const string StateVersionField = "CustomerEncounterStateVersion";
    public const string StateListField = "CustomerEncounterStateList";

    private static readonly int[] Milestones = { 25, 50, 100, 200, 300, 400, 500 };
    private static readonly IReadOnlyList<int> AllVisitMilestones = Array.AsReadOnly(Milestones);
    private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["CUSTOMER01"] = new[]
        {
            "어, 옆집에 불이 켜졌네! 난 파니야. 잘 부탁해!",
            "죽순은 잘 팔렸는데, 점심을 놓쳤지 뭐야.",
            "가게가 커지는 게 보여. 이웃으로서 뿌듯해!",
            "오늘은 쉬는 날! 남의 식당에서 쉬는 게 최고야.",
            "장사하다 막히면 말해. 손님 상대는 좀 알거든.",
            "길 건너에서도 여기 냄새가 나. 아주 좋은 냄새!",
            "바빠도 밥은 꼭 챙겨. 나처럼 놓치지 말고.",
            "처음 먼지 쌓였던 때 기억나? 이젠 참 북적거린다."
        },
        ["CUSTOMER02"] = new[]
        {
            "나 호랑이야. 고양이는 아니고.",
            "여기선 나를 제대로 알아보는군.",
            "키는 아직 더 클 거야. 정말이야.",
            "고양이? 아까 밖에서 들은 얘기야. 신경 안 써.",
            "큰 목소리를 내야 커 보이는 건 아니더라.",
            "네가 웃으면 놀리는 건지 헷갈려.",
            "이젠 먼저 정정하지 않아도 알아주겠지?",
            "여기 오는 길은 눈 감고도 찾아."
        },
        ["CUSTOMER03"] = new[]
        {
            "수련 중이야. 잠깐 쉬러 왔어.",
            "오늘도 마늘 냄새가 나려나.",
            "쑥만 먹은 지 몇 주째지? 세는 걸 잊었어.",
            "선생님은 참는 것도 수행이라셨지.",
            "구미호가 되면 뭘 먼저 할까? 아직 비밀.",
            "가끔은 다른 향이 그리워.",
            "쉬어 가는 것도 수련일 수 있겠지.",
            "꼬리가 늘었는지는 묻지 마. 아직 확인 중이야."
        },
        ["CUSTOMER04"] = new[]
        {
            "먹기 위해 뛰고, 뛰기 위해 먹어!",
            "운동하고 나서 쉬는 시간이 최고야.",
            "아침에 멀리 한 바퀴 돌고 왔어.",
            "달콤한 건 마음까지 가볍게 해.",
            "운동이 습관이 됐어. 신기하지?",
            "카우리한테도 잠깐 쉬라고 했는데.",
            "쉬는 날엔 더 많이 걷게 되더라.",
            "건강하게 오래 즐기는 게 내 목표야."
        },
        ["CUSTOMER05"] = new[]
        {
            "안녕. 내 털, 오늘은 좀 윤기가 나지?",
            "채소는 여전히 별로야. 그래도 먹어.",
            "빗질은 하루도 빼먹지 않아.",
            "비 오는 날엔 털이 정말 골치야.",
            "완벽한 털결은 작은 습관에서 나와.",
            "나도 가끔은 대충 살고 싶어.",
            "오늘은 빗 하나를 새로 샀어.",
            "네가 보는 내 모습만큼, 나도 나를 아껴."
        },
        ["CUSTOMER06"] = new[]
        {
            "자리 하나면 충분해.",
            "필요 없는 건 두고 왔어.",
            "물건이 적으면 마음도 조용하지.",
            "또 그런 소문이 돌았어? ……그래.",
            "오늘은 짐이 더 가벼워졌어.",
            "간단한 게 좋아. 설명도.",
            "무언가를 소유하면 지켜야 할 것도 생기지.",
            "여긴 오래 앉아도 부담스럽지 않네."
        },
        ["CUSTOMER07"] = new[]
        {
            "수업 끝났어! ……아마도.",
            "선생님이 나 찾으시면 못 봤다고 해줘.",
            "오늘은 공부보다 더 중요한 일이 있어.",
            "서당 가는 길을 또 잘못 들었네.",
            "나 사실 공부도 아주 못하진 않아.",
            "공책엔 너희 가게 가는 길이 제일 잘 그려져.",
            "오늘은 정말 수업 끝나고 온 거야!",
            "언젠가 선생님도 데려올게. 그땐 도망 안 가."
        },
        ["CUSTOMER08"] = new[]
        {
            "여기가 따뜻한 곳이구나. 좋아.",
            "극지방에선 이런 냄새가 안 나.",
            "온천을 찾다가 여기까지 왔어.",
            "내 고향에도 네가 올 수 있으면 좋겠네.",
            "덥지 않냐고? 이 정도는 포근해.",
            "여행길에서 만난 친구들을 기록해 둬.",
            "이곳 풍경은 눈이 없어도 참 좋다.",
            "돌아가도 여긴 꼭 기억할 거야."
        },
        ["CUSTOMER09"] = new[]
        {
            "안녕! 오늘 뿔 상태가 꽤 좋아.",
            "올해 대회도 준비해야겠어.",
            "사진은 어느 쪽에서 찍어야 잘 나올까?",
            "상을 받은 뒤로 신경 쓸 게 늘었어.",
            "칭찬해 줘도 돼. 꽤 노력했거든.",
            "뿔을 손질하는 시간이 생각보다 길어.",
            "누구에게나 자기만의 멋이 있는 거겠지.",
            "다음 대회 결과보다 오늘이 더 즐거워."
        },
        ["CUSTOMER10"] = new[]
        {
            "면접 다녀오는 길이야. 결과는 나중에.",
            "안정적이라는 말, 참 듣기 좋지.",
            "이력서는 계속 고쳐 쓰게 되네.",
            "왕이라 불리던 때가 언제였더라.",
            "아직은 포기 안 했어. 걱정 마.",
            "손님으로 앉아 있는 게 조금 어색해.",
            "일이 전부는 아니라는데, 글쎄.",
            "언젠가 좋은 소식 들려줄게. 조용히."
        },
        ["CUSTOMER11"] = new[]
        {
            "혹시 의자에 털 좀 묻었니?",
            "오늘은 빗에 털이 적게 남았어.",
            "바람 부는 날은 조금 무서워.",
            "여기선 마음 놓고 앉아도 될까?",
            "걱정만 하면 더 빠진다는 말이 있더라."
        }
    };

    private static readonly Dictionary<string, IReadOnlyList<int>> CustomerMilestones = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
    {
        ["CUSTOMER01"] = AllVisitMilestones,
        ["CUSTOMER02"] = AllVisitMilestones,
        ["CUSTOMER03"] = AllVisitMilestones,
        ["CUSTOMER04"] = AllVisitMilestones,
        ["CUSTOMER05"] = AllVisitMilestones,
        ["CUSTOMER06"] = AllVisitMilestones,
        ["CUSTOMER07"] = AllVisitMilestones,
        ["CUSTOMER08"] = AllVisitMilestones,
        ["CUSTOMER09"] = AllVisitMilestones,
        ["CUSTOMER10"] = AllVisitMilestones,
        ["CUSTOMER11"] = Array.AsReadOnly(new[] { 25, 50, 100, 200 })
    };

    public static IReadOnlyList<int> VisitMilestones => AllVisitMilestones;
    public static IReadOnlyCollection<string> CustomerIds => Lines.Keys;
    public static bool IsSupportedCustomer(string customerId) => !string.IsNullOrEmpty(customerId) && Lines.ContainsKey(customerId);
    public static IReadOnlyList<int> GetCustomerVisitMilestones(string customerId)
        => customerId != null && CustomerMilestones.TryGetValue(customerId, out var milestones)
            ? milestones : Array.Empty<int>();

    public static bool IsValidEventKey(string customerId, int eventKind, int visitMilestone)
    {
        if (!IsSupportedCustomer(customerId)) return false;
        return TryGetMessage(customerId, (CustomerEncounterEventKind)eventKind, visitMilestone, out _);
    }

    public static bool TryGetMessage(string customerId, CustomerEncounterEventKind eventKind,
        int visitMilestone, out string message)
    {
        message = null;
        if (!Lines.TryGetValue(customerId ?? string.Empty, out string[] lines)) return false;
        if (eventKind == CustomerEncounterEventKind.FirstAppearance)
        {
            if (visitMilestone != 0) return false;
            message = lines[0];
            return !string.IsNullOrEmpty(message);
        }
        if (eventKind != CustomerEncounterEventKind.VisitMilestone) return false;
        IReadOnlyList<int> milestones = GetCustomerVisitMilestones(customerId);
        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            if (milestones[milestoneIndex] != visitMilestone) continue;
            int lineIndex = milestoneIndex + 1;
            if (lineIndex >= lines.Length || string.IsNullOrEmpty(lines[lineIndex])) return false;
            message = lines[lineIndex];
            return true;
        }
        return false;
    }
}

public sealed class CustomerEncounterProgressData
{
    private struct EventKey : IEquatable<EventKey>
    {
        public readonly string CustomerId;
        public readonly CustomerEncounterEventKind Kind;
        public readonly int Milestone;

        public EventKey(string customerId, CustomerEncounterEventKind kind, int milestone)
        { CustomerId = customerId; Kind = kind; Milestone = milestone; }

        public bool Equals(EventKey other) => string.Equals(CustomerId, other.CustomerId, StringComparison.Ordinal)
            && Kind == other.Kind && Milestone == other.Milestone;
        public override bool Equals(object obj) => obj is EventKey other && Equals(other);
        public override int GetHashCode() => ((CustomerId == null ? 0 : StringComparer.Ordinal.GetHashCode(CustomerId)) * 397)
            ^ ((int)Kind * 31) ^ Milestone;
    }

    private readonly Dictionary<EventKey, CustomerEncounterEventState> _states;

    private CustomerEncounterProgressData(Dictionary<EventKey, CustomerEncounterEventState> states)
    { _states = states; }

    public static CustomerEncounterProgressData CreateEmpty() => new CustomerEncounterProgressData(
        new Dictionary<EventKey, CustomerEncounterEventState>());

    public static CustomerEncounterProgressData MigrateLegacy(IEnumerable<SaveCustomerData> customers)
    {
        var progress = CreateEmpty();
        if (customers == null) return progress;
        foreach (SaveCustomerData customer in customers)
        {
            if (customer == null || !CustomerEncounterCatalog.IsSupportedCustomer(customer.Id) || customer.VisitCount <= 0) continue;
            if (CustomerEncounterCatalog.IsValidEventKey(customer.Id, (int)CustomerEncounterEventKind.FirstAppearance, 0))
                progress._states[new EventKey(customer.Id, CustomerEncounterEventKind.FirstAppearance, 0)] = CustomerEncounterEventState.Seen;
            foreach (int milestone in CustomerEncounterCatalog.GetCustomerVisitMilestones(customer.Id))
                if (customer.VisitCount >= milestone && CustomerEncounterCatalog.IsValidEventKey(
                    customer.Id, (int)CustomerEncounterEventKind.VisitMilestone, milestone))
                    progress._states[new EventKey(customer.Id, CustomerEncounterEventKind.VisitMilestone, milestone)] = CustomerEncounterEventState.Seen;
        }
        return progress;
    }

    public static bool TryRestore(IEnumerable<CustomerEncounterSaveData> records,
        out CustomerEncounterProgressData progress, out string error)
    {
        progress = null;
        error = null;
        if (records == null) { error = "MissingEncounterRecords"; return false; }
        var states = new Dictionary<EventKey, CustomerEncounterEventState>();
        foreach (CustomerEncounterSaveData record in records)
        {
            if (record == null || !CustomerEncounterCatalog.IsValidEventKey(record.CustomerId, record.EventKind, record.VisitMilestone)
                || (record.State != (int)CustomerEncounterEventState.Pending && record.State != (int)CustomerEncounterEventState.Seen))
            { error = "InvalidEncounterRecord"; return false; }
            var key = new EventKey(record.CustomerId, (CustomerEncounterEventKind)record.EventKind, record.VisitMilestone);
            if (states.ContainsKey(key)) { error = "DuplicateEncounterRecord"; return false; }
            states.Add(key, (CustomerEncounterEventState)record.State);
        }
        progress = new CustomerEncounterProgressData(states);
        return true;
    }

    public bool RecordValidAppearance(string customerId, int officialVisitCount)
    {
        var key = new EventKey(customerId, CustomerEncounterEventKind.FirstAppearance, 0);
        if (!CustomerEncounterCatalog.IsValidEventKey(customerId, (int)CustomerEncounterEventKind.FirstAppearance, 0)
            || _states.ContainsKey(key)) return false;
        if (officialVisitCount > 0)
        {
            // Old rows have no appearance ledger. A customer with completed visits was already encountered.
            _states.Add(key, CustomerEncounterEventState.Seen);
            return false;
        }
        _states.Add(key, CustomerEncounterEventState.Pending);
        return true;
    }

    public bool RecordCompletedVisit(string customerId, int previousCount, int currentCount)
    {
        if (!CustomerEncounterCatalog.IsSupportedCustomer(customerId) || currentCount <= previousCount) return false;
        bool changed = false;
        foreach (int milestone in CustomerEncounterCatalog.GetCustomerVisitMilestones(customerId))
        {
            if (previousCount >= milestone || currentCount < milestone
                || !CustomerEncounterCatalog.IsValidEventKey(customerId, (int)CustomerEncounterEventKind.VisitMilestone, milestone)) continue;
            var key = new EventKey(customerId, CustomerEncounterEventKind.VisitMilestone, milestone);
            if (_states.ContainsKey(key)) continue;
            _states.Add(key, CustomerEncounterEventState.Pending);
            changed = true;
        }
        return changed;
    }

    public bool TryGetNextPending(string customerId, out CustomerEncounterSaveData record, out string message)
    {
        if (TryGetPendingFirstAppearance(customerId, out record, out message)) return true;
        return TryGetPendingVisitMilestone(customerId, int.MaxValue, out record, out message);
    }

    public bool TryGetPendingFirstAppearance(string customerId, out CustomerEncounterSaveData record, out string message)
    {
        record = null;
        message = null;
        if (!CustomerEncounterCatalog.IsSupportedCustomer(customerId)) return false;
        var firstKey = new EventKey(customerId, CustomerEncounterEventKind.FirstAppearance, 0);
        if (_states.TryGetValue(firstKey, out CustomerEncounterEventState firstState)
            && firstState == CustomerEncounterEventState.Pending)
            return TryCreatePendingRecord(firstKey, out record, out message);

        return false;
    }

    public bool TryGetPendingVisitMilestone(string customerId, int visitsBeforeThisVisit,
        out CustomerEncounterSaveData record, out string message)
    {
        record = null;
        message = null;
        // An older Pending remains eligible on any later visit, not just exactly N+1.
        // A milestone reached after this visit started belongs to a future visit.
        foreach (int milestone in CustomerEncounterCatalog.GetCustomerVisitMilestones(customerId))
        {
            if (milestone > visitsBeforeThisVisit) continue;
            var key = new EventKey(customerId, CustomerEncounterEventKind.VisitMilestone, milestone);
            if (_states.TryGetValue(key, out CustomerEncounterEventState state) && state == CustomerEncounterEventState.Pending)
                return TryCreatePendingRecord(key, out record, out message);
        }
        return false;
    }

    private bool TryCreatePendingRecord(EventKey key, out CustomerEncounterSaveData record, out string message)
    {
        record = null;
        message = null;
        if (!CustomerEncounterCatalog.TryGetMessage(key.CustomerId, key.Kind, key.Milestone, out message)) return false;
        record = new CustomerEncounterSaveData(key.CustomerId, key.Kind, key.Milestone, CustomerEncounterEventState.Pending);
        return true;
    }

    public bool MarkSeen(string customerId, CustomerEncounterEventKind kind, int milestone)
    {
        if (!CustomerEncounterCatalog.IsValidEventKey(customerId, (int)kind, milestone)) return false;
        var key = new EventKey(customerId, kind, milestone);
        if (!_states.TryGetValue(key, out CustomerEncounterEventState state) || state != CustomerEncounterEventState.Pending) return false;
        _states[key] = CustomerEncounterEventState.Seen;
        return true;
    }

    public bool IsPending(string customerId, CustomerEncounterEventKind kind, int milestone)
        => _states.TryGetValue(new EventKey(customerId, kind, milestone), out CustomerEncounterEventState state)
            && state == CustomerEncounterEventState.Pending;

    public bool IsSeen(string customerId, CustomerEncounterEventKind kind, int milestone)
        => _states.TryGetValue(new EventKey(customerId, kind, milestone), out CustomerEncounterEventState state)
            && state == CustomerEncounterEventState.Seen;

    public List<CustomerEncounterSaveData> ToSaveRecords()
        => _states.OrderBy(pair => pair.Key.CustomerId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Kind).ThenBy(pair => pair.Key.Milestone)
            .Select(pair => new CustomerEncounterSaveData(pair.Key.CustomerId, pair.Key.Kind,
                pair.Key.Milestone, pair.Value)).ToList();
}
