using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// 전달된 계정 공용 스냅샷과 JSON 문자열만 변환한다. 서버/파일/UserInfo 연결은 없다.
/// 등록 여부, 직원별 최대 레벨, A2 보정 및 이전 충돌 정책은 이 형식 검증의 책임이 아니다.
/// </summary>
public static class StaffAccountSaveConverter
{
    public const int CurrentVersion = 1;

    public static bool TrySerialize(StaffAccountSaveData data, out string json, out string error)
    {
        json = null;
        if (!Validate(data, out error))
            return false;

        var staff = new JArray();
        foreach (StaffAccountStaffRecord record in data.Staff)
        {
            staff.Add(new JObject { ["Id"] = record.Id, ["Level"] = record.Level });
        }
        // 명시한 필드만 쓴다. 전역 serializer 설정이나 기존 저장 객체를 변경하지 않는다.
        var root = new JObject
        {
            ["Version"] = data.Version,
            ["Staff"] = staff,
            ["PandaTokens"] = data.PandaTokens
        };
        json = root.ToString(Formatting.None);
        return true;
    }

    /// <summary>
    /// null은 호출자가 공용 데이터 자체의 부재를 확인했음을 뜻한다(조회 실패의 대체값 아님).
    /// 빈 문자열, JSON null, 일부 필드 누락은 부재가 아닌 손상이다. 실패 시 Data는 항상 null이다.
    /// </summary>
    public static StaffAccountSaveReadResult Read(string json)
    {
        if (json == null)
            return new StaffAccountSaveReadResult(StaffAccountSaveReadStatus.Missing, null,
                "공용 데이터가 없습니다. 이전 여부를 별도로 판단해야 합니다.");
        if (string.IsNullOrWhiteSpace(json))
            return Invalid("공용 데이터 문자열이 비어 있습니다.");

        JObject root;
        try
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read())
                    return Invalid("공용 데이터 뒤에 추가 내용이 있습니다.");
            }
        }
        catch (JsonException)
        {
            return Invalid("공용 JSON이 손상되었거나 객체 형식이 아니거나 필드가 중복되었습니다.");
        }

        if (!TryInteger(root["Version"], int.MinValue, int.MaxValue, out long version))
            return Invalid("Version은 필수 32비트 정수입니다.");
        if (version != CurrentVersion)
            return new StaffAccountSaveReadResult(StaffAccountSaveReadStatus.UnsupportedVersion, null,
                "지원하지 않는 공용 데이터 형식 버전입니다: " + version);
        if (!HasOnlyFields(root, "Version", "Staff", "PandaTokens"))
            return Invalid("Version 1의 필수 필드가 누락되었거나 알 수 없는 필드가 있습니다.");
        if (!(root["Staff"] is JArray staff))
            return Invalid("Staff는 필수 배열입니다. null은 빈 보유 목록이 아닙니다.");
        if (!TryInteger(root["PandaTokens"], 0, long.MaxValue, out long pandaTokens))
            return Invalid("PandaTokens는 필수 0 이상 64비트 정수입니다.");

        var records = new List<StaffAccountStaffRecord>();
        for (int index = 0; index < staff.Count; index++)
        {
            if (!(staff[index] is JObject record) || !HasOnlyFields(record, "Id", "Level"))
                return Invalid("Staff[" + index + "]의 Id/Level 필드가 누락되었거나 잘못되었습니다.");
            if (record["Id"].Type != JTokenType.String)
                return Invalid("Staff[" + index + "].Id는 문자열이어야 합니다.");
            if (!TryInteger(record["Level"], 1, int.MaxValue, out long level))
                return Invalid("Staff[" + index + "].Level은 양의 32비트 정수여야 합니다.");
            records.Add(new StaffAccountStaffRecord((string)record["Id"], (int)level));
        }

        var data = new StaffAccountSaveData((int)version, records, pandaTokens);
        if (!Validate(data, out string error))
            return Invalid(error);
        return new StaffAccountSaveReadResult(StaffAccountSaveReadStatus.Success, data, null);
    }

    // 메모리 반영 계산도 직렬화 없이 동일한 형식 검증을 재사용한다.
    internal static bool Validate(StaffAccountSaveData data, out string error)
    {
        error = null;
        if (data == null)
            error = "공용 스냅샷이 null입니다.";
        else if (data.Version != CurrentVersion)
            error = "지원하지 않는 공용 데이터 형식 버전입니다: " + data.Version;
        else if (data.Staff == null)
            error = "보유 목록이 null입니다. 명시적인 빈 목록과 구분해야 합니다.";
        else if (data.PandaTokens < 0)
            error = "PandaTokens는 0 이상이어야 합니다.";
        if (error != null)
            return false;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < data.Staff.Count; index++)
        {
            StaffAccountStaffRecord record = data.Staff[index];
            if (record == null)
                error = "Staff[" + index + "]가 null입니다.";
            else if (!IsValidId(record.Id))
                error = "Staff[" + index + "].Id가 비어 있거나 공백을 포함합니다.";
            else if (!ids.Add(record.Id))
                error = "Staff[" + index + "]에 중복 ID가 있습니다. 자동 병합하지 않습니다.";
            else if (record.Level < 1)
                error = "Staff[" + index + "].Level은 양의 정수여야 합니다.";
            if (error != null)
                return false;
        }
        return true;
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

    private static bool TryInteger(JToken token, long minimum, long maximum, out long value)
    {
        value = 0;
        // 문자열/소수/지수/오버플로를 정수로 강제 변환하거나 반올림하지 않는다.
        return token != null && token.Type == JTokenType.Integer &&
            long.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
    }

    private static bool HasOnlyFields(JObject value, params string[] fields)
    {
        if (value.Count != fields.Length)
            return false;
        foreach (string field in fields)
        {
            if (value.Property(field, StringComparison.Ordinal) == null)
                return false;
        }
        return true;
    }

    private static StaffAccountSaveReadResult Invalid(string error)
    {
        return new StaffAccountSaveReadResult(StaffAccountSaveReadStatus.InvalidData, null, error);
    }
}

