using System.Collections.Generic;

namespace Muks.BackEnd
{
    /// <summary>일반 저장이 차단된 이유(로그/진단용).</summary>
    public enum SaveBlockReason
    {
        None,
        NotAuthenticated,
        Loading,
        NotValidated,
        RequiredRowMissing,
        InvalidPayload,
        OwnerMismatch,
        StaleSession,
        RowChanged,
        AmbiguousRows,
        InitializationNotAuthorized,
    }

    /// <summary>세션 내에서 개별 테이블이 어떤 상태로 확인되었는지.</summary>
    internal enum TableVerifyState
    {
        Unknown,
        ConfirmedAbsent,
        Verified,
        Blocked,
    }

    internal sealed class TableGuardEntry
    {
        public TableVerifyState State = TableVerifyState.Unknown;
        public string RowInDate;
        public SaveBlockReason BlockReason = SaveBlockReason.None;
    }

    /// <summary>
    /// 계정 세션·테이블별 저장 허용 상태를 관리합니다. Unity/뒤끝 SDK에 의존하지 않는 순수 C# 클래스로,
    /// 로그인 성공이나 조회 성공만으로는 저장을 허용하지 않고 실제로 검증된 테이블에 한해서만 저장을 허용합니다.
    /// "GameData" 테이블은 계정 전체의 유효성을 대표하는 필수 테이블로 취급되어, 다른 모든 테이블의 저장은
    /// GameData가 검증된 이후에만 허용됩니다.
    /// </summary>
    public sealed class AccountSaveGuard
    {
        private const string MasterTableId = "GameData";

        public int SessionGeneration { get; private set; }
        public string OwnerInDate { get; private set; }
        public bool IsNewAccountSession { get; private set; }

        /// <summary>계정 전체의 저장 가능 여부를 대표하는 GameData 테이블이 검증되었는지.</summary>
        public bool IsGameDataReady => GetState(MasterTableId) == TableVerifyState.Verified;

        private readonly Dictionary<string, TableGuardEntry> _tables = new Dictionary<string, TableGuardEntry>();

        /// <summary>로그인 성공 시 호출합니다. 새 세션 세대를 시작하고 이전 테이블 검증 상태를 모두 초기화합니다.</summary>
        public int BeginSession(string ownerInDate)
        {
            SessionGeneration++;
            OwnerInDate = ownerInDate;
            IsNewAccountSession = false;
            _tables.Clear();
            return SessionGeneration;
        }

        /// <summary>로그아웃/계정 전환 시 호출합니다. 이후 도착하는 이전 세대의 콜백은 모두 무효화됩니다.</summary>
        public void EndSession()
        {
            SessionGeneration++;
            OwnerInDate = null;
            IsNewAccountSession = false;
            _tables.Clear();
        }

        /// <summary>이번 세션이 실제 신규 가입(게스트 201, 페더레이션 신규 연동 201)임을 표시합니다.</summary>
        public void MarkNewAccountSession() => IsNewAccountSession = true;

        /// <summary>주어진 세대·소유자가 현재 세션과 정확히 일치하는지 확인합니다(계정 전환 후 늦은 콜백 차단).</summary>
        public bool IsSessionCurrent(int generation, string ownerInDate)
        {
            return generation == SessionGeneration
                && !string.IsNullOrEmpty(OwnerInDate)
                && OwnerInDate == ownerInDate;
        }

        public bool IsSessionCurrent(int generation) => generation == SessionGeneration;

        /// <summary>조회가 성공했고 rows가 0개일 때 호출합니다. 신규 가입 판단이 아니라 '정상적인 빈 결과'로만 기록합니다.</summary>
        public void MarkTableConfirmedAbsent(string tableId)
        {
            TableGuardEntry entry = GetOrCreate(tableId);
            entry.State = TableVerifyState.ConfirmedAbsent;
            entry.RowInDate = null;
            entry.BlockReason = SaveBlockReason.None;
        }

