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

    private PointerClickSpriteRenderer[] _garbages;
    private int _currentGarbageCount;
    public int Count => _currentGarbageCount;
    private Vector3[] _rotate = new Vector3[7];

    private void Start()
    {
        Init();
    }


    private void Init()
    {
        _currentGarbageCount = 0;
        _garbages = new PointerClickSpriteRenderer[_maxGarbageCount];

        _rotate[0] = new Vector3(0, 0, 0);
        _rotate[1] = new Vector3(0, 0, 90);
        _rotate[2] = new Vector3(0, 0, 180);
        _rotate[3] = new Vector3(0, 0, 270);
        _rotate[4] = new Vector3(0, 0, -90);
        _rotate[5] = new Vector3(0, 0, -180);
        _rotate[6] = new Vector3(0, 0, -270);
    }


    public void DropGarbage(Vector3 startPos)
    {
        PointerClickSpriteRenderer garbage = ObjectPoolManager.Instance.SpawnGarbage(startPos, Quaternion.identity);
        garbage.SpriteRenderer.color = Color.white;

        Vector3 targetPos = _dropArea.position;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxGarbageCount) * _currentGarbageCount), 0, 0);

        if (_currentGarbageCount < _maxGarbageCount)
        {
            _garbages[_currentGarbageCount] = garbage;
            garbage.AddEvent(CleanGarbage);
            _currentGarbageCount++;
        }

        garbage.TweenRotate(_rotate[Random.Range(0, _rotate.Length)], 0.5f, TweenMode.EaseInQuad);
        garbage.TweenMoveX(targetPos.x, 0.5f);
        garbage.TweenMoveY(targetPos.y, 0.5f, TweenMode.EaseInBack).OnComplete(() =>
        {
            if (_maxGarbageCount <= _currentGarbageCount)
            {
                garbage.TweenStop();
                ObjectPoolManager.Instance.DespawnGarbage(garbage);
                return;
            }
            garbage.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, TweenMode.Smootherstep).Loop(LoopType.Yoyo);
        });
    }


    public void CleanGarbage()
    {
        if (_currentGarbageCount == 0)
            return;

        int currentCoinCount = _currentGarbageCount;
        _currentGarbageCount = 0;

        for (int i = 0; i < currentCoinCount; i++)
        {
            int coinIndex = i;
            _garbages[coinIndex].RemoveEvent(CleanGarbage);
            _garbages[coinIndex].TweenStop();
            _garbages[coinIndex].SpriteRenderer.TweenAlpha(0, _garbageEndTime, TweenMode.Smoothstep);

            float targetY = _garbages[coinIndex].transform.position.y + 8;
            _garbages[coinIndex].TweenMoveY(targetY, _garbageEndTime, TweenMode.Smoothstep).
                OnComplete(() =>
                {
                    ObjectPoolManager.Instance.DespawnGarbage(_garbages[coinIndex]);
                    _garbages[coinIndex] = null;
                });

        }
    }
}
