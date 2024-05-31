using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{
    public class TweenImageColor : TweenData
    {
        private Color _startColor;
        private Color _targetColor;
        private Image _image;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_image == null)
                _image = (Image)dataSequence.Component;

            _startColor = _image.color;
            _targetColor = (Color)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            
            _image.color = Color.LerpUnclamped(_startColor, _targetColor, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _image.color = _targetColor;
        }
    }
}
