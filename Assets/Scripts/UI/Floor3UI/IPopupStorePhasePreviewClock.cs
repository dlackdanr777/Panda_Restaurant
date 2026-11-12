/// <summary>
/// Explicit, memory-only Editor authority. The business clock remains the sole time sampler;
/// an implementation must never read its own clock or advance the phase from another Update.
/// </summary>
public interface IPopupStorePhasePreviewClock
{
    bool TryStart();
    void Advance(double elapsed);
    double RemainingSeconds { get; }
    bool CanPlay { get; }
    bool CanEnter { get; }
    double OnePlushPatienceSeconds { get; }
    void SetContext(bool viewVisible, bool applicationPaused, bool focused, bool exitPaused);
    bool TryPenalty(double seconds);
}
