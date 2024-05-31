using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{
    public class TweenTextContents : TweenData
    {
        private StringBuilder _tmpString = new StringBuilder();
        private char[] _targetChar;
        private Text _text;

        private int _stringLength;
        private int _tmpIndex;



        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_text == null)
                _text = (Text)dataSequence.Component;

            _tmpString.Clear();
            _targetChar = ((string)dataSequence.TargetValue).ToCharArray();

            _stringLength = _targetChar.Length;
            _tmpIndex = -1;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);       
            percent = Mathf.Clamp(percent, 0f, 1f);

            int index = Mathf.RoundToInt(percent * _stringLength);
            if (_tmpIndex == index)
                return;

            _tmpIndex = index;
            _tmpString.Clear();
            for (int i = 0; i < _tmpIndex; i++)
            {
                _tmpString.Append(_targetChar[i]);
            }

            _text.text = _tmpString.ToString();
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                _text.text = new string(_targetChar);
        }
    }
}

