using Muks.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FloorUnlockTutorial : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UINavigation _tutorialNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _descriptionNPC;
 
    private Coroutine _coroutine;

    public void StartFloor2Tutorial()
    {
        if (UserInfo.IsTutorialStart)
            return;

        gameObject.SetActive(true);
        if (_coroutine != null)
            StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(TutorialFloor2Routine());
    }


    private IEnumerator TutorialFloor2Routine()
    {
        yield return YieldCache.WaitForSeconds(1f);
        _descriptionNPC.OnSkipOkButtonClicked(OnSkipButtonClicked);

        UserInfo.IsTutorialStart = true;
        _tutorialNav.Push("UITutorial");
        _uiTutorial.ScreenButtonSetActive(true);
        _tutorialNav.Push("UITutorialDescription");
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        
        yield return YieldCache.WaitForSeconds(1f);
        yield return _descriptionNPC.ShowDescription1Text("이제 2층을 운영할 수 있습니다!");
        yield return _descriptionNPC.ShowDescription1Text("그동안 모아둔 가구와 스텝을\n2층에도 새롭게 배치할 수 있어요.");
        yield return _descriptionNPC.ShowDescription1Text("적절히 스텝을 배치해야\n가게가 자동으로 운영됩니다.");
        yield return _descriptionNPC.ShowDescription1Text("기존 공간과는 다른 분위기로\n2층을 꾸며보는 건 어떨까요?");
        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        gameObject.SetActive(false);
    }

    private void OnSkipButtonClicked()
    {
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;

        UserInfo.IsTutorialStart = false;
        gameObject.SetActive(false);
    }
}
