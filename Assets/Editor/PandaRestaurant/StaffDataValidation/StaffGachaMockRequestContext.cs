#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Editor 창과 별개로 유지하는 모의 계정/요청 컨텍스트. 실제 계정 조회, 지급, 저장은 하지 않는다.
/// 한 Editor 수명 안의 테스트 상태일 뿐 다른 기기/재실행의 중복 요청을 보호하지 않는다.
/// </summary>
[InitializeOnLoad]
internal sealed class StaffGachaMockRequestContext : IDisposable
{
    private static StaffGachaMockRequestContext _shared;
    private readonly string _requestPrefix = "editor-staff-mock-" + Guid.NewGuid().ToString("N") + "-";
    private readonly Dictionary<string, GachaStaffData> _displayStaff =
        new Dictionary<string, GachaStaffData>(StringComparer.Ordinal);
    private StaffGachaRequestSession _session = new StaffGachaRequestSession();
    private long _requestNumber;
    private bool _disposed;

    static StaffGachaMockRequestContext()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += ClearSharedForEditorLifecycle;
    }

    public StaffGachaMockRequestContext()
    {
        Diamonds = 110;
        Account = CreateInitialAccount();
    }

    public static StaffGachaMockRequestContext Shared
    {
        get
        {
            if (_shared == null || _shared._disposed) _shared = new StaffGachaMockRequestContext();
            return _shared;
        }
    }

    public int Diamonds { get; private set; }
    public StaffAccountSaveData Account { get; private set; }
    public StaffGachaRequestState State => _session?.State ?? StaffGachaRequestState.Idle;
    public StaffGachaSessionRequest CurrentRequest => _session?.CurrentRequest;
    public StaffGachaSessionRequest LastCompletedRequest { get; private set; }
    public bool CanStartNewRequest => !_disposed && _session.CanStartNewRequest;
    public bool CanReset => !_disposed && _session.CanStartNewRequest;

    public bool TryStart(StaffGachaPurchaseType purchaseType, out string error)
    {
        error = null;
        if (_disposed)
        {
            error = "정리된 모의 요청 컨텍스트는 다시 사용할 수 없습니다.";
            return false;
        }
        // 잠긴 요청에서는 후보 리소스 조회/생성이나 새 계산을 하지 않는다.
        if (!CanStartNewRequest)
        {
            error = "처리 중이거나 결과 미확정인 모의 요청이 있습니다.";
            return false;
        }
        if (purchaseType != StaffGachaPurchaseType.Single && purchaseType != StaffGachaPurchaseType.Multi)
        {
            error = "지원하지 않는 모의 직원 구매 종류입니다.";
            return false;
        }
        if (!TryGetDisplayStaff("STAFF23", out GachaStaffData rare, out error)) return false;
        if (rare.Rank != Rank.Rare)
        {
            error = "고정 미리보기의 STAFF23은 유효한 레어 직원이어야 합니다.";
            return false;
        }
        GachaStaffData[] drawn;
        if (purchaseType == StaffGachaPurchaseType.Single)
            drawn = new[] { rare };
        else
        {
            if (!TryGetDisplayStaff("STAFF01", out GachaStaffData normal, out error)) return false;
            if (normal.Rank != Rank.Normal1 && normal.Rank != Rank.Normal2)
            {
                error = "고정 미리보기의 STAFF01은 유효한 노멀 직원이어야 합니다.";
                return false;
            }
            // 추첨이 아닌 승인된 표시용 고정 입력. 가격/보상 계산은 기존 세션에만 맡긴다.
            drawn = new GachaStaffData[11];
            drawn[0] = drawn[1] = rare;
            for (int i = 2; i < drawn.Length; i++) drawn[i] = normal;
        }
        if (_requestNumber == long.MaxValue)
        {
            error = "모의 요청 ID 한도에 도달했습니다. Editor 수명 전환이 필요합니다.";
            return false;
        }
        string requestId = _requestPrefix + (++_requestNumber).ToString(CultureInfo.InvariantCulture);
        return _session.TryStart(requestId, Account, Diamonds, purchaseType, drawn, out error);
    }

    public bool TryHandleResponse(string id, StaffGachaResponseKind response,
        out StaffGachaSessionRequest completedRequest)
    {
        completedRequest = null;
        if (_disposed || !_session.TryHandleResponse(id, response, out completedRequest)) return false;
        // 최초 성공 확정이 반환한 불변 계획만 모의 계정에 반영한다. 불확정/실패는 반영하지 않는다.
        if (completedRequest != null)
        {
            Diamonds = completedRequest.Plan.DiamondsAfter;
            Account = completedRequest.Plan.AccountResult.UpdatedAccount;
            LastCompletedRequest = completedRequest;
        }
        return true;
    }

    public bool TryReset(out string error)
    {
        error = null;
        if (_disposed)
        {
            error = "정리된 모의 요청 컨텍스트는 초기화할 수 없습니다.";
            return false;
        }
        if (!CanReset)
        {
            error = "처리 중/미확정 요청은 초기화할 수 없습니다. 먼저 결과를 확정하세요.";
            return false;
        }
        _session = new StaffGachaRequestSession();
        Diamonds = 110;
        Account = CreateInitialAccount();
        LastCompletedRequest = null;
        // ID 번호는 유지하여 초기화 전 늦은 응답이 새 요청과 일치하지 않도록 한다.
        return true;
    }

    public bool TryCreateCompletedSequence(out StaffGachaAcquisitionPreviewSequence sequence, out string error)
    {
        sequence = null;
        error = null;
        if (_disposed)
        {
            error = "정리된 모의 요청 컨텍스트는 결과를 표시할 수 없습니다.";
            return false;
        }
        if (LastCompletedRequest == null)
        {
            error = "아직 성공 확정된 모의 요청 결과가 없습니다.";
            return false;
        }
        StaffGachaAcquisitionResult result = LastCompletedRequest.Plan.AccountResult.Acquisition;
        var displayStaff = new GachaStaffData[result.Items.Count];
        for (int i = 0; i < displayStaff.Length; i++)
            if (!TryGetDisplayStaff(result.Items[i].StaffId, out displayStaff[i], out error)) return false;
        // 표시 실패도 완료 기록을 지우지 않는다. 이미 계산된 보상/순서는 다시 계산하지 않는다.
        return StaffGachaAcquisitionPreviewSequence.TryCreateFromCalculated(result, displayStaff, out sequence, out error);
    }

    private bool TryGetDisplayStaff(string id, out GachaStaffData data, out string error)
    {
        error = null;
        if (_displayStaff.TryGetValue(id, out data) && data != null)
        {
            if (IsValidDisplayStaff(data, id)) return true;
            error = "모의 표시 직원과 등록 원본이 일치하지 않습니다: " + id;
            return false;
        }
        StaffData source = Resources.Load<StaffData>("StaffData/" + id);
        if (source == null || !string.Equals(source.Id, id, StringComparison.Ordinal) ||
            !IsSupportedRank(source.Rank) || (source.ThumbnailSprite == null && source.Sprite == null))
        {
            data = null;
            error = "모의 표시에 필요한 등록 직원/등급/이미지를 확인할 수 없습니다: " + id;
            return false;
        }
        data = GachaStaffData.Create(source);
        data.hideFlags = HideFlags.HideAndDontSave;
        _displayStaff[id] = data;
        return true;
    }

    private static bool IsValidDisplayStaff(GachaStaffData data, string id) =>
        data.StaffData != null && string.Equals(data.Id, id, StringComparison.Ordinal) &&
        string.Equals(data.StaffData.Id, id, StringComparison.Ordinal) && data.Rank == data.StaffData.Rank &&
        IsSupportedRank(data.Rank) && (data.ThumbnailSprite != null || data.Sprite != null);

    private static bool IsSupportedRank(Rank rank) => rank == Rank.Normal1 || rank == Rank.Normal2 ||
        rank == Rank.Rare || rank == Rank.Unique || rank == Rank.Special;

    private static StaffAccountSaveData CreateInitialAccount() => new StaffAccountSaveData(
        StaffAccountSaveConverter.CurrentVersion, new[]
        {
            new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4)
        }, 55);

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            ClearSharedForEditorLifecycle();
    }

    private static void ClearSharedForEditorLifecycle()
    {
        StaffGachaMockRequestContext previous = _shared;
        _shared = null;
        previous?.Dispose();
    }

    /// <summary>
    /// Editor 수명 종료/테스트 teardown의 임시 래퍼 정리 전용. 창 닫기나 사용자 초기화에 호출하지 않는다.
    /// 일반 초기화는 잠금을 검사하는 TryReset만 사용한다. 폐기한 참조는 다시 사용할 수 없다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (GachaStaffData data in _displayStaff.Values)
            if (data != null) Object.DestroyImmediate(data);
        _displayStaff.Clear();
        _session = null;
        LastCompletedRequest = null;
        Account = null;
        Diamonds = 0;
    }
}
#endif
