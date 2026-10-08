using System;
using System.Globalization;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public sealed class AttendanceMemory
    {
        public readonly string Last;
        public readonly int Days, Diamonds;
        public readonly long Money, Total, Daily, Weekly;
        public AttendanceMemory(string last, int days, int diamonds, long money, long total, long daily, long weekly)
        { Last = last; Days = days; Diamonds = diamonds; Money = money; Total = total; Daily = daily; Weekly = weekly; }
        public bool SameAttendance(AttendanceMemory other) => other != null && Last == other.Last && Days == other.Days;
        public bool CanAdd(MoneyType type, int amount)
        {
            try
            {
                checked
                {
                    if (amount <= 0) return false;
                    if (type == MoneyType.Dia) { _ = Diamonds + amount; return Diamonds >= 0; }
                    if (type != MoneyType.Gold) return false;
                    _ = Money + amount; _ = Total + amount; _ = Daily + amount; _ = Weekly + amount;
                }
                return true;
            }
            catch (OverflowException) { return false; }
        }
        public Param Values()
        {
            var p = new Param(); p.Add("LastAttendanceTime", Last ?? ""); p.Add("TotalAttendanceDays", Days);
            p.Add("Dia", Diamonds); p.Add("Money", Money); p.Add("TotalAddMoney", Total);
            p.Add("DailyAddMoney", Daily); p.Add("WeeklyAddMoney", Weekly); return p;
        }
    }

    public interface IAttendanceState
    {
        AttendanceMemory Read();
        bool TryCommit(AttendanceClaim claim, out string error);
        void Notify(MoneyType type);
    }

    public static class AttendanceProgress
    {
        public static DateTime GameDay(DateTime koreaTime) => koreaTime.AddHours(-12).Date;
        public static int Today(AttendanceMemory state, DateTime now, out bool due)
        {
            due = true;
            if (DateTime.TryParse(state.Last, out var last))
            {
                var difference = (GameDay(now) - GameDay(last)).TotalDays;
                due = difference > 0;
                if (!due) return Math.Max(1, state.Days);
                if (difference > 1) return 1;
            }
            return Math.Max(1, state.Days + 1);
        }
        // Seven card centers span six intervals. A confirmed claim advances one position.
        public static float Fill(int day, bool claimed) => UnityEngine.Mathf.Clamp01(((Math.Max(1, day) - 1) % 7 + (claimed ? 1 : 0)) / 6f);
    }

    public sealed class AttendanceClaim
    {
        internal readonly BackendManager Owner;
        internal readonly GameDataSaveCoordinator Coordinator;
        internal readonly IAttendanceState State;
        internal readonly GameDataRestoreQuery Query;
        internal GameDataSavePayload Payload;
        public readonly GameDataSaveIdentity Identity;
        public readonly AttendanceMemory Before;
        public readonly MoneyType Type;
        public readonly int Amount, Day;
        public readonly string Time;
        public bool Committed { get; private set; }
        public bool RejectedBeforeSend { get; internal set; }
        public string Error { get; internal set; }
        public bool IsCurrent => Owner.IsCurrentAttendance(this);
        public bool Pending => !Committed && !RejectedBeforeSend && IsCurrent;
        internal AttendanceClaim(BackendManager owner, GameDataSaveCoordinator coordinator, IAttendanceState state,
            GameDataRestoreQuery query, GameDataSaveTarget target, AttendanceMemory before, AttendanceData reward, int day, bool doubled, DateTime now)
        {
            Owner = owner; Coordinator = coordinator; State = state; Query = query; Before = before;
            Identity = new GameDataSaveIdentity("attendance:" + Guid.NewGuid().ToString("N"), target);
            Type = reward.MoneyType; Amount = checked(reward.RewardValue * (doubled ? 2 : 1)); Day = day;
            Time = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        private bool ValidSource() => IsCurrent && Before.SameAttendance(State.Read()) && State.Read().CanAdd(Type, Amount);
        internal Param CreateValues()
        {
            if (!ValidSource()) throw new InvalidOperationException("출석 수령 상태가 변경되었습니다.");
            var current = State.Read();
            var values = new Param(); values.Add("LastAttendanceTime", Time); values.Add("TotalAttendanceDays", Day);
            if (Type == MoneyType.Dia) values.Add("Dia", checked(current.Diamonds + Amount));
            else
            {
                values.Add("Money", checked(current.Money + Amount)); values.Add("TotalAddMoney", checked(current.Total + Amount));
                values.Add("DailyAddMoney", checked(current.Daily + Amount)); values.Add("WeeklyAddMoney", checked(current.Weekly + Amount));
            }
            if (!GameDataSavePayload.TryCapture(values, out Payload, out string error)) throw new InvalidOperationException(error);
            return values;
        }
        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
            => !Committed && !RejectedBeforeSend && Identity.Matches(identity) && Payload != null && Payload.Json == payload.Json &&
               Coordinator.State == GameDataSaveCoordinatorState.Sending && ValidSource() ? null : "현재 출석 요청의 저장 권한이 아닙니다.";
        internal void Confirm(GameDataSaveReceipt receipt)
        {
            if (Committed) return;
            if (!ValidSource() || Coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState ||
                !ReferenceEquals(Coordinator.LastReceipt, receipt) || !Identity.Matches(receipt.Identity) || !receipt.SendStarted ||
                receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || receipt.Payload?.Json != Payload?.Json)
                throw new InvalidOperationException("출석 저장 확정 근거가 일치하지 않습니다.");
            if (!State.TryCommit(this, out string error)) throw new InvalidOperationException(error);
            Committed = true;
            try { State.Notify(Type); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[Attendance] Observer: " + ex.GetType().Name); }
            Owner.NotifyAttendanceChanged();
            Owner.RequestGameDataAutosave(requireGameplay: false);
        }
    }

    public partial class BackendManager
    {
        private IAttendanceState _attendanceState;
        private Func<DateTime> _attendanceNowKst;
        private Func<int, AttendanceData> _attendanceReward;
        private AttendanceClaim _attendanceClaim;
        public event Action AttendanceChanged;
        public AttendanceClaim CurrentAttendanceClaim => _attendanceClaim;
        public GameDataRestoreQuery CurrentAttendanceQuery => GameDataRestore.LegacyQuery;
        private IAttendanceState AttendanceState => _attendanceState ?? (IsOfflineOwner
            ? throw new InvalidOperationException("Offline attendance state must be explicitly injected.")
            : (_attendanceState = new UserAttendanceState()));
        internal bool IsCurrentAttendance(AttendanceClaim claim) => claim != null && ReferenceEquals(_attendanceClaim, claim) &&
            IsCurrentGameDataSaveSession(claim.Query, claim.Identity.Target);
        public bool CanClaimAttendance(out int day, out string error)
        {
            error = null; day = 0;
            var coordinator = GetGameDataSaveCoordinator();
            if (coordinator == null || !coordinator.CanStartPurchase || !GameDataTransport.GameplaySaveAllowed || _attendanceClaim?.Pending == true)
            { error = "진행 중인 저장 또는 출석 수령 결과를 확인하고 있습니다."; return false; }
            day = AttendanceProgress.Today(AttendanceState.Read(), AttendanceNow(), out bool due);
            if (!due) { error = "오늘 출석 보상을 이미 받았습니다."; return false; }
            if (ReadAttendanceReward(day) == null) { error = "수령할 출석 보상이 없습니다."; return false; }
            return true;
        }
        private DateTime AttendanceNow() => _attendanceNowKst != null ? _attendanceNowKst() : UserInfo.GetKoreanTime();
        private AttendanceData ReadAttendanceReward(int day) => _attendanceReward != null ? _attendanceReward(day) :
            AttendanceDataManager.Instance.GetRewardDic().TryGetValue(day, out var reward) ? reward : null;
        public bool TryClaimAttendance(bool doubled, out AttendanceClaim claim, out string error)
        {
            claim = null;
            if (!CanClaimAttendance(out int day, out error)) return false;
            var coordinator = GetGameDataSaveCoordinator();
            try
            {
                var before = AttendanceState.Read();
                var now = AttendanceNow();
                day = AttendanceProgress.Today(before, now, out bool due);
                var reward = ReadAttendanceReward(day);
                if (!due || reward == null) { error = "출석 수령 일차가 변경되었습니다."; return false; }
                claim = new AttendanceClaim(this, coordinator, AttendanceState, GameDataRestore.LegacyQuery,
                    GameDataRestore.LegacyTarget, before, reward, day, doubled, now);
                _attendanceClaim = claim;
                var current = claim;
                bool accepted = coordinator.TryStartPurchase(claim.Identity, claim.CreateValues, claim.Confirm, out error, request =>
                {
                    current.Error = request.Error;
                    current.RejectedBeforeSend = request.Status == GameDataSaveRequestStatus.RejectedBeforeSend;
                    NotifyAttendanceChanged();
                });
                if (!accepted) { claim.RejectedBeforeSend = true; claim.Error = error; }
                NotifyAttendanceChanged();
                return accepted;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
        internal void NotifyAttendanceChanged()
        {
            var handlers = AttendanceChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[Attendance] UI: " + ex.GetType().Name); }
        }
        private string ValidateAttendanceSave(GameDataSavePayload payload)
        {
            if (_attendanceClaim == null || !_attendanceClaim.IsCurrent || !_attendanceClaim.Committed) return null;
            var fields = JObject.Parse(payload.Json);
            var current = JObject.Parse(AttendanceState.Read().Values().GetJson());
            foreach (var field in current.Properties())
                if (fields[field.Name] != null && !JToken.DeepEquals(fields[field.Name], field.Value))
                    return "이전 출석·재화 상태를 덮어쓸 수 없습니다.";
            return null;
        }
    }
}