        /// <summary>필수로 존재해야 하는 데이터가 없는 경우(기존 계정의 데이터 누락)를 차단 상태로 기록합니다.</summary>
        public void MarkTableRequiredButMissing(string tableId)
        {
            TableGuardEntry entry = GetOrCreate(tableId);
            entry.State = TableVerifyState.Blocked;
            entry.RowInDate = null;
            entry.BlockReason = SaveBlockReason.RequiredRowMissing;
        }

        /// <summary>데이터 존재·소유자·파싱·적용까지 완료된 테이블만 검증 완료로 기록합니다.</summary>
        public void MarkTableVerified(string tableId, string rowInDate)
        {
            TableGuardEntry entry = GetOrCreate(tableId);
            entry.State = TableVerifyState.Verified;
            entry.RowInDate = rowInDate;
            entry.BlockReason = SaveBlockReason.None;
        }

        public void MarkTableBlocked(string tableId, SaveBlockReason reason)
        {
            TableGuardEntry entry = GetOrCreate(tableId);
            entry.State = TableVerifyState.Blocked;
            entry.RowInDate = null;
            entry.BlockReason = reason == SaveBlockReason.None ? SaveBlockReason.NotValidated : reason;
        }

        public SaveBlockReason GetBlockReason(string tableId)
        {
            return _tables.TryGetValue(tableId, out TableGuardEntry e) ? e.BlockReason : SaveBlockReason.NotValidated;
        }

        /// <summary>일반 저장(Update) 대상 row를 확인합니다. 검증된 테이블에 한해서만 허용합니다.</summary>
        public bool CanUpdate(string tableId, out string verifiedRowInDate, out SaveBlockReason blockReason)
        {
            if (tableId != MasterTableId && !IsGameDataReady)
            {
                verifiedRowInDate = null;
                blockReason = SaveBlockReason.NotValidated;
                return false;
            }

            if (_tables.TryGetValue(tableId, out TableGuardEntry e) && e.State == TableVerifyState.Verified)
            {
                verifiedRowInDate = e.RowInDate;
                blockReason = SaveBlockReason.None;
                return true;
            }

            verifiedRowInDate = null;
            blockReason = e != null && e.BlockReason != SaveBlockReason.None ? e.BlockReason : SaveBlockReason.NotValidated;
            return false;
        }

        /// <summary>
        /// 신규 삽입(Insert)이 허용되는지 확인합니다. GameData는 신규 가입 세션에서만,
        /// 다른 테이블은 이번 세션에서 정상적으로 빈 결과를 확인한 경우에만 허용합니다.
        /// </summary>
        public bool CanInsert(string tableId, out SaveBlockReason blockReason)
        {
            if (tableId != MasterTableId && !IsGameDataReady)
            {
                blockReason = SaveBlockReason.NotValidated;
                return false;
            }

            if (tableId == MasterTableId)
            {
                if (IsNewAccountSession && GetState(tableId) != TableVerifyState.Verified)
                {
                    blockReason = SaveBlockReason.None;
                    return true;
                }

                blockReason = GetBlockReason(tableId);
                if (blockReason == SaveBlockReason.None)
                    blockReason = SaveBlockReason.InitializationNotAuthorized;
                return false;
            }

            if (GetState(tableId) == TableVerifyState.ConfirmedAbsent)
            {
                blockReason = SaveBlockReason.None;
                return true;
            }

            blockReason = GetBlockReason(tableId);
            if (blockReason == SaveBlockReason.None)
                blockReason = SaveBlockReason.InitializationNotAuthorized;
            return false;
        }

        public void Reset()
        {
            SessionGeneration = 0;
            OwnerInDate = null;
            IsNewAccountSession = false;
            _tables.Clear();
        }

        internal TableVerifyState GetState(string tableId)
        {
            return _tables.TryGetValue(tableId, out TableGuardEntry e) ? e.State : TableVerifyState.Unknown;
        }

        private TableGuardEntry GetOrCreate(string tableId)
        {
            if (!_tables.TryGetValue(tableId, out TableGuardEntry e))
            {
                e = new TableGuardEntry();
                _tables[tableId] = e;
            }
            return e;
        }
    }
}
