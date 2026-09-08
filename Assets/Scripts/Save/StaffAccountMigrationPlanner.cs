using System;
using System.Collections.Generic;

/// <summary>
/// 호출자가 지정한 Stage의 원본 GiveStaffList만 계정 공용 후보로 계산한다.
/// 실제 보유/배치/스킨/재화/저장에 접근하지 않으며 A2 보정도 수행하지 않는다.
/// </summary>
public static class StaffAccountMigrationPlanner
{
    public static StaffAccountMigrationPlan Calculate(
        IReadOnlyCollection<EStage> requiredStages,
        IReadOnlyList<StaffStageOwnershipSnapshot> sources,
        IReadOnlyDictionary<string, int> knownStaffMaxLevels)
    {
        var issues = new List<StaffAccountMigrationIssue>();
        var scope = new List<EStage>();
        var requiredSet = new HashSet<EStage>();
        if (requiredStages == null || requiredStages.Count == 0)
            issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidScope, "필요한 Stage 범위를 명시해야 합니다."));
        else
        {
            foreach (EStage stage in requiredStages)
            {
                if (stage < EStage.Stage1 || stage >= EStage.Length)
                    issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidScope, "유효하지 않은 Stage 범위입니다.", stage));
                else if (requiredSet.Add(stage))
                    scope.Add(stage);
            }
        }

        if (sources == null)
            issues.Add(Issue(StaffAccountMigrationIssueCode.MissingSources, "Stage 원본 스냅샷이 없습니다."));
        if (knownStaffMaxLevels == null)
            issues.Add(Issue(StaffAccountMigrationIssueCode.MissingStaffDefinitions, "확인된 직원별 최대 레벨 스냅샷이 없습니다."));
        if (issues.Count > 0)
            return new StaffAccountMigrationPlan(null, issues);

        // 조회 기준도 복사한다. 이 목록은 유효성 확인에만 쓰며 보유 후보를 생성하지 않는다.
        var definitions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var definition in knownStaffMaxLevels)
        {
            if (definition.Key != null)
                definitions[definition.Key] = definition.Value;
        }

        var sourceByStage = new Dictionary<EStage, List<StaffStageOwnershipSnapshot>>();
        foreach (StaffStageOwnershipSnapshot source in sources)
        {
            if (source == null)
            {
                issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidSource, "null 원본은 Stage를 확인할 수 없습니다."));
                continue;
            }
            // 사용하지 않는 Stage의 자료/준비 여부는 이번 계산의 필수 조건이 아니다.
            if (!requiredSet.Contains(source.Stage))
                continue;

            if (!sourceByStage.TryGetValue(source.Stage, out var stageSources))
            {
                stageSources = new List<StaffStageOwnershipSnapshot>();
                sourceByStage.Add(source.Stage, stageSources);
            }
            stageSources.Add(source);
        }

        var idOrder = new List<string>();
        var recordsById = new Dictionary<string, List<StaffMigrationSourceRecord>>(StringComparer.Ordinal);
        foreach (EStage stage in scope)
        {
            if (!sourceByStage.TryGetValue(stage, out var stageSources))
            {
                issues.Add(Issue(StaffAccountMigrationIssueCode.MissingSource, "필요한 Stage 원본이 누락되었습니다.", stage));
                continue;
            }
            if (stageSources.Count != 1)
            {
                var duplicateRecords = new List<StaffMigrationSourceRecord>();
                foreach (var duplicateSource in stageSources)
                    duplicateRecords.AddRange(CaptureRecords(duplicateSource));
                issues.Add(Issue(StaffAccountMigrationIssueCode.DuplicateSource,
                    "같은 Stage 원본이 여러 개입니다. 임의로 선택하지 않습니다.", stage, duplicateRecords));
                continue;
            }

            StaffStageOwnershipSnapshot source = stageSources[0];
            List<StaffMigrationSourceRecord> records = CaptureRecords(source);
            if (!source.IsReady)
            {
                issues.Add(Issue(StaffAccountMigrationIssueCode.SourceNotReady, "원본 조회가 완료되지 않았습니다.", stage, records));
                continue;
            }
            if (!source.HasVerifiedRawRecords)
            {
                issues.Add(Issue(StaffAccountMigrationIssueCode.UnverifiedRawSource,
                    "기본값 치환/A2/런타임 보정 전 원본인지 확인되지 않았습니다.", stage, records));
                continue;
            }
            if (source.GiveStaffList == null)
            {
                issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidSource,
                    "GiveStaffList가 null입니다. 조회 완료된 빈 목록과 구분해야 합니다.", stage));
                continue;
            }

            foreach (StaffMigrationSourceRecord record in records)
            {
                var recordEvidence = new List<StaffMigrationSourceRecord> { record };
                if (!record.Level.HasValue)
                {
                    issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidStaffRecord, "보유 기록이 null입니다.", stage, recordEvidence));
                    continue;
                }
                if (!IsValidId(record.StaffId))
                {
                    issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidStaffId, "직원 ID가 비어 있거나 공백을 포함합니다.", stage, recordEvidence));
                    continue;
                }

                // 충돌 진단에는 유효성 검사에서 실패한 레벨도 원값 그대로 남긴다.
                if (!recordsById.TryGetValue(record.StaffId, out var sameIdRecords))
                {
                    sameIdRecords = new List<StaffMigrationSourceRecord>();
                    recordsById.Add(record.StaffId, sameIdRecords);
                    idOrder.Add(record.StaffId);
                }
                sameIdRecords.Add(record);

                if (!definitions.TryGetValue(record.StaffId, out int maximumLevel))
                    issues.Add(Issue(StaffAccountMigrationIssueCode.UnknownStaff, "확인된 직원 정보가 없습니다.", stage, recordEvidence));
                else if (maximumLevel < 1 || maximumLevel > StaffData.OfficialMaxLevel)
                    issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidStaffDefinition,
                        $"직원 최대 레벨 근거({maximumLevel})를 확인해야 합니다. 자동 보정하지 않습니다.", stage, recordEvidence));
                else if (record.Level.Value < 1 || record.Level.Value > maximumLevel)
                    issues.Add(Issue(StaffAccountMigrationIssueCode.InvalidLevel,
                        $"원본 레벨 {record.Level.Value}은 확인된 범위 1~{maximumLevel} 밖입니다. 원값을 보존합니다.", stage, recordEvidence));
            }
        }

        foreach (string staffId in idOrder)
        {
            List<StaffMigrationSourceRecord> records = recordsById[staffId];
            int originalLevel = records[0].Level.Value;
            if (records.Exists(record => record.Level.Value != originalLevel))
                issues.Add(Issue(StaffAccountMigrationIssueCode.LevelConflict,
                    $"동일 직원 {staffId}의 원본 레벨이 다릅니다. 자동 선택하지 않습니다.", null, records));
        }

        // 문제 하나라도 있으면 성공 후보를 만들지 않는다. 진단 정보만 반환한다.
        if (issues.Count > 0)
            return new StaffAccountMigrationPlan(null, issues);

        var candidates = new List<StaffAccountMigrationCandidate>();
        foreach (string staffId in idOrder)
            candidates.Add(new StaffAccountMigrationCandidate(staffId, recordsById[staffId][0].Level.Value));
        return new StaffAccountMigrationPlan(candidates, issues);
    }

    private static bool IsValidId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        foreach (char character in id)
        {
            if (char.IsWhiteSpace(character))
                return false;
        }
        return true;
    }

    private static List<StaffMigrationSourceRecord> CaptureRecords(StaffStageOwnershipSnapshot source)
    {
        var records = new List<StaffMigrationSourceRecord>();
        if (source.GiveStaffList != null)
        {
            for (int i = 0; i < source.GiveStaffList.Count; i++)
            {
                SaveStaffData staff = source.GiveStaffList[i];
                records.Add(new StaffMigrationSourceRecord(source.Stage, i, staff?.Id, staff?.Level, staff?.SkinId));
            }
        }
        return records;
    }

    private static StaffAccountMigrationIssue Issue(
        StaffAccountMigrationIssueCode code, string message, EStage? stage = null,
        List<StaffMigrationSourceRecord> records = null)
    {
        return new StaffAccountMigrationIssue(code, message, stage, records);
    }
}

