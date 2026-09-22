using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Current-quest shop hints only. This component never buys, equips, grants,
/// claims a reward, saves, or changes tutorial completion/progression flags.
/// </summary>
public sealed class QuestProgressGuidance : MonoBehaviour
{
    private UIMainCanvas _main;
    private MobileUINavigation _mainNav, _tutorialNav;
    private UIRestaurantAdmin _shop;
    private UIStaff _staff;
    private UIGacha _gacha;
    private UIStaffGacha _staffMachine;
    private UITutorial _guide;
    private UITutorialDescriptionNPC _description;
    private BackendManager _owner;
    private ChallengeManager _challenges;
    private GameDataRestoreQuery _query;
    private string _quest;
    private ChallengeData _current;
    private bool _ownsOverlay, _guidePop, _descriptionPop;
    private string _hintKey;
    private RectTransform _hintTarget;
    private bool _waitingForEquip, _recipeObservedUnowned, _recipeFollowupShown, _recipeFocused;
    private bool _wallpaperPending, _wallpaperFinished, _dialogueActive;
    private float _tableVisibleSince = -1f, _nextPoll;
    private QuestStaffGrantExecution _announcedGrant;
    private Coroutine _dialogue;
    private Coroutine _hintDialogue;
    private int _hintLifetime;
    private bool _reportedError;

    // Each entry is a separate acknowledgement page, using the existing NPC
    // dialogue. These messages never perform the draw, placement or purchase.
    private static readonly Dictionary<string, string[]> StaffDrawPages = new Dictionary<string, string[]>
    {
        ["MainReward01"] = new[] { "뽑기를 눌러 직원을 뽑아봅시다!\n지금은 무료 뽑기 기회를 드릴게요." },
        ["MainReward04"] = new[] { "역시 주방장이 없으니 조금 답답하죠?", "이번에는 주방장을 한번 뽑아볼까요?" },
        ["MainReward05"] = new[] { "근데 웨이터가 없다면\n식당이 원활하게 돌아가지 않겠죠?", "이번에는 웨이터를 뽑아봅시다!" },
        ["MainReward19"] = new[] { "식당이 더러우면 아무도 찾아오지 않을 거예요.", "청소부를 고용해볼까요?" }
    };
    private static readonly Dictionary<string, string[]> StaffResultPages = new Dictionary<string, string[]>
    {
        ["MainReward01"] = new[]
        {
            "와, 매니저를 뽑으셨군요!",
            "상점에서 직원을 배치해봅시다.",
            "닫기를 눌러 상점으로 돌아가볼까요?"
        },
        ["MainReward04"] = new[]
        {
            "와, 주방장 도치가 함께하게 되었어요!",
            "주방장은 조리 효율을 높여주고\n자동으로 설거지를 진행해준답니다.",
            "도치를 배치해볼까요?"
        },
        ["MainReward05"] = new[]
        {
            "장난꾸러기 지지가 웨이터로 합류했어요!",
            "웨이터는 음식 주문과 배달을\n자동으로 진행해줘요.",
            "빠질 수 없는 직원이겠죠?"
        },
        ["MainReward19"] = new[]
        {
            "청소부 판다 멜로를 획득하셨군요!",
            "청소부는 자동으로 테이블과 쓰레기를 치워줘요.",
            "손님이 지불한 코인도 대신 획득해준답니다."
        }
    };
    private static readonly string[] TableReadyPages =
    {
        "제한 평점을 달성해서\n테이블을 구매할 수 있게 되었어요!",
        "새로운 테이블을 구매해봅시다!"
    };
    private static readonly string[] ManagerEquippedPages =
    {
        "매니저는 자동으로 손님을 배치해 줍니다.\n손쉽게 레스토랑을 운영해봅시다!"
    };
    private static readonly string[] RecipeLearnPages =
    {
        "레시피를 배워봅시다!",
        "다양한 레시피를 배우면\n다양한 손님이 등장한답니다."
    };
    private static readonly string[] RecipeLearnedPages = { "다양한 레시피를 모아봅시다!" };
    private static readonly string[] WallpaperExplanationPages =
    {
        "평점이 모자라면 다양한 상품들을 구매할 수 없어요.",
        "선물로 받은 벽지를 이용해서 평점을 올려볼까요?"
    };
    private static readonly string[] WallpaperFreePages = { "평범한 벽지를 받아볼까요?" };

