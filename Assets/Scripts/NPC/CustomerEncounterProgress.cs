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
        }
    };

    public static IReadOnlyList<int> VisitMilestones => Milestones;
    public static IReadOnlyCollection<string> CustomerIds => Lines.Keys;
    public static bool IsSupportedCustomer(string customerId) => !string.IsNullOrEmpty(customerId) && Lines.ContainsKey(customerId);

    public static bool IsValidEventKey(string customerId, int eventKind, int visitMilestone)
    {
        if (!IsSupportedCustomer(customerId)) return false;
        if (eventKind == (int)CustomerEncounterEventKind.FirstAppearance) return visitMilestone == 0;
        return eventKind == (int)CustomerEncounterEventKind.VisitMilestone && Array.IndexOf(Milestones, visitMilestone) >= 0;
    }

    public static bool TryGetMessage(string customerId, CustomerEncounterEventKind eventKind,
        int visitMilestone, out string message)
    {
        message = null;
        if (!Lines.TryGetValue(customerId ?? string.Empty, out string[] lines)) return false;
        int index;
        if (eventKind == CustomerEncounterEventKind.FirstAppearance)
        {
            if (visitMilestone != 0) return false;
            index = 0;
        }
        else
        {
            int milestoneIndex = Array.IndexOf(Milestones, visitMilestone);
            if (eventKind != CustomerEncounterEventKind.VisitMilestone || milestoneIndex < 0) return false;
            index = milestoneIndex + 1;
        }
        message = lines[index];
        return true;
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
            progress._states[new EventKey(customer.Id, CustomerEncounterEventKind.FirstAppearance, 0)] = CustomerEncounterEventState.Seen;
            foreach (int milestone in CustomerEncounterCatalog.VisitMilestones)
                if (customer.VisitCount >= milestone)
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
        if (!CustomerEncounterCatalog.IsSupportedCustomer(customerId) || _states.ContainsKey(key)) return false;
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
        foreach (int milestone in CustomerEncounterCatalog.VisitMilestones)
        {
            if (previousCount >= milestone || currentCount < milestone) continue;
            var key = new EventKey(customerId, CustomerEncounterEventKind.VisitMilestone, milestone);
            if (_states.ContainsKey(key)) continue;
            _states.Add(key, CustomerEncounterEventState.Pending);
            changed = true;
        }
        return changed;
    }

    public bool TryGetNextPending(string customerId, out CustomerEncounterSaveData record, out string message)
    {
        record = null;
        message = null;
        if (!CustomerEncounterCatalog.IsSupportedCustomer(customerId)) return false;
        var firstKey = new EventKey(customerId, CustomerEncounterEventKind.FirstAppearance, 0);
        if (_states.TryGetValue(firstKey, out CustomerEncounterEventState firstState)
            && firstState == CustomerEncounterEventState.Pending)
            return TryCreatePendingRecord(firstKey, out record, out message);

        foreach (int milestone in CustomerEncounterCatalog.VisitMilestones)
        {
            var key = new EventKey(customerId, CustomerEncounterEventKind.VisitMilestone, milestone);
            if (_states.TryGetValue(key, out CustomerEncounterEventState state) && state == CustomerEncounterEventState.Pending)
                return TryCreatePendingRecord(key, out record, out message);
        }
        return false;
    }

    private bool TryCreatePendingRecord(EventKey key, out CustomerEncounterSaveData record, out string message)
    {
        record = new CustomerEncounterSaveData(key.CustomerId, key.Kind, key.Milestone, CustomerEncounterEventState.Pending);
        return CustomerEncounterCatalog.TryGetMessage(key.CustomerId, key.Kind, key.Milestone, out message);
    }

    public bool MarkSeen(string customerId, CustomerEncounterEventKind kind, int milestone)
    {
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
