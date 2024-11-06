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
        yield return _descriptionNPC.ShowDescription2Text("���Ӱ� ������ �մ��� ���ο� �޴��� ���ϳ׿�!");
        yield return _descriptionNPC.ShowDescription2Text("�������� �ʿ��� �޴��� ������ô�!");
        yield return _descriptionNPC.ShowDescription2Text("�������� �𸣴� �޴��� ��� ���� �����.");
        yield return _descriptionNPC.ShowDescription2Text("���� ĸ�� �ӽ��� ���� ������ ���� �����Ǹ� ���ؾ� �Ѵ�ϴ�.");
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
        yield return _descriptionNPC.ShowDescription2Text("�̰��� �������� ���� �� �ִ� ���Դϴ�.");
        yield return _descriptionNPC.ShowDescription2Text("ĸ�� �ӽſ��� �پ��� �����۵��� ���´�ϴ�!");
        yield return _descriptionNPC.ShowDescription2Text("�������� �̿��� �� �����Ǹ� ���ų�, \n�ɷ��� ��ȭ�� �� �ֽ��ϴ�.");
        yield return _descriptionNPC.ShowDescription2Text("�׷� ĸ���� �̾ƺ��ô�! \nù ȸ�� ���� ��Կ�!");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.Gacha1HoleSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _descriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
    }
}
