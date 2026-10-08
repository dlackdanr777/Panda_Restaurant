#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class AdvertisementDiamondRewardTests
{
    // Real reward callback/wallet, serialization, save coordinator, authentication/restore and
    // SDK FlattenRows parser; only transport and storage are memory-owned. No live SDK/account.
    [TestCase(1)]
    [TestCase(3)]
    public void BackendSave_RewardThreeAndNineSurviveAcknowledgedMemoryRelogin(int rewardedAds)
    {
        using (var scope = new Scope())
        {
            var storage = new AdvertisementMemoryRow();
            using (var backend = new AdvertisementMemoryBackend(storage))
            {
                scope.Slot.Reenter = backend.Save;
                for (int i = 1; i <= rewardedAds; i++)
                {
                    scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded");
                    Assert.That(backend.Requests[i - 1].Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
                    backend.AssertPayload(i - 1, 100 + 3 * i, i);
                    backend.Acknowledge(i - 1);
                    Assert.That(backend.Requests[i - 1].Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
                }
                Assert.That(backend.Confirmed, Is.EqualTo(rewardedAds));
            }
            // New owner, new authentication generation, same account and acknowledged memory row.
            using (var relogin = new AdvertisementMemoryBackend(storage))
            {
                Assert.That(relogin.Transport.Restored.Dia, Is.EqualTo(100 + 3 * rewardedAds));
                Assert.That(relogin.Transport.Restored.DailyAdDiaRewardCount, Is.EqualTo(rewardedAds));
                Assert.That(relogin.Owner.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.Ready));
                Assert.That(relogin.Transport.Writes.Count, Is.Zero);
            }
            Assert.That(Backend.IsInitialized || Backend.IsLogin, Is.False);
        }
    }

    [Test]
    public void BackendSave_RewardsWhileFirstSaveWaitsProduceLatestNineAndThreePayload()
    {
        using (var scope = new Scope())
        {
            var storage = new AdvertisementMemoryRow();
            using (var backend = new AdvertisementMemoryBackend(storage))
            {
                scope.Slot.Reenter = backend.Save;
                for (int i = 0; i < 3; i++)
                { scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded"); }
                Assert.That(UserInfo.Dia, Is.EqualTo(109));
                Assert.That(backend.Transport.Writes.Count, Is.EqualTo(1));
                backend.AssertPayload(0, 103, 1);
                backend.Acknowledge(0);
                Assert.That(backend.Transport.Writes.Count, Is.EqualTo(2));
                backend.AssertPayload(1, 109, 3);
                backend.Acknowledge(1);
                Assert.That(backend.Confirmed, Is.EqualTo(3));
                foreach (var request in backend.Requests) Assert.That(request.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
            }
            using (var relogin = new AdvertisementMemoryBackend(storage))
            {
                Assert.That(relogin.Transport.Restored.Dia, Is.EqualTo(109));
                Assert.That(relogin.Transport.Restored.DailyAdDiaRewardCount, Is.EqualTo(3));
            }
        }
    }

    [Test]
    public void BackendSave_UnconfirmedRewardStaysLocalAndDoesNotClaimPersistence()
    {
        using (var scope = new Scope())
        using (var backend = new AdvertisementMemoryBackend(new AdvertisementMemoryRow()))
        {
            scope.Slot.Reenter = backend.Save;
            scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded");
            backend.Transport.Writes[0].Reply(StaffGachaOfflineSession.Response("500", ""));
            try
            {
                Assert.That(UserInfo.Dia, Is.EqualTo(103), "The existing reward is credited locally before save confirmation.");
                Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(1));
                Assert.That(backend.Requests[0].Status, Is.EqualTo(GameDataSaveRequestStatus.Indeterminate));
                Assert.That(backend.Confirmed, Is.Zero);
                Assert.That(backend.Failed, Is.EqualTo(1));
                Assert.That(backend.Transport.Writes.Count, Is.EqualTo(1), "Do not retry an uncertain write automatically.");
                Assert.That((int)JObject.Parse(backend.Transport.Storage.Json)["Dia"], Is.EqualTo(100));
                Assert.That((int)JObject.Parse(backend.Transport.Storage.Json)["DailyAdDiaRewardCount"], Is.Zero);
            }
            finally
            {
                // Resolve this test-owned request with its late ACK; never clear production target locks.
                backend.Acknowledge(0);
            }
        }
    }

    private sealed class AdvertisementMemoryRow
    {
        internal readonly string Account = "phase4-ad-memory-account:" + Guid.NewGuid().ToString("N");
        internal readonly string Row = "phase4-ad-memory-row:" + Guid.NewGuid().ToString("N");
        internal string Json;
        internal AdvertisementMemoryRow()
        {
            var values = UserInfo.GetSaveUserData();
            var staff = new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4) }, 55);
            Assert.That(StaffAccountSaveConverter.TrySerialize(staff, out string json, out string error), Is.True, error);
            values.Add("StaffAccount", json);
            Json = values.GetJson();
        }
    }

    private sealed class AdvertisementMemoryBackend : IDisposable
    {
        internal readonly AdvertisementMemoryTransport Transport;
        internal readonly BackendManager Owner;
        internal readonly List<GameDataSaveRequest> Requests = new List<GameDataSaveRequest>();
        internal int Confirmed, Failed;
        internal AdvertisementMemoryBackend(AdvertisementMemoryRow storage)
        {
            Transport = new AdvertisementMemoryTransport(storage);
            Owner = BackendManager.CreateEditorOfflineOwner(Transport, new AdvertisementNoStage(),
                UserInfo.StaffPurchaseWallet, _ => throw new InvalidOperationException("No gacha purchase in ad-save test."), _ => { });
            try
            {
                var auth = Owner.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
                Transport.LoggedIn = true;
                Assert.That(Owner.NotifyFederationLoginSuccess(auth, StaffGachaOfflineSession.Response("200", "")), Is.True);
                bool restored = false;
                Owner.GetAndRestoreGameDataAsync((_, result) => restored = result.Status == GameDataRestoreStatus.Ready);
                Assert.That(restored, Is.True, Owner.GameDataRestoreResult.Reason);
                Assert.That(Transport.Restored, Is.Not.Null);
            }
            catch { Dispose(); throw; }
        }
        internal void Save()
        {
            // The same autosave API and gameplay guard used by GameManager.AsyncSaveGameData.
            // StageData and animation are intentionally excluded; Dia/count live in GameData.
            Requests.Add(Owner.RequestGameDataAutosave(_ => Confirmed++, _ => Failed++, requireGameplay: true));
        }
        internal void AssertPayload(int index, int diamonds, int count)
        {
            var values = JObject.Parse(Transport.Writes[index].Json);
            Assert.That((int)values["Dia"], Is.EqualTo(diamonds));
            Assert.That((int)values["DailyAdDiaRewardCount"], Is.EqualTo(count));
            Assert.That(Transport.Writes[index].Target.AccountInDate, Is.EqualTo(Transport.Storage.Account));
            Assert.That(Transport.Writes[index].Target.RowInDate, Is.EqualTo(Transport.Storage.Row));
        }
        internal void Acknowledge(int index)
        {
            var write = Transport.Writes[index];
            Transport.Storage.Json = write.Json;
            write.Reply(StaffGachaOfflineSession.Response("204", ""));
        }
        public void Dispose() { if (Owner != null) Owner.DestroyEditorOfflineOwner(); }
    }

    private sealed class AdvertisementMemoryTransport : IGameDataBackendTransport
    {
        internal sealed class Write
        { internal string Json; internal GameDataSaveTarget Target; internal Action<BackendReturnObject> Reply; }
        internal readonly AdvertisementMemoryRow Storage;
        internal readonly List<Write> Writes = new List<Write>();
        internal LoadUserData Restored;
        public bool LoggedIn { get; set; }
        public bool NativeLoggedIn => LoggedIn;
        public string AccountInDate => Storage.Account;
        public bool GameplaySaveAllowed => true;
        internal AdvertisementMemoryTransport(AdvertisementMemoryRow storage) { Storage = storage; }
        public IReadOnlyList<StaffData> ReadCatalog() => Resources.LoadAll<StaffData>("StaffData");
        public Param LatestValues() => UserInfo.GetSaveUserData();
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback)
        { Writes.Add(new Write { Target = target, Json = values.GetJson(), Reply = callback }); }
        public void Get(string account, Action<BackendReturnObject> callback)
        {
            Assert.That(account, Is.EqualTo(Storage.Account));
            var row = new JObject { ["owner_inDate"] = Attribute(new JValue(Storage.Account)), ["inDate"] = Attribute(new JValue(Storage.Row)) };
            foreach (var field in JObject.Parse(Storage.Json).Properties()) row[field.Name] = Attribute(field.Value);
            callback(StaffGachaOfflineSession.Response("200", new JObject
            { ["rows"] = new JArray(row), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None)));
        }
        public bool Restore(BackendReturnObject response)
        {
            // Same SDK flattening and production parser used at the start of UserInfo.TryLoadGameData.
            Restored = new LoadUserData(response.FlattenRows());
            return Restored.IsValid;
        }
        public Param InitialValues() => throw new InvalidOperationException("No account creation in ad-save test.");
        public void Insert(Param values, Action<BackendReturnObject> callback) => throw new InvalidOperationException("No account insertion in ad-save test.");
        private static JObject Attribute(JToken value)
        {
            if (value is JObject obj)
            {
                var map = new JObject();
                foreach (var field in obj.Properties()) map[field.Name] = Attribute(field.Value);
                return new JObject { ["M"] = map };
            }
            if (value is JArray list)
            {
                var array = new JArray(); foreach (var item in list) array.Add(Attribute(item));
                return new JObject { ["L"] = array };
            }
            if (value.Type == JTokenType.String) return new JObject { ["S"] = (string)value };
            if (value.Type == JTokenType.Boolean) return new JObject { ["BOOL"] = (bool)value };
            if (value.Type == JTokenType.Null) return new JObject { ["NULL"] = true };
            return new JObject { ["N"] = value.ToString(Formatting.None) };
        }
    }

    private sealed class AdvertisementNoStage : IStageDataLoadTransport
    {
        public void Get(EStage stage, string account, Func<bool> current, Action<BackendReturnObject> reply) => throw new InvalidOperationException("No Stage SDK in ad-save test.");
        public BackendReturnObject Get(EStage stage, string account, Func<bool> current) => throw new InvalidOperationException("No Stage SDK in ad-save test.");
        public bool Apply(EStage stage, BackendReturnObject response, Func<bool> current, bool asynchronous) => throw new InvalidOperationException("No Stage apply in ad-save test.");
        public StaffStageRuntimeSnapshot ReadStaff(EStage stage) => throw new InvalidOperationException("No Stage read in ad-save test.");
    }
}
#endif
