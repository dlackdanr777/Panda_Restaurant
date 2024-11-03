using Muks.MobileUI;
using Muks.Tween;
using System.Collections;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class Tutorial1 : MonoBehaviour
{
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private GameObject _mainSceneUI;
    [SerializeField] private UITutorial1 _uiTutorial1;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;


    [Header("Objects")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _kitchenCameraPos;
    [SerializeField] private Transform _restaurantCameraPos;
    [SerializeField] private GameObject _table1;
    [SerializeField] private GameObject _burner1;
    [SerializeField] private GameObject _cobWeb1;
    [SerializeField] private GameObject _cobWeb2;
    [SerializeField] private GameObject _cobWeb3;
    [SerializeField] private GameObject _cobWeb4;
    [SerializeField] private GameObject _garbage1;
    [SerializeField] private GameObject _garbage2;
    [SerializeField] private GameObject _garbage3;
    [SerializeField] private GameObject _garbage4;
    [SerializeField] private ParticleSystem[] _boomParticles;
    [SerializeField] private ParticleSystem _table1BoomParticle;
    [SerializeField] private ParticleSystem _burner1BoomParticle;
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
        UserInfo.GiveFurniture("COUNTER01");
        UserInfo.SetEquipFurniture("COUNTER01");
        UserInfo.GiveFurniture("WALLPAPER01");
        UserInfo.SetEquipFurniture("WALLPAPER01");
        UserInfo.GiveStaff("STAFF11");
        UserInfo.SetEquipStaff("STAFF11");
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

        _uiTutorial1.SetTableHoleTargetObjectName("FurnitureTabSlot1");
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

        _uiTutorial1.SetTableHoleTargetObjectName("KitchenTabSlot1");
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
        _table1.SetActive(false);
        _burner1.SetActive(false);
        _camera.transform.position = _kitchenCameraPos.position;
        while (!_uiTutorial1.IsExitHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1.5f);
        _camera.TweenMove(_restaurantCameraPos.position, 5, Ease.Smoothstep);
        yield return YieldCache.WaitForSeconds(1f);
        _burner1.SetActive(true);
        _burner1BoomParticle.Emit(1);
        yield return YieldCache.WaitForSeconds(2.5f);
        _table1.SetActive(true);
        _table1BoomParticle.Emit(1);
        yield return YieldCache.WaitForSeconds(4f);

        yield return _uiDescriptionNPC.ShowDescription2Text("���� �մ��� ������ \n�غ�� �Ϸ��׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("������ �մԵ��� \n���� �츮 ���Ը� �� �𸦰ſ���.");

        _uiTutorial1.AddCustomerButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �ϴܿ� �ִ� ����� \n�մ��� �ҷ� ���� �� �ִ� \nƯ���� �����̶��ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("���縦 ���� �մ��� ȣ���� ���ô�!");
        _uiTutorial1.AddCustomerHoleSetActive(true);
        while (!_uiTutorial1.IsAddCustomerEventEnabled)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial1.AddCustomerButtonSetActive(false);
        _uiTutorial1.AddCustomerHoleSetActive(false);
        yield return YieldCache.WaitForSeconds(5);
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϵ����. �����! \n���� ������ ù ��° �մ��̳׿�!");

        _uiTutorial1.CustomerGuideButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("ī���Ϳ��� �� ���̺�� �ȳ��� �ּ���!");
        _uiTutorial1.CustomerGuideHoleSetActive(true);
        while (!_uiTutorial1.IsCustomerGuideButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(9);
        _uiTutorial1.OrderButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("�մ��� �ָԹ��� �ֹ��ϼ̳׿�!");
        _uiTutorial1.ShopButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("�׷�, ���� �ָԹ� �����Ǹ� ������ô�!");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.ShopMaskSetActive(true);
        while (!_uiTutorial1.IsShopButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        _uiTutorial1.OrderButtonSetActive(false);
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.RecipeHoleSetActive(true);
        while (!_uiTutorial1.IsRecipeHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveRecipe("FOOD01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϼ̽��ϴ�! \n�̰ɷ� �ֹ濡�� �ָԹ��� \n������ �� �ְ� �Ǿ����!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �ⱸ�� ȿ���� ���� \n���� ���� �ð��� �پ���ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�� ���� �մ��� ������Ű�� ���ؼ� \n�ֹ��� ���׷��̵��� �ʿ䰡 �ְ���?");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.ExitHoleSetActive(true);
        while (!_uiTutorial1.IsExitHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial1.OrderButtonSetActive(true);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("�� ���� �ֹ��� �޾ƺ��ô�.");
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial1.OrderHoleSetActive(true);

        while (!_uiTutorial1.IsOrderButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(2f);
        _uiTutorial1.SetOrderHoleTargetObjectName("Tutorial1 Serving Button");
        _uiTutorial1.ServingButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("�ָԹ��� �ϼ��Ǿ�����! \n�ϼ��� ������ ��ư�� ���� ������ �� �ֽ��ϴ�.");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.OrderHoleSetActive(true);
        while (!_uiTutorial1.IsOrderButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription2Text("ù �մ��� �����Ͻ� �� ���׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("�����ϼ̾��. ���� �����!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �������� �ȳ��� �������ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("ó������ �����ߴµ�, \n�������� �� ������ó׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("�׷�, ������ �� ���Ը� �� ��Ź�帳�ϴ�!");

        UserInfo.Tutorial1Clear = true;
        _mainSceneUI.gameObject.SetActive(true);

        _uiTutorial1.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Pop("UITutorial1");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial1.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        gameObject.SetActive(false);
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
