using Muks.Tween;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("ObjectPoolManager");
                _instance = obj.AddComponent<ObjectPoolManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static ObjectPoolManager _instance;


    private static int _staffCount = (int)ERestaurantFloorType.Length;
    private static GameObject _staffParent;
    private static GameObject[] _staffParents = new GameObject[(int)StaffType.Length];
    private static Staff[] _staffPrefabs = new Staff[(int)StaffType.Length];
    private static Queue<Staff>[] _staffPools = new Queue<Staff>[(int)StaffType.Length];

    private static int _customerCount = 30;
    private static int _specialCustomerCount = 5;
    private static int _gatecrasherCustomerCount = 5;
    private static GameObject _customerParent;
    private static NormalCustomer _normalCustomerPrefab;
    private static SpecialCustomer _specialCustomerPrefab;
    private static GatecrasherCustomer _gatecrasherCustomerPrefab;
    private static Queue<NormalCustomer> _normalCustomerPool = new Queue<NormalCustomer>();
    private static Queue<SpecialCustomer> _specialCustomerPool = new Queue<SpecialCustomer>();
    private static Queue<GatecrasherCustomer> _gatecrasherCustomerPool = new Queue<GatecrasherCustomer>();


    private static int _coinCount = 50;
    private static GameObject _coinParent;
    private static PointerDownSpriteRenderer _coinPrefab;
    private static Queue<PointerDownSpriteRenderer> _coinPool = new Queue<PointerDownSpriteRenderer>();


    private static int _garbageCount = 100;
    private static GameObject _garbageParent;
    private static PointerDownSpriteRenderer _garbagePrefab;
    private static Queue<PointerDownSpriteRenderer> _garbagePool = new Queue<PointerDownSpriteRenderer>();
    private static List<PointerDownSpriteRenderer> _enabledGarbagePool = new List<PointerDownSpriteRenderer>();

    private static int _tmpCount = 20;
    private static GameObject _tmpParent;
    private static TextMeshProUGUI _tmpPrefab;
    private static Queue<TextMeshProUGUI> _tmpPool = new Queue<TextMeshProUGUI>();
    private static List<TextMeshProUGUI> _enabledTmpPool = new List<TextMeshProUGUI>();

    private static int _smokeParitcleCount = 10;
    private static GameObject _particleParent;
    private static SmokeParticle _smokeParticlePrefab;
    private static Queue<SmokeParticle> _smokeParitclePool = new Queue<SmokeParticle>();

    private static int _uiCoinCount = 50;
    private static RectTransform _uiCoinPrefab;
    private static Queue<RectTransform> _uiCoinPool = new Queue<RectTransform>();

    private static int _uiDiaCount = 50;
    private static RectTransform _uiDiaPrefab;
    private static Queue<RectTransform> _uiDiaPool = new Queue<RectTransform>();

    private static int _uiEffectCount = 10;
    private static UIParticleEffect[] _uiEffectPrefabs = new UIParticleEffect[(int)UIEffectType.Length];
    private static Queue<UIParticleEffect>[] _uiEffectPool;


    private static Sprite[] _garbageImages;
    private static Canvas _uiCanvas;


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _uiCanvas = new GameObject("UI Canvas").AddComponent<Canvas>();
        _uiCanvas.transform.parent = _instance.transform;
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _uiCanvas.sortingOrder = 10;

        CanvasScaler scaler = _uiCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1;

        StaffPooling();
        CustomerPooling();
        CoinPooling();
        UICoinPooling();
        UIDiaPooling();
        GarbagePooling();
        TmpPooling();
        UIEffectPooling();
        SmokeParticlePooling();
    }


    private static void StaffPooling()
    {
        _staffParent = new GameObject("StaffParent");
        _staffParent.transform.parent = _instance.transform;

        for (int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            if (_staffParents[i] == null)
            {
                _staffParents[i] = new GameObject((StaffType)i + "_Parent");
                _staffParents[i].transform.parent = _staffParent.transform;
            }

            _staffPools[i] = new Queue<Staff>();

            if (_staffPrefabs[i] == null)
                _staffPrefabs[i] = Resources.Load<Staff>("Staff/Staff");
        }

        _staffPrefabs[(int)StaffType.Cleaner] = Resources.Load<StaffCleaner>("Staff/Cleaner");
        _staffCount = (int)ERestaurantFloorType.Length;
        for (int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            for (int j = 0; j < _staffCount; ++j)
            {
                Staff staff = Instantiate(_staffPrefabs[i], _staffParents[i].transform);
                _staffPools[i].Enqueue(staff);
                staff.gameObject.SetActive(false);
            }
        }
    }


    private static void CustomerPooling()
    {
        _customerParent = new GameObject("CustomerParent");
        _customerParent.transform.parent = _instance.transform;

        if(_normalCustomerPrefab == null)
            _normalCustomerPrefab = Resources.Load<NormalCustomer>("ObjectPool/NormalCustomer");

        if(_specialCustomerPrefab == null)
            _specialCustomerPrefab = Resources.Load<SpecialCustomer>("ObjectPool/SpecialCustomer");

        if (_gatecrasherCustomerPrefab == null)
            _gatecrasherCustomerPrefab = Resources.Load<GatecrasherCustomer>("ObjectPool/GatecrasherCustomer");

        for (int i = 0, count = _customerCount; i < count; i++)
        {
            NormalCustomer normalCustomer = Instantiate(_normalCustomerPrefab, _customerParent.transform);
            normalCustomer.Init();
            _normalCustomerPool.Enqueue(normalCustomer);
            normalCustomer.gameObject.SetActive(false);
        }

        for(int i = 0; i < _specialCustomerCount; ++i)
        {
            SpecialCustomer specialCustomer = Instantiate(_specialCustomerPrefab, _customerParent.transform);
            specialCustomer.Init();
            _specialCustomerPool.Enqueue(specialCustomer);
            specialCustomer.gameObject.SetActive(false);
        }

        for (int i = 0; i < _gatecrasherCustomerCount; ++i)
        {
            GatecrasherCustomer gatecrasherCustomer = Instantiate(_gatecrasherCustomerPrefab, _customerParent.transform);
            gatecrasherCustomer.Init();
            _gatecrasherCustomerPool.Enqueue(gatecrasherCustomer);
            gatecrasherCustomer.gameObject.SetActive(false);
        }
    }


    private static void CoinPooling()
    {
        _coinParent = new GameObject("CoinParent");
        _coinParent.transform.parent = _instance.transform;

        if(_coinPrefab == null)
            _coinPrefab = Resources.Load<PointerDownSpriteRenderer>("ObjectPool/Coin");

        for (int i = 0, count = _coinCount; i < count; i++)
        {
            PointerDownSpriteRenderer coin = Instantiate(_coinPrefab, _coinParent.transform);
            _coinPool.Enqueue(coin);
            coin.gameObject.SetActive(false);
        }
    }

    private static void UICoinPooling()
    {
        if (_uiCoinPrefab == null)
            _uiCoinPrefab = Resources.Load<RectTransform>("ObjectPool/UICoin");

        for (int i = 0, count = _uiCoinCount; i < count; i++)
        {
            RectTransform coin = Instantiate(_uiCoinPrefab, _uiCanvas.transform);
            _uiCoinPool.Enqueue(coin);
            coin.gameObject.SetActive(false);
        }
    }

    private static void UIDiaPooling()
    {
        if (_uiDiaPrefab == null)
            _uiDiaPrefab = Resources.Load<RectTransform>("ObjectPool/UIDia");

        for (int i = 0, count = _uiDiaCount; i < count; i++)
        {
            RectTransform dia = Instantiate(_uiDiaPrefab, _uiCanvas.transform);
            _uiDiaPool.Enqueue(dia);
            dia.gameObject.SetActive(false);
        }
    }


    private static void GarbagePooling()
    {
        _garbageParent = new GameObject("GarbageParent");
        _garbageParent.transform.parent = _instance.transform;

        _garbageImages = Resources.LoadAll<Sprite>("ObjectPool/Garbage/GarbageImage");

        if (_garbagePrefab == null)
            _garbagePrefab = Resources.Load<PointerDownSpriteRenderer>("ObjectPool/Garbage/Garbage");

        for (int i = 0, count = _garbageCount; i < count; i++)
        {
            PointerDownSpriteRenderer garbage = Instantiate(_garbagePrefab, _garbageParent.transform);
            _garbagePool.Enqueue(garbage);
            garbage.gameObject.SetActive(false);
        }
    }


    private static void TmpPooling()
    {
        _tmpParent = new GameObject("TmpParent");
        _tmpParent.transform.parent = _instance.transform;

        if (_tmpPrefab == null)
            _tmpPrefab = Resources.Load<TextMeshProUGUI>("ObjectPool/TMP");

        for (int i = 0, count = _tmpCount; i < count; i++)
        {
            TextMeshProUGUI tmp = Instantiate(_tmpPrefab, _tmpParent.transform);
            _tmpPool.Enqueue(tmp);
            tmp.gameObject.SetActive(false);
        }
    }

    private static void UIEffectPooling()
    {
        _uiEffectPool = new Queue<UIParticleEffect>[(int)UIEffectType.Length];
        List<UIParticleEffect> uIParticleEffects = Resources.LoadAll<UIParticleEffect>("ObjectPool/Effect/").ToList();
        for (int i = 0, cnt = (int)UIEffectType.Length; i < cnt; i++)
        {
            _uiEffectPool[i] = new Queue<UIParticleEffect>();
            if (_uiEffectPrefabs[i] == null)
                _uiEffectPrefabs[i] = uIParticleEffects.Find(x => x.Type == (UIEffectType)i);

            if (_uiEffectPrefabs[i] == null)
            {
                DebugLog.LogError("해당 위치에 프리팹이 존재하지 않습니다: " + "ObjectPool/Effect/" + (UIEffectType)i);
                continue;
            }

            for (int j = 0, count = _uiEffectCount; j < count; j++)
            {
                UIParticleEffect effect = Instantiate(_uiEffectPrefabs[i], _uiCanvas.transform);
                _uiEffectPool[i].Enqueue(effect);
                effect.gameObject.SetActive(false);
            }

        }
    }


    private static void SmokeParticlePooling()
    {
        _particleParent = new GameObject("ParticleParent");
        _particleParent.transform.parent = _instance.transform;

        if (_smokeParticlePrefab == null)
            _smokeParticlePrefab = Resources.Load<SmokeParticle>("ObjectPool/SmokeParticle");

        for (int i = 0, count = _smokeParitcleCount; i < count; i++)
        {
            SmokeParticle particle = Instantiate(_smokeParticlePrefab, _particleParent.transform);
            particle.Init();
            _smokeParitclePool.Enqueue(particle);
            particle.gameObject.SetActive(false);
        }
    }


    public Staff SpawnStaff(StaffType type, Vector3 pos, Transform parent)
    {
        Staff staff;
        int typeIndex = (int)type;
        if (_staffPools[typeIndex].Count == 0)
        {
            staff = Instantiate(_staffPrefabs[typeIndex], pos, Quaternion.identity, parent);
            return staff;
        }

        staff = _staffPools[typeIndex].Dequeue();
        staff.gameObject.SetActive(false);
        staff.gameObject.SetActive(true);
        staff.transform.position = pos;
        staff.transform.rotation = Quaternion.identity;
        staff.transform.SetParent(parent);
        staff.ObjectPoolSpawnEvent();
        return staff;
    }


    public void DespawnStaff(StaffType type, Staff staff)
    {
        int typeIndex = (int)type;
        staff.gameObject.SetActive(false);
        staff.ObjectPoolDespawnEvent();
        staff.transform.SetParent(_staffParents[typeIndex].transform);
        _staffPools[typeIndex].Enqueue(staff);
    }


    public NormalCustomer SpawnNormalCustomer(Vector3 pos, Quaternion rot)
    {
        NormalCustomer customer;

        if (_normalCustomerPool.Count == 0 )
        {
            customer = Instantiate(_normalCustomerPrefab, pos, rot, _customerParent.transform);
            return customer;
        }

        customer = _normalCustomerPool.Dequeue();
        customer.gameObject.SetActive(false);
        customer.gameObject.SetActive(true);
        customer.transform.position = pos;
        customer.transform.rotation = rot;
        return customer;
    }


    public void DespawnNormalCustomer(NormalCustomer customer)
    {
        customer.gameObject.SetActive(false);
        _normalCustomerPool.Enqueue(customer);
    }


    public SpecialCustomer SpawnSpecialCustomer(Vector3 pos, Quaternion rot)
    {
        SpecialCustomer customer;

        if (_specialCustomerPool.Count == 0)
        {
            customer = Instantiate(_specialCustomerPrefab, pos, rot, _customerParent.transform);
            return customer;
        }

        customer = _specialCustomerPool.Dequeue();
        customer.gameObject.SetActive(false);
        customer.gameObject.SetActive(true);
        customer.transform.position = pos;
        customer.transform.rotation = rot;
        return customer;
    }

    public GatecrasherCustomer SpawnGatecrasherCustomer(Vector3 pos, Quaternion rot)
    {
        GatecrasherCustomer customer;

        if (_gatecrasherCustomerPool.Count == 0)
        {
            customer = Instantiate(_gatecrasherCustomerPrefab, pos, rot, _customerParent.transform);
            return customer;
        }

        customer = _gatecrasherCustomerPool.Dequeue();
        customer.gameObject.SetActive(false);
        customer.gameObject.SetActive(true);
        customer.transform.position = pos;
        customer.transform.rotation = rot;
        return customer;
    }



    public void DespawnSpecialCustomer(SpecialCustomer customer)
    {
        customer.gameObject.SetActive(false);
        _specialCustomerPool.Enqueue(customer);
    }

    public void DespawnGatecrasherCustomer(GatecrasherCustomer customer)
    {
        customer.gameObject.SetActive(false);
        _gatecrasherCustomerPool.Enqueue(customer);
    }


    public PointerDownSpriteRenderer SpawnCoin(Vector3 pos, Quaternion rot)
    {
        PointerDownSpriteRenderer coin;

        if (_coinPool.Count == 0)
        {
            coin = Instantiate(_coinPrefab, pos, rot, _coinParent.transform);
            return coin;
        }

        coin = _coinPool.Dequeue();
        coin.gameObject.SetActive(false);
        coin.gameObject.SetActive(true);
        coin.transform.position = pos;
        coin.transform.rotation = rot;
        return coin;
    }


    public void DespawnCoin(PointerDownSpriteRenderer coin)
    {
        coin.gameObject.SetActive(false);
        coin.TweenStop();
        coin.RemoveAllEvent();
        _coinPool.Enqueue(coin);
    }


    public RectTransform SpawnUICoin(Vector3 pos, Quaternion rot)
    {
        RectTransform coin;

        if (_uiCoinPool.Count == 0)
        {
            coin = Instantiate(_uiCoinPrefab, pos, rot, _uiCanvas.transform);
            return coin;
        }

        coin = _uiCoinPool.Dequeue();
        coin.gameObject.SetActive(false);
        coin.gameObject.SetActive(true);
        coin.transform.position = pos;
        coin.transform.rotation = rot;
        return coin;
    }


    public void DespawnUICoin(RectTransform coin)
    {
        coin.TweenStop();
        coin.SetParent(_uiCanvas.transform);
        coin.gameObject.SetActive(false);
        _uiCoinPool.Enqueue(coin);
    }


    public RectTransform SpawnUIDia(Vector3 pos, Quaternion rot)
    {
        RectTransform dia;

        if (_uiDiaPool.Count == 0)
        {
            dia = Instantiate(_uiDiaPrefab, pos, rot, _uiCanvas.transform);
            return dia;
        }

        dia = _uiDiaPool.Dequeue();
        dia.gameObject.SetActive(false);
        dia.gameObject.SetActive(true);
        dia.transform.position = pos;
        dia.transform.rotation = rot;
        return dia;
    }


    public void DespawnUIDia(RectTransform dia)
    {
        dia.TweenStop();
        dia.SetParent(_uiCanvas.transform);
        dia.gameObject.SetActive(false);
        _uiDiaPool.Enqueue(dia);
    }


    public PointerDownSpriteRenderer SpawnGarbage(Vector3 pos, Quaternion rot)
    {
        PointerDownSpriteRenderer garbage;

        if (_garbagePool.Count == 0)
        {
            garbage = Instantiate(_garbagePrefab, pos, rot, _garbageParent.transform);
            garbage.SpriteRenderer.sprite = _garbageImages[UnityEngine.Random.Range(0, _garbageImages.Length)];
            _enabledGarbagePool.Add(garbage);
            return garbage;
        }

        garbage = _garbagePool.Dequeue();
        garbage.gameObject.SetActive(false);
        garbage.gameObject.SetActive(true);
        garbage.transform.position = pos;
        garbage.transform.rotation = rot;
        garbage.SpriteRenderer.sprite = _garbageImages[UnityEngine.Random.Range(0, _garbageImages.Length)];
        _enabledGarbagePool.Add(garbage);
        return garbage;
    }


    public void DespawnGarbage(PointerDownSpriteRenderer garbage)
    {
        if (!_enabledGarbagePool.Contains(garbage))
        {
            DebugLog.LogError("반환하려는 오브젝트가 garbarge가 아니거나, 활성화된 Garbage Pool에 등록되있지 않습니다.");
            return;
        }

        garbage.gameObject.SetActive(false);
        garbage.TweenStop();
        garbage.RemoveAllEvent();
        _garbagePool.Enqueue(garbage);
        _enabledGarbagePool.Remove(garbage);
    }

    public int GetEnabledGarbageCount()
    {
        return _enabledGarbagePool.Count;
    }



    public TextMeshProUGUI SpawnTMP(Vector3 pos, Quaternion rot, RectTransform parent)
    {
        TextMeshProUGUI tmp;

        if (_garbagePool.Count == 0)
        {
            tmp = Instantiate(_tmpPrefab, pos, rot, _garbageParent.transform);
            tmp.transform.parent = parent;
            _enabledTmpPool.Add(tmp);
            return tmp;
        }

        tmp = _tmpPool.Dequeue();
        tmp.gameObject.SetActive(false);
        tmp.gameObject.SetActive(true);
        tmp.transform.position = pos;
        tmp.transform.rotation = rot;
        tmp.transform.SetParent(parent);
        tmp.color = Color.white;
        _enabledTmpPool.Add(tmp);
        return tmp;
    }


    public void DespawnTmp(TextMeshProUGUI tmp)
    {
        if (!_enabledTmpPool.Contains(tmp))
        {
            DebugLog.LogError("반환하려는 오브젝트가 tmp가 아니거나, 활성화된 tmp Pool에 등록되있지 않습니다.");
            return;
        }

        tmp.gameObject.SetActive(false);
        tmp.transform.SetParent(_tmpParent.transform);
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.TweenStop();
        _tmpPool.Enqueue(tmp);
        _enabledTmpPool.Remove(tmp);
    }


    public UIParticleEffect SpawnUIEffect(UIEffectType type, Vector3 pos, Quaternion rot)
    {
        UIParticleEffect effect;
        int index = (int)type;


        if (_uiEffectPool[index].Count == 0)
        {
            effect = Instantiate(_uiEffectPrefabs[index], pos, rot, _uiCanvas.transform);
            _uiEffectPool[index].Enqueue(effect);
            return effect;
        }

        effect = _uiEffectPool[index].Dequeue();
        effect.gameObject.SetActive(false);
        effect.gameObject.SetActive(true);
        effect.transform.position = pos;
        effect.transform.rotation = rot;
        return effect;
    }


    public void DespawnUIEffect(UIEffectType type, UIParticleEffect effect)
    {
        int index = (int)type;

        if (_uiEffectPool[index].Contains(effect))
            return;

        effect.gameObject.SetActive(false);
        _uiEffectPool[index].Enqueue(effect);
    }


    public SmokeParticle SpawnSmokeParticle(Vector3 pos, Quaternion rot, float scale = 1)
    {
        SmokeParticle particle;

        if (_smokeParitclePool.Count == 0)
        {
            particle = Instantiate(_smokeParticlePrefab, pos, rot, _particleParent.transform);
            return particle;
        }

        particle = _smokeParitclePool.Dequeue();
        particle.gameObject.SetActive(false);
        particle.gameObject.SetActive(true);
        particle.transform.position = pos;
        particle.transform.rotation = rot;
        particle.SetScale(scale);
        return particle;
    }


    public void DespawnSmokeParticle(SmokeParticle smokeParticle)
    {
        if (_smokeParitclePool.Contains(smokeParticle))
            return;

        smokeParticle.Stop();
        smokeParticle.gameObject.SetActive(false);
        _smokeParitclePool.Enqueue(smokeParticle);
    }
}
