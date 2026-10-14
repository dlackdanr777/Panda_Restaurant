using Muks.PathFinding.AStar;
using Muks.Tween;
using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ManagerCustomerGuideSource { Action, Skill }

// A last-request snapshot is available to a debugger or an isolated capture without per-tick logging.
public struct ManagerCustomerGuideDiagnostic
{
    public string StaffId;
    public ERestaurantFloorType EquipFloor;
    public string SavedManagerId;
    public ERestaurantFloorType RequestedFloor;
    public ERestaurantFloorType SelectedFloor;
    public ManagerCustomerGuideSource Source;
    public string Result;
}

public partial class TableManager : MonoBehaviour
{
    public event Action OnTableUpdateHandler;

    private object _executionIdentity;

    private void OnEnable()
    {
        _executionIdentity = new object();
        LoadingSceneManager.OnLoadSceneHandler += InvalidateExecution;
    }

    private void InvalidateExecution()
    {
        _executionIdentity = null;
        if (_customerController != null) _customerController.EndVisitParties();
    }

    private void OnDisable()
    {
        InvalidateExecution();
        LoadingSceneManager.OnLoadSceneHandler -= InvalidateExecution;
    }

    internal bool IsExecutionCurrent(object identity) => identity != null
        && ReferenceEquals(identity, _executionIdentity);

    private static Action GuardExit(TableUseIdentity use, Action complete)
    {
        bool completed = false;
        return () =>
        {
            if (completed || !use.IsCustomerContextCurrent) return;
            completed = true;
            complete();
        };
    }

    private readonly Dictionary<ERestaurantFloorType, ManagerRuntime> _managerRuntimes =
        new Dictionary<ERestaurantFloorType, ManagerRuntime>();
    public ManagerCustomerGuideDiagnostic LastManagerGuideDiagnostic { get; private set; }

    private struct ManagerRuntime
    {
        public Staff Staff;
        public StaffData Data;
        public EStage Stage;
    }

    // Only the owning floor's StaffGroup binds this identity. Matching a Staff ID alone
    // would let a pooled/replaced instance continue to guide customers after a move.
    internal void RegisterManagerRuntime(ERestaurantFloorType floor, Staff staff)
    {
        if (staff == null || staff.StaffData == null || staff.EquipFloorType != floor
            || staff.EquipStaffType != EquipStaffType.Manager)
            return;
        _managerRuntimes[floor] = new ManagerRuntime
        {
            Staff = staff, Data = staff.StaffData, Stage = UserInfo.CurrentStage
        };
    }

    internal void UnregisterManagerRuntime(ERestaurantFloorType floor, Staff staff)
    {
        if (_managerRuntimes.TryGetValue(floor, out ManagerRuntime current) && current.Staff == staff)
            _managerRuntimes.Remove(floor);
    }

    public bool OnManagerCustomerGuideEvent(Staff manager, ManagerCustomerGuideSource source, bool playSound = false)
    {
        if (!TrySelectManagerGuideTable(manager, source, out TableData table))
            return false;
        if (_customerController.IsEmpty())
            return CompleteManagerGuideDiagnostic("NoWaitingCustomer");
        NormalCustomer customer = _customerController.GetFirstCustomer();
        if (customer == null)
            return CompleteManagerGuideDiagnostic("NoWaitingCustomer");

        if (!OnCustomerGuide(customer, table))
            return CompleteManagerGuideDiagnostic(_customerController.LastVisitDiagnostic);
        CompleteManagerGuideDiagnostic("Guided");
        if (playSound)
        {
            EffectType effect = SoundManager.Instance.GetHallEffectType(table.FloorType, RestaurantType.Hall);
            SoundManager.Instance.PlayEffectAudio(effect, _callSound);
        }
        return true;
    }

