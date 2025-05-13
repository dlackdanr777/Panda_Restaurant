using UnityEngine;

[CreateAssetMenu(fileName = "MiniGame1StageDataList", menuName = "Scriptable Object/MiniGame/MiniGame1/StageDataList")]
public class MiniGame1StageDataList : ScriptableObject
{
    [SerializeField] private MiniGame1StageData[] _stageDataList;
    public MiniGame1StageData[] StageDataList => _stageDataList; 
}