/// <summary>
/// 호출자가 제공하는 읽기 전용 원본. GiveStaffList는 계산 도중 변경하지 않아야 한다.
/// HasVerifiedRawRecords는 파싱 기본값/A2/레벨 clamp 전 원값임을 호출자가 확인한 표시다.
/// 기존 Stage 로더 결과를 그대로 원본으로 간주하는 자동 연결은 제공하지 않는다.
/// </summary>
public sealed class StaffStageOwnershipSnapshot
{
    public EStage Stage { get; }
    public bool IsReady { get; }
    public bool HasVerifiedRawRecords { get; }
    public IReadOnlyList<SaveStaffData> GiveStaffList { get; }

    public StaffStageOwnershipSnapshot(EStage stage, bool isReady, bool hasVerifiedRawRecords, IReadOnlyList<SaveStaffData> giveStaffList)
    {
        Stage = stage;
        IsReady = isReady;
        HasVerifiedRawRecords = hasVerifiedRawRecords;
        GiveStaffList = giveStaffList;
    }
}

public enum StaffAccountMigrationIssueCode
{
    InvalidScope,
    MissingSources,
    MissingStaffDefinitions,
    InvalidSource,
    MissingSource,
    DuplicateSource,
    SourceNotReady,
    UnverifiedRawSource,
    InvalidStaffRecord,
    InvalidStaffId,
    UnknownStaff,
    InvalidStaffDefinition,
    InvalidLevel,
    LevelConflict,
}

