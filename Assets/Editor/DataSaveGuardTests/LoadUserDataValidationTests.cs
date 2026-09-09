using LitJson;
using NUnit.Framework;

namespace DataSaveGuardTests
{
    /// <summary>DATA-SAVE-GUARD-01: LoadUserData 파싱 검증(누락 vs 손상 구분) EditMode 테스트.</summary>
    public class LoadUserDataValidationTests
    {
        [Test]
        public void 서버행이없으면_IsValid는거짓이다()
        {
            JsonData json = JsonMapper.ToObject("[]");
            var data = new LoadUserData(json);
            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 정상데이터는_IsValid참이고값이반영된다()
        {
            JsonData json = JsonMapper.ToObject(
                "[{\"IsFirstTutorialClear\":true,\"Dia\":10,\"Money\":500," +
                "\"GiveRecipeList\":[{\"Id\":\"R1\",\"Level\":2}]," +
                "\"EnabledCustomerDataList\":[{\"Id\":\"C1\",\"VisitCount\":3}]}]");

            var data = new LoadUserData(json);

            Assert.IsTrue(data.IsValid);
            Assert.IsTrue(data.IsFirstTutorialClear);
            Assert.AreEqual(10, data.Dia);
            Assert.AreEqual(500, data.Money);
            Assert.AreEqual(2, data.GiveRecipeLevelDic["R1"]);
            Assert.AreEqual(3, data.EnabledCustomerDataDic["C1"].VisitCount);
        }

        [Test]
        public void 실제로저장된0은_정상값으로처리된다()
        {
            JsonData json = JsonMapper.ToObject("[{\"Money\":0,\"Dia\":0}]");
            var data = new LoadUserData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(0, data.Money);
            Assert.AreEqual(0, data.Dia);
        }

        [Test]
        public void 필드가아예없으면_구버전호환으로기본값을사용하고검증은통과한다()
        {
            // Money/Dia 필드 자체가 없는 구버전 저장 포맷을 가정
            JsonData json = JsonMapper.ToObject("[{\"IsFirstTutorialClear\":true}]");
            var data = new LoadUserData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(0, data.Money);
            Assert.AreEqual(0, data.Dia);
        }

        [Test]
        public void Money값이문자열로손상되면_0으로저장하지않고검증실패로처리한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"Money\":\"잘못된값\"}]");
            var data = new LoadUserData(json);

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 레시피목록항목이손상되면_빈목록으로대체하지않고검증실패로처리한다()
        {
            // Level 필드 누락(항목 자체가 손상)
            JsonData json = JsonMapper.ToObject("[{\"GiveRecipeList\":[{\"Id\":\"R1\"}]}]");
            var data = new LoadUserData(json);

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 고객보유목록항목이손상되면_검증실패로처리한다()
        {
            // VisitCount 필드 누락
            JsonData json = JsonMapper.ToObject("[{\"EnabledCustomerDataList\":[{\"Id\":\"C1\"}]}]");
            var data = new LoadUserData(json);

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 목록필드자체가없으면_정상빈목록으로간주한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"IsFirstTutorialClear\":true}]");
            var data = new LoadUserData(json);

            Assert.IsTrue(data.IsValid);
            Assert.AreEqual(0, data.GiveRecipeLevelDic.Count);
            Assert.AreEqual(0, data.EnabledCustomerDataDic.Count);
        }

        [Test]
        public void IsFirstTutorialClear값이true도false도아니면_검증실패로처리한다()
        {
            JsonData json = JsonMapper.ToObject("[{\"IsFirstTutorialClear\":\"banana\"}]");
            var data = new LoadUserData(json);

            Assert.IsFalse(data.IsValid);
        }
    }
}
