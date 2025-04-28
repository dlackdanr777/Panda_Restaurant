using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BackEnd.Quobject.SocketIoClientDotNet.Client;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1 : MiniGameSystem
{
    [Header("Data")]
    [SerializeField] private Minigame1ItemDatabase _itemDatabase;
    [SerializeField] private MiniGame1StageDataList _stageDatabase;

    [Space]
    [Header("Components")]
    [SerializeField] private MiniGame1_GaugeBar _gaugeBar;
    [SerializeField] private MiniGame1_SelectFrame _selectFrame;
    [SerializeField] private RectTransform _dontTouchArea;   
    [SerializeField] private GameObject _correctImage;
    [SerializeField] private GameObject _wrongImage;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private MiniGame1_ButtonSlot _buttonSlotPrefab;
    [SerializeField] private GridLayoutGroup _buttonSlotParent;

    private List<MiniGame1ItemData> _itemList = new List<MiniGame1ItemData>();
    private List<MiniGame1StageData> _stageDataList = new List<MiniGame1StageData>();  
    private List<MiniGame1_ButtonSlot> _buttonSlotList = new List<MiniGame1_ButtonSlot>();



    //################## 스테이지 변수 ##############################
    private MiniGame1StageData _currentStageData;
    private int _currentStage = 0;
    private int[] _randIndexs;
    private List<MiniGame1ItemData> _selectedItemList = new List<MiniGame1ItemData>();
    private int _selectedItemListIndex = 0;
    private int _wrongCount = 0;
    private bool _roundEnd;

    private void Start()
    {
        ResetVariable();
        Init();
        _gaugeBar.Init();
        _selectFrame.Init();
        _correctImage.SetActive(false);
        _wrongImage.SetActive(false);
        StartCoroutine(Test());
    }

    private void ResetVariable()
    {
        _selectedItemListIndex = 0;
        _wrongCount = 0;
        _currentStage = 0;
        _currentStageData = null;
        _selectedItemList = null;
        _randIndexs = null;
        _roundEnd = false;
    }


    public override void Init()
    {
        _itemList = _itemDatabase.ItemDataList.ToList();
        _stageDataList = _stageDatabase.StageDataList.ToList();

        int xMax = _stageDataList.Max(x => x.CardSize.x);
        int yMax = _stageDataList.Max(x => x.CardSize.y);
        for(int i = 0, cnt = xMax * yMax; i < cnt; ++i)
        {
            MiniGame1_ButtonSlot slot = Instantiate(_buttonSlotPrefab, _buttonSlotParent.transform);
            slot.Init(OnButtonClicked);
            _buttonSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }
    }

    public override void Show()
    {
        // Implement the logic to show the mini-game UI or start the game
        Debug.Log("MiniGame1 is now visible.");
    }

    public override void Hide()
    {
        // Implement the logic to hide the mini-game UI or end the game
        Debug.Log("MiniGame1 is now hidden.");
    }


    private void SetButtonItemData()
    {
        if(_currentStageData == null)
            throw new System.Exception("Current stage data is null.");
        _buttonSlotParent.constraintCount = _currentStageData.CardSize.x;
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        int itemCount = _itemList.Count;

        for (int i = 0, cnt = _buttonSlotList.Count; i < cnt; ++i)
        {
            MiniGame1_ButtonSlot slot = _buttonSlotList[i];
            if (i < cardSize)
            {
                slot.SetData(_itemList[_randIndexs[i % itemCount]]);
            }
            else
            {
                slot.SetData(null);
            }
        }
    }


    private int[] GetRandomIndexArray(int size)
    {
        int[] indexArray = new int[size];
        for (int i = 0; i < size; i++)
        {
            indexArray[i] = i;
        }

        for (int i = 0; i < size; i++)
        {
            int randomIndex = Random.Range(i, size);
            int temp = indexArray[i];
            indexArray[i] = indexArray[randomIndex];
            indexArray[randomIndex] = temp;
        }

        return indexArray;
    }

    private void OnButtonClicked(MiniGame1ItemData data)
    {
        if(_selectedItemList.Count <= _selectedItemListIndex)
        {
            _dontTouchArea.gameObject.SetActive(true);
        }

        MiniGame1ItemData correctData = _selectedItemList[_selectedItemListIndex];
        
        if (data.Id == correctData.Id)
        {
            DebugLog.Log("정답");
            _selectFrame.SetCorrect(_selectedItemListIndex, true);
            _selectedItemListIndex++;
        }
        else
        {
            DebugLog.Log("오답");
            ShowWrongImage();
            _dontTouchArea.gameObject.SetActive(true);
            _selectFrame.SetCorrect(_selectedItemListIndex, false);
            _selectedItemListIndex = 0;
            _wrongCount++;

            //3회 이상 틀릴 경우 끝나야 함
            if(3 < _wrongCount)
            {
                _roundEnd = true;
            }
        }

        if (_selectedItemList.Count <= _selectedItemListIndex)
        {
            //여기서 라운드 종료
            _dontTouchArea.gameObject.SetActive(true);
            _selectedItemListIndex = 0;
            ShowCorrectImage();
            _currentStage++;
            _roundEnd = true;
        }
    }

    private void SetRandIndexs()
    {
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        _randIndexs = GetRandomIndexArray(cardSize);
    }

    private void SetSelectFrame()
    {
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        int selectCount = Mathf.Min(cardSize, _currentStageData.CardCount);
        
        // 사용 가능한 인덱스 (0 ~ 카드사이즈-1)에서 중복 없이 selectCount개 선택
        int[] selectedCardIndices = GetRandomIndexArray(cardSize);
        
        // 필요한 개수만큼만 사용
        List<MiniGame1ItemData> itemDataList = new List<MiniGame1ItemData>();
        for (int i = 0; i < selectCount; i++)
        {
            // selectedCardIndices[i]는 0~카드사이즈-1 사이의 중복 없는 랜덤 값
            // 해당 인덱스를 사용해 _randIndexs 배열에 접근
            int cardIndex = selectedCardIndices[i];
            
            // _randIndexs 배열의 해당 위치에 있는 값을 사용해 아이템 목록에서 아이템을 선택
            int itemIndex = _randIndexs[cardIndex] % _itemList.Count;
            itemDataList.Add(_itemList[itemIndex]);
        }
        _selectedItemList = itemDataList;
        _selectFrame.SetItemImage(itemDataList);
    }

    private IEnumerator Test()
    {
        while(true)
        {
            if(3 <= _wrongCount)
            {
                DebugLog.Log("게임 종료");
                yield break;
            }

            if(_stageDataList.Count <= _currentStage)
            {
                DebugLog.Log("게임 종료");
                yield break;
            }

            _roundEnd = false;
            _dontTouchArea.gameObject.SetActive(true);
            _wrongImage.SetActive(false);
            _correctImage.SetActive(false);
            _currentStageData = _stageDataList[_currentStage];
            SetRandIndexs();
            SetButtonItemData();
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(SpawnSlotAnimation());
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(ForntFilpAnimation());
            SetSelectFrame();
            yield return new WaitForSeconds(0.5f);
            _dontTouchArea.gameObject.SetActive(false);

            yield return new WaitUntil(() => _roundEnd);
            yield return new WaitForSeconds(2f);
        }

    }

    private IEnumerator SpawnSlotAnimation()
    {
        for(int i = 0, cnt = _buttonSlotList.Count; i < cnt; ++i)
        {
            _buttonSlotList[i].gameObject.SetActive(false);
        }

        foreach (var slot in _buttonSlotList)
        {
            if(slot.CurrentData == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.FlipBack();
            slot.ScaleAnimation(1.2f, 1f, 0.5f);
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator ForntFilpAnimation()
    {
        foreach (var slot in _buttonSlotList)
        {
            if(!slot.gameObject.activeSelf)
                continue;

            slot.FlipForwardAnimation();
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void ShowCorrectImage()
    {
        _wrongImage.SetActive(false);
        _correctImage.SetActive(true);
        _correctImage.TweenStop();
        _correctImage.transform.localScale = Vector3.one * 0.2f;
        _correctImage.TweenScale(Vector3.one, 0.3f, Ease.OutBack);
    }

    private void ShowWrongImage()
    {
        _wrongImage.SetActive(true);
        _correctImage.SetActive(false);
        _wrongImage.TweenStop();
        _wrongImage.transform.localScale = Vector3.one * 0.2f;
        _wrongImage.TweenScale(Vector3.one, 0.3f, Ease.OutBack);
    }
}
