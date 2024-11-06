using Muks.UI;
using System.Collections;
using UnityEngine;

public class MiniGameTutorial : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UINavigationCoordinator _coordinator;
    [SerializeField] private UINavigation _mainNav;
    [SerializeField] private UINavigation _tutorialNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _descriptionNPC;
    [SerializeField] private UIGacha _uiGacha;

    private Coroutine _coroutine;
    private bool _isStarted;
    public bool IsStarted => _isStarted;

    private bool _gachaCompleted;

    private void Awake()
    {
        _isStarted = false;
    }

    public void StartTutorial(FoodData foodData, Transform holePos)
    {
        if (_isStarted)
            return;

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(TutorialRoutine(foodData, holePos));
    }


    private IEnumerator TutorialRoutine(FoodData foodData, Transform holePos)
    {
        _isStarted = true;
        while (0 < _coordinator.GetOpenViewCount())
            yield return null;

        _tutorialNav.Push("UITutorial");
        _uiTutorial.ScreenButtonSetActive(true);
        _tutorialNav.Push("UITutorialDescription");

        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.CustomHoleSetActive(true, holePos);
        _uiTutorial.SetCustomHoleTargetObjectName("none");
        yield return _descriptionNPC.ShowDescription2Text("새롭게 등장한 손님이 새로운 메뉴를 원하네요!");
        yield return _descriptionNPC.ShowDescription2Text("제조법이 필요한 메뉴를 배워봅시다!");
        yield return _descriptionNPC.ShowDescription2Text("제조법을 모르는 메뉴는 배울 수가 없어요.");
        yield return _descriptionNPC.ShowDescription2Text("따라서 캡슐 머신을 통해 음식을 위한 레시피를 구해야 한답니다.");
        _uiTutorial.CustomHoleSetActive(false, holePos);
        yield return YieldCache.WaitForSeconds(2f);

        _mainNav.Push("UIGacha");
        _tutorialNav.Push("UITutorial");
        _tutorialNav.Push("UITutorialDescription");
        _uiGacha.SingleButton.gameObject.SetActive(false);
        _uiTutorial.Gacha1ButtonSetActive(true);
        GachaItemData needItemData = ItemManager.Instance.GetGachaItemData("GOTCHA01");
        _uiTutorial.SetGacha1ButtonClickEvent(() => _uiGacha.GetItem(needItemData));
        yield return YieldCache.WaitForSeconds(1f);
        yield return _descriptionNPC.ShowDescription2Text("이곳은 아이템을 뽑을 수 있는 곳입니다.");
        yield return _descriptionNPC.ShowDescription2Text("캡슐 머신에는 다양한 아이템들이 나온답니다!");
        yield return _descriptionNPC.ShowDescription2Text("아이템을 이용해 새 레시피를 배우거나, \n능력을 강화할 수 있습니다.");
        yield return _descriptionNPC.ShowDescription2Text("그럼 캡슐을 뽑아봅시다! \n첫 회는 제가 살게요!");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.Gacha1HoleSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _descriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
    }
}
