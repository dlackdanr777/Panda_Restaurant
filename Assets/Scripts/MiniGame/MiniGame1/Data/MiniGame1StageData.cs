using UnityEngine;


[System.Serializable]
public class MiniGame1StageData
{
    [SerializeField] private int _cardCount;
    public int CardCount => _cardCount;
    [SerializeField] private Vector2 _cardSize;
    public Vector2 CardSize => _cardSize;

    [SerializeField] private float _stageTime;
    public float StageTime => _stageTime;

    [SerializeField] private int _successScore;
    public int SuccessScore => _successScore;

}

[CreateAssetMenu(fileName = "MiniGame1 Stage Data List", menuName = "Scriptable Object/MiniGame/MiniGame1/MiniGame1 Stage Data")]
public class MiniGame1StageDataList : ScriptableObject
{
    [SerializeField] private MiniGame1StageData[] _stageDataList;
    public MiniGame1StageData[] StageDataList => _stageDataList;
}