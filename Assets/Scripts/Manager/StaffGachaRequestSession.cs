using System;
using System.Collections.Generic;

/// <summary>
/// 메인 스레드에서 사용하는 단일 객체/세션의 메모리 요청 상태이다.
/// 외부에서 주입한 응답을 구분할 뿐, 서버 조회/신뢰성 검증/차감/지급/저장은 하지 않는다.
/// 상태와 수락한 ID 기록은 영속적이지 않으며 다른 세션이나 앱 재시작을 보호하지 않는다.
/// </summary>
public sealed class StaffGachaRequestSession
{
    private readonly HashSet<string> _usedRequestIds = new HashSet<string>(StringComparer.Ordinal);

    public StaffGachaRequestState State { get; private set; } = StaffGachaRequestState.Idle;
    // 종료 뒤에도 보관한다. 새 요청의 전체 계산이 성공한 경우에만 교체한다.
    public StaffGachaSessionRequest CurrentRequest { get; private set; }
    public bool CanStartNewRequest => State == StaffGachaRequestState.Idle
        || State == StaffGachaRequestState.Succeeded || State == StaffGachaRequestState.FailedUnapplied;

    public bool TryStart(string requestId, StaffAccountSaveData snapshot, int diamondBalance,
        StaffGachaPurchaseType purchaseType, IReadOnlyList<GachaStaffData> drawnStaff, out string error)
    {
        error = null;
        if (!CanStartNewRequest)
        {
            error = "진행 중이거나 결과 미확정인 요청이 있습니다.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(requestId) || _usedRequestIds.Contains(requestId))
        {
            error = "요청 ID가 비어 있거나 이 세션에서 이미 사용되었습니다.";
            return false;
        }
        if (!StaffGachaPurchasePlanCalculator.TryCalculate(snapshot, diamondBalance, purchaseType,
                drawnStaff, out var plan, out error))
            return false;

        // 수락한 ID만 원문 그대로 기록한다. 실패한 계산 시도는 ID를 소비하지 않는다.
        var request = new StaffGachaSessionRequest(requestId, plan);
        _usedRequestIds.Add(requestId);
        CurrentRequest = request;
        State = StaffGachaRequestState.Processing;
        return true;
    }

    /// <summary>
    /// true는 상태 전환을 뜻한다. completedRequest는 최초 성공 확정에만 제공된다.
    /// 이벤트 없이 상태를 먼저 확정한 뒤 반환하므로 호출자의 후속 처리/재진입도 중복 전달되지 않는다.
    /// 통신 실패 등 성공 여부를 모르는 응답에는 반드시 Indeterminate를 사용한다.
    /// </summary>
    public bool TryHandleResponse(string requestId, StaffGachaResponseKind response,
        out StaffGachaSessionRequest completedRequest)
    {
        completedRequest = null;
        if (CanStartNewRequest || !string.Equals(CurrentRequest.RequestId, requestId, StringComparison.Ordinal))
            return false;

        switch (response)
        {
            case StaffGachaResponseKind.Indeterminate:
                if (State == StaffGachaRequestState.Indeterminate) return false;
                State = StaffGachaRequestState.Indeterminate;
                return true;
            case StaffGachaResponseKind.SuccessConfirmed:
                State = StaffGachaRequestState.Succeeded;
                completedRequest = CurrentRequest;
                return true;
            case StaffGachaResponseKind.UnappliedFailureConfirmed:
                State = StaffGachaRequestState.FailedUnapplied;
                return true;
            default:
                return false;
        }
    }
}

public enum StaffGachaRequestState
{
    Idle,
    Processing,
    Indeterminate,
    Succeeded,
    FailedUnapplied
}

/// <summary>외부 판정을 주입하는 값. 미반영 확정 실패는 단순 통신 실패와 다르다.</summary>
public enum StaffGachaResponseKind
{
    Indeterminate,
    SuccessConfirmed,
    UnappliedFailureConfirmed
}

/// <summary>
/// 수락 당시 ID와 불변 미저장 계산안. 성공 응답도 실제 저장을 증명하지 않는다.
/// </summary>
public sealed class StaffGachaSessionRequest
{
    public string RequestId { get; }
    public StaffGachaPurchasePlan Plan { get; }

    internal StaffGachaSessionRequest(string requestId, StaffGachaPurchasePlan plan)
    {
        RequestId = requestId;
        Plan = plan;
    }
}
