using UnityEngine;

/// <summary>
/// Optional owner of a world press in CameraController's existing input path.
/// Screen positions and unscaled timestamps are shared by mouse and touch input.
/// </summary>
public interface IWorldPointerGestureHandler
{
    bool TryBegin(int pointerId, Vector2 screenPosition, float unscaledTime);
    // A visible interactive target can reserve a tap without currently allowing a drag.
    // False means this press is available to the ordinary background camera gesture.
    bool BlocksCameraDrag(Vector2 screenPosition);
    void Move(int pointerId, Vector2 screenPosition, float unscaledTime);
    void End(int pointerId, Vector2 screenPosition, float unscaledTime);
    void Cancel();
}
