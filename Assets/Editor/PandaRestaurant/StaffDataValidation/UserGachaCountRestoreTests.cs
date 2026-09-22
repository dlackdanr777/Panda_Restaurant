#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class UserGachaCountRestoreTests
{
    [Test]
    public void ActualUserInfoRestore_PreservesGachaCountOnRepeatedLoadsAndNextSaveWithoutPurchaseEffects()
    {
        using (var scope = new RestoreScope())
        {
            int useEvents = 0, diamondEvents = 0, tokenEvents = 0;
            UserInfo.OnUseGachaMachineHandler += () => useEvents++;
            UserInfo.OnChangeDiaHandler += () => diamondEvents++;
            UserInfo.OnChangeSkinTokenHandler += () => tokenEvents++;
            string paymentsBefore = JsonConvert.SerializeObject(PaymentInfo.PaymentDatas);
            string gachaPaymentsBefore = JsonConvert.SerializeObject(PaymentInfo.GachaPaymentDatas);

            // Each response is authoritative, including a legitimate zero after a nonzero load.
            // These are memory responses, not purchases, and no AddUserGachaMachineCount is called.
            foreach (int count in new[] { 1, 12, 0 })
            {
                JObject values = JObject.Parse(LoadUserData.CreateInitialGameData(RestoreScope.FixedUtc).GetJson());
                values["TotalUseGachaMachineCount"] = count;
                values["Dia"] = 73;
                values["Money"] = 1234L;
                values["SkinToken"] = 19;
                values["LastAccessTime"] = RestoreScope.FixedUtc.AddHours(9).ToString();
                values["LastAttendanceTime"] = RestoreScope.FixedUtc.AddHours(9).ToString();
                BackendReturnObject response = Response(values);
                string originalResponse = response.GetReturnValue();

                for (int repeat = 0; repeat < 2; repeat++)
                {
                    Assert.That(UserInfo.TryLoadGameData(response), Is.True, "Actual runtime restore");
                    Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(count));
                    JObject saved = JObject.Parse(UserInfo.GetSaveUserData().GetJson());
                    Assert.That((int)saved["TotalUseGachaMachineCount"], Is.EqualTo(count), "Next save must not reset the restored count");
                    Assert.That(UserInfo.TryLoadGameData(Response(saved)), Is.True, "Actual save-to-restore memory roundtrip");
                    Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(count));
                    JObject savedAgain = JObject.Parse(UserInfo.GetSaveUserData().GetJson());
                    Assert.That((int)savedAgain["TotalUseGachaMachineCount"], Is.EqualTo(count));
                    Assert.That(UserInfo.Dia, Is.EqualTo(73));
                    Assert.That(UserInfo.Money, Is.EqualTo(1234L));
                    Assert.That(UserInfo.SkinToken, Is.EqualTo(19));
                    Assert.That((int)savedAgain["Dia"], Is.EqualTo(73));
                    Assert.That((long)savedAgain["Money"], Is.EqualTo(1234L));
                    Assert.That((int)savedAgain["SkinToken"], Is.EqualTo(19));
                    Assert.That(useEvents, Is.Zero, "Restore must not count another purchase or publish a use event");
                    Assert.That(diamondEvents, Is.Zero, "Restore must not award/spend diamonds");
                    Assert.That(tokenEvents, Is.Zero, "Restore must not award tokens");
                    Assert.That(response.GetReturnValue(), Is.EqualTo(originalResponse));
                }
            }

            Assert.That(JsonConvert.SerializeObject(PaymentInfo.PaymentDatas), Is.EqualTo(paymentsBefore));
            Assert.That(JsonConvert.SerializeObject(PaymentInfo.GachaPaymentDatas), Is.EqualTo(gachaPaymentsBefore));
            scope.AssertRandomUnchanged();
        }
    }

    private static BackendReturnObject Response(JObject values)
    {
        var row = new JObject();
        foreach (JProperty field in values.Properties()) row[field.Name] = Attribute(field.Value);
        string raw = new JObject { ["rows"] = new JArray(row), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
        var response = new BackendReturnObject();
        foreach (var entry in new Dictionary<string, string>
        { { "StatusCode", "200" }, { "ReturnValue", raw }, { "ErrorCode", "" }, { "Message", "" } })
        {
            PropertyInfo property = typeof(BackendReturnObject).GetProperty(entry.Key,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, entry.Key);
            property.SetValue(response, Convert.ChangeType(entry.Value, property.PropertyType));
        }
        return response;
    }

    private static JObject Attribute(JToken value)
    {
        switch (value.Type)
        {
            case JTokenType.Integer: return new JObject { ["N"] = value.ToString(Formatting.None) };
            case JTokenType.Boolean: return new JObject { ["BOOL"] = (bool)value };
            case JTokenType.String: return new JObject { ["S"] = (string)value };
            case JTokenType.Array:
                var array = new JArray();
                foreach (JToken item in (JArray)value) array.Add(Attribute(item));
                return new JObject { ["L"] = array };
            case JTokenType.Object:
                var map = new JObject();
                foreach (JProperty item in ((JObject)value).Properties()) map[item.Name] = Attribute(item.Value);
                return new JObject { ["M"] = map };
            default: throw new InvalidOperationException("Unsupported test response field: " + value.Type);
        }
    }

    // Same detached-GameObject/static-backup pattern as ConsumerScope. The real restore/save
    // methods run, but Awake/Init never run, timer state is private to this scope, and every
    // ServerTime read takes the cached branch (no Backend.Utils.GetServerTime invocation).
    private sealed class RestoreScope : IDisposable
    {
        internal static readonly DateTime FixedUtc = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly UnityEngine.Random.State _random = UnityEngine.Random.state;

        internal RestoreScope()
        {
            try
            {
                foreach (FieldInfo field in typeof(UserInfo).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    object original = field.GetValue(null);
                    _restore.Add(() => field.SetValue(null, original));
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType)) field.SetValue(null, null);
                }
                Field(typeof(UserInfo), "_totalUseGachaMachineCount").SetValue(null, 47);
                Field(typeof(UserInfo), "_diamondDataGeneration").SetValue(null, 0L);
                ReplaceStatic(typeof(Muks.DataBind.DataBind), "_textBindDic", new Muks.DataBind.DataBindContainer<string>());
                BackendManager backend = CreateInactive<BackendManager>();
                Field(typeof(BackendManager), "_cachedServerTime").SetValue(backend, FixedUtc);
                Field(typeof(BackendManager), "_serverTimeCachedAt").SetValue(backend, float.PositiveInfinity);
                ReplaceStatic(typeof(BackendManager), "_instance", backend);
                ReplaceStatic(typeof(TimeManager), "_instance", CreateInactive<TimeManager>());
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private T CreateInactive<T>() where T : Component
        {
            var go = new GameObject("Inactive detached gacha-count restore " + typeof(T).Name);
            go.SetActive(false);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        private void ReplaceStatic(Type type, string name, object value)
        {
            FieldInfo field = Field(type, name);
            object original = field.GetValue(null);
            _restore.Add(() => field.SetValue(null, original));
            field.SetValue(null, value);
        }

        internal void AssertRandomUnchanged() => Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(_random)));

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            for (int i = _restore.Count - 1; i >= 0; i--) _restore[i]();
            UnityEngine.Random.state = _random;
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field;
        }
    }
}
#endif
