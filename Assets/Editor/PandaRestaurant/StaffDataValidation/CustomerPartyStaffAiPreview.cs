#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit preparation and read-only observation in the existing original-project Play.
// Armed boundary checks use existing input/assignment APIs after observing real work.
// No staff decision, arrival or scheduled completion callback is invoked here.
public sealed partial class CustomerPartyStage1Preview
{
    private const string StaffAiKey = "Panda.PartyManual.StaffAi", NaturalAiKey = "Panda.PartyManual.NaturalAi";
    private static bool staffAi, naturalAi, observingStaff;
    private static int staffRole;
    private static double nextStaffSample, nextStaffHeartbeat;
    private static string staffEvidence;
    private static readonly Dictionary<string, string> staffLast = new Dictionary<string, string>();
    private Vector2 manualScroll;
    private static bool reassignOnNextTravel;
    private static EquipStaffType reassignRole;
    private static ERestaurantFloorType reassignFloor;
    private static CameraController pointerDiagnosticCamera;
    private enum AiBoundaryCase { GuardSameId, GuardReplacement, CleanerManualFirst, CleanerStaffFirst, ChefCookingReplacement, ChefWashingSameId, ManagerManualFirst, ManagerStaffFirst, MarketerFullResume, ShopFinalRemoval }
    private static readonly string[] aiBoundaryNames = { "가드 공격 중 해제→같은 ID", "가드 공격 중 해제→다른 ID", "청소 작업 중 수동 먼저", "청소 직원 완료→수동 요청", "셰프 조리 보조 중 해제→교체", "셰프 설거지 중 해제→같은 ID", "매니저 안내: 수동 먼저", "매니저 안내: 직원 먼저", "마케터 만석 호출→매니저 배치→재개", "미완료 2인 주문: 상점 최종 제거" };
    private static int aiBoundarySelection, aiBoundaryFloor;
    private sealed class AiBoundaryProbe
    {
        internal AiBoundaryCase Kind;
        internal ERestaurantFloorType Floor;
        internal CustomerPartyOwnedMemory Memory;
        internal Staff[] Staffs;
        internal TableData[] Tables;
        internal string Phase = "waiting";
        internal Staff Staff;
        internal long Lifetime;
        internal object Action, Cycle;
        internal TableData Table;
        internal TableUseIdentity Use;
        internal CustomerVisitParty Party;
        internal SaveTableData SavedTable;
        internal bool BowlFlag;
        internal int EmptyTransitions, BowlTransitions, Promotions;
        internal float FinishAt, LastCooldown;
        internal readonly Dictionary<TableData, Action<ETableState>> StateHandlers = new Dictionary<TableData, Action<ETableState>>();
        internal Action BowlHandler;
        internal UIRestaurantAdmin Shop;
        internal FurnitureData Furniture;
        internal KitchenBurnerData Burner;
        internal SinkKitchenUtensil Sink;
        internal Muks.Tween.TweenWait Wait;
        internal object WaitCallback, TargetEvent;
        internal GatecrasherCustomer Target;
    }
    private static AiBoundaryProbe aiBoundary;

