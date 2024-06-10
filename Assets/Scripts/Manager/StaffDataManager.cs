using System.Collections.Generic;
using UnityEngine;

public class StaffDataManager : MonoBehaviour
{
    public static StaffDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("StaffDataManager");
                _instance = obj.AddComponent<StaffDataManager>();
                DontDestroyOnLoad(obj);
                Init();
            }

            return _instance;
        }
    }
    private static StaffDataManager _instance;

    private static StaffData[] _staffData;
    private static Dictionary<string, StaffData> _staffDataDic = new Dictionary<string, StaffData>();


    public StaffData GetStaffData(string id)
    {
        if (!_staffDataDic.TryGetValue(id, out StaffData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(_instance);
        Init();
    }

    private static void Init()
    {
        _staffDataDic.Clear();
        _staffData = Resources.LoadAll<StaffData>("StaffData");
        for(int i = 0, cnt = _staffData.Length; i < cnt; i++)
        {
            _staffDataDic.Add(_staffData[i].Id, _staffData[i]);
        }
    }
}
