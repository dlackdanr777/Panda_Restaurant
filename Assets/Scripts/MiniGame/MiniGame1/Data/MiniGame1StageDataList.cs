using UnityEngine;

[CreateAssetMenu(fileName = "MiniGame1 Stage Data List", menuName = "Scriptable Object/MiniGame/MiniGame1/MiniGame1 Stage Data")]
public class MiniGame1StageDataList : ScriptableObject
{
    [SerializeField] private MiniGame1StageData[] _stageDataList;
    public MiniGame1StageData[] StageDataList => _stageDataList; 
}