    private static ERestaurantFloorType AiFloor => (ERestaurantFloorType)SessionState.GetInt(FloorKey, 0);
    private static void DrawStaffAiControls()
    {
        EditorGUILayout.LabelField("직원 AI · 실제 PlayerLoop 관찰", EditorStyles.boldLabel);
        if (GUILayout.Button("선택층 직원 6역할 배치 + 관찰")) PrepareStaffAi(false);
        if (GUILayout.Button("양층 직원 6역할 배치 + 관찰")) PrepareStaffAi(true);
        if (GUILayout.Button("고정 혼합 6팀 추가 (생성만)"))
            for (int i = 0; i < 6; i++)
                controller.TrySpawnWaitingParty(i % 2 == 0 ? new[] { "CUSTOMER01" } : new[] { "CUSTOMER01", "CUSTOMER02" }, out _);
        if (GUILayout.Button("같은 메뉴 2인 추가 (생성만)")) controller.TrySpawnWaitingParty(new[] { "CUSTOMER01", "CUSTOMER01" }, out _);
        using (new EditorGUI.DisabledScope(!observingStaff))
            if (GUILayout.Button("추가 주문 조건 2인 생성 (A 2회 / B 1회)")) PrepareAiAdditionalOrderParty();
        EditorGUILayout.HelpBox("추가 주문 조건은 직원 관찰을 시작한 뒤 메모리에 직접 준비합니다. 자연 스킬 발동 검사가 아닙니다. 생성 이후 안내·주문·서빙은 기존 흐름으로 진행합니다.", MessageType.None);
        EditorGUILayout.LabelField("선택층 역할 (배치 변경 대상)");
        staffRole = GUILayout.SelectionGrid(staffRole, Enum.GetNames(typeof(EquipStaffType)).Where(x => x != "Length").ToArray(), 3);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("해제")) ChangeAiStaff(0);
            if (GUILayout.Button("같은 직원 재배치")) ChangeAiStaff(1);
            if (GUILayout.Button("직원 교체")) ChangeAiStaff(2);
        }
        if (GUILayout.Button("선택 역할을 반대 층으로 이동")) ChangeAiStaff(3);
        if (GUILayout.Button("선택 직원 다음 업무 이동 중 1회 재배치"))
        { reassignOnNextTravel = true; reassignRole = (EquipStaffType)staffRole; reassignFloor = AiFloor; }
        if (reassignOnNextTravel)
        {
            EditorGUILayout.HelpBox("자동 검수 예약: " + reassignFloor + " / " + reassignRole + "의 다음 업무 이동에서 동일 배치를 한 번 갱신합니다.", MessageType.Warning);
            if (GUILayout.Button("예약한 자동 재배치 취소")) reassignOnNextTravel = false;
        }
        if (GUILayout.Button("진상 CUSTOMER81 생성 (확률검사 아님)")) controller.SpawnCustomer("CUSTOMER81");
        EditorGUILayout.LabelField("자연 설정 / 관찰", settings.EnableNaturalParties + " / " + observingStaff);
        EditorGUILayout.HelpBox("직원 배치는 준비 API입니다. 이후 업무는 기존 AI가 수행합니다. 안내 버튼의 층 고정은 이 모드에서 적용하지 않습니다. 직원 UI로도 직접 변경할 수 있습니다.", MessageType.None);
        DrawAiBoundaryControls();
        DrawStaffAiCameraControls();
    }
    private static void DrawAiBoundaryControls()
    {
        EditorGUILayout.LabelField("취소·경합 1회 예약 (기본 꺼짐)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(aiBoundary != null))
        {
            aiBoundarySelection = GUILayout.SelectionGrid(aiBoundarySelection, aiBoundaryNames, 1);
            EditorGUILayout.LabelField("관측 대상층");
            aiBoundaryFloor = GUILayout.SelectionGrid(aiBoundaryFloor, new[] { "1층", "VIP층" }, 2);
        }
        EditorGUILayout.HelpBox("실제 PlayerLoop 업무를 기다린 뒤 기존 API/합성 EventSystem 입력을 한 번 실행합니다. OS 입력 검증이 아닙니다. 매니저 사례는 대기 팀 1개, 마케터 사례는 매니저 해제·실제 만석을 준비하세요. 마케터 사례만 만석 호출 시도 후 매니저를 배치합니다. 상점 사례는 A 2회/B 1회 팀의 완료 구성원과 미완료 주문이 함께 있을 때 해제 후 본체를 닫으며 재장착은 직접 합니다.", MessageType.None);
        using (new EditorGUI.DisabledScope(!observingStaff || aiBoundary != null))
            if (GUILayout.Button("선택 경계 1회 예약")) ArmAiBoundary();
        if (aiBoundary != null)
        {
            EditorGUILayout.HelpBox(aiBoundaryNames[(int)aiBoundary.Kind] + " / " + aiBoundary.Floor + " / " + aiBoundary.Phase, MessageType.Warning);
            if (GUILayout.Button("경계 예약·관찰 취소")) StopAiBoundary("cancelled by user");
        }
    }
    private static void ArmAiBoundary()
    {
        Require(ready && observingStaff && CustomerPartyManualPlayBootstrap.Active && memory != null && !BackEnd.Backend.IsInitialized, "Memory AI observation required");
        reassignOnNextTravel = false;
        var scene = SceneManager.GetActiveScene();
        aiBoundary = new AiBoundaryProbe { Kind = (AiBoundaryCase)aiBoundarySelection, Floor = (ERestaurantFloorType)aiBoundaryFloor, Memory = memory,
            Staffs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Staff>(true)).ToArray(),
            Tables = tables.GetTableDataList((ERestaurantFloorType)aiBoundaryFloor).ToArray() };
        WriteStaffObservation("boundary/armed", aiBoundary.Kind + "; floor=" + aiBoundary.Floor + "; input=existing API or synthetic EventSystem; no forced work completion");
    }
    private static Staff BoundaryStaff(AiBoundaryProbe p, EquipStaffType role) => p.Staffs.FirstOrDefault(s => s != null && s.isActiveAndEnabled && s.StaffData != null && s.EquipFloorType == p.Floor && s.EquipStaffType == role);
    private static string BoundarySnapshot(AiBoundaryProbe p)
    {
        var action = p.Staff == null ? null : ReadAiField(p.Staff, "_staffAction");
        var burner = ReadAiField(action, "_burnerData") as KitchenBurnerData;
        var batch = p.Use?.Orders;
        var saved = p.SavedTable;
        return "case=" + p.Kind + "; phase=" + p.Phase + "; frame=" + Time.frameCount + "; floor=" + p.Floor
            + "; staff=" + p.Staff?.StaffData?.Id + "; lifetime=" + p.Staff?.WorkLifetime + "; action=" + AiIdentity(action) + "; capturedAction=" + AiIdentity(p.Action)
            + "; capturedLifetime=" + p.Lifetime
            + "; busy=" + ReadAiField(action, "_isUsed") + "; state=" + ReadAiField(p.Staff, "_state") + "; lease=" + AiIdentity(ReadAiField(action, "_lease"))
            + "; guard=" + ReadAiField(action, "_startAction") + "; guardTarget=" + ReadAiGuardTarget(ReadAiField(action, "_gatecrasherCustomer") as GatecrasherCustomer)
            + "; capturedGuardTarget=" + AiIdentity(ReadAiField(p.Action, "_gatecrasherCustomer")) + "; oldWait=" + AiIdentity(ReadAiField(p.Action, "_wait"))
            + "; capturedTarget=" + ReadAiGuardTarget(p.Target) + "; capturedEvent=" + AiIdentity(p.TargetEvent)
            + "; waitObject=" + AiIdentity(p.Wait) + "; waitEnabled=" + (p.Wait != null && p.Wait.enabled)
            + "; originalCallback=" + AiIdentity(p.WaitCallback) + "; liveWaitCallback=" + AiIdentity(ReadAiField(p.Wait, "_onCompleted"))
            + "; burner=" + AiIdentity(burner) + "; bonus=" + burner?.AddCookSpeedMul + "; cooking=" + AiIdentity(burner == null ? null : ReadAiField(burner.CookingData, "_identity"))
            + "; capturedBurner=" + AiIdentity(p.Burner) + "; capturedBurnerOwner=" + p.Burner?.UseStaff?.StaffData?.Id + "; capturedBonus=" + p.Burner?.AddCookSpeedMul
            + "; capturedSink=" + AiIdentity(p.Sink) + "; sinkOwner=" + p.Sink?.UseStaff?.StaffData?.Id + "; washing=" + p.Sink?.IsStaffWashing
            + "; table=" + p.Table?.TableType + "; state=" + p.Table?.TableState + "; capturedUse=" + AiIdentity(p.Use) + "; liveUse=" + AiIdentity(p.Table?.CurrentUse)
            + "; cycle=" + AiIdentity(p.Cycle) + "; liveCycle=" + AiIdentity(p.Table?.CleaningCycle) + "; emptyTransitions=" + p.EmptyTransitions
            + "; bowlProcessed=" + saved?.CleaningBowlProcessed + "; additionalBowlCommits=" + p.BowlTransitions
            + "; orders=" + (batch == null ? "" : string.Join("|", batch.Orders.Select(o => AiIdentity(o) + "/seat" + o.SeatIndex + "/" + o.State + "/lease" + AiIdentity(o.Identity.ServingReservation))))
            + "; completion=" + (p.Use == null ? "" : string.Join("|", p.Use.Completions.Select(c => "seat" + c.Seat + "/meals=" + c.MealsCompleted + "/completedPrice=" + c.CompletedPrice
                + "/coin=" + c.CoinAmount + "/fixed=" + c.CoinFixed + "/dropped=" + c.CoinDropped + "/final=" + c.FinalCompleted + "/visit=" + c.VisitCounted
                + "/customer=" + c.CustomerCounted + "/tip=" + c.TipAmount + "/tipPaid=" + c.TipPaid)))
            + "; settled=" + p.Use?.SettlementCompleted + "; removed=" + p.Use?.FurnitureRemoved + "; penalty=" + p.Use?.ServicePenaltyApplied
            + "; aborted=" + p.Use?.ServiceAborted + "; reason=" + p.Use?.ServiceAbortReason + "; teams=" + controller.Count + "; maxTeams=" + GameManager.Instance.MaxWaitCustomerCount
            + "; promotions=" + UserInfo.PromotionCount + "; touches=" + controller.TabCount + "; visits=" + UserInfo.TotalCumulativeCustomerCount + "; cleanCount=" + UserInfo.TotalCleanCount
            + "; bowls=" + UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, p.Floor) + "; money=" + UserInfo.Money + "; satisfaction=" + UserInfo.GetSatisfaction(UserInfo.CurrentStage)
            + "; tip=" + UserInfo.GetTip(UserInfo.CurrentStage) + "; cookingCount=" + UserInfo.TotalCookCount
            + "; managerResult=" + tables.LastManagerGuideDiagnostic.Result + "; managerFloor=" + tables.LastManagerGuideDiagnostic.SelectedFloor;
    }
    private static void LogAiBoundary(AiBoundaryProbe p, string point) => WriteStaffObservation("boundary/" + point, BoundarySnapshot(p));
    private static void ObserveAiBoundary(AiBoundaryProbe p)
    { p.Phase = "observing"; p.FinishAt = Time.time + 8f; }
    private static void StopAiBoundary(string reason)
    {
        var p = aiBoundary; aiBoundary = null;
        if (p == null) return;
        foreach (var pair in p.StateHandlers) if (pair.Key != null) pair.Key.OnChangeTableStateHandler -= pair.Value;
        if (p.BowlHandler != null) UserInfo.OnChangeSinkBowlHandler -= p.BowlHandler;
        WriteStaffObservation("boundary/end", p.Kind + "; " + reason + "; phase=" + p.Phase + "; emptyTransitions=" + p.EmptyTransitions + "; additionalBowlCommits=" + p.BowlTransitions);
    }
    private static void WatchBoundaryTable(AiBoundaryProbe p, TableData table, Action<ETableState> additional = null)
    {
        Action<ETableState> handler = state =>
        {
            if (!ReferenceEquals(aiBoundary, p)) return;
            try
            {
                if (ReferenceEquals(table, p.Table) && ReferenceEquals(table.CleaningCycle, p.Cycle) && state == ETableState.Empty) p.EmptyTransitions++;
                LogAiBoundary(p, "table-event");
                additional?.Invoke(state);
            }
            catch (Exception error)
            {
                WriteStaffObservation("boundary/FAILED", p.Kind + "; observed table event; " + error);
                StopAiBoundary("FAILED: " + error.Message); Debug.LogException(error);
            }
        };
        p.StateHandlers.Add(table, handler); table.OnChangeTableStateHandler += handler;
    }
    private static void CaptureBoundaryTable(AiBoundaryProbe p, TableData table)
    {
        p.Table = table; p.Use = table.CurrentUse; p.Cycle = table.CleaningCycle;
        p.SavedTable = ReadAiField(table.TableFurniture, "_saveTableData") as SaveTableData;
        p.BowlFlag = p.SavedTable != null && p.SavedTable.CleaningBowlProcessed;
        WriteStaffObservation("boundary/capture", "table=" + table.TableType + "; cycle=" + AiIdentity(p.Cycle) + "; initialBowlProcessed=" + p.BowlFlag
            + "; cleanCountIsGarbageStatistic=true; table evidence=one Empty transition and no duplicate bowl commit");
        p.BowlHandler = () =>
        {
            if (!ReferenceEquals(aiBoundary, p) || !ReferenceEquals(table.CleaningCycle, p.Cycle) || p.SavedTable == null) return;
            bool processed = p.SavedTable.CleaningBowlProcessed;
            if (processed && !p.BowlFlag) p.BowlTransitions++;
            p.BowlFlag = processed;
            LogAiBoundary(p, "sink-event");
        };
        UserInfo.OnChangeSinkBowlHandler += p.BowlHandler;
    }
    private static void ManualBoundaryClean(AiBoundaryProbe p)
    {
        var pointer = ReadAiField(p.Table.TableFurniture, "_pointerDownSpriteRenderer") as PointerDownSpriteRenderer;
        Require(pointer != null && pointer.isActiveAndEnabled && EventSystem.current != null, "Existing manual table pointer required");
        LogAiBoundary(p, "pre-manual-clean-EventSystem");
        ExecuteEvents.Execute(pointer.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerDownHandler);
        LogAiBoundary(p, "post-manual-clean-EventSystem");
    }
    private static void BoundaryReassign(AiBoundaryProbe p, bool replacement)
    {
        var role = p.Staff.EquipStaffType;
        var ids = CustomerPartyOwnedMemory.StaffAiStaffIds[role];
        string id = p.Staff.StaffData.Id;
        if (replacement) id = ids.First(x => x != id);
        var other = p.Floor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1;
        Require(UserInfo.GetEquipStaff(UserInfo.CurrentStage, other, role)?.Id != id, "Replacement is already on the other floor; prepare a free owned employee first");
        ObserveAiBoundary(p);
        LogAiBoundary(p, "pre-remove");
        UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, p.Floor, role);
        LogAiBoundary(p, "post-remove-pre-equip");
        UserInfo.SetEquipStaff(UserInfo.CurrentStage, p.Floor, role, id);
        LogAiBoundary(p, "post-equip");
    }
    private static void TickAiBoundary()
    {
        var p = aiBoundary;
        if (p == null) return;
        if (!CustomerPartyManualPlayBootstrap.Active || memory == null || !ReferenceEquals(memory, p.Memory) || BackEnd.Backend.IsInitialized) { StopAiBoundary("cancelled: memory session ended"); return; }
        try
        {
            if (p.Phase == "observing")
            {
                if (Time.time >= p.FinishAt) { LogAiBoundary(p, "after-live-continuation"); StopAiBoundary("input completed; assess evidence, not an automatic PASS"); }
                return;
            }
            if (p.Kind == AiBoundaryCase.ShopFinalRemoval) { TickBoundaryShop(p); return; }
            if (p.Kind == AiBoundaryCase.MarketerFullResume) { TickBoundaryMarketer(p); return; }
            if (p.Kind == AiBoundaryCase.ManagerManualFirst || p.Kind == AiBoundaryCase.ManagerStaffFirst) { TickBoundaryManager(p); return; }
            if (p.Phase == "waiting-for-staff-clean")
            {
                Require(p.Staff != null && p.Staff.WorkLifetime == p.Lifetime && ReferenceEquals(ReadAiField(p.Staff, "_staffAction"), p.Action)
                    && ReferenceEquals(p.Table.CleaningCycle, p.Cycle), "Captured cleaner work changed before completion");
                return;
            }
            var role = p.Kind == AiBoundaryCase.GuardSameId || p.Kind == AiBoundaryCase.GuardReplacement ? EquipStaffType.Guard
                : p.Kind == AiBoundaryCase.CleanerManualFirst || p.Kind == AiBoundaryCase.CleanerStaffFirst ? EquipStaffType.Cleaner : EquipStaffType.Chef;
            p.Staff = BoundaryStaff(p, role);
            if (p.Staff == null) return;
            p.Action = ReadAiField(p.Staff, "_staffAction");
            p.Lifetime = p.Staff.WorkLifetime;
            if (role == EquipStaffType.Guard)
            {
                var target = ReadAiField(p.Action, "_gatecrasherCustomer") as GatecrasherCustomer;
                var wait = ReadAiField(p.Action, "_wait") as Muks.Tween.TweenWait;
                if (target != null && target.isActiveAndEnabled && !target.IsEndEvent && !target.IsInDisguise && target.VisitFloor == p.Floor
                    && ReferenceEquals(controller.GatecrasherCustomer[(int)p.Floor], target) && Equals(ReadAiField(p.Action, "_startAction"), true)
                    && ReferenceEquals(ReadAiField(p.Action, "_targetEvent"), target.ServiceEventIdentity)
                    && (float)ReadAiField(target, "_currentTouchCount") > 0 && wait != null && wait.enabled && ReadAiField(wait, "_onCompleted") != null)
                {
                    p.Target = target; p.TargetEvent = target.ServiceEventIdentity; p.Wait = wait; p.WaitCallback = ReadAiField(wait, "_onCompleted");
                    BoundaryReassign(p, p.Kind == AiBoundaryCase.GuardReplacement);
                }
            }
            else if (role == EquipStaffType.Cleaner)
            {
                if (p.Phase != "waiting") return;
                var wait = ReadAiField(p.Action, "_tweenData") as Muks.Tween.TweenWait;
                var callback = ReadAiField(wait, "_onCompleted") as Action;
                var table = ReadAiField(callback?.Target, "data") as TableData;
                if (!Equals(ReadAiField(p.Action, "_isUsed"), true) || !Equals(ReadAiField(p.Staff, "_state"), EStaffState.Action)
                    || wait == null || !wait.enabled || table == null || table.FloorType != p.Floor || table.TableState != ETableState.NeedCleaning
                    || !ReferenceEquals(ReadAiField(callback.Target, "cleaningCycle"), table.CleaningCycle) || !table.TableFurniture.CanClean) return;
                p.Wait = wait; p.WaitCallback = callback;
                CaptureBoundaryTable(p, table); LogAiBoundary(p, "actual-cleaning-wait-captured");
                p.Phase = "waiting-for-staff-clean";
                WatchBoundaryTable(p, table, state =>
                {
                    if (p.Kind != AiBoundaryCase.CleanerStaffFirst || p.Phase != "waiting-for-staff-clean" || state != ETableState.Empty) return;
                    Require(p.Staff.WorkLifetime == p.Lifetime && ReferenceEquals(ReadAiField(p.Staff, "_staffAction"), p.Action)
                        && ReferenceEquals(ReadAiField(p.Wait, "_onCompleted"), p.WaitCallback) && Equals(ReadAiField(p.Wait, "_isCompleting"), true), "Captured cleaner must be completing its actual wait");
                    ObserveAiBoundary(p); ManualBoundaryClean(p);
                });
                if (p.Kind == AiBoundaryCase.CleanerManualFirst) { ObserveAiBoundary(p); ManualBoundaryClean(p); }
            }
            else
            {
                var burner = ReadAiField(p.Action, "_burnerData") as KitchenBurnerData;
                var sink = ReadAiField(p.Action, "_sink") as SinkKitchenUtensil;
                bool cooking = burner != null && burner.UseStaff == p.Staff && burner.KitchenUtensil.IsStaffWorking && burner.AddCookSpeedMul > 0 && !burner.CookingData.IsDefault();
                bool washing = sink != null && sink.UseStaff == p.Staff && sink.IsStaffWashing && UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, p.Floor) > 0;
                if ((p.Kind == AiBoundaryCase.ChefCookingReplacement && cooking) || (p.Kind == AiBoundaryCase.ChefWashingSameId && washing))
                {
                    p.Burner = burner; p.Sink = sink;
                    BoundaryReassign(p, p.Kind == AiBoundaryCase.ChefCookingReplacement);
                }
            }
        }
        catch (Exception error)
        {
            WriteStaffObservation("boundary/FAILED", p.Kind + "; phase=" + p.Phase + "; " + error);
            StopAiBoundary("FAILED: " + error.Message);
            Debug.LogException(error);
        }
    }
    private static void TickBoundaryManager(AiBoundaryProbe p)
    {
        if (p.Phase != "waiting") return;
        p.Staff = BoundaryStaff(p, EquipStaffType.Manager);
        var party = controller.FirstReadyParty;
        if (p.Staff == null || party == null || controller.Count != 1 || !p.Tables.Any(t => t.TableState == ETableState.Empty)) return;
        p.Action = ReadAiField(p.Staff, "_staffAction"); p.Party = party;
        if (p.Kind == AiBoundaryCase.ManagerManualFirst)
        {
            if ((float)ReadAiField(p.Action, "_actionCoolTime") > Mathf.Max(.05f, Time.deltaTime * p.Staff.SpeedMul)) return;
            ObserveAiBoundary(p); LogAiBoundary(p, "pre-manual-guide-existing-API");
            bool accepted = tables.OnCustomerGuideEvent(p.Floor);
            p.Table = party.CurrentTable; p.Use = p.Table?.CurrentUse;
            LogAiBoundary(p, "post-manual-guide-" + accepted + "-await-natural-manager");
        }
        else
        {
            p.Phase = "waiting-for-natural-manager";
            foreach (var table in p.Tables)
                WatchBoundaryTable(p, table, state =>
                {
                    if (p.Phase != "waiting-for-natural-manager" || state != ETableState.Move || !ReferenceEquals(table.CurrentUse?.Party, party)) return;
                    p.Table = table; p.Use = table.CurrentUse; ObserveAiBoundary(p);
                    LogAiBoundary(p, "natural-manager-first-pre-manual-guide");
                    bool accepted = tables.OnCustomerGuideEvent(p.Floor);
                    LogAiBoundary(p, "post-manual-guide-" + accepted);
                });
        }
    }
    private static void TickBoundaryMarketer(AiBoundaryProbe p)
    {
        p.Staff = BoundaryStaff(p, EquipStaffType.Marketer);
        if (p.Staff == null) return;
        p.Action = ReadAiField(p.Staff, "_staffAction");
        float cooldown = (float)ReadAiField(p.Action, "_actionCoolTime");
        if (p.Phase == "waiting")
        {
            if (controller.Count != GameManager.Instance.MaxWaitCustomerCount || BoundaryStaff(p, EquipStaffType.Manager) != null) return;
            p.Promotions = UserInfo.PromotionCount; p.LastCooldown = cooldown; p.Phase = "full-waiting-for-real-call"; LogAiBoundary(p, "full-captured"); return;
        }
        if (p.Phase == "full-waiting-for-real-call")
        {
            Require(controller.Count == GameManager.Instance.MaxWaitCustomerCount && UserInfo.PromotionCount == p.Promotions, "Full-queue precondition changed before the real call attempt");
            bool called = cooldown > p.LastCooldown; p.LastCooldown = cooldown;
            if (!called) return;
            LogAiBoundary(p, "full-real-marketer-call-no-new-team");
            p.Party = controller.FirstReadyParty;
            Require(p.Party != null, "A ready front team is required for the manager-resume case");
            var other = p.Floor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1;
            string id = CustomerPartyOwnedMemory.StaffAiStaffIds[EquipStaffType.Manager].First(x => UserInfo.GetEquipStaff(UserInfo.CurrentStage, other, EquipStaffType.Manager)?.Id != x);
            p.Phase = "waiting-for-natural-guide-and-call";
            UserInfo.SetEquipStaff(UserInfo.CurrentStage, p.Floor, EquipStaffType.Manager, id);
            LogAiBoundary(p, "post-explicit-manager-placement"); return;
        }
        if (p.Phase == "waiting-for-natural-guide-and-call" && p.Party.CurrentTable != null && UserInfo.PromotionCount > p.Promotions)
        {
            p.Table = p.Party.CurrentTable; p.Use = p.Table.CurrentUse;
            LogAiBoundary(p, "natural-guide-then-marketer-resumed"); ObserveAiBoundary(p);
        }
    }
    private static bool BoundaryHasPendingWork(AiBoundaryProbe p) => p.Table != null && ReferenceEquals(p.Table.CurrentUse, p.Use)
        && p.Use != null && !p.Use.ServiceAborted && !p.Use.CompletionStarted && p.Use.Orders != null
        && p.Use.Orders.Orders.Any(o => o.State == PartyFoodState.Cooking || o.State == PartyFoodState.Queued || o.State == PartyFoodState.Carrying || o.Identity.ServingReservation != null);
    private static void TickBoundaryShop(AiBoundaryProbe p)
    {
        if (p.Phase == "waiting")
        {
            p.Shop = Find<UIRestaurantAdmin>(SceneManager.GetActiveScene());
            var nav = ReadAiField(p.Shop, "_uiNav") as Muks.UI.UINavigation;
            if (nav == null || nav.Count != 0) return;
            var table = p.Tables.Where(t => t.TableType != TableType.Table1 && t.CurrentUse != null && !t.CurrentUse.IsSingle && t.PartyOrders != null
                && t.CurrentUse.Completions.Any(c => c.FinalCompleted && c.VisitCounted)
                && t.PartyOrders.Orders.Any(o => o.State == PartyFoodState.Cooking || o.State == PartyFoodState.Carrying))
                .OrderByDescending(t => t.CurrentUse.Completions.Any(c => c.FinalCompleted && c.VisitCounted)).FirstOrDefault();
            if (table == null) return;
            CaptureBoundaryTable(p, table); WatchBoundaryTable(p, table);
            p.Furniture = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, p.Floor, table.TableFurniture.Type);
            Require(p.Furniture != null && UserInfo.GetEquipFurnitureFloorType(UserInfo.CurrentStage, p.Furniture) == p.Floor, "Native using-button must resolve to the captured floor");
            p.Phase = "opening-shop"; LogAiBoundary(p, "pre-open-shop-pending-work"); nav.Push("RestaurantAdminUI"); return;
        }
        if (p.Phase == "opening-shop" && p.Shop.IsReadyForDetail)
        {
            Require(BoundaryHasPendingWork(p), "Captured order completed before furniture detail opened; target not met");
            p.Phase = "opening-furniture";
            p.Shop.FurnitureView.ShowUIFurniture(p.Floor, p.Table.TableFurniture.Type);
            Require(p.Shop.FurnitureView.TrySelectFurniture(p.Furniture.Id), "Existing equipped furniture selection failed"); return;
        }
        if (p.Phase == "opening-furniture" && p.Shop.FurnitureView.VisibleState == VisibleState.Appeared)
        {
            Require(BoundaryHasPendingWork(p), "Captured work ended before removal; target not met");
            var preview = ReadAiField(p.Shop.FurnitureView, "_uiFurniturePreview");
            var button = ReadAiField(ReadAiField(preview, "_usingButton"), "_button") as Button;
            Require(button != null && button.IsActive() && button.IsInteractable() && EventSystem.current != null, "Native equipped-furniture button must be interactive");
            p.Phase = "closing-shop"; LogAiBoundary(p, "pre-native-unequip-EventSystem");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Require(UserInfo.GetEquipFurniture(UserInfo.CurrentStage, p.Floor, p.Table.TableFurniture.Type) == null, "Native button did not unequip captured table");
            Require(BoundaryHasPendingWork(p), "No unfinished work remains immediately before shop close; target not met");
            LogAiBoundary(p, "pre-shop-Hide-pending-work"); p.Shop.Hide(); return;
        }
        if (p.Phase == "closing-shop" && p.Shop.VisibleState == VisibleState.Disappeared)
        { LogAiBoundary(p, "post-final-shop-close"); ObserveAiBoundary(p); }
    }
    private static void PrepareAiAdditionalOrderParty()
    {
        Require(ready && observingStaff && EditorApplication.isPlaying && !EditorApplication.isPaused && SessionState.GetBool(StaffAiKey, false)
            && CustomerPartyManualPlayBootstrap.Active && memory != null && !BackEnd.Backend.IsInitialized, "Memory AI observation required");
        if (!controller.TrySpawnWaitingParty(new[] { "CUSTOMER01", "CUSTOMER02" }, out var party))
        {
            status = "추가 주문 조건 팀 생성 실패: " + controller.LastVisitDiagnostic;
            WriteStaffObservation("preparation", status);
            return;
        }
        if (party == null || !party.IsActive || party.CurrentTable != null || !controller.WaitingParties.Contains(party)
            || party.Members.Count != 2 || party.Members.Any(m => m.Customer == null || m.Customer.OrderCount != 1))
        {
            status = "팀 생성 후 대기·기본 주문 횟수 전제가 달라 추가 주문 조건을 적용하지 않았습니다.";
            WriteStaffObservation("preparation", status + "; party=" + AiIdentity(party));
            return;
        }
        party.Members[0].Customer.AddOrderCount(1);
        status = "추가 주문 조건 준비: A 2회 / B 1회. 메모리 직접 준비이며 자연 스킬 발동이 아닙니다.";
        WriteStaffObservation("preparation", "additional-order-condition; direct-memory-preparation; party=" + AiIdentity(party)
            + "; A=" + AiIdentity(party.Members[0].Customer) + "/orders=" + party.Members[0].Customer.OrderCount
            + "; B=" + AiIdentity(party.Members[1].Customer) + "/orders=" + party.Members[1].Customer.OrderCount);
    }
    private static void DrawStaffAiCameraControls()
    {
        var camera = Find<CameraController>(SceneManager.GetActiveScene());
        EditorGUILayout.LabelField("카메라 관찰 보조", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(ReadAiCameraState(camera), EditorStyles.wordWrappedLabel);
        bool pointerLog = Equals(ReadAiField(camera, "_logManualPointerDiagnostics"), true);
        bool requestedPointerLog = EditorGUILayout.Toggle("실제 포인터 진단 (메모리만)", pointerLog);
        if (requestedPointerLog != pointerLog)
        {
            Require(ready && EditorApplication.isPlaying && CustomerPartyManualPlayBootstrap.Active && memory != null && !BackEnd.Backend.IsInitialized, "Memory AI session required");
            var field = typeof(CameraController).GetField("_logManualPointerDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Editor pointer diagnostic field required");
            field.SetValue(camera, requestedPointerLog); pointerDiagnosticCamera = camera;
            WriteStaffObservation("preparation", "pointer-diagnostics=" + requestedPointerLog + "; runtime logging only");
        }
        EditorGUILayout.HelpBox("포인터 진단은 실제 down/held/up 로그만 남깁니다. 입력을 생성하지 않으며 세션 종료 시 꺼집니다.", MessageType.None);
        EditorGUILayout.HelpBox("아래 버튼은 기존 CameraController.MoveCamera만 호출하는 관찰 보조 API입니다. 직원 배치·안내층·AI 판단·층 잠금은 변경하지 않습니다.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("카메라: 1층 홀")) MoveAiObservationCamera(camera, ERestaurantFloorType.Floor1, RestaurantType.Hall);
            if (GUILayout.Button("카메라: 1층 주방")) MoveAiObservationCamera(camera, ERestaurantFloorType.Floor1, RestaurantType.Kitchen);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("카메라: VIP 홀")) MoveAiObservationCamera(camera, ERestaurantFloorType.Floor2, RestaurantType.Hall);
            if (GUILayout.Button("카메라: VIP 주방")) MoveAiObservationCamera(camera, ERestaurantFloorType.Floor2, RestaurantType.Kitchen);
        }
    }
    private static string ReadAiCameraState(CameraController camera)
    {
        var navigation = ReadAiField(camera, "_navigationCoordinator") as Muks.UI.UINavigationCoordinator;
        return "floor=" + camera.CurrentFloor + "; restaurant=" + camera.CurrentRestaurant
            + "; openViewCount=" + (navigation == null ? "missing" : navigation.GetOpenViewCount().ToString())
            + "; worldInputBlocked=" + camera.IsWorldInputBlocked + "; tutorial=" + UserInfo.IsTutorialStart
            + "\nmoveAction=" + ReadAiField(camera, "_isMoveAction") + "; dragging=" + ReadAiField(camera, "_isDragging")
            + "; dragEnabled=" + ReadAiField(camera, "_isDraggingEnabled") + "; stopped=" + ReadAiField(camera, "_isStopAction");
    }
    private static void MoveAiObservationCamera(CameraController camera, ERestaurantFloorType floor, RestaurantType restaurant)
    {
        Require(ready && EditorApplication.isPlaying && !EditorApplication.isPaused && SessionState.GetBool(StaffAiKey, false)
            && CustomerPartyManualPlayBootstrap.Active && memory != null && !BackEnd.Backend.IsInitialized, "Memory AI session required");
        camera.MoveCamera(floor, restaurant);
        WriteStaffObservation("camera-observation-api", "explicit MoveCamera; floor=" + floor + "; restaurant=" + restaurant);
    }
    private static string ReadAiGuardTarget(GatecrasherCustomer target)
    {
        if (target == null) return "none";
        return AiIdentity(target) + "; event=" + AiIdentity(ReadAiField(target, "_serviceEventIdentity"))
            + "; touch=" + ReadAiField(target, "_currentTouchCount") + "/" + target.TotalTouchCount
            + "; active=" + target.isActiveAndEnabled + "; ended=" + target.IsEndEvent + "; disguised=" + target.IsInDisguise;
    }
    private static void PrepareStaffAi(bool bothFloors)
    {
        Require(ready && CustomerPartyManualPlayBootstrap.Active && memory != null && !BackEnd.Backend.IsInitialized, "Memory AI session required");
        for (int f = 0; f < 2; f++)
            foreach (var role in CustomerPartyOwnedMemory.StaffAiStaffIds.Keys)
                UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, (ERestaurantFloorType)f, role);
        foreach (var pair in CustomerPartyOwnedMemory.StaffAiStaffIds)
        {
            UserInfo.SetEquipStaff(UserInfo.CurrentStage, AiFloor, pair.Key, pair.Value[0]);
            if (bothFloors) UserInfo.SetEquipStaff(UserInfo.CurrentStage, AiFloor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1, pair.Key, pair.Value[1]);
        }
        Find<CameraController>(SceneManager.GetActiveScene()).MoveCamera(AiFloor, RestaurantType.Hall);
        if (!observingStaff)
        {
            string dir = Path.Combine(Path.GetTempPath(), "CustomerTableUseReview"); Directory.CreateDirectory(dir);
            staffEvidence = Path.Combine(dir, "staff-ai-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            observingStaff = true; staffLast.Clear(); nextStaffSample = nextStaffHeartbeat = 0;
        }
        WriteStaffObservation("preparation", "equipped=" + (bothFloors ? "both" : AiFloor.ToString()) + "; natural=" + settings.EnableNaturalParties + "; SDK=" + BackEnd.Backend.IsInitialized);
        status = "직원 AI 관찰 중. 고정팀 추가 또는 자연 호출을 기다리세요. 실제 직원 UI·상점 조작은 자동 실행하지 않습니다.";
        Debug.Log("STAFF_AI_OBSERVATION: " + staffEvidence);
    }
    private static void ChangeAiStaff(int operation)
    {
        var role = (EquipStaffType)staffRole;
        var current = UserInfo.GetEquipStaff(UserInfo.CurrentStage, AiFloor, role);
        var ids = CustomerPartyOwnedMemory.StaffAiStaffIds[role];
        string id = current == null ? ids[0] : current.Id;
        if (operation == 0 || operation == 3) UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, AiFloor, role);
        if (operation == 1) UserInfo.SetEquipStaff(UserInfo.CurrentStage, AiFloor, role, id);
        if (operation == 2)
        {
            string replacement = id == ids[0] ? ids[1] : ids[0];
            var other = AiFloor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1;
            if (UserInfo.GetEquipStaff(UserInfo.CurrentStage, other, role)?.Id == replacement)
                UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, other, role);
            UserInfo.SetEquipStaff(UserInfo.CurrentStage, AiFloor, role, replacement);
        }
        if (operation == 3) UserInfo.SetEquipStaff(UserInfo.CurrentStage, AiFloor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1, role, id);
        WriteStaffObservation("preparation", "staff-change=" + operation + "; role=" + role + "; floor=" + AiFloor + "; previous=" + id);
    }
    private static object ReadAiField(object obj, string name)
    {
        if (obj == null) return null;
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(obj);
        }
        return null;
    }
    private static int AiIdentity(object value) => value == null ? 0 : RuntimeHelpers.GetHashCode(value);
    private static void TickStaffAiObservation()
    {
        if (!observingStaff || !ready || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        TickAiBoundary(); // Armed probes observe every Editor tick; the normal log cadence is unchanged.
        if (EditorApplication.timeSinceStartup < nextStaffSample) return;
        nextStaffSample = EditorApplication.timeSinceStartup + .5;
        bool heartbeat = EditorApplication.timeSinceStartup >= nextStaffHeartbeat;
        if (heartbeat) nextStaffHeartbeat = EditorApplication.timeSinceStartup + 10;
        var scene = SceneManager.GetActiveScene();
        ObserveStaffChange("camera", ReadAiCameraState(Find<CameraController>(scene)), "", heartbeat);
        foreach (var staff in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Staff>(true)))
        {
            var action = ReadAiField(staff, "_staffAction");
            string key = "staff/" + staff.name;
            var lease = ReadAiField(action, "_lease");
            var target = ReadAiField(action, "_currentData") as TableData;
            var burner = ReadAiField(action, "_burnerData") as KitchenBurnerData;
            string value = "id=" + staff.StaffData?.Id + "; role=" + staff.EquipStaffType + "; floor=" + staff.EquipFloorType + "; level=" + staff.Level
                + "; active=" + staff.isActiveAndEnabled + "; state=" + ReadAiField(staff, "_state") + "; action=" + action?.GetType().Name + "#" + AiIdentity(action)
                + "; busy=" + ReadAiField(action, "_isUsed") + "; work=" + ReadAiField(action, "_actionType") + "; lease=" + AiIdentity(lease)
                + "; target=" + target?.TableType + "; use=" + AiIdentity(target?.CurrentUse) + "; order=" + AiIdentity(ReadAiField(action, "_order"))
                + "; burner=" + (burner == null ? "none" : AiIdentity(burner) + "/" + burner.CookingData.FoodData?.Id + "/bonus=" + burner.AddCookSpeedMul)
                + "; guard=" + ReadAiField(action, "_startAction") + "; guardTarget=" + ReadAiGuardTarget(ReadAiField(action, "_gatecrasherCustomer") as GatecrasherCustomer)
                + "; capturedGuardEvent=" + AiIdentity(ReadAiField(action, "_targetEvent"));
            if (heartbeat && action is MarketerAction) value += "; cooldown=" + ReadAiField(action, "_actionCoolTime");
            ObserveStaffChange(key, value, heartbeat ? "; pos=" + staff.transform.position.ToString("F2") : "", heartbeat);
            if (reassignOnNextTravel && staff.isActiveAndEnabled && staff.EquipFloorType == reassignFloor && staff.EquipStaffType == reassignRole
                && Equals(ReadAiField(staff, "_state"), EStaffState.Run) && Equals(ReadAiField(action, "_isUsed"), true))
            {
                reassignOnNextTravel = false;
                WriteStaffObservation("preparation", "armed-same-assignment; " + value);
                UserInfo.SetEquipStaff(UserInfo.CurrentStage, reassignFloor, reassignRole, staff.StaffData.Id);
            }
        }
        var kitchen = Find<KitchenSystem>(scene);
        for (int f = 0; f < 2; f++)
        {
            foreach (var table in tables.GetTableDataList((ERestaurantFloorType)f))
            {
                var use = table.CurrentUse; var batch = table.PartyOrders;
                string value = "state=" + table.TableState + "; use=" + AiIdentity(use) + "; people=" + use?.Party.Members.Count + "; round=" + use?.Round
                    + "; batch=" + batch?.State + "; orders=" + (batch == null ? "" : string.Join("|", batch.Orders.Select(o => AiIdentity(o) + "/seat" + o.SeatIndex + "/" + o.Food.Id + "/" + o.State)))
                    + "; abort=" + use?.ServiceAbortReason + "; cleaningBowl=" + (ReadAiField(table.TableFurniture, "_saveTableData") as SaveTableData)?.CleaningBowlProcessed;
                ObserveStaffChange("table/" + f + "/" + table.TableType, value, "", heartbeat);
            }
            var sink = kitchen.GetSinkKitchenUtensil((ERestaurantFloorType)f);
            ObserveStaffChange("sink/" + f, "bowls=" + UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, (ERestaurantFloorType)f) + "; washing=" + sink.IsStaffWashing + "; owner=" + sink.UseStaff?.StaffData?.Id, "", heartbeat);
            ObserveStaffChange("guard-target/" + f, ReadAiGuardTarget(controller.GatecrasherCustomer[f]), "", heartbeat);
        }
        ObserveStaffChange("session", "teams=" + controller.Count + "; people=" + controller.WaitingPersonCount + "; visits=" + UserInfo.TotalCumulativeCustomerCount
            + "; coins=" + UserInfo.Money + "; satisfaction=" + UserInfo.GetSatisfaction(UserInfo.CurrentStage)
            + "; callTouches=" + controller.TabCount + "; promotions=" + UserInfo.PromotionCount + "; natural=" + controller.LastNaturalPartyDiagnostic
            + "; exterminated1=" + UserInfo.TotalExterminationGatecrasherCustomer1Count
            + "; exterminated2=" + UserInfo.TotalExterminationGatecrasherCustomer2Count
            + "; exterminatedWeekly=" + UserInfo.WeeklyExterminationGatecrasherCustomerCount, "", heartbeat);
    }
    private static void ObserveStaffChange(string key, string value, string detail, bool heartbeat)
    {
        if (!staffLast.TryGetValue(key, out string previous) || previous != value || heartbeat)
        { staffLast[key] = value; WriteStaffObservation(key, value + detail); }
    }
    [Serializable] private sealed class StaffObservation { public float seconds, gameSeconds; public string key, value; }
    private static void WriteStaffObservation(string key, string value)
    {
        if (!observingStaff) return;
        File.AppendAllText(staffEvidence, JsonUtility.ToJson(new StaffObservation { seconds = Time.realtimeSinceStartup, gameSeconds = Time.time, key = key, value = value }) + Environment.NewLine);
    }
    private static void StopStaffAiObservation()
    {
        reassignOnNextTravel = false;
        if (pointerDiagnosticCamera != null)
            typeof(CameraController).GetField("_logManualPointerDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pointerDiagnosticCamera, false);
        pointerDiagnosticCamera = null;
        StopAiBoundary("session ended");
        aiBoundarySelection = aiBoundaryFloor = 0;
        if (!observingStaff) return;
        WriteStaffObservation("stop", "SDK=" + BackEnd.Backend.IsInitialized + "; memoryGame=" + memory.GameWrites + "; memoryStage=" + memory.StageWrites);
        observingStaff = false; staffLast.Clear();
    }
}
#endif
