using UnityEngine;

public static class DebugLog
{
    public static void Log(object obj)
    {
#if UNITY_EDITOR
        Debug.Log(obj);
#endif
    }

    public static void LogError(object obj)
    {
#if UNITY_EDITOR
        Debug.LogError(obj);
#endif
    }
}
