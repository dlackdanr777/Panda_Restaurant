using Muks.Tween;
using UnityEngine;

public class DropGarbageArea : MonoBehaviour
{
    [Header("Area Options")]
    [SerializeField] private Transform _dropArea;
    [SerializeField] private float _areaRangeX;

    [Space]
    [Header("Coin Options")]
    [SerializeField] private int _maxGarbageCount;
    [SerializeField] private float _garbageEndTime;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _cleanSound;

    private PointerDownSpriteRenderer[] _garbages;
    private int _currentGarbageCount;
    public int Count => _currentGarbageCount;
    private Vector3[] _rotate = new Vector3[7];

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        _currentGarbageCount = 0;
        _garbages = new PointerDownSpriteRenderer[_maxGarbageCount];

        _rotate[0] = new Vector3(0, 0, 0);
        _rotate[1] = new Vector3(0, 0, 90);
        _rotate[2] = new Vector3(0, 0, 180);
        _rotate[3] = new Vector3(0, 0, 270);
        _rotate[4] = new Vector3(0, 0, -90);
        _rotate[5] = new Vector3(0, 0, -180);
        _rotate[6] = new Vector3(0, 0, -270);
    }

    public void LoadData(int Count)
    {
        _currentGarbageCount = Mathf.Clamp(Count, 0, _maxGarbageCount);

        for (int i = 0, cnt = _garbages.Length; i < cnt; ++i)
        {
            if (_garbages[i] == null)
                continue;

            ObjectPoolManager.Instance.DespawnGarbage(_garbages[i]);
        }

        for (int i = 0, cnt = _currentGarbageCount; i < cnt; i++)
        {
            int index = i;
            Vector3 targetPos = _dropArea.position;
            targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxGarbageCount) * index), 0, 0);

            _garbages[index] = ObjectPoolManager.Instance.SpawnGarbage(targetPos, Quaternion.identity);
            _garbages[index].TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
            _garbages[index].AddEvent(CleanGarbage);
        }
    }


    public void DropGarbage(Vector3 startPos)
    {
        PointerDownSpriteRenderer garbage = ObjectPoolManager.Instance.SpawnGarbage(startPos, Quaternion.identity);
        garbage.SpriteRenderer.color = Color.white;

        Vector3 targetPos = _dropArea.position;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxGarbageCount) * _currentGarbageCount), 0, 0);

        if (_currentGarbageCount < _maxGarbageCount)
        {
            _garbages[_currentGarbageCount] = garbage;
            garbage.AddEvent(CleanGarbage);
            _currentGarbageCount++;
        }

        garbage.TweenRotate(_rotate[Random.Range(0, _rotate.Length)], 0.5f, Ease.InQuad);
        garbage.TweenMoveX(targetPos.x, 0.5f);
        garbage.TweenMoveY(targetPos.y, 0.5f, Ease.InBack).OnComplete(() =>
        {
            if (_maxGarbageCount <= _currentGarbageCount)
            {
                garbage.TweenStop();
                ObjectPoolManager.Instance.DespawnGarbage(garbage);
                return;
            }
            garbage.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
        });
    }


    public void CleanGarbage()
    {
        if (_currentGarbageCount == 0)
            return;

        UserInfo.AddCleanCount();
        int currentCoinCount = _currentGarbageCount;
        _currentGarbageCount = 0;
        SoundManager.Instance.PlayEffectAudio(_cleanSound);

        for (int i = 0; i < currentCoinCount; i++)
        {
            int coinIndex = i;
            _garbages[coinIndex].RemoveEvent(CleanGarbage);
            _garbages[coinIndex].TweenStop();
            _garbages[coinIndex].SpriteRenderer.TweenAlpha(0, _garbageEndTime, Ease.Smoothstep);

            float targetY = _garbages[coinIndex].transform.position.y + 8;
            _garbages[coinIndex].TweenMoveY(targetY, _garbageEndTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    ObjectPoolManager.Instance.DespawnGarbage(_garbages[coinIndex]);
                    _garbages[coinIndex] = null;
                });

        }
    }
}
