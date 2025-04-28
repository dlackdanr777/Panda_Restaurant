using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1_SelectFrame : MonoBehaviour
{
    [SerializeField] private MiniGame1SelectFrameItemImage _itemImagePrefab;
    [SerializeField] private Image _arrowImagePrefab;
    [SerializeField] private RectTransform _parentRectTransform;


    [Header("Option")]
    [SerializeField] private float _itemSize = 5f;

    private List<MiniGame1SelectFrameItemImage> _itemImageList = new List<MiniGame1SelectFrameItemImage>();
    private List<Image> _arrowImageList = new List<Image>();
    private List<MiniGame1ItemData> _itemDataList = new List<MiniGame1ItemData>();

    public void Init()
    {
        for(int i = 0; i < _itemSize; ++i)
        {
            MiniGame1SelectFrameItemImage itemImage = Instantiate(_itemImagePrefab, _parentRectTransform);
            if(i != _itemSize - 1)
            {
                Image arrowImage = Instantiate(_arrowImagePrefab, _parentRectTransform);
                arrowImage.gameObject.SetActive(false);
                _arrowImageList.Add(arrowImage);
            }

            itemImage.gameObject.SetActive(false);
            _itemImageList.Add(itemImage);
        }
    }


    public void SetItemImage(List<MiniGame1ItemData> itemDataList)
    {
        _itemDataList = itemDataList;
        
        // 아이템 이미지 설정
        for(int i = 0; i < _itemImageList.Count; ++i)
        {
            if(i >= itemDataList.Count)
            {
                _itemImageList[i].gameObject.SetActive(false);
                
                // 비활성화된 아이템 이전의 화살표도 비활성화
                if(i > 0 && i - 1 < _arrowImageList.Count)
                {
                    _arrowImageList[i - 1].gameObject.SetActive(false);
                }
                continue;
            }

            _itemImageList[i].gameObject.SetActive(true);
            _itemImageList[i].SetItemImage(itemDataList[i]);
            
            // 아이템 사이에 화살표 표시 (단, 마지막 아이템 이후에는 화살표 없음)
            if(i < itemDataList.Count - 1 && i < _arrowImageList.Count)
            {
                _arrowImageList[i].gameObject.SetActive(true);
            }
            // 마지막 아이템 이후에는 화살표 비활성화
            else if(i < _arrowImageList.Count)
            {
                _arrowImageList[i].gameObject.SetActive(false);
            }
        }
        
        // 불필요한 화살표 비활성화 (안전 점검)
        for(int i = itemDataList.Count - 1; i < _arrowImageList.Count; i++)
        {
            _arrowImageList[i].gameObject.SetActive(false);
        }
    }


    public void SetCorrect(int index, bool isCorrect)
    {
        if(index >= _itemImageList.Count || index < 0) return;
        
        if(isCorrect)
        {
            _itemImageList[index].ShowCorrectImage();
        }
        else
        {
            _itemImageList[index].ShowWrongImage();
        }
    }
}
