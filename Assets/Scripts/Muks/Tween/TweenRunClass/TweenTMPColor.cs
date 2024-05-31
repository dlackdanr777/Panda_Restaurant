using TMPro;
using UnityEngine;

namespace Muks.Tween
{
    public class TweenTMPColor : TweenData
    {
        private Color _startColor;
        private Color _targetColor;
        private TextMeshProUGUI _text;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_text == null)
                _text = (TextMeshProUGUI)dataSequence.Component;

            _startColor = _text.color;
            _targetColor = (Color)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            
            _text.color = Color.LerpUnclamped(_startColor, _targetColor, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _text.color = _targetColor;
        }
    }
}

