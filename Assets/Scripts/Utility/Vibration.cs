using UnityEngine;

public static class Vibration
{
    private static AndroidJavaClass _unityPlayer;
    private static AndroidJavaObject _currentActivity;
    private static AndroidJavaObject _vibrator;

    static Vibration()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if(_unityPlayer == null)
                _unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            if(_currentActivity == null)
                _currentActivity = _unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if(_vibrator == null)
                _vibrator = _currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vibration initialization failed: {e.Message}");
        }
#endif
    }

    public static void Vibrate(long milliseconds)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!SoundManager.Instance.IsVibration)
                return;

            if (_unityPlayer == null)
                _unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            if (_currentActivity == null)
                _currentActivity = _unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (_vibrator == null)
                _vibrator = _currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (_vibrator != null && _vibrator.Call<bool>("hasVibrator"))
            {
                _vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vibration failed: {e.Message}");
        }
#endif
    }

    public static void StopVibration()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_unityPlayer == null)
                _unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            if (_currentActivity == null)
                _currentActivity = _unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (_vibrator == null)
                _vibrator = _currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (_vibrator != null)
            {
                _vibrator.Call("cancel");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to stop vibration: {e.Message}");
        }
#endif
    }
}

