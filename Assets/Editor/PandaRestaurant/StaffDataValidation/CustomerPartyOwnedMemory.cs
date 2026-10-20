#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Explicit memory transport for the existing owned Stage1 preview. No SDK request,
// PlayerPrefs/account import, or disk account persistence is implemented here.
internal sealed class CustomerPartyOwnedMemory : IGameDataBackendTransport,IStageDataLoadTransport,IStageDataWriteTransport,IDisposable
{
    internal static readonly IReadOnlyDictionary<EquipStaffType,IReadOnlyList<string>> StaffAiStaffIds =
        new ReadOnlyDictionary<EquipStaffType,IReadOnlyList<string>>(new Dictionary<EquipStaffType,IReadOnlyList<string>>
        {
            [EquipStaffType.Manager] = Array.AsReadOnly(new[] { "STAFF06", "STAFF07" }),
            [EquipStaffType.Chef] = Array.AsReadOnly(new[] { "STAFF16", "STAFF17" }),
            [EquipStaffType.Waiter] = Array.AsReadOnly(new[] { "STAFF01", "STAFF02" }),
            [EquipStaffType.Cleaner] = Array.AsReadOnly(new[] { "STAFF21", "STAFF22" }),
            [EquipStaffType.Marketer] = Array.AsReadOnly(new[] { "STAFF11", "STAFF12" }),
            [EquipStaffType.Guard] = Array.AsReadOnly(new[] { "STAFF26", "STAFF30" })
        });
    internal BackendManager Owner;
    internal int GameWrites,StageWrites;
    internal bool FailNextStageWrite;
    internal readonly Dictionary<EStage,JObject> Rows=new Dictionary<EStage,JObject>();
    private JObject game;
    private StageInfo[] stages;
    private GachaCollectionPreviewCatalog catalog;
    private double started;
    public bool NativeLoggedIn{get;private set;}
    public bool LoggedIn=>NativeLoggedIn;
    public string AccountInDate=>"2026-10-15T03:00:00.001Z";
    private string GameRow=>"2026-10-15T03:00:00.002Z";
    public bool GameplaySaveAllowed=>UserInfo.IsFirstTutorialClear&&!UserInfo.IsTutorialStart;
    private DateTime UtcNow()=>new DateTime(2026,10,15,3,0,0,DateTimeKind.Utc).AddSeconds(Math.Max(0,Time.realtimeSinceStartupAsDouble-started));
    private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)??throw new MissingFieldException(type.Name,name);
    private static void Set(object obj,string name,object value)=>Field(obj.GetType(),name).SetValue(obj,value);
    private static JObject Parse(string text)
    {using(var reader=new JsonTextReader(new StringReader(text)){DateParseHandling=DateParseHandling.None})return JObject.Load(reader);}

    private static StaffAccountStaffRecord[] CreateStaffAiRecords()
    {
        var dataManager = StaffDataManager.Instance;
        var records = new List<StaffAccountStaffRecord>();
        foreach (var role in StaffAiStaffIds)
            foreach (string id in role.Value)
            {
                var data = dataManager.GetStaffData(id);
                if (data == null || data.Id != id || data.MaxLevel < 1 || data.GetRuntimeLevel(1) != 1
                    || dataManager.GetStaffGroupType(data) != dataManager.GetStaffGroupType(role.Key)
                    || !dataManager.GetStaffDataList(role.Key, ERestaurantFloorType.Floor1).Contains(data)
                    || !dataManager.GetStaffDataList(role.Key, ERestaurantFloorType.Floor2).Contains(data))
                    throw new InvalidOperationException("Invalid representative staff catalog entry: " + role.Key + "/" + id);
                records.Add(new StaffAccountStaffRecord(id, 1));
            }
        return records.ToArray();
    }

    internal void Initialize(bool furnitureFix = false, bool staffAi = false,
        string encounterCustomerId = null, int encounterVisitCount = 0)
    {
        if (encounterCustomerId != null && (!CustomerEncounterCatalog.IsSupportedCustomer(encounterCustomerId)
            || (encounterVisitCount != 0 && encounterVisitCount != 24 && encounterVisitCount != 49)))
            throw new InvalidOperationException("Invalid memory-only encounter fixture.");
        started=Time.realtimeSinceStartupAsDouble;
        stages=(StageInfo[])Field(typeof(UserInfo),"_stageInfos").GetValue(null);
        if(stages.Any(s=>s==null))throw new InvalidOperationException("Original UserInfo initialization required");
        game=Parse(LoadUserData.CreateInitialGameData(UtcNow().AddHours(9)).GetJson());
        foreach(var property in game.Properties().Where(p=>p.Name.StartsWith("Is")&&p.Name.EndsWith("TutorialClear")).ToArray())property.Value=true;
        game["FirstTutorialStartRewardGranted"]=true;
        game["LastAttendanceTime"]=UtcNow().AddHours(9).ToString("O");game["TotalAttendanceDays"]=1;
        var enabledCustomerIds = new List<string> { "CUSTOMER01", "CUSTOMER02", "CUSTOMER04" };
        if (encounterCustomerId != null && !enabledCustomerIds.Contains(encounterCustomerId)) enabledCustomerIds.Add(encounterCustomerId);
        game["EnabledCustomerDataList"] = JArray.FromObject(enabledCustomerIds.Select(id => new SaveCustomerData(id, "",
            id == encounterCustomerId ? encounterVisitCount : furnitureFix && !staffAi ? 0 : 25)));
        game["GiveRecipeList"]=new JArray(new[]{"FOOD01","FOOD02","FOOD04","FOOD05","FOOD13","FOOD14"}.Select(id=>new JObject{["Id"]=id,["Level"]=1}));
        game["ClearMainChallengeList"]=new JArray(Enumerable.Range(1,12).Select(i=>"MainReward"+i.ToString("D2")));
        var ownedStaff=staffAi?CreateStaffAiRecords():new[]{new StaffAccountStaffRecord("STAFF01",1)};
        var account=new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,ownedStaff,0,new GachaEconomySaveData(0,0,0,0));
        if(!StaffAccountSaveConverter.TrySerialize(account,out string staff,out string error))throw new InvalidOperationException(error);
        game["StaffAccount"]=staff;
        for(int index=0;index<stages.Length;index++)
        {
            var stage=new StageInfo();
            if(index==0)
            {
                Set(stage,"_satisfaction",10f);
                var tables=(FurnitureData[,])Field(typeof(StageInfo),"_equipFurnitureDatas").GetValue(stage);
                var kitchens=(KitchenUtensilData[,])Field(typeof(StageInfo),"_equipKitchenUtensilDatas").GetValue(stage);
                var ownedFurniture=(List<string>)Field(typeof(StageInfo),"_giveFurnitureList").GetValue(stage);
                var ownedKitchen=(List<string>)Field(typeof(StageInfo),"_giveKitchenUtensilList").GetValue(stage);
                var equipment=(Dictionary<ERestaurantFloorType,Dictionary<EquipStaffType,StaffData>>)Field(typeof(StageInfo),"_equipStaffTypeDic").GetValue(stage);
                for(int f=0;f<2;f++)
                {
                    for(int t=0;t<5;t++){var art=FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)t).First(d=>d.Id.StartsWith("TABLE01"));if(furnitureFix&&f==1)art=FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)t).First(d=>d.Id.StartsWith("TABLE11"));tables[f,t]=art;if(!ownedFurniture.Contains(art.Id))ownedFurniture.Add(art.Id);}
                    for(int k=0;k<4;k++){var art=KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)k).First(d=>d.Id.StartsWith("COOKER01"));kitchens[f,k]=art;if(!ownedKitchen.Contains(art.Id))ownedKitchen.Add(art.Id);}
                    if(!furnitureFix&&!staffAi)equipment[(ERestaurantFloorType)f][EquipStaffType.Waiter]=StaffDataManager.Instance.GetStaffData("STAFF01");
                }
            }
            if(furnitureFix&&index==0){var list=(List<string>)Field(typeof(StageInfo),"_giveFurnitureList").GetValue(stage);list.Add("TABLE02_02");list.Add("TABLE13_02");}
            Rows[(EStage)index]=Parse(stage.SaveData().GetParam().GetJson());
        }
        Owner=BackendManager.CreateEditorOfflineOwner(this,this,UserInfo.StaffPurchaseWallet,_=>throw new InvalidOperationException("Purchase not part of UI preview"),_=>throw new InvalidOperationException("No purchase effects"));
        Field(typeof(BackendManager),"_instance").SetValue(null,Owner);Owner.SetEditorOfflineUtcClock(UtcNow);
        Set(Owner,"_firstTutorialState",Activator.CreateInstance(typeof(UserInfo).Assembly.GetType("UserFirstTutorialState",true),true));
        Set(Owner,"_attendanceState",Activator.CreateInstance(typeof(UserInfo).Assembly.GetType("UserAttendanceState",true),true));
        Set(Owner,"_attendanceNowKst",(Func<DateTime>)(()=>UtcNow().AddHours(9)));
        var auth=Owner.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);NativeLoggedIn=true;
        if(!Owner.NotifyFederationLoginSuccess(auth,Response("200","")))throw new InvalidOperationException("Memory authentication rejected");
        bool restored=false;string reason=null;
        Owner.GetAndRestoreGameDataAsync((q,r)=>{restored=r.Status==GameDataRestoreStatus.Ready;reason=r.Reason;});
        if(!restored)throw new InvalidOperationException("Memory restore failed: "+reason);
        Owner.LoadAllStageData(false);
        if(!UserInfo.IsFirstTutorialClear||UserInfo.CheckNoAttendance())throw new InvalidOperationException("Unexpected tutorial/attendance memory state");
        catalog=new GachaCollectionPreviewCatalog();
        var snapshot=(GachaEconomySnapshot)typeof(UserInfo).GetMethod("CaptureGachaEconomy",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{AccountInDate,Owner.StaffRuntime.Snapshot});
        var economy=new GachaEconomyService(new GachaEconomyMemoryStore(snapshot),catalog.Items,Resources.Load<GachaEconomySettings>("GachaEconomySettings"),clock:new Clock(UtcNow),displayRandom:new System.Random(1917));
        Set(Owner,"_gachaEconomy",economy);Set(Owner,"_gachaEconomyQuery",Owner.CurrentMailReceiveQuery);
    }
    private sealed class Clock:IGachaExchangeClock
    {private readonly Func<DateTime> now;internal Clock(Func<DateTime> now){this.now=now;}public bool TryGetUtcNow(out DateTime utc){utc=now();return true;}}
    private static BackendReturnObject Response(string code,string json)=>StaffGachaOfflineSession.Response(code,json);
    private static JObject Attribute(JToken value)
    {
        if(value is JObject obj)return new JObject{["M"]=new JObject(obj.Properties().Select(p=>new JProperty(p.Name,Attribute(p.Value))))};
        if(value is JArray array)return new JObject{["L"]=new JArray(array.Select(Attribute))};
        if(value.Type==JTokenType.String)return new JObject{["S"]=value.DeepClone()};
        if(value.Type==JTokenType.Boolean)return new JObject{["BOOL"]=value.DeepClone()};
        if(value.Type==JTokenType.Null)return new JObject{["NULL"]=true};
        return new JObject{["N"]=value.ToString(Formatting.None)};
    }
    private BackendReturnObject Read(JObject values,string rowId)
    {
        var row=new JObject{["owner_inDate"]=Attribute(new JValue(AccountInDate)),["inDate"]=Attribute(new JValue(rowId))};
        foreach(var property in values.Properties())row[property.Name]=Attribute(property.Value);
        return Response("200",new JObject{["rows"]=new JArray(row),["firstKey"]=JValue.CreateNull()}.ToString(Formatting.None));
    }
    private static void Merge(JObject row,Param values){foreach(var property in Parse(values.GetJson()).Properties())row[property.Name]=property.Value.DeepClone();}
    private void Guard(string account,Func<bool> current){if(account!=AccountInDate||!current())throw new InvalidOperationException("Stale memory request");}
    public IReadOnlyList<StaffData> ReadCatalog()=>Resources.LoadAll<StaffData>("StaffData");
    public bool Restore(BackendReturnObject response)=>UserInfo.TryLoadGameData(response);
    public Param InitialValues()=>throw new InvalidOperationException("No account insertion");
    public Param LatestValues(){UserInfo.ApplyDailyWeeklyResetIfNeeded();return UserInfo.GetSaveUserData();}
    public void Insert(Param values,Action<BackendReturnObject> reply)=>throw new InvalidOperationException("No account insertion");
    public void Get(string account,Action<BackendReturnObject> reply){Guard(account,()=>NativeLoggedIn);reply(Read(game,GameRow));}
    public void Update(GameDataSaveTarget target,Param values,Action<BackendReturnObject> reply)
    {if(target.AccountInDate!=AccountInDate||target.RowInDate!=GameRow)throw new InvalidOperationException("Wrong memory save target");Merge(game,values);GameWrites++;reply(Response("204",""));}
    public void Get(EStage stage,string account,Func<bool> current,Action<BackendReturnObject> reply){Guard(account,current);reply(Read(Rows[stage],"2026-10-15T03:00:1"+(int)stage+".001Z"));}
    public BackendReturnObject Get(EStage stage,string account,Func<bool> current){Guard(account,current);return Read(Rows[stage],"2026-10-15T03:00:1"+(int)stage+".001Z");}
    public bool Apply(EStage stage,BackendReturnObject response,Func<bool> current,bool asynchronous)=>(bool)typeof(UserInfo).GetMethod("ApplyLoadedStageData",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stage,response,current,asynchronous});
    public StaffStageRuntimeSnapshot ReadStaff(EStage stage)=>stages[(int)stage].CaptureStaffRuntimeSnapshot();
    public void ReadForSave(EStage stage,string account,Func<bool> current,bool asynchronous,Action<BackendReturnObject> reply)=>Get(stage,account,current,reply);
    public void Update(EStage stage,string account,string row,Func<Param> latest,Func<bool> current,bool asynchronous,Action<BackendReturnObject> reply)
    {Guard(account,current);if(FailNextStageWrite){FailNextStageWrite=false;reply(Response("500",""));return;}Merge(Rows[stage],latest());StageWrites++;reply(Response("204",""));}
    internal void WriteEvidence(string directory)=>File.WriteAllText(Path.Combine(directory,"memory-transports.json"),JsonConvert.SerializeObject(new{GameWrites,StageWrites,game,stages=Rows},Formatting.Indented));
    public void Dispose()
    {
        NativeLoggedIn=false;
        if(Owner!=null){Field(typeof(BackendManager),"_instance").SetValue(null,null);Owner.DestroyEditorOfflineOwner();Owner=null;}
        catalog?.Dispose();catalog=null;
    }
}
#endif
