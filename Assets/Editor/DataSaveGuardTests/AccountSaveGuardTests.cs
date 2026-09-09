using Muks.BackEnd;
using NUnit.Framework;

namespace DataSaveGuardTests
{
    /// <summary>DATA-SAVE-GUARD-01: AccountSaveGuard 상태 전이 EditMode 테스트 (Unity/뒤끝 SDK 의존 없음).</summary>
    public class AccountSaveGuardTests
    {
        [Test]
        public void 초기상태_모든저장차단()
        {
            var guard = new AccountSaveGuard();
            Assert.IsFalse(guard.CanUpdate("GameData", out _, out _));
            Assert.IsFalse(guard.CanInsert("GameData", out _));
        }

        [Test]
        public void 로그인성공만으로는_저장이허용되지않는다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");

            // 로그인/조회 성공만 있었을 뿐 검증된 테이블이 없으므로 저장 불가
            Assert.IsFalse(guard.CanUpdate("GameData", out _, out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.NotValidated, reason);
            Assert.IsFalse(guard.CanInsert("GameData", out _));
        }

        [Test]
        public void 기존계정_GameData조회성공후_빈행이면_자동삽입금지()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1"); // 신규가입 아님(기존 계정 로그인)

            // GameData 조회 200 + rows=0 (기존 계정) → 신규 생성 금지
            guard.MarkTableRequiredButMissing("GameData");

            Assert.IsFalse(guard.CanInsert("GameData", out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.RequiredRowMissing, reason);
            Assert.IsFalse(guard.CanUpdate("GameData", out _, out _));
        }

        [Test]
        public void 신규가입_GameData없음이면_최초삽입허용_이후검증되면삽입권한소모()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-new");
            guard.MarkNewAccountSession();
            guard.MarkTableConfirmedAbsent("GameData");

            Assert.IsTrue(guard.CanInsert("GameData", out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.None, reason);

            // 삽입 성공 후 검증 완료로 기록
            guard.MarkTableVerified("GameData", "indate-1");

            // 검증 완료 이후에는 일반 저장은 Update만 허용, Insert는 다시 불가(1회성 소모)
            Assert.IsTrue(guard.CanUpdate("GameData", out string rowInDate, out _));
            Assert.AreEqual("indate-1", rowInDate);
            Assert.IsFalse(guard.CanInsert("GameData", out _));
        }

        [Test]
        public void GameData검증되지않으면_다른테이블도저장불가()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            // Stage1Data 등 다른 테이블이 우연히 확인되어도 GameData가 준비되지 않으면 저장 불가
            guard.MarkTableConfirmedAbsent("Stage1Data");

            Assert.IsFalse(guard.CanInsert("Stage1Data", out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.NotValidated, reason);
        }

        [Test]
        public void GameData검증완료후_다른테이블은_빈결과일때만_최초삽입허용()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "gd-indate");

            // 결제 이력 등은 신규 계정이 아니어도 정상적으로 처음 생성될 수 있음
            guard.MarkTableConfirmedAbsent("PaymentData");
            Assert.IsTrue(guard.CanInsert("PaymentData", out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.None, reason);
        }

        [Test]
        public void 손상된데이터는_검증완료로표시되지않고_저장이차단된다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableBlocked("GameData", SaveBlockReason.InvalidPayload);

            Assert.IsFalse(guard.CanUpdate("GameData", out _, out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.InvalidPayload, reason);
            Assert.IsFalse(guard.CanInsert("GameData", out _));
        }

        [Test]
        public void 복수행이나행이바뀐경우_저장이차단된다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("owner-1");
            guard.MarkTableVerified("GameData", "gd-indate");
            guard.MarkTableBlocked("GameData", SaveBlockReason.AmbiguousRows);

            Assert.IsFalse(guard.CanUpdate("GameData", out _, out SaveBlockReason reason));
            Assert.AreEqual(SaveBlockReason.AmbiguousRows, reason);
        }

        [Test]
        public void 로그아웃후_이전세션의늦은콜백은_현재세션과다르다고판단된다()
        {
            var guard = new AccountSaveGuard();
            int genA = guard.BeginSession("account-A");
            guard.MarkTableVerified("GameData", "a-indate");

            guard.EndSession();
            int genB = guard.BeginSession("account-B");
            guard.MarkTableVerified("GameData", "b-indate");

            // A 계정 시절 캡처했던 세대/오너로는 더 이상 현재 세션이 아님(늦은 콜백 차단)
            Assert.IsFalse(guard.IsSessionCurrent(genA, "account-A"));
            Assert.IsTrue(guard.IsSessionCurrent(genB, "account-B"));
            Assert.AreNotEqual(genA, genB);
        }

        [Test]
        public void 계정전환후_이전세대콜백을_소유자까지비교해서걸러낸다()
        {
            var guard = new AccountSaveGuard();
            int genA = guard.BeginSession("account-A");
            guard.EndSession();
            int genB = guard.BeginSession("account-B");

            // 세대 번호만 우연히 같아지는 극단적 상황에서도 오너가 다르면 현재 세션이 아님
            Assert.IsFalse(guard.IsSessionCurrent(genA, "account-A"));
            Assert.IsTrue(guard.IsSessionCurrent(genB, "account-B"));
        }

        [Test]
        public void 새세션시작시_이전세션의테이블검증상태는모두초기화된다()
        {
            var guard = new AccountSaveGuard();
            guard.BeginSession("account-A");
            guard.MarkTableVerified("GameData", "a-indate");
            guard.MarkTableVerified("Stage1Data", "a-stage-indate");

            guard.BeginSession("account-B");

            Assert.IsFalse(guard.CanUpdate("GameData", out _, out _));
            Assert.IsFalse(guard.CanUpdate("Stage1Data", out _, out _));
        }
    }
}
