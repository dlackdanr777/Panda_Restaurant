using System;
using System.Collections.Generic;

/// <summary>
/// 전달된 조회 상태에 따라 기존 공용 JSON 읽기 또는 미저장 이전 후보 계산만 수행한다.
/// 조회/저장/초기화/지급은 수행하지 않으며, 후보 생성은 이전 완료나 게임 진입 준비 완료가 아니다.
/// </summary>
public static class StaffAccountLoadPlanner
{
    public static StaffAccountLoadResult Plan(
        StaffAccountQueryStatus queryStatus, string json,
        IReadOnlyCollection<EStage> requiredStages = null,
        IReadOnlyList<StaffStageOwnershipSnapshot> sources = null,
        IReadOnlyDictionary<string, int> knownStaffMaxLevels = null)
    {
        if (queryStatus == StaffAccountQueryStatus.NotCompleted)
            return new StaffAccountLoadResult(StaffAccountLoadStatus.QueryPending, error: "공용 데이터 조회가 완료되지 않았습니다.");
        if (queryStatus == StaffAccountQueryStatus.Failed)
            return new StaffAccountLoadResult(StaffAccountLoadStatus.QueryFailed, error: "공용 데이터 조회가 실패했습니다.");
        if (queryStatus == StaffAccountQueryStatus.SucceededFieldPresent)
        {
            if (json == null)
                return new StaffAccountLoadResult(StaffAccountLoadStatus.InvalidExistingData, error: "존재하는 공용 필드의 값이 null입니다.");
            StaffAccountSaveReadResult read = StaffAccountSaveConverter.Read(json);
            if (read.Status != StaffAccountSaveReadStatus.Success)
                return new StaffAccountLoadResult(read.Status == StaffAccountSaveReadStatus.UnsupportedVersion
                    ? StaffAccountLoadStatus.UnsupportedVersion : StaffAccountLoadStatus.InvalidExistingData, error: read.Error);
            return new StaffAccountLoadResult(StaffAccountLoadStatus.ExistingDataLoaded, existingData: read.Data);
        }
        if (queryStatus != StaffAccountQueryStatus.SucceededFieldAbsent || json != null)
            return new StaffAccountLoadResult(StaffAccountLoadStatus.InvalidQueryInput, error: "조회 상태가 잘못되었거나 부재 확인과 JSON 값이 모순됩니다.");

        StaffAccountMigrationPlan plan = StaffAccountMigrationPlanner.Calculate(requiredStages, sources, knownStaffMaxLevels);
        if (!plan.CanMigrate)
            return new StaffAccountLoadResult(StaffAccountLoadStatus.MigrationBlocked,
                migrationIssues: plan.Issues, error: "Stage 원본의 이전 계획을 확인해야 합니다.");
        var staff = new List<StaffAccountStaffRecord>();
        foreach (StaffAccountMigrationCandidate candidate in plan.Candidates)
            staff.Add(new StaffAccountStaffRecord(candidate.StaffId, candidate.Level));
        return new StaffAccountLoadResult(StaffAccountLoadStatus.UnsavedMigrationCandidate,
            migrationCandidate: new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, staff, 0));
    }
}

/// <summary>호출자가 확인한 조회 상태. JSON의 null 여부로 이 상태를 추정하지 않는다.</summary>
public enum StaffAccountQueryStatus
{
    NotCompleted,
    Failed,
    SucceededFieldAbsent,
    SucceededFieldPresent
}

public enum StaffAccountLoadStatus
{
    QueryPending,
    QueryFailed,
    ExistingDataLoaded,
    UnsavedMigrationCandidate,
    InvalidQueryInput,
    InvalidExistingData,
    UnsupportedVersion,
    MigrationBlocked
}

/// <summary>
/// 기존 데이터와 미저장 후보는 서로 다른 상태/필드로만 반환한다.
/// ExistingData도 형식 검증 결과일 뿐, 등록/최대 레벨/실제 계정 복원 검증 결과는 아니다.
/// 실패/미완료이면 두 데이터 필드 모두 null이며 진단만 남는다.
/// </summary>
public sealed class StaffAccountLoadResult
{
    public StaffAccountLoadStatus Status { get; }
    public StaffAccountSaveData ExistingData { get; }
    public StaffAccountSaveData MigrationCandidate { get; }
    public IReadOnlyList<StaffAccountMigrationIssue> MigrationIssues { get; }
    public string Error { get; }

    internal StaffAccountLoadResult(StaffAccountLoadStatus status,
        StaffAccountSaveData existingData = null, StaffAccountSaveData migrationCandidate = null,
        IReadOnlyList<StaffAccountMigrationIssue> migrationIssues = null, string error = null)
    {
        Status = status;
        ExistingData = existingData;
        MigrationCandidate = migrationCandidate;
        MigrationIssues = Array.AsReadOnly(migrationIssues == null
            ? Array.Empty<StaffAccountMigrationIssue>() : new List<StaffAccountMigrationIssue>(migrationIssues).ToArray());
        Error = error;
    }
}
