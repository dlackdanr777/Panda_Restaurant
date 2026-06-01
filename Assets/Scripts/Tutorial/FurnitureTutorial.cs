using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections;

public class FurnitureTutorial : MonoBehaviour
{
    [SerializeField] private UIFurniture _uiFurniture;
    [SerializeField] private string _targetChallengeId;

    [Space]
    [SerializeField] private Transform _punchHoleTarget;
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;

    private void Start()
    {
        ChallengeManager.Instance.OnMainChallengeUpdateHandler += RefreshFurnitureBuyEventBinding;
        UserInfo.OnClearChallengeHandler += RefreshFurnitureBuyEventBinding;
        RefreshFurnitureBuyEventBinding();
    }

    private void OnDestroy()
    {
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.OnMainChallengeUpdateHandler -= RefreshFurnitureBuyEventBinding;
        UserInfo.OnClearChallengeHandler -= RefreshFurnitureBuyEventBinding;
        StopAllCoroutines();
        if (_uiFurniture != null)
            _uiFurniture.OnBuyEvent -= OnFurnitureBought;
    }

    private void RefreshFurnitureBuyEventBinding()
    {
        if (_uiFurniture == null) return;

        _uiFurniture.OnBuyEvent -= OnFurnitureBought;
        StopAllCoroutines();
        ChallengeData current = ChallengeManager.Instance.GetCurrentMainChallengeData();
        if (current != null && current.Id == _targetChallengeId)
        {
            _uiFurniture.OnBuyEvent += OnFurnitureBought;
            //DebugLog.Log("가구 튜토리얼 이벤트 바인딩");
        }

    }

    private void OnFurnitureBought()
    {
        StopAllCoroutines();
        StartCoroutine(StartTutorial());
    }


    private IEnumerator StartTutorial()
    {
        if(UserInfo.IsTutorialStart || UserInfo.IsFurnitureTutorialClear) yield break;

        UserInfo.IsTutorialStart = true;
        _uiNav.Push("UITutorial");
        _uiNav.Push("UITutorialDescription");
        _uiDescriptionNPC.SkipButtonSetActive(false);
        _uiTutorial.ScreenButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, 400f, "",_punchHoleTarget, false);
        yield return YieldCache.WaitForSeconds(2);
        yield return _uiDescriptionNPC.ShowDescription2Text("가구를 구매하면 평점이 오릅니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("평점이 오르면 새로운 가구나\n레시피를 배울 수 있게 됩니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("평점을 올리고 다양한 아이템을\n구매해보세요!");

        _uiTutorial.PunchHoleSetActive(false);
        _uiTutorial.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Pop("UITutorial");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        UserInfo.IsFurnitureTutorialClear = true;
        gameObject.SetActive(false);
    }
}