    public static QuestProgressGuidance Install(UIMainCanvas canvas)
    {
        if (canvas == null) return null;
        var existing = canvas.GetComponent<QuestProgressGuidance>();
        if (existing != null) return existing;
        var guidance = canvas.gameObject.AddComponent<QuestProgressGuidance>();
        guidance.Bind(canvas);
        return guidance;
    }

    public static bool IsInstalledIn(Scene scene)
        => scene.IsValid() && scene.GetRootGameObjects().Any(root =>
            root.GetComponentsInChildren<QuestProgressGuidance>(true).Any(item => item.enabled
                && item._guide != null && item._description != null && item._tutorialNav != null));

    private void Bind(UIMainCanvas canvas)
    {
        _main = canvas;
        _mainNav = canvas.GetComponent<MobileUINavigation>();
        _shop = canvas.GetComponentInChildren<UIRestaurantAdmin>(true);
        _staff = canvas.GetComponentInChildren<UIStaff>(true);
        _gacha = canvas.GetComponentInChildren<UIGacha>(true);
        _staffMachine = _gacha != null ? _gacha.GetComponentInChildren<UIStaffGacha>(true) : null;
        var roots = gameObject.scene.GetRootGameObjects();
        _guide = roots.SelectMany(root => root.GetComponentsInChildren<UITutorial>(true)).FirstOrDefault();
        _description = roots.SelectMany(root => root.GetComponentsInChildren<UITutorialDescriptionNPC>(true)).FirstOrDefault();
        _tutorialNav = _guide != null ? _guide.GetComponentInParent<MobileUINavigation>(true) : null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + 0.05f;
        try { Poll(); }
        catch (Exception exception)
        {
            // Hints are not completion callbacks or save guards. A display failure
            // must release its input and cannot interrupt product persistence.
            ReleaseOverlay();
            if (!_reportedError) Debug.LogWarning("[QuestProgressGuidance] Hint stopped: " + exception.GetType().Name);
            _reportedError = true;
            enabled = false;
        }
    }

    private void Poll()
    {
        if (!ReadCurrentContext()) { CancelPresentation(); return; }
        if (UserInfo.IsTutorialStart) { CancelPresentation(); return; }
        if (_dialogueActive)
        {
            if (!IsTableThreeViewCurrent()) CancelPresentation();
            return;
        }
        if (QuestStaffTutorialPolicy.TryGetMapping(_quest, out string staffId, out _))
            PollStaff(staffId);
        else if (_quest == "MainReward03" && _current is Type03ChallengeData recipe && recipe.BuyRecipeId == "FOOD02")
            PollRecipe();
        else if (_quest == "MainReward10" && _current is Type01ChallengeData furniture
            && furniture.NeedFurnitureIds != null && furniture.NeedFurnitureIds.Contains("TABLE01_03"))
            PollWallpaper();
        else ReleaseOverlay();
    }

