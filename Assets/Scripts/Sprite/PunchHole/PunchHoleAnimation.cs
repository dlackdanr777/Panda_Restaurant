using Muks.Tween;
using UnityEngine;

public class PunchHoleAnimation : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpriteMask[] _punchHoleMasks;

    public void TweenScale(float startScale, float targetScale, float duraiton, Ease ease)
    {
        for(int i = 0, cnt = _punchHoleMasks.Length; i < cnt; ++i)
        {
            Vector3 tmpScale = _punchHoleMasks[i].transform.localScale;
            Vector3 tmpStartScale = tmpScale * startScale;
            Vector3 tmpTargetScale = tmpScale * targetScale;

            _punchHoleMasks[i].transform.localScale = tmpStartScale;
            _punchHoleMasks[i].TweenScale(tmpTargetScale, duraiton, ease);
        }

    }
}
