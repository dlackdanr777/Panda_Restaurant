using UnityEngine;

namespace Muks.Tween
{
    public class TweenCanvasGroupAlpha : TweenData
    {
        private float _startAlpha;
        private float _targetAlpha;
        private CanvasGroup _canvasGroup;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_canvasGroup == null)
                _canvasGroup = (CanvasGroup)dataSequence.Component;

            _targetAlpha = (float)dataSequence.TargetValue;
            _startAlpha = _canvasGroup.alpha;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            
            _canvasGroup.alpha = Mathf.LerpUnclamped(_startAlpha, _targetAlpha, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _canvasGroup.alpha = _targetAlpha;
        }
    }
}