    private bool ReadCurrentContext()
    {
        if (_main == null || _mainNav == null || _shop == null || _guide == null || _description == null || _tutorialNav == null)
            return false;
        if (_owner == null) _owner = FindObjectOfType<BackendManager>();
        if (_challenges == null) _challenges = FindObjectOfType<ChallengeManager>();
        if (_owner == null || _challenges == null || !UserInfo.IsFirstTutorialClear || UserInfo.CurrentStage != EStage.Stage1
            || !_owner.CanSaveLegacyGameData || _owner.StaffRuntime.Mode != StaffAccountRuntimeMode.Common)
            return false;
        var query = _owner.CurrentMailReceiveQuery;
        var current = _challenges.GetCurrentMainChallengeData();
        if (query == null || !_owner.IsCurrentGameDataQuery(query) || current == null
            || current.Challenges != Challenges.Main || UserInfo.GetIsClearChallenge(current)) return false;
        if (!ReferenceEquals(query, _query) || current.Id != _quest)
        {
            CancelPresentation();
            _query = query;
            _quest = current.Id;
            _waitingForEquip = _recipeObservedUnowned = _recipeFollowupShown = _recipeFocused = false;
            _wallpaperPending = _wallpaperFinished = false;
            _tableVisibleSince = -1f;
            _announcedGrant = null;
        }
        _current = current;
        return true;
    }

    private bool IsContextCurrent()
    {
        return _owner != null && _query != null && _owner.IsCurrentGameDataQuery(_query)
            && _owner.CanSaveLegacyGameData && _owner.StaffRuntime.Mode == StaffAccountRuntimeMode.Common
            && UserInfo.CurrentStage == EStage.Stage1 && UserInfo.IsFirstTutorialClear && !UserInfo.IsTutorialStart
            && _challenges != null && _challenges.GetCurrentMainChallengeData()?.Id == _quest
            && !UserInfo.GetIsClearChallenge(_quest);
    }

    private void PollStaff(string staffId)
    {
        if (!(_current is Type05ChallengeData employment) || employment.NeedStaffId != staffId)
        { ReleaseOverlay(); return; }
        var completed = _owner.LastCompletedQuestStaffGrant;
        string resultKey = _quest == "MainReward01" ? "manager-result" : "staff-result:" + _quest;
        if (IsConfirmedResultCurrent(completed, _quest, _query) && completed.Before.RequiredStaffId == staffId
            && _owner.CanPresentQuestStaffGrant(completed) && !StaffGachaPurchaseDisplay.IsAcknowledged(completed)
            && (!ReferenceEquals(_announcedGrant, completed) || _hintKey == resultKey)
            && _mainNav.FirstView == _gacha && _gacha.VisibleState == VisibleState.Appeared
            && _staffMachine != null && _staffMachine.IsQuestEntry && _staffMachine.ResultCard != null
            && _staffMachine.ResultCard.gameObject.activeInHierarchy)
        {
            var close = _staffMachine.ResultSkipButton;
            var closeLabel = close != null ? close.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) : null;
            RectTransform closeTarget = close != null && close.gameObject.activeInHierarchy && close.interactable
                && closeLabel != null && closeLabel.text == "닫기" ? close.transform as RectTransform : null;
            // The card animation still owns Skip until its real Close is ready.
            // Do not cover the reveal or describe an unconfirmed acquisition.
            if (closeTarget == null) { ReleaseOverlay(); return; }
            _announcedGrant = completed;
            ShowDialogueHint(resultKey, closeTarget, StaffResultPages[_quest]);
            return;
        }
        if (_staff == null || _mainNav.FirstView != _staff || !_shop.IsReadyForDetail || !_staff.IsReadyForGuidance
            || _staff.SelectedStaff == null || _staff.SelectedStaff.Id != staffId)
        { _waitingForEquip = false; ReleaseOverlay(); return; }

