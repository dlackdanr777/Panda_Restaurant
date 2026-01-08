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
        DebugLog.Log($"[PaymentInfo] GetSavePaymentData : {_paymentDatas.Count}");
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
        BackendReturnObject bro = BackendManager.Instance.GetMyData("PaymentData");
        if (!bro.IsSuccess())
        {
            DebugLog.LogError("데이터 불러오기 실패: " + bro.GetMessage());
            return;
        }

        JsonData json = bro.FlattenRows();
        if (json.Count <= 0)
        {
            DebugLog.LogError("저장된 데이터가 없습니다.");
            return;
        }

        string paymentDataJson = json[0]["PaymentDatas"].ToString();
        _paymentDatas = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PaymentData>>(paymentDataJson);
        _gachaPaymentDatas = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GachaPaymentData>>(json[0]["GachaDatas"].ToString());
    }
}
