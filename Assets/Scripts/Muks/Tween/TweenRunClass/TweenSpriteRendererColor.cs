using UnityEngine;

namespace Muks.Tween
{
    public class TweenSpriteRendererColor : TweenData
    {
        private Color _startColor;
        private Color _targetColor;
        private SpriteRenderer _spriteRenderer;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_spriteRenderer == null)
                _spriteRenderer = (SpriteRenderer)dataSequence.Component;

            _startColor = _spriteRenderer.color;
            _targetColor = (Color)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            _spriteRenderer.color = Color.LerpUnclamped(_startColor, _targetColor, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _spriteRenderer.color = _targetColor;
        }
    }
}

