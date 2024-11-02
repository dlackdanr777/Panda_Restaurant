using Muks.MobileUI;
using System.Collections;
using UnityEngine;

public class Tutorial1 : MonoBehaviour
{
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private GameObject _mainSceneUI;
    [SerializeField] private UITutorial1 _uiTutorial1;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;


    [Header("Objects")]
    [SerializeField] private GameObject _cobWeb1;
    [SerializeField] private GameObject _cobWeb2;
    [SerializeField] private GameObject _cobWeb3;
    [SerializeField] private GameObject _cobWeb4;
    [SerializeField] private GameObject _garbage1;
    [SerializeField] private GameObject _garbage2;
    [SerializeField] private GameObject _garbage3;
    [SerializeField] private GameObject _garbage4;
    [SerializeField] private ParticleSystem[] _boomParticles;
    [SerializeField] private PunchHoleAnimation _punchHole;

    private int _touchCount;

    private void Start()
    {
        if (UserInfo.Tutorial1Clear)
        {
            gameObject.SetActive(false);
            return;
        }

        _mainSceneUI.gameObject.SetActive(false);
        _punchHole.gameObject.SetActive(false);
        StartCoroutine(StartTutorial());
    }


    private IEnumerator StartTutorial()
    {
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Push("UITutorial1");
        _uiNav.Push("UITutorialDescription");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription1Text("�ȳ��ϼ���.   \n�̹��� ���� �� ������̽���?");
        yield return _uiDescriptionNPC.ShowDescription1Text("���� ������� ��Ȱ�� ���� ��� ���� \n�����ϰ� �ȳ��� �帱 \"����\" �Դϴ�.");
        yield return _uiDescriptionNPC.ShowDescription1Text("�ϳ��ϳ� ������ �帱 �״� õõ�� ���������!");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� ���� ������ �ʿ��ϰڱ���.");
        _punchHole.gameObject.SetActive(true);
        _punchHole.TweenScale(1.5f, 1, 0.35f, Muks.Tween.Ease.OutBack);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("ȭ�� ��ġ�� ������ ������ �ּ���!");
        _uiTutorial1.ScreenButtonSetActive(true);
        _punchHole.gameObject.SetActive(false);
        _uiTutorial1.StartTouch(Step2TouchEvent);
        while(_touchCount < 3)
            yield return YieldCache.WaitForSeconds(0.02f);
        _touchCount = 0;
        _uiTutorial1.StopTouch();
        _uiTutorial1.ScreenButtonSetActive(false);

        _garbage1.gameObject.SetActive(false);
        _garbage2.gameObject.SetActive(false);
        _garbage3.gameObject.SetActive(false);
        _garbage4.gameObject.SetActive(false);
        _cobWeb1.gameObject.SetActive(false);
        _cobWeb2.gameObject.SetActive(false);
        _cobWeb3.gameObject.SetActive(false);
        _cobWeb4.gameObject.SetActive(false);

        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("�Ǹ��ؿ�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("������ �� �ɽ�����?");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �Ĵ��̶�� \n�θ��� ���� ����̳׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�켱 ���̺��� \n��ġ�� �����?");
        _uiTutorial1.ShopButtonSetActive(true);
        _uiTutorial1.PunchHoleSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �������� �� \n���̺��� ������ �ּ���.");
        _uiTutorial1.ShopMaskSetActive(true);

        while (!_uiTutorial1.IsShopButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        _uiTutorial1.ShopButtonSetActive(false);
        _uiTutorial1.ShopMaskSetActive(false);
        _uiTutorial1.PunchHoleSetActive(false);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.PunchHoleSetActive(true);
        _uiTutorial1.TableHoleSetActive(true);
        
        while(!_uiTutorial1.IsTableHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Buy Button");
        while(!UserInfo.IsGiveFurniture("TABLE01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Equip Button");
        FurnitureData table1Data = FurnitureDataManager.Instance.GetFurnitureData("TABLE01_01");

        while (!UserInfo.IsEquipFurniture(table1Data))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("�������� ������ �����ϱ� ���� \n���� �ⱸ�� ��ġ�� �����?");
        yield return _uiDescriptionNPC.ShowDescription2Text("�ֹ� ī�װ��� �̵��� \n���� �ⱸ�� ������ �ּ���!");
        yield return YieldCache.WaitForSeconds(1);

        _uiNav.Push("UITutorial1");
        _uiTutorial1.BackHoleSetActive(true);
        while(!_uiTutorial1.IsBackHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.KitchenHoleSetActive(true);
        while (!_uiTutorial1.IsKitchenHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.TableHoleSetActive(true);
        while(!_uiTutorial1.IsTableHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveKitchenUtensil("COOKER01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Equip Button");
        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData("COOKER01_01");
        while (!UserInfo.IsEquipKitchenUtensil(kitchenData))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϼ̽��ϴ�! \n�մ��� ������ �غ� �����̳׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� ��������� \n�ѹ� Ȯ���� �����?");

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.ExitHoleSetActive(true);
        while(!_uiTutorial1.IsExitHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1.3f);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �մ��� ������ \n�غ�� �Ϸ��׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("������ �մԵ��� \n���� �츮 ���Ը� �� �𸦰ſ���.");
    }

    private void Step2TouchEvent()
    {
        if (3 < _touchCount)
            return;

        _touchCount++;

        for(int i = 0, cnt = _boomParticles.Length; i < cnt; ++i)
        {
            _boomParticles[i].Emit(1);
        }
    }

}
