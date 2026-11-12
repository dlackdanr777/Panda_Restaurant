using BackEnd;
using LitJson;
using Muks.BackEnd;

public static partial class UserInfo
{
    private static string _popupStoreAccountJson;
    internal static void LoadPopupStoreAccountField(JsonData rows)
    {
        _popupStoreAccountJson = null;
        if (rows != null && rows.IsArray && rows.Count == 1 && rows[0].IsObject
            && rows[0].ContainsKey(PopupStoreGameDataRecord.FieldName))
        {
            var value = rows[0][PopupStoreGameDataRecord.FieldName];
            if (value != null && value.IsString) _popupStoreAccountJson = (string)value;
        }
    }
    internal static void AddPopupStoreAccountSaveField(Param values)
    { if (_popupStoreAccountJson != null) values.Add(PopupStoreGameDataRecord.FieldName, _popupStoreAccountJson); }
    internal static void SetPopupStoreAccountRecord(string json) { _popupStoreAccountJson = json; }
}

/// <summary>Original wallet only; no second balance, SDK call or AddDia retry path.</summary>
public sealed class UserPopupStoreAccountMemory : IPopupStoreAccountMemory
{
    private StaffPurchaseDiamondWallet Wallet => (StaffPurchaseDiamondWallet)UserInfo.StaffPurchaseWallet;
    public int Diamonds => UserInfo.Dia;
    public void SetRecord(string json) => UserInfo.SetPopupStoreAccountRecord(json);
    public bool TryReserveReward(object owner, out string error) => Wallet.TryReserveReward(owner, Wallet.Diamonds, out error);
    public bool TryCommitReward(object owner, int amount, out string error) => Wallet.TryCommitReservedReward(owner, amount, out error);
    public void ReleaseReward(object owner) => Wallet.ReleaseBeforeSend(owner);
    public void NotifyReward() => Wallet.NotifyCommittedReward();
}
