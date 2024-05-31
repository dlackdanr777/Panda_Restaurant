using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.Tween
{
    /// <summary> Tween 시퀸스 기능을 위한 클래스 (반드시 Tween.Sequence()를 통해 생성) </summary>
    public class Sequence
    {
        private LinkedList<SequenceData> _sequenceDataList = new LinkedList<SequenceData>();
        private List<TweenData> _currentTweenDataList = new List<TweenData>();
        private Action _currentCallback;
        private float _elapsedTime;
        private float _waitTime;

        private bool _isStart;
        private bool _isEnd;
        public bool IsEnd => _isEnd;


        /// <summary> 시퀀스 시작 함수(시작 시 시퀀스 추가, 변경 불가능) </summary>
        public void Play()
        {
            SetData();
            _isStart = true;
        }


        /// <summary> Tween 명령을 시퀸스 맨 뒤에 추가 </summary>
        public Sequence Append(TweenData tweenData)
        {
            if (_isStart)
            {
                Debug.LogError("현재 시퀸스가 실행중 입니다.");
                return this;
            }

            tweenData.enabled = false;
            SequenceData data = new SequenceData();
            data.TweenDataList = new List<TweenData>{ tweenData };

            _sequenceDataList.AddLast(data);
            return this;
        }



        /// <summary> Tween 명령을 현재 마지막 시퀸스의 Tween 명령과 합침 </summary>
        public Sequence Join(TweenData tweenData)
        {
            if (_isStart)
            {
                Debug.LogError("현재 시퀸스가 실행중 입니다.");
                return this;
            }

            if (_sequenceDataList.Last.Value.TweenDataList == null)
            {
                Debug.LogError("마지막 시퀸스에 Tween명령이 존재하지 않아 Join할 수 없습니다.");
                return this;
            }

            SequenceData data = _sequenceDataList.Last.Value;
            data.TweenDataList.Add(tweenData);
            tweenData.enabled = false;

            _sequenceDataList.RemoveLast();
            _sequenceDataList.AddLast(data);
            return this;
        }


        /// <summary> 대기 명령을 시퀸스 맨 뒤에 추가 </summary>
        public Sequence AppendInterval(float interval)
        {
            if (_isStart)
            {
                Debug.LogError("현재 시퀸스가 실행중 입니다.");
                return this;
            }

            SequenceData data = new SequenceData();
            data.WaitTime = interval;
            _sequenceDataList.AddLast(data);
            return this;
        }


        /// <summary> 콜백 함수를 현재 마지막 시퀸스에 추가 </summary>
        public Sequence AppendCallback(Action callback)
        {
            if (_isStart)
            {
                Debug.LogError("현재 시퀸스가 실행중 입니다.");
                return this;
            }

            SequenceData data = _sequenceDataList.Last.Value;
            data.Callback = callback;

            _sequenceDataList.RemoveLast();
            _sequenceDataList.AddLast(data);
            return this;
        }


        /// <summary> Tween class에서 현재 시퀸스를 업데이트하기 위한 함수 (임의 사용 X) </summary>
        public void Update(float _deltaTime)
        {
            //시작중이 아니거나 끝난상태면 리턴
            if (!_isStart || _isEnd)
                return;

            //대기 시간 시퀀스가 진행중이면 일정시간 대기 후 콜백 및 SetData()로 다음 시퀀스를 불러옴
            if (0 < _waitTime)
            {
                _elapsedTime += _deltaTime;

                if(_waitTime <= _elapsedTime)
                {
                    _currentCallback?.Invoke();
                    SetData();
                    return;
                }

                return;
            }

            //Tween 시퀀스가 진행중이면
            if (0 < _currentTweenDataList.Count)
            {
                _elapsedTime += _deltaTime;

                //일정시간 전까진 계속 리턴
                if (_elapsedTime < _deltaTime * 5)
                    return;

                //시퀀스안의 TweenData의 숫자만큼 반복
                for (int i = 0, count = _currentTweenDataList.Count; i < count; i++)
                {

                    //경과시간이 지나지 않았으면 continue
                    if (_elapsedTime < _currentTweenDataList[i].TotalDuration)
                        continue;

                    //이미 종료가 됬다면  continue
                    if (!_currentTweenDataList[i].enabled)
                        continue;
                    
                    //Tween내의 완료 대리자를 실행하며 비활성화 시킴
                    _currentTweenDataList[i].OnCompletedStart();
                    _currentTweenDataList[i].enabled = false;
                }

                //만약 전부 비활성화 상태면 아래로 넘어감
                for (int i = 0, count = _currentTweenDataList.Count; i < count; i++)
                {
                    if (_currentTweenDataList[i].enabled)
                        return;
                }

                //시퀀스 콜백 실행및 SetData()를 통해 다음 시퀀스를 불러옴
                _currentCallback?.Invoke();
                SetData();
            }
        }


        private void SetData()
        {
            if (_sequenceDataList.Count <= 0)
            {
                _isEnd = true;
                return;
            }

            SequenceData currentSequenceData = _sequenceDataList.First.Value;
            _sequenceDataList.RemoveFirst();

            _elapsedTime = 0;
            _currentTweenDataList.Clear();
            _currentCallback = null;
            _waitTime = currentSequenceData.WaitTime;


            if (currentSequenceData.TweenDataList != null)
            {
                _currentTweenDataList = currentSequenceData.TweenDataList;
                for (int i = 0, count = _currentTweenDataList.Count; i < count; i++)
                {
                    _currentTweenDataList[i].enabled = true;
                }
            }

            if(currentSequenceData.Callback != null)
            {
                _currentCallback = currentSequenceData.Callback;
            }

        }
    }
}

