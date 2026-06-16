using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections;

public class RecipeTutorial : MonoBehaviour
{
    [SerializeField] private UIRecipeTab _uiRecipeTab;
    [SerializeField] private string _targetChallengeId;

    [Space]
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;

    private void Start()
    {
        ChallengeManager.Instance.OnMainChallengeUpdateHandler += RefreshRecipeBuyEventBinding;
        UserInfo.OnClearChallengeHandler += RefreshRecipeBuyEventBinding;
        RefreshRecipeBuyEventBinding();
    }

    private void OnDestroy()
    {
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.OnMainChallengeUpdateHandler -= RefreshRecipeBuyEventBinding;
        UserInfo.OnClearChallengeHandler -= RefreshRecipeBuyEventBinding;
        StopAllCoroutines();
        if (_uiRecipeTab != null)
            _uiRecipeTab.OnBuyEvent -= OnRecipeBought;
    }

    private void RefreshRecipeBuyEventBinding()
    {
        if (_uiRecipeTab == null) return;

        _uiRecipeTab.OnBuyEvent -= OnRecipeBought;
        StopAllCoroutines();
        ChallengeData current = ChallengeManager.Instance.GetCurrentMainChallengeData();
        if (current != null && current.Id == _targetChallengeId)
            _uiRecipeTab.OnBuyEvent += OnRecipeBought;
    }

    private void OnRecipeBought()
    {
        StopAllCoroutines();
        StartCoroutine(StartTutorial());
    }

    private IEnumerator StartTutorial()
    {
        if (UserInfo.IsTutorialStart || UserInfo.IsRecipeTutorialClear) yield break;

        UserInfo.IsTutorialStart = true;
        _uiNav.Push("UITutorial");
        _uiNav.Push("UITutorialDescription");
        _uiDescriptionNPC.SkipButtonSetActive(false);
        _uiTutorial.ScreenButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(2);
        yield return _uiDescriptionNPC.ShowDescription2Text("새로운 레시피를 배우셨군요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("새로운 레시피를 배우면\n" + Utility.SetStringColor("새로운 손님", ColorType.Positive) + "이 등장합니다!");
        yield return _uiDescriptionNPC.ShowDescription2Text("도감을 통해서 새로운 손님의\n등장조건을 확인할 수 있습니다.");

        _uiTutorial.PunchHoleSetActive(false);
        _uiTutorial.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Pop("UITutorial");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        UserInfo.IsRecipeTutorialClear = true;
    }
}