    private bool TrySelectManagerGuideTable(Staff manager, ManagerCustomerGuideSource source, out TableData table)
    {
        table = null;
        ERestaurantFloorType floor = manager != null ? manager.EquipFloorType : ERestaurantFloorType.Error;
        LastManagerGuideDiagnostic = new ManagerCustomerGuideDiagnostic
        {
            StaffId = manager != null && manager.StaffData != null ? manager.StaffData.Id : null,
            EquipFloor = floor, RequestedFloor = floor, SelectedFloor = ERestaurantFloorType.Error,
            Source = source
        };
        if (manager == null || !manager.isActiveAndEnabled || manager.StaffData == null
            || manager.EquipStaffType != EquipStaffType.Manager
            || floor < ERestaurantFloorType.Floor1 || floor >= ERestaurantFloorType.Length)
            return CompleteManagerGuideDiagnostic("InactiveOrInvalidManager");

        StaffData saved = UserInfo.GetEquipStaff(UserInfo.CurrentStage, floor, EquipStaffType.Manager);
        ManagerCustomerGuideDiagnostic diagnostic = LastManagerGuideDiagnostic;
        diagnostic.SavedManagerId = saved != null ? saved.Id : null;
        LastManagerGuideDiagnostic = diagnostic;
        if (saved == null || !string.Equals(saved.Id, manager.StaffData.Id, StringComparison.Ordinal))
            return CompleteManagerGuideDiagnostic("AssignmentChanged");
        if (!_managerRuntimes.TryGetValue(floor, out ManagerRuntime current)
            || current.Staff != manager || current.Data != manager.StaffData || current.Stage != UserInfo.CurrentStage)
            return CompleteManagerGuideDiagnostic("StaleRuntime");
        if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, floor))
            return CompleteManagerGuideDiagnostic("FloorLocked");
        // Manual guide requests retain their tutorial redirect. An automatic Floor2
        // request is rejected instead of redirecting it to a floor with no manager.
        if (RestrictFirstTutorialCustomersToFloor1 && floor != ERestaurantFloorType.Floor1)
            return CompleteManagerGuideDiagnostic("TutorialFloorRestricted");

        table = GetTableType(floor, ETableState.Empty);
        if (table == null)
            return CompleteManagerGuideDiagnostic("NoTableOnAssignedFloor");
        diagnostic = LastManagerGuideDiagnostic;
        diagnostic.SelectedFloor = table.FloorType;
        LastManagerGuideDiagnostic = diagnostic;
        if (table.FloorType != floor)
        {
            table = null;
            return CompleteManagerGuideDiagnostic("TableFloorMismatch");
        }
        CompleteManagerGuideDiagnostic("Selected");
        return true;
    }

    private bool CompleteManagerGuideDiagnostic(string result)
    {
        ManagerCustomerGuideDiagnostic diagnostic = LastManagerGuideDiagnostic;
        diagnostic.Result = result;
        LastManagerGuideDiagnostic = diagnostic;
        return false;
    }

    [Header("Transform")]
    [SerializeField] private Transform _moneyUITr;

    [Space]
    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private KitchenSystem _kitchenSystem;
    [SerializeField] private SatisfactionSystem _satisfactionSystem;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private FeverSystem _feverSystem;


    [Space]
    [Header("Button Option")]
    [SerializeField] private Button _guideButton;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _callSound;



    public Vector3 GetDoorPos(RestaurantType type, Vector3 pos)
    {
        if (type == RestaurantType.Kitchen)
            return _kitchenSystem.GetDoorPos(pos);

        else
            return _furnitureSystem.GetDoorPos(pos);
    }


    private void Start()
    {
        Init();
        UpdateTable();
    }


    private void Init()
    {
        _guideButton.onClick.AddListener(() => OnCustomerGuideEventPlaySound(-1));

        // 벨을 누른 순간이 아닌 로딩 단계에서 미리 오디오 데이터를 올려 재생 지연을 없앤다
        SoundManager.Instance.PreloadAudioClip(_callSound);

        UpdateTable();
        _customerController.OnChangeCustomerHandler += UpdateTable;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
        UserInfo.OnChangeFurnitureHandler += OnChangeTableEvent;
        UserInfo.OnChangeFloorHandler += UpdateTable;
    }


    public bool OnCustomerGuideEvent(int sitPos = -1)
    {
        return GuideFirstCustomer(null, sitPos, false);
    }


    public bool OnCustomerGuideEvent(ERestaurantFloorType choiceFloor, int sitPos = -1)
    {
        return GuideFirstCustomer(choiceFloor, sitPos, false);
    }

    public bool OnManagerCustomerGuideEvent(Staff staff, int sitPos = -1)
    {
        if (!IsCurrentEquippedManager(staff))
            return false;

        ERestaurantFloorType floor = staff.EquipFloorType;
        if (RestrictFirstTutorialCustomersToFloor1 && floor != ERestaurantFloorType.Floor1)
            return false;

        return GuideFirstCustomer(floor, sitPos, true);
    }

    private bool GuideFirstCustomer(ERestaurantFloorType? requestedFloor, int sitPos, bool strictRequestedFloor)
    {
        if (_customerController.IsEmpty())
            return false;

        NormalCustomer customer = _customerController.GetFirstCustomer();
        if (customer == null)
        {
            DebugLog.LogError("손님 정보가 없습니다.");
            return false;
        }

        if (_customerController.FirstReadyParty == null) return false;

        if (!TrySelectCustomerGuideTable(customer, requestedFloor, out ERestaurantFloorType choiceFloor,
            out TableData data, strictRequestedFloor))
        {
            DebugLog.Log("남는 테이블이 없습니다.");
            UpdateTable();
            return false;
        }

        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return false;
        }

        sitPos = Mathf.Clamp(sitPos, -1, 1);
        return OnCustomerGuide(customer, data, sitPos);
    }

    private bool IsCurrentEquippedManager(Staff staff)
    {
        return staff != null
            && staff.EquipStaffType == EquipStaffType.Manager
            && staff.StaffData != null
            && UserInfo.GetEquipStaff(UserInfo.CurrentStage, staff.EquipFloorType, EquipStaffType.Manager) == staff.StaffData;
    }


    public void OnCustomerGuideEventPlaySound(int sitPos = -1)
    {
        if (OnCustomerGuideEvent(sitPos))
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.Hall1, _callSound);
        }
            
    }

    public void OnCustomerGuideEventPlaySound(ERestaurantFloorType floor, int sitPos = -1)
    {
        if (RestrictFirstTutorialCustomersToFloor1)
            floor = ERestaurantFloorType.Floor1;
        EffectType effectType = SoundManager.Instance.GetHallEffectType(floor, RestaurantType.Hall);
        if (OnCustomerGuideEvent(floor, sitPos))
            SoundManager.Instance.PlayEffectAudio(effectType, _callSound);
    }

    public void OnManagerCustomerGuideEventPlaySound(Staff staff, int sitPos = -1)
    {
        if (OnManagerCustomerGuideEvent(staff, sitPos))
        {
            EffectType effectType = SoundManager.Instance.GetHallEffectType(staff.EquipFloorType, RestaurantType.Hall);
            SoundManager.Instance.PlayEffectAudio(effectType, _callSound);
        }
    }

    public bool OnCustomerGuideEventPlayUISound(int sitPos = -1)
    {
        if (!OnCustomerGuideEvent(sitPos))
            return false;

        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _callSound);
        return true;
    }

    // Keep the tutorial's actual table and chair destination on Floor1, not just its UI.
    // Completion/Skip clears this scope; saved floor unlocks are never changed here.
    private static bool RestrictFirstTutorialCustomersToFloor1 =>
        UserInfo.CurrentStage == EStage.Stage1 && UserInfo.IsTutorialStart && !UserInfo.IsFirstTutorialClear;

    private bool TrySelectCustomerGuideTable(NormalCustomer customer, ERestaurantFloorType? requestedFloor,
        out ERestaurantFloorType choiceFloor, out TableData data, bool strictRequestedFloor = false)
    {
        choiceFloor = requestedFloor ?? ERestaurantFloorType.Floor1;
        data = null;
        if (RestrictFirstTutorialCustomersToFloor1)
        {
            if (strictRequestedFloor && requestedFloor.HasValue && requestedFloor != ERestaurantFloorType.Floor1)
                return false;
            if (!strictRequestedFloor)
                choiceFloor = ERestaurantFloorType.Floor1;
        }
        else if (!requestedFloor.HasValue)
        {
            if (GetTableType(ETableState.Empty) == null)
                return false;
            choiceFloor = GetWeightRandomChoiceFloor(customer);
        }

        // A full first floor leaves the customer in the existing waiting queue.
        // In particular, do not retry selection against the other unlocked floors.
        data = GetTableType(choiceFloor, ETableState.Empty);
        if (data == null || data.FloorType != choiceFloor)
        {
            data = null;
            return false;
        }

        return true;
    }



    private bool OnCustomerGuide(NormalCustomer customer, TableData data, int sitPos = -1)
    {
        if (data == null || data.CurrentUse != null || data.CurrentCustomer != null
            || data.TableState != ETableState.Empty || _executionIdentity == null
            || _customerController.IsEmpty() || !ReferenceEquals(_customerController.GetFirstCustomer(), customer)
            || _customerController.FirstReadyParty == null) return false;
        CustomerVisitParty party = _customerController.FirstReadyParty;
        CustomerVisitMember member = party.Members[0];
        if (!party.IsActive || party.CurrentUse != null || data.ChairTrs == null || data.ChairTrs.Length < party.Members.Count
            || data.ChairTrs[0] == null || (party.Members.Count == 2 && (data.ChairTrs[1] == null || data.ChairTrs[0] == data.ChairTrs[1]))) return false;
        int seatIndex = party.Members.Count == 2 ? 0 : sitPos == 0 || sitPos == 1 ? sitPos : UnityEngine.Random.Range(0, data.ChairTrs.Length);
        TableUseIdentity use = data.BeginUse(this, _executionIdentity, customer, member, seatIndex);
        if (use == null) return false;
        data.CurrentCustomer = use.IsSingle ? customer : null;
        if (!use.IsSingle) return GuideParty(party, data, use);
        customer.SetVisitFloor(data.FloorType);
        customer.SetLayer("Customer", 0);
        customer.StopWaiting();
        data.TableState = ETableState.Move;
        data.OrdersCount = customer.OrderCount;
        data.Satisfaction = 0;
        data.SitDir = seatIndex == 0 ? -1 : 1;
        data.SitIndex = seatIndex;

        Vector3 targetPos = data.GetSeatPosition(data.SitIndex);
        targetPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;

        if (!_customerController.TakeReadyParty(party, use)) { data.EndUse(); data.CurrentCustomer = null; return false; }
        if (!use.IsMemberCurrent(member)) return false;
        customer.Move(targetPos, 0, () =>
        {
            if (!use.IsCurrent(data) || use.Arrived) return;
            use.Arrived = true;

            // 손님이 도착했으므로 즉시 위치 설정 (대기 시간 제거)
            // StopMove()로 경로 탐색 race condition 완전 차단
            customer.StopMove();
            customer.transform.position = data.GetSeatPosition(data.SitIndex);
            customer.SetSitTableData(data);
            data.OrderButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);
            data.ServingButton.SetWorldTransform(data.ChairTrs[data.SitIndex]);

            customer.SetSpriteDir(-data.SitDir);
            customer.SetLayer("SitCustomer", 0);
            customer.ChangeState(CustomerState.Sit);
            customer.FixSpritePosition(false);

            Tween.Wait(1.5f, () =>
            {
                if (!use.IsCurrent(data) || use.Seated) return;
                use.Seated = true;

                if (!_satisfactionSystem.CheckCustomerTendency(customer.NormalCustomerData.TendencyType))
                {
                    AngerExitCustomer(data);
                    return;
                }

                OnCustomerSeating(data);
            });
        });
        _customerController.PublishGuide();
        return use.IsPartyCurrent(data);
    }

    private bool GuideParty(CustomerVisitParty party, TableData data, TableUseIdentity use)
    {
        if (!_customerController.TakeReadyParty(party, use)) { data.EndUse(); return false; }
        data.TableState = ETableState.Move;
        foreach (var member in party.Members)
        {
            if (!use.IsMemberCurrent(member)) return false;
            var customer = member.Customer;
            customer.SetVisitFloor(data.FloorType); customer.StopWaiting(); customer.SetLayer("Customer", 0);
            Vector3 target = data.GetSeatPosition(member.SeatIndex);
            target.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
            customer.Move(target, 0, () =>
            {
                if (!use.IsMemberCurrent(member) || !customer.ConfirmReachedMoveTarget() || !use.TryArrive(member)) return;
                customer.StopMove(); customer.transform.position = data.GetSeatPosition(member.SeatIndex);
                customer.SetSitTableData(data); customer.SetSpriteDir(member.SeatIndex == 0 ? 1 : -1);
                customer.SetLayer("SitCustomer", 0); customer.ChangeState(CustomerState.Sit); customer.FixSpritePosition(false);
                Tween.Wait(1.5f, () =>
                {
                    if (!use.TrySeat(member) || !use.AllSeated) return;
                    PreparePartyOrders(data, use);
                });
            });
        }
        UpdateTable();
        _customerController.PublishGuide();
        return use.IsPartyCurrent(data);
    }

    public bool CancelPartyGuide(TableData table)
    {
        if (table == null || table.CurrentUse == null || table.CurrentUse.IsSingle) return false;
        table.EndUse(); UpdateTable(); return true;
    }



    public void OnCustomerSeating(TableData data)
    {
        TableUseIdentity use = data == null ? null : data.CurrentUse;
        if (use == null || !use.IsCurrent(data) || use.CompletionStarted) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        if (data.OrdersCount <= 0)
        {
            EndEat(data);
            return;
        }

        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();
        data.CurrentCustomer.FixSpritePosition(false);

        FoodData foodData = data.CurrentCustomer.NormalCustomerData.GetRandomOrderFood();
        int foodLevel = UserInfo.GetRecipeLevel(foodData);
        TableOrderIdentity order = null;
        CookingData cookingData = new CookingData(foodData, data, Mathf.Clamp(0.5f, foodData.GetCookingTime(foodLevel) - GameManager.Instance.SubCookingTime, 100000), foodData.GetSellPrice(foodLevel), () =>
        {
            if (order == null || !order.IsCurrent(data) || order.CookingCompleted
                || use.CompletionStarted) return;
            order.CookingCompleted = true;

            data.TableState = ETableState.CanServing;
            data.ServingButton.SetData(foodData);
            foodData = null;
            UpdateTable();
        });
        order = cookingData.Identity;

        int tip = Mathf.FloorToInt(foodData.GetSellPrice(foodLevel) * GameManager.Instance.TipMul * _satisfactionSystem.AddCustomerTipMul(data.CurrentCustomer.NormalCustomerData.TendencyType));
        data.TotalTip += tip;
        data.CurrentFood = cookingData;

        int totalPrice = (int)(cookingData.Price * data.CurrentCustomer.CurrentFoodPriceMul * GameManager.Instance.GetFoodPriceMul(data.FloorType, foodData.FoodType));
        data.TotalPrice += totalPrice;
        data.OrderButton.SetData(foodData);

        data.TableState = ETableState.Seating;
        data.OrdersCount -= 1;

        data.CurrentCustomer.StartWaitingForFood();

        //만약 설거지거리가 쌓여있는 상태에서 주문이 발생하면 만족도 -4점 감소
        if(!UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, data.FloorType))
        {
            UserInfo.AddSatisfaction(UserInfo.CurrentStage, -4);
        }

        //만약 주문 요리와 손님의 등장요리가 같다면 만족도 2점 증가
        if (data.CurrentFood.FoodData.Id.Equals(data.CurrentCustomer.NormalCustomerData.RequiredDish))
        {
            data.Satisfaction += 2;
        }

        UpdateTable();
    }




    public void OnCustomerOrder(TableData data)
    {
        if (data != null && data.CurrentUse != null && !data.CurrentUse.IsSingle)
        { TryAcceptPartyOrders(data); return; }
        TableOrderIdentity order = data == null ? null : data.CurrentFood.Identity;
        if (order == null || !order.IsCurrent(data) || order.Use.CompletionStarted) return;
        if (data.TableState != ETableState.Seating && data.TableState != ETableState.UseStaff
            && data.TableState != ETableState.DontUse) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        string orderFoodId = data.CurrentFood.FoodData.Id;
        if (!UserInfo.IsGiveRecipe(orderFoodId))
        {
            AngerExitCustomer(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        if (order.OrderAccepted) return;
        order.OrderAccepted = true;
        _kitchenSystem.EqueueFood(data.FloorType, data.CurrentFood);
        data.TableState = ETableState.WaitFood;
        UpdateTable();
    }

    public void OnWaitFood(TableData data)
    {
        if (data == null || (data.CurrentUse != null && !data.CurrentUse.IsSingle)) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        data.TableState = ETableState.WaitFood;
        UpdateTable();
    }


    public void OnServing(TableData data)
    {
        if (data != null && data.CurrentUse != null && !data.CurrentUse.IsSingle) return;
        TryDeliverFood(data == null ? null : data.CurrentFood.Identity, null);
    }

    private void OnServingSingle(TableData data, TableOrderIdentity order)
    {
        if (order == null || !order.IsCurrent(data) || order.ServingStarted
            || order.Use.CompletionStarted) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        if (!_satisfactionSystem.CheckCustomerTendency(data.CurrentCustomer.NormalCustomerData.TendencyType))
        {
            AngerExitCustomer(data);
            return;
        }

        if (data.TableState != ETableState.CanServing && data.TableState != ETableState.StaffServing) return;
        order.ServingStarted = true;
        data.CurrentCustomer.StopWaitingForFood();
        FoodData foodData = FoodDataManager.Instance.GetFoodData(data.CurrentFood.FoodData.Id);
        data.TableState = ETableState.Eating;
        if (order.IsCurrent(data)) StartCoroutine(EatCurrentOrder(data, foodData, order));
        UpdateTable();
    }


    private IEnumerator EatRoutine(TableData data, FoodData foodData)
    {
        // Capture before the iterator's first MoveNext, not after a later coroutine resume.
        return EatCurrentOrder(data, foodData, data.CurrentFood.Identity);
    }

    private IEnumerator EatCurrentOrder(TableData data, FoodData foodData, TableOrderIdentity order)
    {
        DebugLog.Log(data.name + " 11");
        yield return YieldCache.WaitForSeconds(0.5f);
        if (order == null || !order.IsCurrent(data) || !order.ServingStarted || order.EatingCompleted || order.Use.CompletionStarted)
            yield break;

        DebugLog.Log(data.name + " 22");
        data.CurrentCustomer.transform.position = data.GetSeatPosition(data.SitIndex);
        data.CurrentCustomer.ChangeState(CustomerState.Eat);
        data.CurrentCustomer.ShowFood(foodData.Sprite);

        yield return YieldCache.WaitForSeconds(1.5f);
        if (!order.IsCurrent(data) || order.EatingCompleted || order.Use.CompletionStarted) yield break;
        order.EatingCompleted = true;
        DebugLog.Log(data.name + " 33");

        if (data.OrdersCount <= 0)
        {
            EndEat(data);
            DebugLog.Log(data.name + " 44");
        }

        else
        {
            OnCustomerSeating(data);
            DebugLog.Log(data.name + " 55");
        }


    }

    public void OnUseStaff(TableData data)
    {
        if (data == null || (data.CurrentUse != null && !data.CurrentUse.IsSingle)) return;
        data.TableState = ETableState.UseStaff;
        UpdateTable();
    }

    public void OnServigStaff(TableData data)
    {
        if (data == null || (data.CurrentUse != null && !data.CurrentUse.IsSingle)) return;
        data.TableState = ETableState.StaffServing;
        UpdateTable();
    }


    public void ExitCustomer(TableData data)
    {
        TableUseIdentity use = data == null ? null : data.CurrentUse;
        if (use == null || !use.IsCurrent(data)) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;
        exitCustomer.StopWaitingForFood();
        exitCustomer.StopWaiting();
        Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
        customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
        exitCustomer.transform.position = customerPos;
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        exitCustomer.FixSpritePosition(true);
        exitCustomer.StartHappy();
        UserInfo.AddSatisfaction(UserInfo.CurrentStage, data.Satisfaction * exitCustomer.AddSatisfactionMul);
        _feverSystem.AddFeverGauge(exitCustomer.AddFeverGaugeMul);
        data.EndUse();
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        data.Satisfaction = 0;
        DirtyTable(data);
        UpdateTable();
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, GuardExit(use, () =>
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer.StopHappy();
            exitCustomer = null;
            UpdateTable();
        }));
    }


        public void AngerExitCustomer(TableData data)
    {
        TableUseIdentity use = data == null ? null : data.CurrentUse;
        if (use != null && !use.IsSingle) { HoldUnsupportedPartyPolicy(use, "MidVisitFailurePolicyPending"); return; }
        if (use == null || !use.IsCurrent(data)) return;
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;
        exitCustomer.StopWaitingForFood();
        exitCustomer.StopWaiting();
        Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
        customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
        exitCustomer.transform.position = customerPos;
        exitCustomer.ChangeState(CustomerState.Idle);
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        exitCustomer.StartAnger();
        exitCustomer.FixSpritePosition(true);
        data.EndUse();
        data.CurrentCustomer = null;
        data.TotalTip = 0;
        data.TotalPrice = 0;
        data.TableState = ETableState.Empty;

        UserInfo.AddSatisfaction(UserInfo.CurrentStage, BasicRecipeRejectionSatisfaction);
        UpdateTable();
        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, GuardExit(use, () =>
        {
            exitCustomer.StopAnger();
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        }));
    }


    public void NotFurnitureTable(TableData data)
    {
        if (data.TableType == TableType.Table1)
            return;

        TableUseIdentity use = data.CurrentUse;
        if (use != null && !use.IsSingle) { HoldUnsupportedPartyPolicy(use, "FurnitureRemovedPolicyPending"); return; }
        data.EndUse(null, ETableState.DontUse);

        data.TotalTip = 0;
        data.TotalPrice = 0;

        if (data.CurrentCustomer == null)
        {
            data.TableState = ETableState.DontUse;
            return;
        }

        NormalCustomer exitCustomer = data.CurrentCustomer;

        if (data.TableState != ETableState.Move)
        {
            Vector3 customerPos = data.ChairTrs[data.SitIndex].position;
            customerPos.y = data.TableFurniture.transform.position.y + AStar.Instance.NodeSize * 0.5f;
            exitCustomer.transform.position = customerPos;
        }

        data.CurrentCustomer = null;
        exitCustomer.SetLayer("Customer", 0);
        exitCustomer.HideFood();
        exitCustomer.FixSpritePosition(true);
        data.TableState = ETableState.DontUse;
        UpdateTable();

        exitCustomer.Move(GameManager.Instance.OutDoorPos, 0, () =>
        {
            if (use == null || !use.IsCustomerContextCurrent) return;
            ObjectPoolManager.Instance.DespawnNormalCustomer(exitCustomer);
            exitCustomer = null;
            UpdateTable();
        });
    }


    private void EndEat(TableData data)
    {
        TableUseIdentity use = data == null ? null : data.CurrentUse;
        if (use == null || !use.IsCurrent(data) || use.CompletionStarted) return;
        use.CompletionStarted = true;

        int basePaymentTip = data.TotalTip;
        EStage paymentStage = UserInfo.CurrentStage;
        float payoutBonusPercent = 0f;
        GameManager existingGameManager;
        if (GameManager.TryGetExistingInstance(out existingGameManager))
        {
            payoutBonusPercent =
                existingGameManager.StaffSkillEffectRegistry.GetHighestPercent(
                    StaffSkillEffectType.RestaurantTipPayoutPercent);
        }

        int finalPaymentTip =
            StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout(
                basePaymentTip,
                payoutBonusPercent);
        data.CurrentCustomer.ChangeState(CustomerState.Idle);
        data.CurrentCustomer.HideFood();
        UserInfo.CustomerVisits(data.CurrentCustomer.CustomerData);
        UserInfo.AddCustomerCount();

        StartCoinAnime(data);
        StartGarbageAnime(data);
        Tween.Wait(0.5f, () =>
        {
            if (!use.IsCurrent(data) || use.SettlementCompleted) return;
            use.SettlementCompleted = true;

            ExitCustomer(data);

            // Exit ends the use. This already-claimed settlement still owns its captured tip.
            UserInfo.AddTip(paymentStage, finalPaymentTip);
            UpdateTable();
        });
    }


    private void DirtyTable(TableData data)
    {
        if (data.TableState == ETableState.DontUse)
        {
            NotFurnitureTable(data);
            return;
        }

        data.TableState = ETableState.NeedCleaning;
        UpdateTable();
    }


    private void StartCoinAnime(TableData data)
    {
        int sitIndex = data.SitIndex;

        float feverFoodPriceMultiplier = 1f;
        GameManager existingGameManager;
        if (GameManager.TryGetExistingInstance(out existingGameManager))
        {
            feverFoodPriceMultiplier =
                existingGameManager.FeverRuntimeContext.FoodPriceMultiplier;
        }

        int foodPrice = (int)(data.TotalPrice * feverFoodPriceMultiplier);
        data.DropCoinAreas[sitIndex].DropCoin(data.ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0), foodPrice);
        data.TotalPrice = 0;
        data.TotalTip = 0;
    }


    private void StartGarbageAnime(TableData data)
    {
        TableUseIdentity use = data.CurrentUse;
        int sitIndex = data.SitIndex;
        int count = UnityEngine.Random.Range(1, 3);
        float time = 0.02f;
        for (int i = 0; i < count; i++)
        {
            bool dropped = false;
            Tween.Wait(time, () =>
            {
                if (dropped || use == null || !use.IsCurrent(data)) return;
                dropped = true;
                data.DropGarbageArea.DropGarbage(data.ChairTrs[sitIndex].position + new Vector3(0, 1.2f, 0));
            });
            time += 0.02f;
        }
    }



    private void OnChangeFurnitureEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        UpdateTable();
    }


    public void UpdateTable()
    {
        OnTableUpdateHandler?.Invoke();
        if (_customerController.IsEmpty())
        {
            _guideButton.gameObject.SetActive(false);
            return;
        }

        int unlockFloorCount = RestrictFirstTutorialCustomersToFloor1
            ? (int)ERestaurantFloorType.Floor1
            : (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        //TODO: 나중에 삭제 후 위에꺼 쓰기(임시로 1층만 사용)
        //int unlockFloorCount = (int)ERestaurantFloorType.Floor1;
        for (int i = 0; i <= unlockFloorCount; ++i)
        {
            TableData data = GetTableType((ERestaurantFloorType)i, ETableState.Empty);
            if (data == null)
            {
                _guideButton.gameObject.SetActive(false);
                continue;
            }

            _guideButton.gameObject.SetActive(true);
            break;
        }
    }


    /// <summary> 직원 위치를 반환하는 함수</summary>
    public Vector2 GetStaffPos(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (type == EquipStaffType.Chef /*|| type == EquipStaffType.Chef2*/)
        {
            return _kitchenSystem.GetStaffPos(floorType, type);
        }
        else
        {                    
            return _furnitureSystem.GetStaffPos(floorType, type);
        }
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
        return _furnitureSystem.GetTableType(floorType, state);
    }

    public TableData GetTableType(ETableState state)
    {
        return _furnitureSystem.GetTableType(state);
    }

    public TableData GetTableData(ERestaurantFloorType floorType, TableType type)
    {
        return _furnitureSystem.GetTableType(floorType, type);
    }

    public List<TableData> GetTableDataList(ERestaurantFloorType floorType)
    {
        return _furnitureSystem.GetTableDataList(floorType);
    }

    public List<DropCoinArea> GetDropCoinAreaList(ERestaurantFloorType floorType)
    {
        return _furnitureSystem.GetDropCoinAreaList(floorType);
    }

    public Vector3 GetFoodPos(ERestaurantFloorType floorType, RestaurantType type, Vector3 pos)
    {
        return _furnitureSystem.GetFoodPos(floorType, type, pos);
    }

    public List<TableData> GetTableDataList(ERestaurantFloorType floorType, ETableState state)
    {
        List<TableData> returnDataList = new List<TableData>();
        List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);
        for (int i = 0, cnt = tableDataList.Count; i < cnt; ++i)
        {
            if (tableDataList[i].TableState != state)
                continue;

            returnDataList.Add(tableDataList[i]);
        }

        return returnDataList;
    }

    public TableData GetTableTypeByNeedFood(ERestaurantFloorType floorType, ETableState state)
    {
        List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);

        for (int i = 0, cnt = tableDataList.Count; i < cnt; i++)
        {
            if (tableDataList[i].TableState != state)
                continue;

            if (tableDataList[i].CurrentFood.IsDefault())
                continue;

            if (!UserInfo.IsGiveRecipe(tableDataList[i].CurrentFood.FoodData.Id))
                continue;

            return tableDataList[i];
        }

        return null;
    }



