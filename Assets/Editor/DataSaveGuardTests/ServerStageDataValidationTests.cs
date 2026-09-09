using LitJson;
using NUnit.Framework;

namespace DataSaveGuardTests
{
    /// <summary>DATA-SAVE-GUARD-01: ServerStageData 파싱 검증(누락 vs 손상 구분, 구버전 호환 보존) EditMode 테스트.</summary>
    public class ServerStageDataValidationTests
    {
        [Test]
        public void 서버행이없으면_SetData는아무것도하지않고IsValid는기본값거짓이다()
        {
            var data = new ServerStageData();
            data.SetData(JsonMapper.ToObject("[]"));
            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 정상데이터는_IsValid참이고값이반영된다()
        {
            JsonData json = JsonMapper.ToObject(
                "[{\"UnlockFloor\":1,\"Score\":100,\"Tip\":5,\"Satisfaction\":0.5,\"FeverGauge\":0.2," +
                "\"GiveStaffList\":[{\"Id\":\"STAFF01\",\"Level\":3}]}]");

            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(100, data.Score);
            Assert.AreEqual(5, data.Tip);
            Assert.AreEqual(1, data.GiveStaffList.Count);
            Assert.AreEqual("STAFF01", data.GiveStaffList[0].Id);
            Assert.AreEqual(3, data.GiveStaffList[0].Level);
        }

        [Test]
        public void Score값이손상되면_0으로대체하지않고검증실패로처리한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"Score\":\"잘못된값\"}]");
            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 필드가아예없으면_구버전호환으로기본값을사용한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"Tip\":3}]");
            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(0, data.Score);
        }

        [Test]
        public void 실제로저장된0은_정상값으로처리된다()
        {
            JsonData json = JsonMapper.ToObject("[{\"Score\":0,\"Tip\":0}]");
            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(0, data.Score);
            Assert.AreEqual(0, data.Tip);
        }

        [Test]
        public void 구버전EquipStaffDataList는_딕셔너리로마이그레이션되어검증을통과한다()
        {
            // 구버전: Floor0(Manager=STAFF_A, Marketer="", Waiter=STAFF_B, ...) 순서 리스트
            JsonData json = JsonMapper.ToObject(
                "[{\"EquipStaffDataList\":[[\"STAFF_A\",\"\",\"STAFF_B\"]]}]");

            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual("STAFF_A", data.EquipStaffDataDic["Floor1"]["Manager"]);
            Assert.AreEqual("STAFF_B", data.EquipStaffDataDic["Floor1"]["Waiter"]);
        }

        [Test]
        public void 신규EquipStaffDataDic형식은_그대로검증을통과한다()
        {
            JsonData json = JsonMapper.ToObject(
                "[{\"EquipStaffDataDic\":{\"Floor1\":{\"Manager\":\"STAFF_A\"}}}]");

            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual("STAFF_A", data.EquipStaffDataDic["Floor1"]["Manager"]);
        }

        [Test]
        public void GiveStaffList항목에Id가없으면_해당항목을조용히버리지않고검증실패로처리한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"GiveStaffList\":[{\"Level\":3}]}]");

            var data = new ServerStageData();
            data.SetData(json);

            Assert.IsFalse(data.IsValid);
        }
    }
}
