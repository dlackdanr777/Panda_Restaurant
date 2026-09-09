using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using Muks.BackEnd;

[System.Serializable]
public sealed class PaymentData
{
    public string Data;
    public string Date;

    public PaymentData(string data, string date)
    {
        Data = data;
        Date = date;
    }
}

[Serializable]
public sealed class GachaPaymentData
{
    public string Data;
    public string Date;

    public GachaPaymentData(string data, string date)
    {
        Data = data;
        Date = date;
    }
}

public static class PaymentInfo
{


    private static List<PaymentData> _paymentDatas = new List<PaymentData>();
    public static List<PaymentData> PaymentDatas => _paymentDatas;

    private static List<GachaPaymentData> _gachaPaymentDatas = new List<GachaPaymentData>();
    public static List<GachaPaymentData> GachaPaymentDatas => _gachaPaymentDatas;

    public static void AddPaymentData(string data)
    {
        _paymentDatas.Add(new PaymentData(data, UserInfo.GetKoreanTime().ToString("yyyy-MM-dd HH:mm:ss")));
    }

    public static bool IsGivePaymentData(string data)
    {
        return _paymentDatas.Exists(x => x.Data == data);
    }

    public static void AddGachaData(string data)
    {
        _gachaPaymentDatas.Add(new GachaPaymentData(data, UserInfo.GetKoreanTime().ToString("yyyy-MM-dd HH:mm:ss")));
    }

    private static Param GetSavePaymentData()
    {
        Param param = new Param();
        param.Add("PaymentDatas", Newtonsoft.Json.JsonConvert.SerializeObject(_paymentDatas));
        param.Add("GachaDatas", Newtonsoft.Json.JsonConvert.SerializeObject(_gachaPaymentDatas));
        return param;
    }


    public static void SavePaymentData()
    {
        Param param = GetSavePaymentData();
        BackendManager.Instance.SaveGameData("PaymentData", param);
    }

    public static void LoadPaymentData()
    {
        // 같은 프로세스에서 계정이 전환됐을 경우 이전 계정의 결제/가챠 이력이 새 계정에 남지 않도록 먼저 비웁니다.
        _paymentDatas = new List<PaymentData>();
        _gachaPaymentDatas = new List<GachaPaymentData>();

        AccountSaveGuard guard = BackendManager.Instance.SaveGuard;
        BackendReturnObject bro = BackendManager.Instance.GetMyData("PaymentData");
        if (bro == null || !bro.IsSuccess())
        {
            DebugLog.LogError("데이터 불러오기 실패: " + (bro != null ? bro.GetMessage() : "null response"));
            return;
        }

        JsonData json = bro.FlattenRows();
        if (json.Count <= 0)
        {
            // PaymentData는 실제 결제가 발생하기 전까지 없는 것이 정상입니다.
            guard.MarkTableConfirmedAbsent("PaymentData");
            DebugLog.Log("저장된 결제 데이터가 없습니다(정상).");
            return;
        }

        try
        {
            string paymentDataJson = json[0]["PaymentDatas"].ToString();
            string gachaDataJson = json[0]["GachaDatas"].ToString();
            List<PaymentData> paymentList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PaymentData>>(paymentDataJson);
            List<GachaPaymentData> gachaList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GachaPaymentData>>(gachaDataJson);
            if (paymentList == null || gachaList == null)
            {
                guard.MarkTableBlocked("PaymentData", SaveBlockReason.InvalidPayload);
                DebugLog.LogError("[PaymentInfo] 결제 데이터 파싱 실패(null)");
                return;
            }

            _paymentDatas = paymentList;
            _gachaPaymentDatas = gachaList;
            guard.MarkTableVerified("PaymentData", bro.GetInDate());
        }
        catch (Exception e)
        {
            guard.MarkTableBlocked("PaymentData", SaveBlockReason.InvalidPayload);
            DebugLog.LogError("[PaymentInfo] 결제 데이터 파싱 실패: " + e.Message);
        }
    }
}
