#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CustomerPartyStage1Preview
{
    private static StaffController serviceStaff;
    private static StaffWaiter serviceWaiter;
    private static StaffData serviceData;
    private static bool serviceCancelled, serviceRestored;
    private static double cancellationTime;
    private static int initialCookCount;
    private static PartyFoodOrder cancelledFood;
    private static TableOrderIdentity cancelledIdentity;
    private static Vector3 carryStartedPosition;
    private static double carryStartedAt;
    private static readonly HashSet<TableServiceReservation> orderReservations = new HashSet<TableServiceReservation>();
    private static readonly List<object> serviceResults = new List<object>();
    private static bool waiterStairsObserved, firstMealWaited, parallelMealObserved;
    private static bool manualWaiterReleased, completedWhileEating, deliveredWhileEating, partnerEatingAfterFirst;
    private static bool IsServicePreview => OrderMode >= 4;

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Open existing isolated Phase3B project")]
    public static void OpenExistingServingProject()
    {
        string path=EditorUtility.OpenFolderPanel("Select the existing owned party preview project","","");
        if(string.IsNullOrEmpty(path)) return;
        if(!File.Exists(Path.Combine(path,Marker)) || Directory.Exists(Path.Combine(path,".git")) || File.Exists(Path.Combine(path,".git")))
            throw new InvalidOperationException("Select the existing owned preview project, not the source checkout.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(EditorApplication.applicationPath,
            "-projectPath \""+path+"\" -executeMethod CustomerPartyStage1Preview.StartWaiterMeals") { UseShellExecute=false });
    }

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3B waiter meals (owned copy only)")]
    public static void StartWaiterMeals() => StartOrderMode(4);
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3B carry cancellation (owned copy only)")]
    public static void StartWaiterCancellation() => StartOrderMode(5);
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3B manual serving overlap (owned copy only)")]
    public static void StartManualServingOverlap() => StartOrderMode(6);

    private static void InitializeServiceMemory(GameObject services)
    {
        var data = Service<StaffDataManager>(services); Static(typeof(StaffDataManager),"_instance",data); CallStatic(typeof(StaffDataManager),"Init");
        serviceData = data.GetStaffData("STAFF01");
        var stage = ((StageInfo[])Field(typeof(UserInfo),"_stageInfos").GetValue(null))[0];
        Get<Dictionary<string,SaveStaffData>>(stage,"_giveStaffDic")[serviceData.Id] = new SaveStaffData(serviceData.Id,1);
        var equipment = Get<Dictionary<ERestaurantFloorType,Dictionary<EquipStaffType,StaffData>>>(stage,"_equipStaffTypeDic");
        equipment[ERestaurantFloorType.Floor1][EquipStaffType.Waiter] = serviceData;
        equipment[ERestaurantFloorType.Floor2][EquipStaffType.Waiter] = serviceData;
        Set(GameManager.Instance,"_addGachaItemStaffSpeedMulDic",Enum.GetValues(typeof(StaffGroupType)).Cast<StaffGroupType>().ToDictionary(t=>t,t=>0f));
        var furnishing = Service<FurnitureDataManager>(services); Static(typeof(FurnitureDataManager),"_instance",furnishing); CallStatic(typeof(FurnitureDataManager),"InitData");
        // Existing table art and authored upper hall position exercise the original stairs.
        var tables = furnishing.GetFurnitureDataList(FurnitureType.Table3);
        var art = tables.First(d=>d.Id.StartsWith("TABLE01"));
        var equipped = Get<FurnitureData[,]>(stage,"_equipFurnitureDatas");
        equipped[0,(int)FurnitureType.Table3]=art; equipped[1,(int)FurnitureType.Table3]=art;
        var savedTables=Get<SaveTableData[,]>(stage,"_saveTableDatas");
        savedTables[0,(int)TableType.Table1].SetNeedCleaning(true);
        savedTables[1,(int)TableType.Table1].SetNeedCleaning(true);
    }

    private static void InitializeServiceStaff(Scene scene, GameObject services)
    {
        CallStatic(typeof(ObjectPoolManager),"StaffPooling");
        serviceStaff = Find<StaffController>(scene);
        Set(serviceStaff,"_customerController",controller); Set(serviceStaff,"_tableManager",manager);
        Set(serviceStaff,"_kitchenSystem",Find<KitchenSystem>(scene)); Set(serviceStaff,"_feverSystem",IsCompletionPreview ? Get<FeverSystem>(manager,"_feverSystem") : Service<FeverSystem>(services));
        Set(serviceStaff,"_staffGroups",Get<StaffGroup[]>(serviceStaff,"_staffGroups").Where(g=>(int)g.FloorType<2).ToArray());
        Require(controller.MarketerSkillEffect != null,"Authored marketer dependency must survive isolation");
        Require(!Get<UIAddCutomerController>(controller,"_uiController").gameObject.activeInHierarchy,"Customer-call UI must remain inactive");
        serviceStaff.enabled=true; serviceStaff.gameObject.SetActive(true);
        Require(Get<List<StaffGroup>>(serviceStaff,"_staffGroupList").Count==2,"Both real StaffGroups must initialize completely");
        foreach(var group in Get<StaffGroup[]>(serviceStaff,"_staffGroups"))
            Require(Get<List<Staff>>(group,"_staffList").Count==(int)EquipStaffType.Length,"Every production staff slot must initialize");
        serviceResults.Clear();
    }
    private static void ResetServiceObservation()
    {
        observedBatch=null; observedTable=null; completedAt=0; serviceCancelled=false; serviceRestored=false;
        cancelledFood=null; cancelledIdentity=null; orderReservations.Clear(); waiterStairsObserved=false; firstMealWaited=false; parallelMealObserved=false;
        manualWaiterReleased=false; completedWhileEating=false; deliveredWhileEating=false; partnerEatingAfterFirst=false;
        serviceWaiter=Object.FindObjectsByType<StaffWaiter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .First(w=>w.StaffData!=null && (int)w.EquipFloorType==floor);
        initialCookCount=UserInfo.TotalCookCount;
    }
    private static void TickService(double now)
    {
        if(phase<6 && now-began>195) throw new TimeoutException("Service evidence must complete while recording is active");
        if(serviceWaiter!=null && Get<Coroutine>(serviceWaiter,"_teleportCoroutine")!=null) waiterStairsObserved=true;
        if(phase==4 && observedBatch==null)
        {
            observedTable=party.CurrentTable; observedBatch=observedTable.PartyOrders;
            Require(observedBatch!=null && observedBatch.Orders.Count==2,"Two production menus required");
            Require(observedTable.TableType==TableType.Table3,"The authored upper-hall table must be used");
            SaveFrame("floor"+(floor+1)+"-menus.png");
        }
        if(observedBatch!=null)
        {
            camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(3,observedTable.transform.position.y-1,-20),.08f);
            camera.orthographicSize=14;
            var reservation=observedTable.CurrentUse.OrderReservation;
            if(reservation!=null) orderReservations.Add(reservation);
            if(OrderMode==6) TickManualServingOverlap();
            firstMealWaited |= observedBatch.Orders.Any(o=>o.Identity.EatingCompleted) && observedBatch.Orders.Any(o=>!o.Identity.EatingCompleted);
            parallelMealObserved |= observedBatch.Orders.Any(o=>o.Identity.EatingAnimationStarted && !o.Identity.EatingCompleted)
                && observedBatch.Orders.Any(o=>!o.Identity.EatingAnimationStarted);
            observations.Add("SERVICE,"+now.ToString("F3")+","+floor+","+serviceWaiter.transform.position.ToString("F3")+","+OrderStatus);
            if(OrderMode==5 && !serviceCancelled && cancelledFood==null && observedBatch.Orders.Any(o=>o.State==PartyFoodState.Carrying))
            {
                cancelledFood=observedBatch.Orders.First(o=>o.State==PartyFoodState.Carrying); cancelledIdentity=cancelledFood.Identity;
                carryStartedPosition=serviceWaiter.transform.position; carryStartedAt=now;
            }
            if(OrderMode==5 && !serviceCancelled && cancelledFood!=null && cancelledFood.State==PartyFoodState.Carrying
                && now-carryStartedAt>.5 && Vector3.Distance(carryStartedPosition,serviceWaiter.transform.position)>2)
            {
                serviceCancelled=true; cancellationTime=now;
                SaveFrame("floor"+(floor+1)+"-carrying-before-cancel.png");
                serviceWaiter.SetStaffData(null,(ERestaurantFloorType)floor);
                Require(cancelledFood.State==PartyFoodState.ReadyForServing && ReferenceEquals(cancelledIdentity,cancelledFood.Identity),"Cancellation must restore the same cooked order");
                SaveFrame("floor"+(floor+1)+"-cancelled-carry.png");
            }
            if(serviceCancelled && !serviceRestored && now-cancellationTime>2)
            {
                serviceRestored=true; serviceWaiter.SetStaffData(serviceData,(ERestaurantFloorType)floor);
            }
            if(phase==4 && observedTable.TableState==ETableState.PartyCurrentMealsCompleted)
            {
                Require(observedBatch.Orders.All(o=>o.Identity.EatingCompleted),"Every current meal must finish once");
                Require(UserInfo.TotalCookCount-initialCookCount==2,"Exactly two cooked foods per party");
                Require(UserInfo.TotalCumulativeCustomerCount==0 && UserInfo.GetSatisfaction(EStage.Stage1)==0,"No 3C rewards");
                Require(mouseClicks==(OrderMode==6 ? (floor+1)*2 : 0),"Unexpected pointer input count");
                if(OrderMode==5) Require(serviceCancelled && serviceRestored,"Carry cancellation was not observed");
                Require(orderReservations.Count==1,"One whole-batch order reservation/visit required");
                Require(waiterStairsObserved,"Actual waiter stair coroutine must run");
                Require(firstMealWaited,"First completed customer must wait for the partner");
                if(OrderMode==5) Require(ReferenceEquals(cancelledIdentity,cancelledFood.Identity) && cancelledFood.Identity.EatingCompleted,"The cancelled food must be delivered exactly once");
                if(OrderMode==6) Require(completedWhileEating && deliveredWhileEating && partnerEatingAfterFirst,"All real-time meal overlap transitions must be observed");
                serviceResults.Add(new { floor, table=observedTable.TableType.ToString(), orderVisits=orderReservations.Count,
                    waiterStairsObserved, firstMealWaited, parallelMealObserved, serviceCancelled, serviceRestored,
                    completedWhileEating, deliveredWhileEating, partnerEatingAfterFirst,
                    cooked=UserInfo.TotalCookCount-initialCookCount, occupants=observedTable.OccupantCount,
                    remaining=observedBatch.Orders.Select(o=>o.RemainingAdditionalOrders).ToArray() });
                completedAt=now; phase=6; SaveFrame("floor"+(floor+1)+"-meals-completed.png");
            }
        }
        if(phase==6 && now-completedAt>4) { recorder?.Stop(); phase=7; }
        if(phase!=7 || (recorder!=null && !recorder.IsFinished)) return;
        recorder?.PollCompletion(); recorder=null;
        string stem=floor==0?"floor1-service":"vip-service";
        var video=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(output,stem+".json")));
        Require((int)video["encoderExitCode"]==0 && string.IsNullOrEmpty((string)video["error"]),"Video encoding failed");
        manager.CancelPartyGuide(observedTable);
        foreach(var waiting in controller.WaitingParties.ToArray()) controller.CancelWaitingParty(waiting);
        if(floor==0)
        {
            observedBatch=null; observedTable=null; serviceWaiter=null;
            floor=1; phase=0; began=now; camera.transform.position=new Vector3(3,12,-20);
        }
        else
        {
            File.WriteAllLines(Path.Combine(output,"actual-service-playerloop.csv"),observations);
            File.WriteAllText(Path.Combine(output,"completed.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {
                mode=OrderMode, floors=2, clicks=mouseClicks, timeScale=Time.timeScale, cooked=UserInfo.TotalCookCount,
                backendInitialized=BackEnd.Backend.IsInitialized, visits=UserInfo.TotalCumulativeCustomerCount,
                boundary="Current meals completed; additional rounds and settlement remain Phase3C", results=serviceResults,
                input=OrderMode==6 ? "Timed Editor EventSystem pointer replay through authored ServingButton; not native OS clicks" : "No order or serving pointer input"
            },Newtonsoft.Json.Formatting.Indented));
            Stop();
        }
    }
    private static void TickManualServingOverlap()
    {
        if(observedBatch.State!=PartyOrderBatchState.Accepted) return;
        if(!manualWaiterReleased)
        {
            serviceWaiter.SetStaffData(null,(ERestaurantFloorType)floor); manualWaiterReleased=true;
        }
        var a=observedBatch.Orders[0]; var b=observedBatch.Orders[1];
        bool aEating=a.Identity.EatingAnimationStarted && !a.Identity.EatingCompleted;
        completedWhileEating |= aEating && b.Identity.CookingCompleted;
        deliveredWhileEating |= aEating && b.Identity.ServingStarted;
        partnerEatingAfterFirst |= a.Identity.EatingCompleted && b.Identity.EatingAnimationStarted && !b.Identity.EatingCompleted;
        var kitchen=Get<KitchenUtensilGroup[]>(Find<KitchenSystem>(SceneManager.GetActiveScene()),"_kitchenUtensilGroups")
            .First(g=>(int)g.FloorType==floor);
        var burner=Get<KitchenBurnerData[]>(kitchen,"_burnerDatas").FirstOrDefault(x=>ReferenceEquals(x.CookingData.Identity,b.Identity));
        // Input timing only: the existing kitchen clock, movement, orders and meals are never advanced here.
        if(a.State==PartyFoodState.ReadyForServing && burner!=null && burner.Time>0 && burner.Time<=1.2f)
            ReplayServingPointer(observedTable.ServingButton);
        if(b.State==PartyFoodState.ReadyForServing && aEating)
        {
            SaveFrame("floor"+(floor+1)+"-a-eating-b-ready.png");
            ReplayServingPointer(observedTable.CompanionServingButton);
        }
        if(partnerEatingAfterFirst && !File.Exists(Path.Combine(output,"floor"+(floor+1)+"-a-waiting-b-eating.png")))
            SaveFrame("floor"+(floor+1)+"-a-waiting-b-eating.png");
    }
    private static void ReplayServingPointer(TableButton button)
    {
        Require(button!=null && button.gameObject.activeInHierarchy,"Authored serving button must be visible");
        Canvas.ForceUpdateCanvases();
        Vector2 point=orderCamera.WorldToScreenPoint(button.transform.position);
        var hit=RouteOrderPointer(point,null,true);
        Require(hit!=null && hit.GetComponentInParent<TableButton>()==button,"Pointer raycast must hit the intended authored button");
        RouteOrderPointer(point,hit,false);
        observations.Add("SCRIPTED_SERVING_POINTER,"+Time.frameCount+","+Time.time.ToString("F3"));
    }
    private static void CleanupService()
    {
        if(serviceStaff==null) return;
        serviceStaff.enabled=false;
        foreach(var worker in Object.FindObjectsByType<StaffWaiter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(worker.StaffData!=null) worker.SetStaffData(null,worker.EquipFloorType);
        serviceStaff=null; serviceWaiter=null;
    }
}
#endif
