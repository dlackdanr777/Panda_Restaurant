using Muks.DataBind;
using Muks.UI;
using Muks.BackEnd;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GachaTutorial : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UINavigationCoordinator _coordinator;
    [SerializeField] private UINavigation _mainNav;
    [SerializeField] private UINavigation _tutorialNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _descriptionNPC;
    [SerializeField] private UIGacha _uiGacha;
    [SerializeField] private UIItemGacha _itemGacha;
    [SerializeField] private Button _shopButton;
    [SerializeField] private Button _recipeButton;
    [SerializeField] private Button _gachaButton;
 
    private Coroutine _coroutine;
    private bool _gachaCompleted;
    private FoodData _foodData;
    private bool _tutorialRunActive;
    private bool _itemInputAccepted;
    private bool _saveErrorShown;
    private BackendManager _saveOwner;
    private GameDataRestoreQuery _saveQuery;
    private GameDataSaveRequest _itemSave;
    private GameDataSaveRequest _completionSave;

    // MainReward12 is the item-machine tutorial, not an employment/paid-machine
    // unlock. Check the actual current CSV quest and predecessor claims.
    public static bool IsCurrentItemTutorialQuest()
    {
        if (UserInfo.CurrentStage != EStage.Stage1 || !UserInfo.IsFirstTutorialClear
            || UserInfo.IsMiniGameTutorialClear || UserInfo.GetIsClearChallenge("MainReward12")) return false;
        var current = ChallengeManager.Instance.GetCurrentMainChallengeData();
        if (current == null || current.Id != "MainReward12" || current.Type != ChallengeType.TYPE25) return false;
        for (int i = 1; i < 12; i++)
            if (!UserInfo.GetIsClearChallenge("MainReward" + i.ToString("D2"))) return false;
        return true;
    }

    protected virtual bool CaptureSaveSession()
    {
        _saveOwner = BackendManager.Instance;
        _saveQuery = _saveOwner.CurrentMailReceiveQuery;
        return IsCapturedSessionCurrent() && CanBeginItemMutation();
    }

    protected virtual bool IsCapturedSessionCurrent() => _saveOwner != null && _saveQuery != null
        && _saveOwner.IsCurrentGameDataQuery(_saveQuery);

    protected virtual bool CanBeginItemMutation() => IsCapturedSessionCurrent()
        && _saveOwner.StaffRuntime.CanMutate
        && _saveOwner.CanSaveLegacyGameData
        && (_saveOwner.CurrentGameDataSaveCoordinator == null
            || _saveOwner.CurrentGameDataSaveCoordinator.CanStartPurchase);

    protected virtual GameDataSaveRequest SaveTutorialProgress() =>
        _saveOwner.RequestGameDataAutosave(requireGameplay: false);

    protected virtual void ReportProgressError(string message) => PopupManager.Instance.ShowDisplayText(message);

    private bool TryAcceptTutorialItem()
    {
        if (!_tutorialRunActive || _itemInputAccepted || !IsCurrentItemTutorialQuest()
            || !IsCapturedSessionCurrent() || !CanBeginItemMutation()) return false;
        _itemInputAccepted = true; // HoleClickHandler can dispatch the same pointer-up twice.
        bool saveRequested = false;
        bool applyAttempted = false;
        try
        {
            GachaItemData item = ItemManager.Instance.GetGachaItemData("GOTCHA91");
            applyAttempted = true;
            if (!_itemGacha.StartAddItem(item))
            {
                ReportProgressError("아이템 안내를 진행할 수 없습니다. 현재 상태를 확인해 주세요.");
                return false;
            }
            saveRequested = true;
            _itemSave = SaveTutorialProgress();
            return true;
        }
        catch (System.Exception exception)
        {
            DebugLog.LogError("아이템 안내 처리 확인 필요: " + exception.Message);
            // Never replay a partially applied result. Preserve it through the normal
            // guarded path if possible; neither this catch nor its view is a receipt.
            if (applyAttempted && UserInfo.IsGiveGachaItem("GOTCHA91")
                && !saveRequested && IsCapturedSessionCurrent())
            {
                try { _itemSave = SaveTutorialProgress(); }
                catch (System.Exception saveException) { DebugLog.LogError(saveException.Message); }
            }
            ReportProgressError("처리 상태를 확인할 수 없습니다. 추가 조작을 멈춰 주세요.");
            _saveErrorShown = true;
            return false;
        }
    }

    private bool IsSaveConfirmed(GameDataSaveRequest request) => !_saveErrorShown
        && IsCapturedSessionCurrent() && request != null
        && request.Status == GameDataSaveRequestStatus.SuccessConfirmed && request.Receipt != null;

    private GameDataSaveRequest BeginCompletionSave()
    {
        if (_completionSave != null) return _completionSave;
        if (!_tutorialRunActive || !_itemInputAccepted || !IsSaveConfirmed(_itemSave)) return null;
        UserInfo.IsMiniGameTutorialClear = true; // serialized intent; UI waits for the receipt below
        _completionSave = SaveTutorialProgress();
        return _completionSave;
    }

    private IEnumerator WaitForSave(GameDataSaveRequest request)
    {
        while (!IsSaveConfirmed(request))
        {
            bool failed = !IsCapturedSessionCurrent() || request == null
                || (request.Status != GameDataSaveRequestStatus.Accepted
                    && request.Status != GameDataSaveRequestStatus.Sending);
            if (failed && !_saveErrorShown)
            {
                _saveErrorShown = true;
                ReportProgressError("저장 상태를 확인할 수 없습니다. 추가 조작을 멈춰 주세요.");
            }
            yield return null; // Unknown/failed requests are not reset or retransmitted.
        }
    }

    private void Awake()
    {
        DataBind.SetUnityActionValue("ShortCut10", OnShortCut10ButtonClicked);
    }

    public void StartTutorial()
    {
        if (_tutorialRunActive || UserInfo.IsTutorialStart || !IsCurrentItemTutorialQuest()
            || !CaptureSaveSession())
            return;

        _tutorialRunActive = true;
        _itemInputAccepted = false;
        _saveErrorShown = false;
        _gachaCompleted = false;
        gameObject.SetActive(true);
        if (_coroutine != null)
            StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(TutorialRoutine());
    }

    private IEnumerator TutorialRoutine()
    {


        UserInfo.IsTutorialStart = true;
        _tutorialNav.Push("UITutorial");
        _uiTutorial.ScreenButtonSetActive(true);
        _tutorialNav.Push("UITutorialDescription");
        _descriptionNPC.SkipButtonSetActive(false);
        yield return YieldCache.WaitForSeconds(1f);

        // A persisted tutorial item resumes guidance without another item/count grant.
        if (UserInfo.IsGiveGachaItem("GOTCHA91") && UserInfo.TotalUseGachaMachineCount > 0)
        {
            _itemInputAccepted = true;
            _gachaCompleted = true;
            _itemSave = SaveTutorialProgress();
        }
        else
        {

        yield return _descriptionNPC.ShowDescription1Text("가챠샵에 오신 걸 환영합니다!");
        yield return _descriptionNPC.ShowDescription1Text("다이아를 사용해서 평소에 얻기 힘든\n아이템을 획득할 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("레시피, 스킨 그리고 가게 스탯을\n강화하는 아이템을 얻을 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("가챠를 한번 뽑아볼까요?");
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.Gacha1ButtonSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, 350, "Tutorial Gacha1 Button", _uiTutorial.Gacha1Button.transform);
        _uiGacha.GachaStepHandler += OnGachaCompletedEvent;
        _uiTutorial.Gacha1Button.AddListener(() => TryAcceptTutorialItem());
        _itemGacha.SingleButton.gameObject.SetActive(false);
        _tutorialNav.Push("UITutorial");
        while (!_itemInputAccepted)
            yield return YieldCache.WaitForSeconds(0.01f);

        _uiTutorial.Gacha1ButtonSetActive(false);
        }
        yield return WaitForSave(_itemSave);
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _mainNav.Push("UIGacha");
        while (!_gachaCompleted)
            yield return YieldCache.WaitForSeconds(0.01f);

        _uiGacha.GachaStepHandler -= OnGachaCompletedEvent;
        _gachaCompleted = false;
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        _tutorialNav.Push("UITutorial");
        _tutorialNav.Push("UITutorialDescription");
        _descriptionNPC.SkipButtonSetActive(false);
        _uiTutorial.ScreenButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1);
        yield return _descriptionNPC.ShowDescription1Text("이제 레시피를 통해서 새로운 메뉴를 제작할 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("새 손님을 위해 바로 음식을 만들러 가봐요!");
        _mainNav.AllPop();
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, 200, _shopButton.name, _shopButton.transform, false);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);


        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.CustomHoleSetActive(true, 250, _recipeButton.name, _recipeButton.transform);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);
            
        yield return YieldCache.WaitForSeconds(1f);
        yield return _descriptionNPC.ShowDescription1Text("가챠로 얻은 레시피는\n미니게임을 통해서 얻을 수 있습니다.");
        yield return _descriptionNPC.ShowDescription1Text("한번 김치찌개를 만들어 볼까요?");

        yield return WaitForSave(BeginCompletionSave());
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        _tutorialRunActive = false;
        _coroutine = null;
        gameObject.SetActive(false);

    }


    private void OnGachaCompletedEvent(int step)
    {
        if (!_tutorialRunActive || !_itemInputAccepted || _saveErrorShown || step < 4)
            return;

        _gachaCompleted = true;
    }


    private void OnSkipButtonClicked()
    {
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;

        GachaItemData needItemData = ItemManager.Instance.GetGachaItemData(_foodData.NeedItem);
        if (!UserInfo.IsGiveGachaItem(needItemData))
            UserInfo.GiveGachaItem(needItemData);

        UserInfo.IsTutorialStart = false;
        UserInfo.IsMiniGameTutorialClear = true;
        gameObject.SetActive(false);
    }


    private void OnShortCut10ButtonClicked()
    {
        if (_tutorialRunActive || UserInfo.IsTutorialStart || !UIGacha.IsProgressionEntryUnlocked()) return;
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        gameObject.SetActive(true);
        _coroutine = StartCoroutine(ShortCut10Func());
    }

    private void OnDestroy()
    {
        if (_uiGacha != null) _uiGacha.GachaStepHandler -= OnGachaCompletedEvent;
        if (_tutorialRunActive && IsCapturedSessionCurrent()) UserInfo.IsTutorialStart = false;
    }


    private IEnumerator ShortCut10Func()
    {
        gameObject.SetActive(true);
        _mainNav.AllPop();
        _tutorialNav.Push("UITutorial");
        _uiTutorial.ScreenButtonSetActive(true);
        _tutorialNav.Push("UITutorialDescription");
        _descriptionNPC.SkipButtonSetActive(false);
        while (0 < _mainNav.Count)
        {
            yield return YieldCache.WaitForSeconds(0.1f);
            _mainNav.AllPop();
        }
        yield return YieldCache.WaitForSeconds(1f);

        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, 200, _gachaButton.name, _gachaButton.transform, false);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);


        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        gameObject.SetActive(false);
    }
}
