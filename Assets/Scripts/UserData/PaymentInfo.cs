using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using LitJson;
using Muks.BackEnd;
using Muks.PathFinding;
using Unity.Profiling;

public enum PaymentDataLoadResult
{
    Loaded,
    NotFound
}

public enum PaymentDataLoadFailure
{
    RequestFailed,
    InvalidData,
    StaleResponse
}

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
    private static readonly ProfilerMarker RequestMarker =
        new ProfilerMarker("Panda.PaymentData.Request");
    private static readonly ProfilerMarker ResponseParsingMarker =
        new ProfilerMarker("Panda.PaymentData.ResponseParsing");
    private static readonly ProfilerMarker DataApplyMarker =
        new ProfilerMarker("Panda.PaymentData.DataApply");

    private static int _loadRequestVersion;

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

    public static void LoadPaymentDataAsync(
        string expectedOwnerInDate,
        Action<PaymentDataLoadResult> onSuccess,
        Action<PaymentDataLoadFailure> onFail)
    {
        int requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        int completionState = 0;
        MainThreadDispatcher dispatcher = MainThreadDispatcher.Instance;

        bool IsCurrentRequest()
        {
            return requestVersion == Volatile.Read(ref _loadRequestVersion)
                && string.Equals(expectedOwnerInDate, Backend.UserInDate, StringComparison.Ordinal);
        }

        void CompleteFailure(PaymentDataLoadFailure failure)
        {
            if (Interlocked.Exchange(ref completionState, 1) != 0)
                return;

            dispatcher.Enqueue(() => onFail?.Invoke(failure));
        }

        void ApplyAndComplete(
            List<PaymentData> paymentDatas,
            List<GachaPaymentData> gachaPaymentDatas,
            PaymentDataLoadResult result)
        {
            if (!IsCurrentRequest())
            {
                CompleteFailure(PaymentDataLoadFailure.StaleResponse);
                return;
            }

            if (Interlocked.Exchange(ref completionState, 1) != 0)
                return;

            dispatcher.Enqueue(() =>
            {
                if (!IsCurrentRequest())
                {
                    onFail?.Invoke(PaymentDataLoadFailure.StaleResponse);
                    return;
                }

                using (DataApplyMarker.Auto())
                {
                    _paymentDatas = paymentDatas;
                    _gachaPaymentDatas = gachaPaymentDatas;
                }

                onSuccess?.Invoke(result);
            });
        }

        using (RequestMarker.Auto())
        {
            BackendManager.Instance.GetMyDataAsync("PaymentData", (bro) =>
            {
                if (!IsCurrentRequest())
                {
                    CompleteFailure(PaymentDataLoadFailure.StaleResponse);
                    return;
                }

                try
                {
                    List<PaymentData> paymentDatas;
                    List<GachaPaymentData> gachaPaymentDatas;
                    PaymentDataLoadResult result;
                    PaymentDataLoadFailure? parsingFailure = null;

                    using (ResponseParsingMarker.Auto())
                    {
                        JsonData json = bro.FlattenRows();
                        if (json.Count == 0)
                        {
                            paymentDatas = new List<PaymentData>();
                            gachaPaymentDatas = new List<GachaPaymentData>();
                            result = PaymentDataLoadResult.NotFound;
                        }
                        else
                        {
                            if (json.Count != 1
                                || !json[0].ContainsKey("PaymentDatas")
                                || json[0]["PaymentDatas"] == null)
                            {
                                parsingFailure = PaymentDataLoadFailure.InvalidData;
                                paymentDatas = null;
                                gachaPaymentDatas = null;
                                result = PaymentDataLoadResult.Loaded;
                            }
                            else
                            {
                                paymentDatas = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PaymentData>>(
                                    json[0]["PaymentDatas"].ToString());
                                gachaPaymentDatas = !json[0].ContainsKey("GachaDatas")
                                    ? new List<GachaPaymentData>()
                                    : Newtonsoft.Json.JsonConvert.DeserializeObject<List<GachaPaymentData>>(
                                        json[0]["GachaDatas"].ToString());

                                if (paymentDatas == null || gachaPaymentDatas == null
                                    || paymentDatas.Exists(data => data == null || string.IsNullOrEmpty(data.Data))
                                    || gachaPaymentDatas.Exists(data => data == null || string.IsNullOrEmpty(data.Data)))
                                {
                                    parsingFailure = PaymentDataLoadFailure.InvalidData;
                                }

                                result = PaymentDataLoadResult.Loaded;
                            }
                        }
                    }

                    if (parsingFailure.HasValue)
                    {
                        CompleteFailure(parsingFailure.Value);
                        return;
                    }

                    ApplyAndComplete(paymentDatas, gachaPaymentDatas, result);
                }
                catch (Exception)
                {
                    DebugLog.LogError("[PaymentInfo] PaymentData 파싱 실패");
                    CompleteFailure(PaymentDataLoadFailure.InvalidData);
                }
            }, (state) =>
            {
                if (!IsCurrentRequest())
                {
                    CompleteFailure(PaymentDataLoadFailure.StaleResponse);
                    return;
                }

                DebugLog.LogError("[PaymentInfo] PaymentData 조회 실패: " + state);
                CompleteFailure(PaymentDataLoadFailure.RequestFailed);
            });
        }
    }

    public static void CancelPendingLoad()
    {
        Interlocked.Increment(ref _loadRequestVersion);
    }
}
