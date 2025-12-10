using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Furniture : MonoBehaviour
{
    protected enum FurnitureBatchTypeY
    {
        Lower,
        Upper,
        Center,
        None,
    }

    protected enum FurnitureBatchTypeX
    {
        Left,
        Right,
        Center,
        None,
    }

    [SerializeField] protected FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] protected bool _thumbnailSpriteEnabled = false;
    [SerializeField] protected FurnitureBatchTypeY _batchType;
    [SerializeField] protected FurnitureBatchTypeX _batchTypeX;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] protected Sprite _defalutSprite;

    protected TableManager _tableManager;

    protected ERestaurantFloorType _floor;
    protected FurnitureType _furnitureType;

    protected FurnitureData _data;

    public virtual void Init(TableManager tableManager, ERestaurantFloorType floor)
    {
        _tableManager = tableManager;
        _floor = floor;
    }

    public virtual void SetFurnitureData(FurnitureData data)
    {
        if(_data == data)
            return;

        _data = data;
        StopAllCoroutines();
        if (data == null)
        {
            if (_defalutSprite == null)
                _spriteRenderer.gameObject.SetActive(false);

            else
            {
                _spriteRenderer.sprite = _defalutSprite;
                SetRendererScale();
            }

            return;
        }

        _spriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = _thumbnailSpriteEnabled ? data.ThumbnailSprite : data.Sprite;
        SetRendererScale();
        StartCoroutine(AnimationCoroutine());   
    }

    private void SetRendererScale()
    {
        float y = 0;
        float x = 0;

        if (_batchType == FurnitureBatchTypeY.Lower)
            y = _spriteRenderer.sprite.bounds.size.y * 0.5f * _spriteRenderer.transform.lossyScale.y;

        else if (_batchType == FurnitureBatchTypeY.Upper)
            y = -(_spriteRenderer.sprite.bounds.size.y * 0.5f * _spriteRenderer.transform.lossyScale.y);

        else if (_batchType == FurnitureBatchTypeY.Center)
            y = 0;

        else if (_batchType == FurnitureBatchTypeY.None)
            y = _spriteRenderer.transform.localPosition.y;


        if (_batchTypeX == FurnitureBatchTypeX.Left)
            x = _spriteRenderer.sprite.bounds.size.x * 0.5f * _spriteRenderer.transform.lossyScale.x;

        else if (_batchTypeX == FurnitureBatchTypeX.Right)
            x = -(_spriteRenderer.sprite.bounds.size.x * 0.5f * _spriteRenderer.transform.lossyScale.x);

        else if (_batchTypeX == FurnitureBatchTypeX.Center)
            x = 0;

        else if (_batchType == FurnitureBatchTypeY.None)
            x = _spriteRenderer.transform.localPosition.x;

        _spriteRenderer.transform.localPosition = new Vector3(x, y, 0);
    }


    private IEnumerator AnimationCoroutine()
    {
        if (_data == null || _data.AnimationSpriteList == null || _data.AnimationSpriteList.Count == 0)
            yield break;


        float frameTime = 0.2f; // 프레임 당 시간
        List<Sprite> animationSprites = _data.AnimationSpriteList;
        while (true)
        {
            // 기본 스프라이트로 대기
            _spriteRenderer.sprite = _thumbnailSpriteEnabled ? _data.ThumbnailSprite : _data.Sprite;
            yield return YieldCache.WaitForSeconds(Random.Range(10f, 20f));

            if (_data == null || animationSprites == null || animationSprites.Count == 0)
                yield break;

            // 모든 애니메이션 프레임 재생
            int index = 0;
            while (index < animationSprites.Count)
            {
                if (_data == null || animationSprites == null || animationSprites.Count == 0)
                    yield break;

                _spriteRenderer.sprite = animationSprites[index];
                yield return YieldCache.WaitForSeconds(frameTime);
                index++;
            }

            // 애니메이션 종료 후 기본 스프라이트로 복귀
            _spriteRenderer.sprite = _thumbnailSpriteEnabled ? _data.ThumbnailSprite : _data.Sprite;
        }
    }
}
