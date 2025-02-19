using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{
    public class TweenGridLayoutGroupSpacing : TweenData
    {
        private Vector2 _startValue;
        private Vector2 _targetValue;
        private GridLayoutGroup _layoutGroup;


        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_layoutGroup == null)
                _layoutGroup = (GridLayoutGroup)dataSequence.Component;

            _startValue = _layoutGroup.spacing;
            _targetValue = (Vector2)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            _layoutGroup.spacing = Vector2.LerpUnclamped(_startValue, _targetValue, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != Ease.Spike)
                _layoutGroup.spacing = _targetValue;
        }
    }
}
