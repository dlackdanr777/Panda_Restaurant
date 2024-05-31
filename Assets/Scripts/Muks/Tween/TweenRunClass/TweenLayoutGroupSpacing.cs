using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{
    public class TweenLayoutGroupSpacing : TweenData
    {
        private float _startValue;
        private float _targetValue;
        private HorizontalOrVerticalLayoutGroup _layoutGroup;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_layoutGroup == null)
                _layoutGroup = (HorizontalOrVerticalLayoutGroup)dataSequence.Component;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            _layoutGroup.spacing = Mathf.LerpUnclamped(_startValue, _targetValue, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _layoutGroup.spacing = _targetValue;
        }
    }
}
