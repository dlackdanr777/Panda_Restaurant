using Muks.Tween;
using System.Collections.Generic;
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

    
    private List<PointerDownSpriteRenderer> _garbageList = new List<PointerDownSpriteRenderer>();
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
        for (int i = 0; i < _garbageList.Count; i++)
        {
            ObjectPoolManager.Instance.DespawnGarbage(_garbageList[i]);
        }
        _garbageList.Clear();

        _rotate[0] = new Vector3(0, 0, 0);
        _rotate[1] = new Vector3(0, 0, 90);
        _rotate[2] = new Vector3(0, 0, 180);
        _rotate[3] = new Vector3(0, 0, 270);
        _rotate[4] = new Vector3(0, 0, -90);
        _rotate[5] = new Vector3(0, 0, -180);
        _rotate[6] = new Vector3(0, 0, -270);
    }

    public void LoadData(int count)
    {
        _currentGarbageCount = Mathf.Clamp(count, 0, _maxGarbageCount);

        for (int i = 0, cnt = _garbageList.Count; i < cnt; ++i)
        {
            ObjectPoolManager.Instance.DespawnGarbage(_garbageList[i]);
        }
        _garbageList.Clear();

        for (int i = 0, cnt = _currentGarbageCount; i < cnt; i++)
        {
            int index = i;
            Vector3 targetPos = _dropArea.position;
            targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxGarbageCount) * index), 0, 0);

            PointerDownSpriteRenderer garbage = ObjectPoolManager.Instance.SpawnGarbage(targetPos, Quaternion.identity);
            garbage.TweenMove(targetPos + new Vector3(0, 0.2f, 0), 2f, Ease.Smootherstep).Loop(LoopType.Yoyo);
            garbage.AddEvent(CleanGarbage);
            _garbageList.Add(garbage);
        }
    }


    public void DropGarbage(Vector3 startPos)
    {
        PointerDownSpriteRenderer garbage = ObjectPoolManager.Instance.SpawnGarbage(startPos, Quaternion.identity);
        garbage.SpriteRenderer.color = Color.white;
        garbage.AddEvent(CleanGarbage);

        Vector3 targetPos = _dropArea.position;
        targetPos += new Vector3(-(_areaRangeX * 0.5f) + ((_areaRangeX / _maxGarbageCount) * _currentGarbageCount), 0, 0);

        if (_garbageList.Count < _maxGarbageCount)
        {
            _garbageList.Add(garbage);
        }

        _currentGarbageCount = Mathf.Clamp(_garbageList.Count, 0, _maxGarbageCount);
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
        if (_garbageList.Count <= 0)
            return;

        UserInfo.AddCleanCount();
        _currentGarbageCount = 0;
        SoundManager.Instance.PlayEffectAudio(_cleanSound);

        for (int i = 0; i < _garbageList.Count; i++)
        {
            int index = i;
            PointerDownSpriteRenderer garbage = _garbageList[index];
            garbage.RemoveEvent(CleanGarbage);
            garbage.TweenStop();
            garbage.SpriteRenderer.TweenAlpha(0, _garbageEndTime, Ease.Smoothstep);

            float targetY = garbage.transform.position.y + 8;
            garbage.TweenMoveY(targetY, _garbageEndTime, Ease.Smoothstep).
                OnComplete(() =>
                {
                    garbage.TweenStop();
                    ObjectPoolManager.Instance.DespawnGarbage(garbage);
                });

        }
        _garbageList.Clear();
    }
}