        bool owned = UserInfo.IsGiveStaff(UserInfo.CurrentStage, _staff.SelectedStaff);
        if (!owned)
        {
            _waitingForEquip = false;
            if (!_owner.TryGetCurrentQuestStaffOffer(out var offer, out _) || offer.QuestId != _quest || offer.StaffId != staffId)
            { ReleaseOverlay(); return; }
            ShowDialogueHint("staff-draw:" + _quest, _staff.BuyButtonRect, StaffDrawPages[_quest]);
            return;
        }
        if (_quest != "MainReward01")
        {
            // Result Close already returns to this native staff detail. Keep
            // the button unobscured: the player, not this guide, equips staff.
            if (IsConfirmedResultCurrent(_announcedGrant, _quest, _query)
                && !UserInfo.IsEquipStaff(UserInfo.CurrentStage, _staff.SelectedStaff))
                ShowDialogueHint("staff-equip:" + _quest, _staff.EquipButtonRect, Array.Empty<string>());
            else ReleaseOverlay();
            return;
        }
        bool equipped = UserInfo.IsEquipStaff(UserInfo.CurrentStage, _staff.SelectedStaff);
        if (!equipped)
        {
            _waitingForEquip = true;
            ShowHint("manager-equip", _staff.EquipButtonRect, "상점에서 매니저를 배치해봅시다!\n배치하기 버튼을 눌러주세요.");
        }
        else if (_waitingForEquip)
        {
            if (ShowInformationHint("manager-equipped", ManagerEquippedPages)) _waitingForEquip = false;
        }
        else if (_hintKey != "manager-equipped") ReleaseOverlay();
    }

    // Saved/current evidence, not a UI bool, authorizes success wording. Tests
    // use the real execution/receipt state and verify that pending/old requests fail.
    internal static bool IsConfirmedResultCurrent(QuestStaffGrantExecution operation, string quest, GameDataRestoreQuery query)
        => operation != null && ReferenceEquals(operation.Query, query) && operation.QuestId == quest
            && operation.LocalCompleted && operation.CompletionCount == 1 && operation.Request != null
            && operation.Request.Status == GameDataSaveRequestStatus.SuccessConfirmed && operation.Request.Receipt != null;

    private void PollRecipe()
    {
        UIRecipeTab recipe = _shop.RecipeView;
        if (!_shop.IsReadyForDetail || _mainNav.FirstView != _shop || recipe == null || !recipe.gameObject.activeInHierarchy
            || recipe.SelectedData == null || recipe.SelectedData.Id != "FOOD02")
        { ReleaseOverlay(); return; }
        if (!UserInfo.IsGiveRecipe("FOOD02"))
        {
            _recipeObservedUnowned = true;
            if (!_recipeFocused) _recipeFocused = recipe.FocusRecipe("FOOD02");
            ShowDialogueHint("recipe-learn", recipe.BuyButtonRect, RecipeLearnPages);
        }
        else if (_recipeObservedUnowned && !_recipeFollowupShown)
        {
            _recipeFollowupShown = ShowInformationHint("recipe-learned", RecipeLearnedPages);
        }
        else if (_hintKey != "recipe-learned") ReleaseOverlay();
    }

    private bool IsTableThreeViewCurrent()
        => IsContextCurrent() && _shop.IsReadyForDetail && _shop.FurnitureView != null
            && _mainNav.FirstView == _shop.FurnitureView && _shop.FurnitureView.VisibleState == VisibleState.Appeared
            && _shop.FurnitureView.SelectedData != null && _shop.FurnitureView.SelectedData.Id == "TABLE01_03";

    private void PollWallpaper()
    {
        var furniture = _shop.FurnitureView;
        if (_wallpaperPending)
        {
            if (!_shop.IsReadyForDetail || furniture == null || _mainNav.FirstView != furniture
                || furniture.VisibleState != VisibleState.Appeared || furniture.SelectedData?.Id != "WALLPAPER01")
            { _wallpaperPending = false; ReleaseOverlay(); return; }
            if (!UserInfo.IsGiveFurniture(UserInfo.CurrentStage, "WALLPAPER01"))
            {
                if (furniture.SelectedData.BuyPrice != 0) { _wallpaperPending = false; ReleaseOverlay(); return; }
                ShowDialogueHint("wallpaper-free", furniture.BuyButtonRect, WallpaperFreePages);
                return;
            }
            if (UserInfo.IsEquipFurniture(UserInfo.CurrentStage, furniture.SelectedData))
            {
                _wallpaperPending = false;
                _wallpaperFinished = true;
                ReleaseOverlay();
                if (IsContextCurrent()) _shop.ShowQuestFurniture("TABLE01_03");
            }
            else ShowHint("wallpaper-equip", furniture.EquipButtonRect, "선물로 받은 벽지를 배치해볼까요?\n배치하기 버튼을 눌러주세요.");
            return;
        }
        if (!IsTableThreeViewCurrent()) { _tableVisibleSince = -1f; ReleaseOverlay(); return; }
        if (UserInfo.IsGiveFurniture(UserInfo.CurrentStage, "TABLE01_03"))
        { ReleaseOverlay(); return; }
        if (_wallpaperFinished)
        {
            if (UserInfo.IsScoreValid(furniture.SelectedData))
                ShowDialogueHint("wallpaper-table-ready", furniture.BuyButtonRect, TableReadyPages);
            else ReleaseOverlay();
            return;
        }
        if (UserInfo.IsScoreValid(furniture.SelectedData)) { ReleaseOverlay(); return; }
        if (_tableVisibleSince < 0f) _tableVisibleSince = Time.unscaledTime;
        if (Time.unscaledTime - _tableVisibleSince < 1f) return;
        if (!OpenOverlay()) return;
        CancelHintDialogue();
        _hintKey = "wallpaper-explanation";
        _hintTarget = null;
        _guide.BeginGuidancePassthrough(null);
        _description.EndGuidancePassthrough();
        _dialogueActive = true;
        _dialogue = StartCoroutine(ExplainWallpaper());
    }

    private IEnumerator ExplainWallpaper()
    {
        int lifetime = _hintLifetime;
        foreach (string page in WallpaperExplanationPages)
        {
            if (lifetime != _hintLifetime || !_dialogueActive || !_ownsOverlay) yield break;
            if (!IsTableThreeViewCurrent()) { CancelPresentation(); yield break; }
            yield return _description.ShowDescription2Text(page);
        }
        if (lifetime != _hintLifetime || !_dialogueActive || !_ownsOverlay) yield break;
        _dialogueActive = false;
        _dialogue = null;
        bool current = IsTableThreeViewCurrent() && !UserInfo.IsGiveFurniture(UserInfo.CurrentStage, "TABLE01_03")
            && !UserInfo.IsScoreValid(_shop.FurnitureView.SelectedData);
        ReleaseOverlay();
        if (current && _shop.ShowQuestFurniture("WALLPAPER01")) _wallpaperPending = true;
    }

    private bool OpenOverlay()
    {
        if (_ownsOverlay) return _guide.gameObject.activeInHierarchy && _description.gameObject.activeInHierarchy;
        if (_tutorialNav == null || _tutorialNav.Count != 0 || !_tutorialNav.ViewsVisibleStateCheck()
            || UserInfo.IsTutorialStart) return false;
        _guidePop = _guide.PopEnabled;
        _descriptionPop = _description.PopEnabled;
        _tutorialNav.Push("UITutorial");
        _tutorialNav.Push("UITutorialDescription");
        _ownsOverlay = _tutorialNav.CheckActiveView("UITutorial") && _tutorialNav.CheckActiveView("UITutorialDescription");
        if (_ownsOverlay) _description.SkipButtonSetActive(false);
        return _ownsOverlay;
    }

    private void ShowHint(string key, RectTransform target, string text)
    {
        ShowDialogueHint(key, target, new[] { text });
    }

    private bool ShowInformationHint(string key, string[] pages)
    {
        if (!OpenOverlay()) return false;
        if (_hintKey == key && _hintTarget == null) return true;
        CancelHintDialogue();
        _hintKey = key;
        _hintTarget = null;
        _guide.BeginGuidancePassthrough(null);
        _description.EndGuidancePassthrough();
        _hintDialogue = StartCoroutine(ExplainInformationHint(_hintLifetime, key, pages));
        return true;
    }

    private IEnumerator ExplainInformationHint(int lifetime, string key, string[] pages)
    {
        foreach (string page in pages)
        {
            if (!IsInformationHintCurrent(lifetime, key)) yield break;
            yield return _description.ShowDescription2Text(page);
        }
        if (!IsInformationHintCurrent(lifetime, key)) yield break;
        _hintDialogue = null;
        // Informational follow-ups use the same click-to-advance dialogue as
        // action hints. No fixed display timer delays or dismisses user input.
        ReleaseOverlay();
    }

    private bool IsInformationHintCurrent(int lifetime, string key)
        => lifetime == _hintLifetime && _ownsOverlay && _hintKey == key && _hintTarget == null;

    private void ShowDialogueHint(string key, RectTransform target, string[] pages)
    {
        if (target != null && !target.gameObject.activeInHierarchy) { ReleaseOverlay(); return; }
        if (target == null) { ReleaseOverlay(); return; }
        if (!OpenOverlay()) return;
        if (_hintKey == key && _hintTarget == target) return;
        CancelHintDialogue();
        _hintKey = key;
        _hintTarget = target;
        _guide.BeginGuidancePassthrough(null);
        if (pages.Length == 0)
        {
            CompleteActionHint(_hintLifetime, key, target);
            return;
        }

        // The user reads and advances the same dialogue used by the tutorial.
        // Only then reveal the real control; no NPC card covers the purchase or
        // placement button, and no timed callback can hide the following hint.
        _description.EndGuidancePassthrough();
        _hintDialogue = StartCoroutine(ExplainActionHint(_hintLifetime, key, target, pages));
    }

    private IEnumerator ExplainActionHint(int lifetime, string key, RectTransform target, string[] pages)
    {
        foreach (string page in pages)
        {
            if (!IsActionHintCurrent(lifetime, key, target)) yield break;
            yield return _description.ShowDescription2Text(page);
        }
        CompleteActionHint(lifetime, key, target);
    }

    private bool IsActionHintCurrent(int lifetime, string key, RectTransform target)
        => lifetime == _hintLifetime && _ownsOverlay && _hintKey == key && _hintTarget == target
            && target != null && target.gameObject.activeInHierarchy;

    private void CompleteActionHint(int lifetime, string key, RectTransform target)
    {
        if (lifetime != _hintLifetime || !_ownsOverlay || _hintKey != key || _hintTarget != target)
            return;
        _hintDialogue = null;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            ReleaseOverlay();
            return;
        }
        _description.BeginGuidancePassthrough();
        _guide.BeginGuidancePassthrough(target);
    }

    private void CancelHintDialogue()
    {
        ++_hintLifetime;
        if (_hintDialogue != null) StopCoroutine(_hintDialogue);
        _hintDialogue = null;
    }

    private void CancelPresentation()
    {
        if (_dialogue != null) StopCoroutine(_dialogue);
        _dialogue = null;
        _dialogueActive = false;
        _tableVisibleSince = -1f;
        _wallpaperPending = false;
        _waitingForEquip = false;
        ReleaseOverlay();
    }

    private void ReleaseOverlay()
    {
        CancelHintDialogue();
        _hintKey = null;
        _hintTarget = null;
        if (!_ownsOverlay) return;
        _ownsOverlay = false;
        if (_guide != null) { _guide.EndGuidancePassthrough(); _guide.PopEnabled = true; }
        if (_description != null) { _description.EndGuidancePassthrough(); _description.PopEnabled = true; }
        try
        {
            if (_tutorialNav != null)
            {
                if (_tutorialNav.CheckActiveView("UITutorialDescription")) _tutorialNav.Pop("UITutorialDescription");
                if (_tutorialNav.CheckActiveView("UITutorial")) _tutorialNav.Pop("UITutorial");
            }
        }
        finally
        {
            if (_guide != null) _guide.PopEnabled = _guidePop;
            if (_description != null) _description.PopEnabled = _descriptionPop;
        }
    }

    private void OnDisable() => CancelPresentation();
    private void OnDestroy() => CancelPresentation();
}
