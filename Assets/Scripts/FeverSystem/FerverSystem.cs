using System;
using System.Collections;
using UnityEngine;

public class FeverSystem : MonoBehaviour
{
    public event Action OnStartFeverHandler;
    public event Action OnEndFeverHandler;

    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIFever _uiFever;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private FeverTutorial _feverTutorial;

    private bool _isFeverStart = false;
    public bool IsFeverStart => _isFeverStart;

    private static int _currentMaxFeverGauge = 500;
    public static int CurrentMaxFeverGauge => _currentMaxFeverGauge;
    private int[] _maxFeverGauges = new int[]{550, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1000, 1000, 1000, 1000, 1000, 1000};

    public static float FeverGauge => UserInfo.GetFeverGauge(UserInfo.CurrentStage);
    public void SetFeverGauge(float value) { UserInfo.SetFeverGauge(UserInfo.CurrentStage, value); }
    private Coroutine _feverRoutine = null;

    public void AddFeverGauge(float addMul = 1)
    {
        if (_isFeverStart || UserInfo.IsTutorialStart)
            return;

        // 이미 현재 맥스 이상이면 수정하지 않음 (로드된 게이지가 덮어쓰이는 것 방지)
        if (FeverGauge >= _currentMaxFeverGauge)
            return;

        float newGauge = Mathf.Clamp(FeverGauge + ConstValue.ADD_FEVER_GAUGE * addMul, 0, _currentMaxFeverGauge);
        UserInfo.SetFeverGauge(UserInfo.CurrentStage, newGauge);
        DebugLog.Log($"Fever Gauge : {FeverGauge} / {_currentMaxFeverGauge}");
        _uiFever.OnChangeGauge();
    }

    public void StartTutorial()
    {
        if (FeverGauge >= _currentMaxFeverGauge)
        {
            _feverTutorial.StartTutorial();
        }
    }


    public void FeverStart()
    {
        if(_isFeverStart)
            return;

        if(UserInfo.IsFeverTutorialClear == false)
        {
            _feverTutorial.StartTutorial();
            return;
        }

        _isFeverStart = true;

        if(_feverRoutine != null)
        {
            StopCoroutine(_feverRoutine);
            _feverRoutine = null;
        }

        _feverRoutine = StartCoroutine(StartFeverRoutine());

    }

    private void Awake()
    {
        _isFeverStart = false;
        UserInfo.OnChangeFurnitureHandler += OnEquipFurnitureEvent;
        OnEquipFurnitureEvent(ERestaurantFloorType.Floor1, FurnitureType.Table1); // MaxFeverGauge 먼저 설정
        _uiFever.Init(this); // 올바른 MaxFeverGauge 반영 후 Init
    }

    private void Start()
    {
    }


    private void OnEnable()
    {
        _uiFever.OnChangeGauge();
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= OnEquipFurnitureEvent;
    }


    private void OnEquipFurnitureEvent(ERestaurantFloorType floor, FurnitureType type)
    {
        int equipTableCount = 0;
        for(int i = 0, cnt = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage); i <= cnt; ++i)
        {
            ERestaurantFloorType floorType = (ERestaurantFloorType)i;
            for(int j = 0, cntJ = (int)FurnitureType.Table5; j <= cntJ; ++j)
            {
                FurnitureType furnitureType = (FurnitureType)j;
                if (UserInfo.IsEquipFurniture(UserInfo.CurrentStage, floorType, furnitureType))
                {
                    equipTableCount++;
                }
            }
        }
        _currentMaxFeverGauge = Mathf.Clamp(_maxFeverGauges[equipTableCount], _maxFeverGauges[0], ConstValue.MAX_PEVER_GAUGE);
        _uiFever.OnChangeGauge();
    }


    private IEnumerator StartFeverRoutine()
    {
        float time = ConstValue.PEVER_TIME + GameManager.Instance.AddFerverTime;
        float timer = 0;
        float addTabTimer = 0f;
        SetFeverGauge(0); // 피버 시작 즉시 0으로 저장 (저장 도중 중단 시 MAX로 남는 문제 방지)
        OnStartFeverHandler?.Invoke();
        _mainScene.PlayMainMusic();
        GameManager.Instance.SetGameSpeed(1.5f);
        while (timer < time)
        {
            yield return YieldCache.WaitForSeconds(0.02f);

            addTabTimer += 0.02f;
            if (addTabTimer >= 0.5f)
            {
                if (!CustomerController.IsMaxCount)
                {
                    _customerController.AddTabCount();
                }
                addTabTimer = 0f; // 타이머 리셋
            }

            _uiFever.OnChangeGaugeNoAnime(Mathf.Lerp(1, 0, timer / time));
            timer += 0.02f;
        }

        _isFeverStart = false;
        _mainScene.PlayMainMusic();
        GameManager.Instance.SetGameSpeed(0);
        OnEndFeverHandler?.Invoke();
    }
}
