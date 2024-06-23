using UnityEngine;

public static class DebugLog
{
    public static void Log(object obj)
    {
        Debug.Log(obj);
    }

    public static void LogError(object obj) { Debug.LogError(obj);}
}
