using System;
using Muks.DataBind;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Opt-in input feedback. Never subscribes to onClick: automatic gameplay calls stay silent.</summary>
[DisallowMultipleComponent]
public sealed class GachaButtonInputSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    private Button _button;
    private SoundEffectType _sound;
    private Func<bool> _canPlay;
    private int? _pressedPointer;

    public static GachaButtonInputSound Bind(Button button, SoundEffectType sound = SoundEffectType.ButtonClickSound,
        Func<bool> canPlay = null)
    {
        if (button == null) return null;
        var feedback = button.GetComponent<GachaButtonInputSound>();
        if (feedback == null) feedback = button.gameObject.AddComponent<GachaButtonInputSound>();
        feedback._button = button; feedback._sound = sound; feedback._canPlay = canPlay;
        // Only these opted-in buttons stop using the automatic onClick audio binding.
        foreach (var getter in button.GetComponents<ButtonGetter>()) getter.SuppressAutomaticSoundBinding();
        return feedback;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data == null || data.button != PointerEventData.InputButton.Left || _pressedPointer.HasValue || !CanPlay()) return;
        _pressedPointer = data.pointerId;
        Play(_sound); // Same press-down frame as ButtonPressEffect; no release/cancel duplicate.
    }
    public void OnPointerUp(PointerEventData data)
    { if (data != null && _pressedPointer == data.pointerId) _pressedPointer = null; }
    public void OnSubmit(BaseEventData data)
    { if (data != null && CanPlay()) Play(_sound); }
    private bool CanPlay() => isActiveAndEnabled && _button != null && _button.IsActive() &&
        _button.IsInteractable() && (_canPlay == null || _canPlay());
    private void OnDisable() => _pressedPointer = null;
    public static void Play(SoundEffectType sound)
    {
        var manager = SoundManager.TryGetExistingInstance();
        if (manager != null && !manager.IsEffectAudioMuted) manager.PlayEffectAudio(EffectType.None, sound);
    }
}
