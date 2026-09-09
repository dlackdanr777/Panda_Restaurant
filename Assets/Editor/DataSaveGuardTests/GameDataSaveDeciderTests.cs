using Muks.BackEnd;
using NUnit.Framework;

namespace DataSaveGuardTests
{
    /// <summary>
    /// DATA-SAVE-GUARD-01-R1: BackendManager.SaveGameDataAsync/SaveGameData가 실제로 호출하는
    /// GameDataSaveDecider.Decide()를 검증합니다. "행이 1개 조회됐다"는 사실만으로 CanUpdate를
    /// 우회해 Update를 결정하지 않는지가 핵심(R1 최우선 결함) 회귀 테스트입니다.
    /// </summary>
    public class GameDataSaveDeciderTests
    {
        [Test]
        public void R1시나리오A_GameData가Blocked면_행이1개조회되어도Update를결정하지않는다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableBlocked("GameData", SaveBlockReason.InvalidPayload);

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 1, fetchedInDate: "row-1");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.InvalidPayload, decision.BlockReason);
        }

        [Test]
        public void R1시나리오B_GameData정상이어도Stage가Blocked면_행이1개조회되어도Update를결정하지않는다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "gd-indate");
            guard.MarkTableBlocked("Stage2Data", SaveBlockReason.InvalidPayload);

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "Stage2Data", fetchedRowCount: 1, fetchedInDate: "row-1");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
        }

        [Test]
        public void 검증된행과조회된행의inDate가다르면_ROW_CHANGED로차단한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "row-A");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 1, fetchedInDate: "row-B");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.RowChanged, decision.BlockReason);
        }

        [Test]
        public void 검증된테이블인데조회결과행이사라지면_차단하고자동삽입하지않는다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "row-A");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 0, fetchedInDate: null);

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.RowChanged, decision.BlockReason);
        }

        [Test]
        public void 검증된테이블에서조회행이복수면_첫행을적용하지않고차단한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "row-A");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 2, fetchedInDate: "row-A");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.AmbiguousRows, decision.BlockReason);
        }

        [Test]
        public void 검증된행과조회된행이일치하면_해당inDate로Update를결정한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "row-A");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 1, fetchedInDate: "row-A");

            Assert.AreEqual(SaveDecisionAction.Update, decision.Action);
            Assert.AreEqual("row-A", decision.TargetInDate);
        }

        [Test]
        public void 신규가입계정은GameData가없을때Insert를결정한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-new");
            guard.MarkNewAccountSession();
            guard.MarkTableConfirmedAbsent("GameData");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 0, fetchedInDate: null);

            Assert.AreEqual(SaveDecisionAction.Insert, decision.Action);
        }

        [Test]
        public void 기존계정은GameData가없을때Insert를결정하지않고차단한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1"); // 신규가입 아님
            guard.MarkTableRequiredButMissing("GameData");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 0, fetchedInDate: null);

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.RequiredRowMissing, decision.BlockReason);
        }

        [Test]
        public void 신규계정이라도재확인시행이이미존재하면_삽입하지않고차단한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-new");
            guard.MarkNewAccountSession();
            guard.MarkTableConfirmedAbsent("GameData");

            // 삽입 직전 재확인에서 뜻밖에 행이 이미 존재(동시 삽입 등) - 임의로 업데이트하지 않고 차단
            SaveDecision decision = GameDataSaveDecider.Decide(guard, "GameData", fetchedRowCount: 1, fetchedInDate: "row-X");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
            Assert.AreEqual(SaveBlockReason.AmbiguousRows, decision.BlockReason);
        }

        [Test]
        public void GameData가Ready여도확인되지않은다른테이블은삽입도차단한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "gd-indate");
            // Stage2Data는 이번 세션에서 아직 한 번도 로드되지 않은 Unknown 상태

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "Stage2Data", fetchedRowCount: 1, fetchedInDate: "row-1");

            Assert.AreEqual(SaveDecisionAction.Block, decision.Action);
        }

        [Test]
        public void 결제이력처럼확인된빈결과테이블은신규가입아니어도삽입을결정한다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1"); // 기존 계정
            guard.MarkTableVerified("GameData", "gd-indate");
            guard.MarkTableConfirmedAbsent("PaymentData");

            SaveDecision decision = GameDataSaveDecider.Decide(guard, "PaymentData", fetchedRowCount: 0, fetchedInDate: null);

            Assert.AreEqual(SaveDecisionAction.Insert, decision.Action);
        }
    }
}