public DropGarbageArea GetMinDistanceGarbageArea(ERestaurantFloorType floorType, Vector3 startPos)
{
    return GetMinDistanceObject(
        RestaurantType.Hall,
        startPos,
        _furnitureSystem.GetDropGarbageAreaList(floorType),
        area => area.transform.position,
        area => area.Count > 0
    );
}

public DropCoinArea GetMinDistanceCoinArea(ERestaurantFloorType floorType, Vector3 startPos)
{
    return GetMinDistanceObject(
        RestaurantType.Hall,
        startPos,
        _furnitureSystem.GetDropCoinAreaList(floorType),
        area => area.transform.position,
        area => area.Count > 0
    );
}

public TableData GetMinDistanceTable( Vector3 startPos, List<TableData> tableDataList)
{
    return GetMinDistanceObject(
        RestaurantType.Hall,
        startPos,
        tableDataList,
        table => table.transform.position,
        null,
        1f
    );
}

// GetTableDataList(floorType, state)처럼 상태별 임시 리스트를 새로 만들지 않고,
// 기존 층별 테이블 목록을 그대로 순회하며 상태 조건을 검사해 가장 가까운 테이블을 찾음
public TableData GetMinDistanceTableByState(Vector3 startPos, ERestaurantFloorType floorType, ETableState state)
{
    List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);
    return GetMinDistanceObject(
        RestaurantType.Hall,
        startPos,
        tableDataList,
        table => table.transform.position,
        table => table.TableState == state,
        1f
    );
}