/// <summary>별도 SkinId/배치 정보가 없는 불변 공용 직원 레코드.</summary>
public sealed class StaffAccountStaffRecord
{
    public string Id { get; }
    public int Level { get; }

    public StaffAccountStaffRecord(string id, int level)
    {
        Id = id;
        Level = level;
    }
}

/// <summary>
/// 입력 목록을 복사한 메모리 스냅샷. 유효성은 변환기가 판정하며 생성 시 보정하지 않는다.
/// PandaTokens는 손실 없는 64비트 정수 표현이며 게임 정책상 잔액 상한을 정하지 않는다.
/// </summary>
public sealed class StaffAccountSaveData
{
    public int Version { get; }
    public IReadOnlyList<StaffAccountStaffRecord> Staff { get; }
    public long PandaTokens { get; }

    public StaffAccountSaveData(int version, IReadOnlyList<StaffAccountStaffRecord> staff, long pandaTokens)
    {
        Version = version;
        PandaTokens = pandaTokens;
        if (staff != null)
        {
            var copy = new StaffAccountStaffRecord[staff.Count];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = staff[index];
            Staff = Array.AsReadOnly(copy);
        }
    }
}

public enum StaffAccountSaveReadStatus
{
    Success,
    Missing,
    InvalidData,
    UnsupportedVersion
}

public sealed class StaffAccountSaveReadResult
{
    public StaffAccountSaveReadStatus Status { get; }
    // Missing/InvalidData/UnsupportedVersion에서 적용 가능한 부분 데이터는 노출하지 않는다.
    public StaffAccountSaveData Data { get; }
    public string Error { get; }

    internal StaffAccountSaveReadResult(StaffAccountSaveReadStatus status, StaffAccountSaveData data, string error)
    {
        Status = status;
        Data = data;
        Error = error;
    }
}
