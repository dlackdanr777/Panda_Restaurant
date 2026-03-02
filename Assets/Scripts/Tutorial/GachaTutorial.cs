using Muks.UI;
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
 
    private Coroutine _coroutine;
    private bool _gachaCompleted;
    private FoodData _foodData;

    public void StartTutorial()
    {
        if (UserInfo.IsTutorialStart)
            return;

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

        yield return _descriptionNPC.ShowDescription1Text("가챠샵에 오신 걸 환영합니다!");
        yield return _descriptionNPC.ShowDescription1Text("다이아를 사용해서 평소에 얻기 힘든\n아이템을 획득할 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("레시피, 스킨 그리고 가게 스탯을\n강화하는 아이템을 얻을 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("가챠를 한번 뽑아볼까요?");
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.Gacha1ButtonSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, 350, "Tutorial Gacha1 Button", _uiTutorial.Gacha1Button.transform);
        _uiTutorial.Gacha1Button.AddListener(() => _itemGacha.StartAddItem(ItemManager.Instance.GetGachaItemData("GOTCHA91")));
        _itemGacha.SingleButton.gameObject.SetActive(false);
        _tutorialNav.Push("UITutorial");
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);

        _uiTutorial.Gacha1ButtonSetActive(false);
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _mainNav.Push("UIGacha");
        _uiGacha.GachaStepHandler += OnGachaCompletedEvent;
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
        _uiTutorial.CustomHoleSetActive(true, 200, _shopButton.name, _shopButton.transform);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);


        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.CustomHoleSetActive(true, 250, _recipeButton.name, _recipeButton.transform);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.01f);
            
        yield return YieldCache.WaitForSeconds(1f);
        yield return _descriptionNPC.ShowDescription1Text("가챠로 얻은 레시피는\n미니게임을 통해서 얻을 수 있습니다.");
        yield return _descriptionNPC.ShowDescription1Text("한번 김치찌개를 만들어 볼까요?");

        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        UserInfo.IsMiniGameTutorialClear = true;
        gameObject.SetActive(false);

    }


    // private IEnumerator TutorialRoutine(FoodData foodData, Transform holePos)
    // {
    //     _descriptionNPC.OnSkipOkButtonClicked(OnSkipButtonClicked);
    //     _foodData = foodData;
    //     _mainNav.AllPop();
    //     while (0 < _coordinator.GetOpenViewCount())
    //         yield return null;

    //     UserInfo.IsTutorialStart = true;
    //     _tutorialNav.Push("UITutorial");
    //     _uiTutorial.ScreenButtonSetActive(true);
    //     _tutorialNav.Push("UITutorialDescription");

    //     yield return YieldCache.WaitForSeconds(1f);
    //     _uiTutorial.PunchHoleSetActive(true);
    //     _uiTutorial.CustomHoleSetActive(true, 250, "none", holePos);
    //     yield return _descriptionNPC.ShowDescription2Text("새롭게 등장한 손님이 새로운 메뉴를 원하네요!");
    //     yield return _descriptionNPC.ShowDescription2Text("제조법이 필요한 메뉴를 배워봅시다!");
    //     _uiTutorial.CustomHoleSetActive(false, 250, "none", holePos);
    //     yield return YieldCache.WaitForSeconds(1);
    //     _uiTutorial.CustomHoleSetActive(true, 170, _shopButton.name, _shopButton.transform, false);
    //     while (!_uiTutorial.IsButtonClicked)
    //         yield return YieldCache.WaitForSeconds(0.02f);

    //     _tutorialNav.Push("UITutorial");
    //     _uiTutorial.CustomHoleSetActive(false, 170, "none", _shopButton.transform);
    //     yield return YieldCache.WaitForSeconds(1);
    //     _uiTutorial.RecipeHoleSetActive(true);
    //     _uiRecipe.SetView(_foodData);
    //     while (!_uiTutorial.IsButtonClicked)
    //         yield return YieldCache.WaitForSeconds(0.02f);

    //     yield return YieldCache.WaitForSeconds(1);
    //     _uiTutorial.BuyHoleSetActive(true);
    //     _uiTutorial.SetBuyHoleTargetObjectName("none");
    //     yield return YieldCache.WaitForSeconds(2);
    //     _tutorialNav.Push("UITutorialDescription");
    //     yield return _descriptionNPC.ShowDescription2Text("제조법을 모르는 메뉴는 배울 수가 없어요.");
    //     yield return _descriptionNPC.ShowDescription2Text("따라서 캡슐 머신을 통해 음식을 위한 레시피를 구해야 한답니다.");
    //     _uiTutorial.BuyHoleSetActive(false);
    //     yield return YieldCache.WaitForSeconds(1);
    //     _uiTutorial.ExitHoleSetActive(true);
    //     while (!_uiTutorial.IsButtonClicked)
    //         yield return YieldCache.WaitForSeconds(0.02f);

    //     _tutorialNav.Push("UITutorial");
    //     yield return YieldCache.WaitForSeconds(2);
    //     _uiTutorial.CustomHoleSetActive(true, 170, _gachaButton.name, _gachaButton.transform, false);
    //     while (!_uiTutorial.IsButtonClicked)
    //         yield return YieldCache.WaitForSeconds(0.02f);

    //     _tutorialNav.Push("UITutorial");
    //     _tutorialNav.Push("UITutorialDescription");
    //     _uiTutorial.CustomHoleSetActive(false, 170, _gachaButton.name, _gachaButton.transform, false);
    //     //_uiGacha.SingleButton.gameObject.SetActive(false);
    //     _uiTutorial.Gacha1ButtonSetActive(true);
    //     GachaItemData needItemData = ItemManager.Instance.GetGachaItemData(_foodData.NeedItem);
    //     //_uiTutorial.SetGacha1ButtonClickEvent(() => _uiGacha.GetItem(needItemData));
    //     yield return YieldCache.WaitForSeconds(2f);
    //     yield return _descriptionNPC.ShowDescription2Text("이곳은 아이템을 뽑을 수 있는 곳입니다.");
    //     yield return _descriptionNPC.ShowDescription2Text("캡슐 머신에는 다양한 아이템들이 나온답니다!");
    //     yield return _descriptionNPC.ShowDescription2Text("아이템을 이용해 새 레시피를 배우거나, \n능력을 강화할 수 있습니다.");
    //     yield return _descriptionNPC.ShowDescription2Text("그럼 캡슐을 뽑아봅시다! \n첫 회는 제가 살게요!");
    //     yield return YieldCache.WaitForSeconds(1);
    //     _uiTutorial.CustomHoleSetActive(true, 350, "Tutorial Gacha1 Button", _uiTutorial.Gacha1Button.transform);
    //     _tutorialNav.Push("UITutorial");
    //     while (!_uiTutorial.IsButtonClicked)
    //         yield return YieldCache.WaitForSeconds(0.02f);

    //     _uiTutorial.Gacha1ButtonSetActive(false);
    //     _descriptionNPC.PopEnabled = true;
    //     _uiTutorial.PopEnabled = true;
    //     yield return YieldCache.WaitForSeconds(0.02f);
    //     _tutorialNav.Pop("UITutorialDescription");
    //     _tutorialNav.Pop("UITutorial");
    //     _mainNav.Push("UIGacha");
    //     _uiGacha.GachaStepHandler += OnGachaCompletedEvent;
    //     while (!_gachaCompleted)
    //         yield return YieldCache.WaitForSeconds(0.01f);

    //     _uiGacha.GachaStepHandler -= OnGachaCompletedEvent;
    //     //_uiGacha.SingleButton.gameObject.SetActive(true);
    //     _gachaCompleted = false;
    //     _descriptionNPC.PopEnabled = false;
    //     _uiTutorial.PopEnabled = false;
    //     _tutorialNav.Push("UITutorial");
    //     _tutorialNav.Push("UITutorialDescription");
    //     DebugLog.Log("Tutorial: Gacha Completed");
    //     _uiTutorial.ScreenButtonSetActive(true);
    //     yield return YieldCache.WaitForSeconds(1);
    //     yield return _descriptionNPC.ShowDescription1Text("이제 레시피를 통해서 새로운 메뉴를 제작할 수 있어요.");
    //     yield return _descriptionNPC.ShowDescription1Text("새 손님을 위해 바로 음식을 만들러 가봐요!");

    //     _descriptionNPC.PopEnabled = true;
    //     _uiTutorial.PopEnabled = true;
    //     yield return YieldCache.WaitForSeconds(0.02f);
    //     _tutorialNav.Pop("UITutorialDescription");
    //     _tutorialNav.Pop("UITutorial");
    //     _descriptionNPC.PopEnabled = false;
    //     _uiTutorial.PopEnabled = false;
    //     UserInfo.IsTutorialStart = false;
    //     UserInfo.IsMiniGameTutorialClear = true;
    //     gameObject.SetActive(false);
    // }



    private void OnGachaCompletedEvent(int step)
    {
        if (step < 4)
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
        if(!UserInfo.IsGiveGachaItem(needItemData))
            UserInfo.GiveGachaItem(needItemData);

        UserInfo.IsTutorialStart = false;
        UserInfo.IsMiniGameTutorialClear = true;
        gameObject.SetActive(false);
    }
}
