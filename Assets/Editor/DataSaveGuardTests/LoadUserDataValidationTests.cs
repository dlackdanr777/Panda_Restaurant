using LitJson;
using NUnit.Framework;

namespace DataSaveGuardTests
{
    /// <summary>
    /// DATA-SAVE-GUARD-01: LoadUserData 파싱 검증(필수 vs 확인된 구버전 선택 필드 구분) EditMode 테스트.
    /// 필수 필드 목록은 git 이력(a6af9c7, 최초 LoadData.cs)에서 처음부터 존재가 확인된 필드만 사용합니다.
    /// </summary>
    public class LoadUserDataValidationTests
    {
        // 모든 필수 필드를 채운 기본 유효 행. 개별 테스트는 필요한 부분만 문자열 치환으로 변형합니다.
        private const string ValidRowJson =
            "[{\"IsFirstTutorialClear\":true,\"IsMiniGameTutorialClear\":true," +
            "\"IsGatecrasher1TutorialClear\":true,\"IsGatecrasher2TutorialClear\":true," +
            "\"IsSpecialCustomer1TutorialClear\":true,\"IsSpecialCustomer2TutorialClear\":true," +
            "\"Money\":500,\"TotalAddMoney\":100,\"DailyAddMoney\":10,\"Score\":100," +
            "\"TotalCookCount\":1,\"DailyCookCount\":1,\"TotalCumulativeCustomerCount\":1," +
            "\"DailyCumulativeCustomerCount\":1,\"PromotionCount\":1,\"TotalAdvertisingViewCount\":1," +
            "\"DailyAdvertisingViewCount\":1,\"TotalCleanCount\":1,\"DailyCleanCount\":1}]";

        [Test]
        public void 서버행이없으면_IsValid는거짓이다()
        {
            JsonData json = JsonMapper.ToObject("[]");
            var data = new LoadUserData(json);
            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 모든필수필드가있는정상데이터는_IsValid참이고값이반영된다()
        {
            JsonData json = JsonMapper.ToObject(ValidRowJson);
            var data = new LoadUserData(json);

            Assert.IsTrue(data.IsValid, data.FailReason);
            Assert.IsTrue(data.IsFirstTutorialClear);
            Assert.AreEqual(500, data.Money);
            Assert.AreEqual(100, data.Score);
        }

        [Test]
        public void 실제로저장된0은_정상값으로처리된다()
        {
            string json = ValidRowJson.Replace("\"Money\":500", "\"Money\":0").Replace("\"Score\":100", "\"Score\":0");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsTrue(data.IsValid, data.FailReason);
            Assert.AreEqual(0, data.Money);
            Assert.AreEqual(0, data.Score);
        }

        [Test]
        public void 필수Money필드자체가없으면_기본값0으로저장하지않고검증실패로처리한다()
        {
            // "Money" 키 자체를 제거 - 구버전 호환이 아니라 데이터 누락(손상)으로 취급되어야 함
            string json = ValidRowJson.Replace("\"Money\":500,", "");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void Money값이문자열로손상되면_검증실패로처리한다()
        {
            string json = ValidRowJson.Replace("\"Money\":500", "\"Money\":\"잘못된값\"");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 확인된구버전선택필드Dia가없으면_기본값0을사용하고검증은통과한다()
        {
            // Dia는 git 이력상 최초 스키마(a6af9c7)에 없었고 이후 버전에서 추가된 것으로 확인된 필드입니다.
            var data = new LoadUserData(JsonMapper.ToObject(ValidRowJson));

            Assert.IsTrue(data.IsValid, data.FailReason);
            Assert.AreEqual(0, data.Dia);
        }

        [Test]
        public void 레시피목록항목이손상되면_빈목록으로대체하지않고검증실패로처리한다()
        {
            // Level 필드 누락(항목 자체가 손상)
            string json = ValidRowJson.Replace("}]", ",\"GiveRecipeList\":[{\"Id\":\"R1\"}]}]");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 고객보유목록항목이손상되면_검증실패로처리한다()
        {
            string json = ValidRowJson.Replace("}]", ",\"EnabledCustomerDataList\":[{\"Id\":\"C1\"}]}]");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsFalse(data.IsValid);
        }

        [Test]
        public void 목록필드자체가없으면_정상빈목록으로간주한다()
        {
            var data = new LoadUserData(JsonMapper.ToObject(ValidRowJson));

            Assert.IsTrue(data.IsValid, data.FailReason);
            Assert.AreEqual(0, data.GiveRecipeLevelDic.Count);
            Assert.AreEqual(0, data.EnabledCustomerDataDic.Count);
        }

        [Test]
        public void IsFirstTutorialClear값이true도false도아니면_검증실패로처리한다()
        {
            string json = ValidRowJson.Replace("\"IsFirstTutorialClear\":true", "\"IsFirstTutorialClear\":\"banana\"");
            var data = new LoadUserData(JsonMapper.ToObject(json));

            Assert.IsFalse(data.IsValid);
        }
    }
}

