using Muks.MobileUI;
using Muks.Tween;
using System.Collections;
using UnityEngine;

public class FirstTutorial : MonoBehaviour
{
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private GameObject _mainSceneUI;
    [SerializeField] private UITutorial _uiTutorial;
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
        if (UserInfo.IsFirstTutorialClear)
        {
            gameObject.SetActive(false);
            return;
        }

        _mainSceneUI.gameObject.SetActive(false);
        _punchHole.gameObject.SetActive(false);
        UserInfo.IsTutorialStart = true;
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
        _uiNav.Push("UITutorial");
        _uiNav.Push("UITutorialDescription");
        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription1Text("�ȳ��ϼ���.   \n�̹��� ���� �� ������̽���?");
        yield return _uiDescriptionNPC.ShowDescription1Text("���� ������� ��Ȱ�� ���� ��� ���� �����ϰ� �ȳ��� �帱 \"����\" �Դϴ�.");
        yield return _uiDescriptionNPC.ShowDescription1Text("�ϳ��ϳ� ������ �帱 �״� õõ�� ���������!");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� ���� ������ �ʿ��ϰڱ���.");
        _punchHole.gameObject.SetActive(true);
        _punchHole.TweenScale(1.5f, 1, 0.35f, Muks.Tween.Ease.OutBack);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("ȭ�� ��ġ�� ������ ������ �ּ���!");
        _uiTutorial.ScreenButtonSetActive(true);
        _punchHole.gameObject.SetActive(false);
        _uiTutorial.StartTouch(Step2TouchEvent);
        while(_touchCount < 3)
            yield return YieldCache.WaitForSeconds(0.02f);
        _touchCount = 0;
        _uiTutorial.StopTouch();
        _uiTutorial.ScreenButtonSetActive(false);

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
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �Ĵ��̶� �θ��� ���� ����̳׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�켱 ���̺��� ��ġ�� �����?");
        _uiTutorial.ShopButtonSetActive(true);
        _uiTutorial.PunchHoleSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �������� �� ���̺��� ������ �ּ���.");
        _uiTutorial.ShopMaskSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        _uiTutorial.ShopButtonSetActive(false);
        _uiTutorial.ShopMaskSetActive(false);
        _uiTutorial.PunchHoleSetActive(false);

        _uiTutorial.SetTableHoleTargetObjectName("FurnitureTabSlot1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.TableHoleSetActive(true);
        
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while(!UserInfo.IsGiveFurniture("TABLE01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        FurnitureData table1Data = FurnitureDataManager.Instance.GetFurnitureData("TABLE01_01");

        while (!UserInfo.IsEquipFurniture(table1Data))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("�������� ������ �����ϱ� ���� ���� �ⱸ�� ��ġ�� �����?");
        yield return _uiDescriptionNPC.ShowDescription2Text("�ֹ� ī�װ��� �̵��� ���� �ⱸ�� ������ �ּ���!");
        yield return YieldCache.WaitForSeconds(1);

        _uiNav.Push("UITutorial");
        _uiTutorial.BackHoleSetActive(true);
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.KitchenHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial.SetTableHoleTargetObjectName("KitchenTabSlot1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.TableHoleSetActive(true);
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveKitchenUtensil("COOKER01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData("COOKER01_01");
        while (!UserInfo.IsEquipKitchenUtensil(kitchenData))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϼ̽��ϴ�! \n�մ��� ������ �غ� �����̳׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �Ĵ��� �ѹ� Ȯ���� �����?");

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ExitHoleSetActive(true);
        _table1.SetActive(false);
        _burner1.SetActive(false);
        _camera.transform.position = _kitchenCameraPos.position;
        while (!_uiTutorial.IsButtonClicked)
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

        yield return _uiDescriptionNPC.ShowDescription2Text("���� �մ��� ������ �غ�� �Ϸ��׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("������ �մԵ��� ���� �츮 ���Ը� �� �𸦰ſ���.");

        _uiTutorial.AddCustomerButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �ϴܿ� �ִ� ����� �մ��� �ҷ� ���� �� �ִ� Ư���� �����̶��ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("���縦 ���� �մ��� ȣ���� ���ô�!");
        _uiTutorial.AddCustomerHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial.AddCustomerButtonSetActive(false);
        _uiTutorial.AddCustomerHoleSetActive(false);
        yield return YieldCache.WaitForSeconds(5);
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϵ����. �����! \n���� ������ ù ��° �մ��̳׿�!");

        _uiTutorial.CustomerGuideButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("ī���Ϳ��� �� ���̺�� �ȳ��� �ּ���!");
        _uiTutorial.CustomerGuideHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(8);
        _uiTutorial.OrderButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("�մ��� �ָԹ��� �ֹ��ϼ̳׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("�����Ǹ� ����� ���� ���¿��� �ֹ��� ���� �� �����ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�𸣴� �������� �ֹ��� ������ �մ��� �峭�� ġ�°Ŷ�� ��������?");
        _uiTutorial.ShopButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("�������� �մ��� �Ĵ��� ���������� �����Ǹ� ������ô�!");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ShopMaskSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        _uiTutorial.OrderButtonSetActive(false);
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.RecipeHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveRecipe("FOOD01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("���ϼ̽��ϴ�! \n�̰ɷ� �ֹ濡�� �ָԹ��� ������ �� �ְ� �Ǿ����!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �ⱸ�� ȿ���� ���� ���� ���� �ð��� �پ���ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�� ���� �մ��� ������Ű�� ���ؼ� �ֹ��� ���׷��̵��� �ʿ䰡 �ְ���?");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ExitHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.OrderButtonSetActive(true);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("�� ���� �ֹ��� �޾ƺ��ô�.");
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.OrderHoleSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(2f);
        _uiTutorial.SetOrderHoleTargetObjectName("Tutorial Serving Button");
        _uiTutorial.ServingButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("�ָԹ��� �ϼ��Ǿ�����!");
        yield return _uiDescriptionNPC.ShowDescription2Text("���� �մԿ��� ������ �����غ��ô�.");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.OrderHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription2Text("ù �մ��� �����Ͻ� �� ���׿�!");
        yield return _uiDescriptionNPC.ShowDescription2Text("�մ��� �ִ� �ڸ��� �����⵵ ġ������ؿ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�����Ⱑ ��� ���̸� �ٸ� �մԵ��� ���谨�� �����ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�������� �մ��� �Ĵ��� ���������� �����⸦ ġ���ݽô�!");
        _uiTutorial.Table1HoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("�����ϼ̾��. ���� �����! \n���� �������� �ȳ��� �������ϴ�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("ó������ �����ߴµ�, �������� �� ������ó׿�.");
        yield return _uiDescriptionNPC.ShowDescription2Text("�׷�, ������ �� ���Ը� �� ��Ź�帳�ϴ�!");
        _uiTutorial.PunchHoleSetActive(false);
        UserInfo.IsFirstTutorialClear = true;
        _mainSceneUI.gameObject.SetActive(true);

        _uiTutorial.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Pop("UITutorial");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
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
