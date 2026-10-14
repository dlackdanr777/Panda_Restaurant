#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static partial class CustomerPartyStage1Preview
{
    private static bool IsCompletionPreview => OrderMode >= 7;
    private static TableUseIdentity completionUse;
    private static bool personalWaitObserved, nextRoundObserved, departureObserved, dropsAnimated;
    private static double departureAt, exitedAt, cleanedAt, sinkAt;
    private static long[] fixedCoins;
    private static int baselineBowls, baselineVisits, observedRound;
    private static CustomerVisitParty nextParty;
    private static Vector3 firstCoinPosition;
    private static PointerDownSpriteRenderer firstCoin;
    private static TextMesh completionEvidence;

    // Evidence-only readout in the existing isolated runner, never production UI.
    private static void DrawCompletionEvidence()
    {
        if (completionEvidence == null)
        {
            var label = new GameObject("Owned evidence readout");
            label.transform.SetParent(camera.transform, false);
            completionEvidence = label.AddComponent<TextMesh>();
            completionEvidence.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            completionEvidence.GetComponent<Renderer>().sharedMaterial = completionEvidence.font.material;
            completionEvidence.GetComponent<Renderer>().sortingLayerName = "UI";
            completionEvidence.GetComponent<Renderer>().sortingOrder = 30000;
            completionEvidence.fontSize = 36; completionEvidence.color = Color.yellow;
            completionEvidence.anchor = TextAnchor.UpperLeft;
        }
        float height = camera.orthographicSize;
        completionEvidence.characterSize = height * .012f;
        completionEvidence.transform.localPosition = new Vector3(-height * camera.aspect + .3f, height - .2f, 1);
        var table = observedTable;
        completionEvidence.text = "ISOLATED EVIDENCE | " + (floor == 0 ? "Floor 1" : "VIP") + " | 1x time | round " + observedRound
            + "\nPersonal final " + (completionUse == null ? 0 : completionUse.Completions.Count(r => r.FinalCompleted)) + "/2"
            + " | Coins " + (table == null ? "0/0" : string.Join("/", table.DropCoinAreas.Select(c => c.CurrentMoney)))
            + " | Trash " + (table == null ? 0 : table.DropGarbageArea.Count)
            + " | Sink bowls " + UserInfo.GetSinkBowlCount(EStage.Stage1, (ERestaurantFloorType)floor)
            + "\n" + (table == null ? "Entering / guiding" : table.TableState.ToString())
            + " | Individual exits " + (completionUse == null ? 0 : completionUse.Completions.Count(r => r.Returned)) + "/2";
    }

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3C complete visit (owned copy only)")]
    public static void StartCompleteVisit() => StartOrderMode(7);
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3C one additional order (owned copy only)")]
    public static void StartAdditionalVisit() => StartOrderMode(8);
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Open existing isolated Phase3C project")]
    public static void OpenExistingCompletionProject()
    {
        string path=EditorUtility.OpenFolderPanel("Select the existing owned party preview project","","");
        if(string.IsNullOrEmpty(path)) return;
        if(!File.Exists(Path.Combine(path,Marker)) || Directory.Exists(Path.Combine(path,".git")) || File.Exists(Path.Combine(path,".git")))
            throw new InvalidOperationException("Select the existing owned preview project.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(EditorApplication.applicationPath,
            "-projectPath \""+path+"\" -executeMethod CustomerPartyStage1Preview.StartCompleteVisit") { UseShellExecute=false });
    }

    private static void PrepareCompletionParty()
    {
        completionUse=null; personalWaitObserved=false; nextRoundObserved=false; departureObserved=false; dropsAnimated=false;
        departureAt=exitedAt=cleanedAt=sinkAt=0; nextParty=null; firstCoin=null; observedRound=0;
        baselineBowls=UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor);
        baselineVisits=UserInfo.TotalCumulativeCustomerCount;
        if(OrderMode!=8) return;
        // Invoke the shipped +1 order effect once, in a reproducible isolated success
        // condition. Never overwrite OrderCount, menu candidates or movement state.
        var skill=Resources.Load<CustomerOrderCountUpSkill>("CustomerData/Skill/CustomerType06");
        Require(skill!=null,"Existing additional-order effect is required");
        var random=UnityEngine.Random.state;
        int seed=0;
        for(;seed<1000;seed++)
        {
            UnityEngine.Random.InitState(seed);
            if(UnityEngine.Random.Range(0f,100f)<skill.SkillActivatePercent) break;
        }
        UnityEngine.Random.InitState(seed); skill.Activate(party.Members[1].Customer); UnityEngine.Random.state=random;
        Require(party.Members[0].Customer.OrderCount==1 && party.Members[1].Customer.OrderCount==2,"Existing effect must produce 1/2 orders");
        observations.Add("EXISTING_ORDER_EFFECT,CustomerType06,member=1,seed="+seed);
    }

    private static void TickCompletion(double now)
    {
        if(phase<6 && now-began>195) throw new TimeoutException("Normal visit evidence exceeded its recording budget");
        if(serviceWaiter!=null && Get<Coroutine>(serviceWaiter,"_teleportCoroutine")!=null) waiterStairsObserved=true;
        if(phase==4 && completionUse==null)
        {
            observedTable=party.CurrentTable; completionUse=observedTable.CurrentUse;
            Require(observedTable.TableType==TableType.Table3,"Authored upper table is required");
        }
        if(completionUse==null) return;
        if(completionUse.OrderReservation!=null) orderReservations.Add(completionUse.OrderReservation);
        observedBatch=completionUse.Orders;
        if(observedRound!=observedBatch.Round)
        {
            observedRound=observedBatch.Round;
            if(observedRound>1)
            {
                Require(OrderMode==8 && observedBatch.Orders.Count==1 && observedBatch.Orders[0].SeatIndex==1,"Only B may order in round two");
                Require(observedTable.OccupantCount==2,"Additional round must retain party occupancy");
                nextRoundObserved=true;
            }
            SaveFrame("floor"+(floor+1)+"-round"+observedRound+".png");
        }
        if(!departureObserved)
        {
            camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(3,observedTable.transform.position.y-1,-20),.08f);
            camera.orthographicSize=14;
        }
        var results=completionUse.Completions;
        if(!personalWaitObserved && results.Count(r=>r.FinalCompleted)==1 && !completionUse.CompletionStarted)
        {
            Require(observedTable.DropCoinAreas.Sum(c=>c.CurrentMoney)==0 && observedTable.DropGarbageArea.Count==0,"No early coin or garbage");
            Require(UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor)==baselineBowls,"No early sink bowl");
            Require(observedTable.TableState!=ETableState.NeedCleaning,"No early cleaning transition");
            personalWaitObserved=true; SaveFrame("floor"+(floor+1)+"-personal-wait-no-drops.png");
        }
        if(completionUse.CompletionStarted && !departureObserved)
        {
            Require(personalWaitObserved,"Personal final completion must precede the partner");
            Require(results.All(r=>r.FinalCompleted&&r.CoinFixed&&r.CoinDropped),"Both personal coin results must commit at joint departure");
            Require(observedTable.TableState==ETableState.NeedCleaning,"Joint departure starts cleaning cycle");
            Require(UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor)==baselineBowls+1,"One table bowl at departure");
            Require(results.All(r=>!r.Returned),"Drops must precede reaching the exit");
            Require(results.Select(r=>(long)r.CoinAmount).SequenceEqual(observedTable.DropCoinAreas.Select(c=>c.CurrentMoney)),"Each seat owns its captured amount");
            fixedCoins=results.Select(r=>(long)r.CoinAmount).ToArray();
            departureObserved=true; departureAt=now; SaveFrame("floor"+(floor+1)+"-joint-stand-drops.png");
            Require(controller.TrySpawnWaitingParty(new[]{"CUSTOMER04"},out nextParty),"Next single party reservation failed");
            firstCoin=Get<System.Collections.Generic.List<PointerDownSpriteRenderer>>(observedTable.DropCoinAreas[0],"_coinList").Single();
            Require(firstCoin.gameObject.activeInHierarchy && firstCoin.SpriteRenderer.sprite!=null,"Real visible coin prefab is required");
            firstCoinPosition=firstCoin.transform.position;
        }
        if(departureObserved && !dropsAnimated && now-departureAt>.6)
        {
            Require(firstCoin!=null && Vector3.Distance(firstCoinPosition,firstCoin.transform.position)>.1f,"Actual coin Tween must advance");
            Require(observedTable.DropGarbageArea.Count>=2 && observedTable.DropGarbageArea.Count<=4,"Existing per-person garbage count required");
            foreach(var garbage in Get<System.Collections.Generic.List<PointerDownSpriteRenderer>>(observedTable.DropGarbageArea,"_garbageList"))
                Require(garbage.gameObject.activeInHierarchy && garbage.SpriteRenderer.sprite!=null,"Real garbage must render");
            dropsAnimated=true; SaveFrame("floor"+(floor+1)+"-coin-garbage-tweens.png");
        }
        if(departureObserved && !completionUse.SettlementCompleted && now-departureAt>1)
        {
            var active=results.Where(r=>!r.Returned).Select(r=>r.Member.Customer.transform.position).ToArray();
            if(active.Length>0)
                camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(active.Average(p=>p.x),active.Average(p=>p.y)+3,-20),.06f);
        }
        if(completionUse.SettlementCompleted && exitedAt==0)
        {
            exitedAt=now; serviceWaiter.SetStaffData(null,(ERestaurantFloorType)floor);
            Require(results.All(r=>r.Returned),"Each customer must finish its own exit");
            Require(UserInfo.TotalCumulativeCustomerCount-baselineVisits==2,"Exactly two personal visits");
            Require(UserInfo.TotalCookCount-initialCookCount==(OrderMode==8?3:2),"Each accepted food cooks once");
            Require(orderReservations.Count==(OrderMode==8?2:1),"One waiter visit per order round");
            Require(waiterStairsObserved&&dropsAnimated,"Actual stairs and drop motion required");
            Require(string.IsNullOrEmpty(observedTable.LastServiceDiagnostic),observedTable.LastServiceDiagnostic);
            SaveFrame("floor"+(floor+1)+"-both-exited.png");
        }
        if(exitedAt>0 && cleanedAt==0)
        {
            camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(3,observedTable.transform.position.y-1,-20),.12f);
            if(now-exitedAt>3)
            {
                var pointer=Get<PointerDownSpriteRenderer>(observedTable.TableFurniture,"_pointerDownSpriteRenderer");
                pointer.OnPointerDown(new PointerEventData(orderEvents));
                Require(observedTable.TableState==ETableState.Empty,"Actual manual clean must complete");
                Require(UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor)==baselineBowls+1,"Cleaning cannot duplicate the dish");
                cleanedAt=now; SaveFrame("floor"+(floor+1)+"-cleaned-no-extra-bowl.png");
            }
        }
        if(cleanedAt>0 && sinkAt==0 && now-cleanedAt>2)
        {
            var kitchen=Get<KitchenUtensilGroup[]>(Find<KitchenSystem>(SceneManager.GetActiveScene()),"_kitchenUtensilGroups").First(g=>(int)g.FloorType==floor);
            var sink=kitchen.GetSinkKitchenUtensil();
            var gauge=Get<SinkGaugeBar>(sink,"_sinkGaugeBar");
            camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(gauge.transform.position.x,gauge.transform.position.y-2,-20),.12f);
            camera.orthographicSize=6;
            Require(Get<TMPro.TextMeshPro>(gauge,"_tmp").text.StartsWith((baselineBowls+1)+"/"),"Original sink gauge must display the actual bowl count");
            if(now-cleanedAt>6) { sinkAt=now; SaveFrame("floor"+(floor+1)+"-actual-sink-one-bowl.png"); }
        }
        if(sinkAt>0 && phase==4 && now-sinkAt>2)
        {
            Require(controller.FirstReadyParty==nextParty,"Next single must actually arrive at queue");
            Require(manager.OnCustomerGuideEvent((ERestaurantFloorType)floor),"Next use guide failed"); phase=5;
        }
        if(phase==5)
        {
            camera.transform.position=Vector3.Lerp(camera.transform.position,new Vector3(3,observedTable.transform.position.y-1,-20),.1f); camera.orthographicSize=14;
            if(nextParty.CurrentTable?.CurrentUse?.Seated==true)
            {
                Require(ReferenceEquals(nextParty.CurrentTable,observedTable),"The cleaned table must accept the next visit");
                serviceResults.Add(new {floor, roundCount=observedRound, orderVisits=orderReservations.Count, personalWaitObserved,nextRoundObserved,
                    departureObserved,dropsAnimated, fixedCoins, garbage=results.Select(r=>r.GarbageCount).ToArray(),
                    bowls=UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor)-baselineBowls,
                    cooked=UserInfo.TotalCookCount-initialCookCount, visits=UserInfo.TotalCumulativeCustomerCount-baselineVisits,
                    actualIndividualExits=results.Count(r=>r.Returned), nextSingleSeated=true, diagnostic=observedTable.LastServiceDiagnostic});
                completedAt=now; phase=6; SaveFrame("floor"+(floor+1)+"-next-single-seated.png");
            }
        }
        observations.Add("COMPLETION,"+now.ToString("F3")+","+floor+","+phase+",round="+observedRound+",final="+results.Count(r=>r.FinalCompleted)+",coins="+string.Join("/",observedTable.DropCoinAreas.Select(c=>c.CurrentMoney))+",garbage="+observedTable.DropGarbageArea.Count+",bowls="+UserInfo.GetSinkBowlCount(EStage.Stage1,(ERestaurantFloorType)floor)+",state="+observedTable.TableState);
        if(phase==6 && now-completedAt>2) {recorder.Stop();phase=7;}
        if(phase!=7 || !recorder.IsFinished) return;
        recorder.PollCompletion();recorder=null;
        var video=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(output,floor==0?"floor1-service.json":"vip-service.json")));
        Require((int)video["encoderExitCode"]==0 && string.IsNullOrEmpty((string)video["error"]),"Encoding failed");
        if (nextParty?.CurrentTable != null)
        {
            var nextTable = nextParty.CurrentTable;
            var nextCustomer = nextParty.Members[0].Customer;
            nextTable.EndUse(); nextTable.CurrentCustomer = null;
            nextTable.OrderButton.gameObject.SetActive(false); nextTable.ServingButton.gameObject.SetActive(false);
            nextCustomer.StopWaitingForFood(); nextCustomer.StopMove();
            ObjectPoolManager.Instance.DespawnNormalCustomer(nextCustomer);
        }
        foreach(var waiting in controller.WaitingParties.ToArray()) controller.CancelWaitingParty(waiting);
        if(floor==0) {floor=1;phase=0;began=now;observedTable=null;observedBatch=null;completionUse=null;}
        else
        {
            File.WriteAllLines(Path.Combine(output,"actual-completion-playerloop.csv"),observations);
            File.WriteAllText(Path.Combine(output,"completed.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{
                mode=OrderMode,floors=serviceResults.Count,timeScale=Time.timeScale,backendInitialized=BackEnd.Backend.IsInitialized,results=serviceResults,
                input="Existing table pointerDown replay for cleaning; no order/serving clicks; no native OS clicks",
                extraOrder=OrderMode==8?"Existing CustomerType06 effect activated once on member B with isolated success seed":"None",
                boundary="Fixed party normal visit completed; natural occurrence and mid-visit failure policy remain disabled"
            },Newtonsoft.Json.Formatting.Indented));
            Stop();
        }
    }
}
#endif
