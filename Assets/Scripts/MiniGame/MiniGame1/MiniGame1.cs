using System.Collections.Generic;
using System.Linq;
using BackEnd.Quobject.SocketIoClientDotNet.Client;
using UnityEngine;

public class MiniGame1 : MiniGameSystem
{
    [Header("Data")]
    [SerializeField] private Minigame1ItemDatabase _itemDatabase;
    [SerializeField] private MiniGame1StageDataList _stageDatabase;

    [Space]
    [Header("Components")]


    [Space]
    [Header("Slot Option")]
    [SerializeField] private MiniGame1_ButtonSlot _buttonSlotPrefab;
    [SerializeField] private Transform _buttonSlotParent;

    private List<MiniGame1ItemData> _itemList = new List<MiniGame1ItemData>();
    private List<MiniGame1StageData> _stageDataList = new List<MiniGame1StageData>();
    
    private List<MiniGame1_ButtonSlot> _buttonSlotList = new List<MiniGame1_ButtonSlot>();

    public override void Init()
    {
        _itemList = _itemDatabase.ItemDataList.ToList();
        _stageDataList = _stageDatabase.StageDataList.ToList();

        for(int i = 0, cnt = 16; i < cnt; ++i)
        {
            MiniGame1_ButtonSlot slot = Instantiate(_buttonSlotPrefab, _buttonSlotParent);
            slot.Init(OnButtonClicked);
            _buttonSlotList.Add(slot);
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

    private void OnButtonClicked(MiniGame1ItemData data)
    {
        Debug.Log($"Button clicked with data: {data.Id}");
    }
}