/// <summary>원본 출처/값만 복사한 진단 자료. 성공 후보나 실제 저장 객체가 아니다.</summary>
public sealed class StaffMigrationSourceRecord
{
    public EStage Stage { get; }
    public int RecordIndex { get; }
    public string StaffId { get; }
    public int? Level { get; }
    public string SkinId { get; }

    internal StaffMigrationSourceRecord(EStage stage, int recordIndex, string staffId, int? level, string skinId)
    {
        Stage = stage;
        RecordIndex = recordIndex;
        StaffId = staffId;
        Level = level;
        SkinId = skinId;
    }
}

public sealed class StaffAccountMigrationIssue
{
    public StaffAccountMigrationIssueCode Code { get; }
    public string Message { get; }
    public EStage? Stage { get; }
    public IReadOnlyList<StaffMigrationSourceRecord> Records { get; }

    internal StaffAccountMigrationIssue(StaffAccountMigrationIssueCode code, string message, EStage? stage,
        List<StaffMigrationSourceRecord> records)
    {
        Code = code;
        Message = message;
        Stage = stage;
        Records = Array.AsReadOnly(records == null ? Array.Empty<StaffMigrationSourceRecord>() : records.ToArray());
    }
}

/// <summary>공용 성장 후보에는 Id/Level만 포함한다. 스킨 선택은 원본에 남긴다.</summary>
public sealed class StaffAccountMigrationCandidate
{
    public string StaffId { get; }
    public int Level { get; }

    internal StaffAccountMigrationCandidate(string staffId, int level)
    {
        StaffId = staffId;
        Level = level;
    }
}

public sealed class StaffAccountMigrationPlan
{
    public bool CanMigrate => Candidates != null && Issues.Count == 0;
    // 실패 시 null, 조회가 완료된 정상 빈 보유일 때는 읽기 전용 빈 목록이다.
    public IReadOnlyList<StaffAccountMigrationCandidate> Candidates { get; }
    public IReadOnlyList<StaffAccountMigrationIssue> Issues { get; }

    internal StaffAccountMigrationPlan(List<StaffAccountMigrationCandidate> candidates, List<StaffAccountMigrationIssue> issues)
    {
        Candidates = candidates == null ? null : Array.AsReadOnly(candidates.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
    }
}
