namespace Muks.BackEnd
{
    public enum SaveDecisionAction
    {
        Block,
        Update,
        Insert,
    }

    /// <summary>SDK 응답 없이 순수 값만으로 표현되는 저장 판단 결과.</summary>
    public readonly struct SaveDecision
    {
        public SaveDecisionAction Action { get; }
        /// <summary>Action이 Update일 때 실제로 사용해야 하는 검증된 row inDate.</summary>
        public string TargetInDate { get; }
        public SaveBlockReason BlockReason { get; }

        private SaveDecision(SaveDecisionAction action, string targetInDate, SaveBlockReason reason)
        {
            Action = action;
            TargetInDate = targetInDate;
            BlockReason = reason;
        }

        public static SaveDecision Block(SaveBlockReason reason) => new SaveDecision(SaveDecisionAction.Block, null, reason);
        public static SaveDecision Update(string targetInDate) => new SaveDecision(SaveDecisionAction.Update, targetInDate, SaveBlockReason.None);
        public static SaveDecision Insert() => new SaveDecision(SaveDecisionAction.Insert, null, SaveBlockReason.None);
    }

    /// <summary>
    /// 조회 결과(행 개수·inDate)와 AccountSaveGuard 상태만으로 실제 쓰기 동작(Update/Insert/Block)을
    /// 결정하는 단일 판단 지점입니다. BackendManager의 저장 경로는 반드시 이 결정을 통해서만 분기해야 하며,
    /// "행이 1개 조회됐다"는 사실만으로 CanUpdate를 우회해 Update를 호출해서는 안 됩니다.
    /// SDK 타입에 의존하지 않아 Unity/뒤끝 없이도 단위 테스트가 가능합니다.
    /// </summary>
    public static class GameDataSaveDecider
    {
        public static SaveDecision Decide(AccountSaveGuard guard, string tableId, int fetchedRowCount, string fetchedInDate)
        {
            bool canUpdate = guard.CanUpdate(tableId, out string verifiedRowInDate, out SaveBlockReason updateReason);
            if (canUpdate)
            {
                if (fetchedRowCount == 0)
                    return SaveDecision.Block(SaveBlockReason.RowChanged);
                if (fetchedRowCount > 1)
                    return SaveDecision.Block(SaveBlockReason.AmbiguousRows);
                if (fetchedInDate != verifiedRowInDate)
                    return SaveDecision.Block(SaveBlockReason.RowChanged);

                return SaveDecision.Update(verifiedRowInDate);
            }

            bool canInsert = guard.CanInsert(tableId, out SaveBlockReason insertReason);
            if (canInsert)
            {
                if (fetchedRowCount != 0)
                    return SaveDecision.Block(SaveBlockReason.AmbiguousRows);

                return SaveDecision.Insert();
            }

            SaveBlockReason reason = updateReason != SaveBlockReason.None ? updateReason : insertReason;
            return SaveDecision.Block(reason);
        }
    }
}