public KitchenBurnerData GetMinDistanceBurner(Vector3 startPos, List<KitchenBurnerData> dataList)
{
    return GetMinDistanceObject(
        RestaurantType.Kitchen,
        startPos,
        dataList,
        data => data.KitchenUtensil.transform.position,
        null,
        1f
    );
}


    // 제네릭 함수로 가장 가까운 객체를 찾는 메서드
    private T GetMinDistanceObject<T>(
        RestaurantType restaurantType,
        Vector3 startPos,
        IEnumerable<T> objects,
        Func<T, Vector3> positionGetter,
        Func<T, bool> validationCheck = null,
        float doorThreshold = 0.5f) where T : class
    {
        Vector3 targetDoorPos = GetDoorPos(restaurantType, startPos);
        T minEqualObject = null;
        T minNotEqualObject = null;

        float minEqualDist = float.MaxValue;
        float minNotEqualDist = float.MaxValue;

        foreach (var obj in objects)
        {
            // 유효성 검사 함수가 있고 해당 객체가 유효하지 않으면 건너뜀
            if (validationCheck != null && !validationCheck(obj))
                continue;

            Vector3 objPosition = positionGetter(obj);
            Vector3 doorPos = GetDoorPos(restaurantType, objPosition);
            float doorDistance = Mathf.Abs(targetDoorPos.y - doorPos.y);
            float startDistance = Vector3.Distance(objPosition, startPos);

            if (doorDistance <= doorThreshold)
            {
                if (startDistance < minEqualDist)
                {
                    minEqualDist = startDistance;
                    minEqualObject = obj;
                }
            }
            else
            {
                float objDoorDistance = Vector3.Distance(objPosition, doorPos);
                if (objDoorDistance < minNotEqualDist)
                {
                    minNotEqualDist = objDoorDistance;
                    minNotEqualObject = obj;
                }
            }
        }

        return minEqualObject ?? minNotEqualObject;
    }


    private ERestaurantFloorType GetWeightRandomChoiceFloor(NormalCustomer customer)
    {
        //선호 음식 가중치를 계산 후 랜덤으로 Floor을 지정한다.
        WeightedRandom<FoodType> foodRandom = customer.GetFoodTypeWeightDic();

        Dictionary<FoodType, List<ERestaurantFloorType>> floorDataDic = new();
        HashSet<FoodType> enabledFoodTypes = new();

        int maxFloorIndex = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        for (int i = 0; i <= maxFloorIndex; i++)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            TableData tableData = GetTableType(floor, ETableState.Empty);
            if (tableData == null) continue;

            FoodType foodType = _furnitureSystem.GetFoodType(floor);
            if (!floorDataDic.ContainsKey(foodType))
                floorDataDic.Add(foodType, new List<ERestaurantFloorType>());

            floorDataDic[foodType].Add(floor);
            foodRandom.Add(foodType, 0.1f);
            enabledFoodTypes.Add(foodType);
        }

        // 활성화되지 않은 FoodType 한 번만 제거 (불필요한 전체 루프 제거)
        foodRandom.RemoveAll(f => !enabledFoodTypes.Contains(f));

        // 확률 기반 FoodType 선택
        FoodType choiceFoodType = foodRandom.GetRamdomItem();
        List<ERestaurantFloorType> availableFloors = floorDataDic[choiceFoodType];
        ERestaurantFloorType choiceFloor = availableFloors[UnityEngine.Random.Range(0, availableFloors.Count)];
        return choiceFloor;
    }


    private void OnChangeTableEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (type < FurnitureType.Table1 || FurnitureType.Table5 < type)
            return;

        List<TableData> tableDataList = _furnitureSystem.GetTableDataList(floorType);
        if (tableDataList == null || tableDataList.Count <= 0)
            return;

        Tween.Wait(0.01f, () =>
        {
            for (int i = 0, cnt = tableDataList.Count; i < cnt; ++i)
            {
                TableData data = tableDataList[i];
                if (data == null)
                    continue;

                if (data.TableState == ETableState.DontUse)
                    continue;

                if (data.TableState == ETableState.UseStaff)
                    continue;
                if (data.TableState == ETableState.PartyRejecting || (data.CurrentUse != null && !data.CurrentUse.IsSingle && data.CurrentUse.CompletionStarted)) continue;
                data.RefreshPartyOrderButtons();
                var party = data.CurrentParty;
                if (party == null) continue;
                foreach (var member in party.Members)
                    if (data.TableState != ETableState.Move || data.CurrentUse.HasArrived(member))
                        member.Customer.transform.position = data.GetSeatPosition(member.SeatIndex);
            }
        });

    }

    private void OnDestroy()
    {
        InvalidateExecution();
        LoadingSceneManager.OnLoadSceneHandler -= InvalidateExecution;
        _customerController.OnChangeCustomerHandler -= UpdateTable;
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
        UserInfo.OnChangeFurnitureHandler -= OnChangeTableEvent;
        UserInfo.OnChangeFloorHandler -= UpdateTable;
    }
}
