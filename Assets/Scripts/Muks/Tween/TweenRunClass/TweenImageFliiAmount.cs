using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{
    public class TweenImageFliiAmount : TweenData
    {
        private float _startAmount;
        private float _targetAmount;
        private Image _image;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_image == null)
                _image = (Image)dataSequence.Component;

            _startAmount = _image.fillAmount;
            _targetAmount = (float)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            _image.fillAmount = Mathf.LerpUnclamped(_startAmount, _targetAmount, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _image.fillAmount = _targetAmount;
        }
    }
}
