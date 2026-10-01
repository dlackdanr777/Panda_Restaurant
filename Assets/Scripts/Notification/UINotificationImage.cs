using UnityEngine;

// Same timeline as the former Tween chain, evaluated in Update so no Tween components or closures are allocated.
public class UINotificationImage : MonoBehaviour
{
    private const float Angle = 15f;
    private const float ScaleMultiplier = 1.1f;

    private const float SwingOutEnd = 0.4f;
    private const float SwingBackEnd = 0.9f;
    private const float SwingAgainEnd = 1.22f;
    private const float ReturnEnd = 1.47f;
    private const float CycleDuration = 2.47f;
    private const float ScaleDuration = 0.25f;

    private static readonly Quaternion LeftRotation = Quaternion.Euler(0f, 0f, -Angle);
    private static readonly Quaternion RightRotation = Quaternion.Euler(0f, 0f, Angle);

    private Transform _transform;
    private Quaternion _tmpRotation;
    private Vector3 _tmpScale;
    private Vector3 _targetScale;
    private float _elapsed;

    private void Awake()
    {
        _transform = transform;
        _tmpRotation = _transform.rotation;
        _tmpScale = _transform.localScale;
        _targetScale = _tmpScale * ScaleMultiplier;
    }


    private void OnEnable()
    {
        _elapsed = 0f;
        Apply(0f);
    }


    private void OnDisable()
    {
        _transform.rotation = _tmpRotation;
        _transform.localScale = _tmpScale;
    }


    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= CycleDuration)
            _elapsed %= CycleDuration;

        Apply(_elapsed);
    }


    private void Apply(float t)
    {
        Quaternion rotation;
        if (t < SwingOutEnd)
            rotation = Quaternion.LerpUnclamped(_tmpRotation, LeftRotation, Smoothstep(t, SwingOutEnd));
        else if (t < SwingBackEnd)
            rotation = Quaternion.LerpUnclamped(LeftRotation, RightRotation, Smoothstep(t - SwingOutEnd, SwingBackEnd - SwingOutEnd));
        else if (t < SwingAgainEnd)
            rotation = Quaternion.LerpUnclamped(RightRotation, LeftRotation, Smoothstep(t - SwingBackEnd, SwingAgainEnd - SwingBackEnd));
        else if (t < ReturnEnd)
            rotation = Quaternion.LerpUnclamped(LeftRotation, _tmpRotation, Smoothstep(t - SwingAgainEnd, ReturnEnd - SwingAgainEnd));
        else
            rotation = _tmpRotation;

        Vector3 scale;
        if (t < ScaleDuration)
            scale = Vector3.LerpUnclamped(_tmpScale, _targetScale, Smoothstep(t, ScaleDuration));
        else if (t < SwingAgainEnd)
            scale = _targetScale;
        else if (t < ReturnEnd)
            scale = Vector3.LerpUnclamped(_targetScale, _tmpScale, Smoothstep(t - SwingAgainEnd, ReturnEnd - SwingAgainEnd));
        else
            scale = _tmpScale;

        _transform.rotation = rotation;
        _transform.localScale = scale;
    }


    private static float Smoothstep(float elapsed, float duration)
    {
        float percent = elapsed / duration;
        return percent * percent * (3f - 2f * percent);
    }
}
