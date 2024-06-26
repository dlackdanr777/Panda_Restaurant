using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Muks.Tween
{

    public class TweenData : MonoBehaviour
    {

        public float ElapsedDuration; // 현재 경과 시간
        public float TotalDuration; //총 시간
        public bool IsLoop; //반복 여부

        protected TweenMode _tweenMode;
        protected Queue<TweenDataSequence> _dataSequences = new Queue<TweenDataSequence>();
        protected Dictionary<TweenMode, Func<float, float, float>> _percentHandler;

        private bool _isRightMove = true;
        private LoopType _loopType;

        private Action _onUpdated;
        private Action _onStarted;
        private Action _onCompleted;
        private Dictionary<int, Action> _onUpdatedDic = new Dictionary<int, Action>();
        private Dictionary<int, Action> _onStartedDic = new Dictionary<int, Action>();
        private Dictionary<int, Action> _onCompletedDic = new Dictionary<int, Action>();
        private int _currentDataSequenceId;
        private int _dataSequenceIdCount = -1;


        public virtual void SetData(TweenDataSequence dataSequence)
        {
            _currentDataSequenceId = dataSequence.Id;
            TotalDuration = dataSequence.Duration;
            _tweenMode = dataSequence.TweenMode;

            //해당 애니메이션 id값과 같은 id를 가진 대리자가 존재할 경우 진행중 대리자 적재
            if (_onUpdatedDic.TryGetValue(dataSequence.Id, out _onUpdated))
                _onUpdatedDic.Remove(dataSequence.Id);
            else
                _onUpdated = null;

            //해당 애니메이션 id값과 같은 id를 가진 대리자가 존재할 경우 시작 대리자 실행
            if (_onStartedDic.TryGetValue(_currentDataSequenceId, out _onStarted))
            {
                _onStarted?.Invoke();
                _onStartedDic.Remove(_currentDataSequenceId);
            }

            //해당 애니메이션 id값과 같은 id를 가진 대리자가 존재할 경우 완료 대리자 적재
            if (_onCompletedDic.TryGetValue(_currentDataSequenceId, out _onCompleted))
            {
                _onCompletedDic.Remove(_currentDataSequenceId);
            }
            else
            {
                _onCompleted = null;
            }

        }


        public void AddDataSequence(TweenDataSequence dataSequence)
        {
            //대리자가 어느 DataSequence와 같이 추가되는지 확인하기 위해 Id값 부여
            dataSequence.SetId(++_dataSequenceIdCount);
            _dataSequences.Enqueue(dataSequence);
        }



        /// <summary>무한 반복</summary>
        public void Loop(LoopType loopType = LoopType.Restart)
        {
            TweenDataSequence sequence = _dataSequences.Last();
            _dataSequences.Clear();
            _dataSequences.Enqueue(sequence);
            SetData(_dataSequences.Dequeue());
            _loopType = loopType;
            IsLoop = true;
        }


        /// <summary>반복 횟수 설정</summary>
        public void Repeat(int count)
        {
            TweenDataSequence sequence = _dataSequences.Last();

            for (int i = 1; i < count; i++)
            {
                AddDataSequence(sequence);
            }
        }


        /// <summary>Tween의 모든 데이터를 초기화하는 함수</summary>
        public void Clear()
        {
            _dataSequences.Clear();

            _onStartedDic.Clear();
            _onStarted = null;
            _onCompletedDic.Clear();
            _onCompleted = null;
            _onUpdatedDic.Clear();
            _onUpdated = null;

            IsLoop = false;
            enabled = false;

            ElapsedDuration = 0;
        }


        /// <summary>애니메이션 시작시 실행되는 대리자를 받는 함수</summary>
        public TweenData OnStart(Action onStarted)
        {
            _onStartedDic.Add(_dataSequenceIdCount, onStarted);
            return this;
        }


        /// <summary>애니메이션 종료후 실행되는 대리자를 받는 함수</summary>
        public TweenData OnComplete(Action onCompleted)
        {
            _onCompletedDic.Add(_dataSequenceIdCount, onCompleted);
            return this;
        }


        /// <summary>애니메이션 진행중 반복적으로 실행되는 대리자를 받는 함수</summary>
        public TweenData OnUpdate(Action onUpdated)
        {
            _onUpdatedDic.Add(_dataSequenceIdCount, onUpdated);
            return this;
        }


        /// <summary> 현재 완료 대리자를 실행하고 null값으로 변경하는 함수 </summary>
        public void OnCompletedStart()
        {
            _onCompleted?.Invoke();
            _onCompleted = null;
        }


        private void Awake()
        {
            _percentHandler = new Dictionary<TweenMode, Func<float, float, float>>
            {
                { TweenMode.Constant, Constant },
                {TweenMode.Quadratic, Quadratic },
                { TweenMode.Smoothstep, Smoothstep },
                { TweenMode.Smootherstep, Smootherstep },
                {TweenMode.Spike, Spike },
                {TweenMode.EaseInQuint, EaseInQuint },
                {TweenMode.EaseOutQuint, EaseOutQuint },
                {TweenMode.EaseInOutQuint, EaseInOutQuint },
                {TweenMode.EaseInExpo, EaseInExpo },
                {TweenMode.EaseOutExpo, EaseOutExpo },
                {TweenMode.EaseInOutExpo, EaseInOutExpo },
                {TweenMode.EaseInElastic, EaseInElastic },
                {TweenMode.EaseOutElastic, EaseOutElastic },
                {TweenMode.EaseInOutElastic, EaseInOutElastic },
                {TweenMode.EaseInBack, EaseInBack },
                {TweenMode.EaseOutBack, EaseOutBack },
                {TweenMode.EaseInOutBack, EaseInOutBack },
                {TweenMode.EaseInBounce, EaseInBounce },
                {TweenMode.EaseOutBounce, EaseOutBounce },
                {TweenMode.EaseInOutBounce, EaseInOutBounce },
                { TweenMode.Sinerp, Sinerp },
                { TweenMode.Coserp, Coserp }
            };
        }


        protected virtual void Update()
        {
            _onUpdated?.Invoke();

            //만약 반복 설정이 되어있다면?
            if (IsLoop)
            {
                switch (_loopType)
                {
                    case LoopType.Restart:
                        ElapsedDuration += Time.deltaTime;
                        ElapsedDuration = Mathf.Clamp(ElapsedDuration, 0, TotalDuration);

                        if (TotalDuration <= ElapsedDuration)
                        {
                            ElapsedDuration = 0;
                            _onCompleted?.Invoke();
                        }

                        break;

                    case LoopType.Yoyo:
                        ElapsedDuration += _isRightMove ? Time.deltaTime : -Time.deltaTime;
                        ElapsedDuration = Mathf.Clamp(ElapsedDuration, 0, TotalDuration);

                        if (_isRightMove && TotalDuration <= ElapsedDuration)
                        {
                            _isRightMove = false;
                            _onCompleted?.Invoke();
                        }
                        else if (!_isRightMove && ElapsedDuration <= 0)
                        {
                            _isRightMove = true;
                            _onCompleted?.Invoke();
                        }
                        break;
                }
            }

            else
            {
                ElapsedDuration += Time.deltaTime;
                ElapsedDuration = Mathf.Clamp(ElapsedDuration, 0, TotalDuration);

                //현재 경과 시간이 총 경과시간을 넘지 않았을 경우 리턴
                if (ElapsedDuration < TotalDuration)
                    return;

                //총 경과시간을 넘었을 경우 콜백 함수 실행 및 초기화
                _onCompleted?.Invoke();
                _onCompleted = null;
                _onUpdated = null;

                //적재된 애니메이션들이 있을 경우 큐에서 뽑아 애니메이션 설정을 다시 진행 한다.
                if (0 < _dataSequences.Count)
                {
                    ElapsedDuration = 0;
                    SetData(_dataSequences.Dequeue());
                }
                //없을 경우 완료 함수 실행후 종료
                else
                {
                    TweenCompleted();
                    enabled = false;
                }
            }
        }


        /// <summary>Tween애니메이션이 종료될 경우 불러오는 함수 </summary>
        protected virtual void TweenCompleted()
        {
        }


        /// <summary> 등속 운동 </summary>
        private float Constant(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent;
        }


        /// <summary> 가속 운동 </summary>
        private float Quadratic(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            percent = percent * percent;

            return percent;
        }


        /// <summary> 점점 빠르게 가다 점점 느리게 </summary>
        private float Smoothstep(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            percent = percent * percent * (3f - 2f * percent);

            return percent;
        }


        /// <summary> 점점 빠르게 가다 점점 느리게(Smoothstep 보다 더 느리게) </summary>
        private float Smootherstep(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            percent = percent * percent * percent * (percent * (6f * percent - 15f) + 10f);

            return percent;
        }


        /// <summary> 목표 위치로 갔다가 다시 원래 위치로 이동</summary>
        private float Spike(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            if(elapsedDuration <= totalDuration * 0.5f)
                return  Mathf.Pow(percent/ 0.5f, 3);

            return Mathf.Pow((1 - percent) / 0.5f, 3);
        }


        private float EaseInQuint(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent * percent * percent * percent * percent;
        }


        private float EaseOutQuint(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return 1 - Mathf.Pow((1 - percent), 5);
        }


        private float EaseInOutQuint(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent < 0.5f ? 16 * percent * percent * percent * percent * percent
                : 1 - Mathf.Pow(-2 * percent + 2, 5) / 2;
        }


        private float EaseInExpo(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent == 0 ? 0 : Mathf.Pow(2, 10 * percent - 10);
        }


        private float EaseOutExpo(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent == 1 ? 1 : 1 - Mathf.Pow(2, -10 * percent);
        }


        private float EaseInOutExpo(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            float returnValue = percent == 0
                ? 0
                : percent == 1
                ? 1
                : percent < 0.5f ? Mathf.Pow(2, 20 * percent - 10) / 2
                : (2 - Mathf.Pow(2, -20 * percent + 10)) / 2;

            return returnValue;
        }


        private float EaseInElastic(float elapsedDuration, float totalDuration)
        {
            float c = (2 * Mathf.PI) / 3f;
            float percent = elapsedDuration / totalDuration;

            return percent == 0 ? 0 : percent == 1 ?
                1: -Mathf.Pow(2, 10 * percent - 10) * Mathf.Sin((percent * 10 - 10.75f) * c);
        }


        private float EaseOutElastic(float elapsedDuration, float totalDuration)
        {
            float c = (2 * Mathf.PI) / 3f;
            float percent = elapsedDuration / totalDuration;

            return percent == 0 ? 0 : percent == 1 ?
                1 : Mathf.Pow(2, -10 * percent) * Mathf.Sin((percent * 10 - 0.75f) * c) + 1;
        }


        private float EaseInOutElastic(float elapsedDuration, float totalDuration)
        {
            float c = (2 * Mathf.PI) / 4.5f;
            float percent = elapsedDuration / totalDuration;

            return percent == 0 ? 0 : percent == 1 ? 1 : percent < 0.5f
            ? -(Mathf.Pow(2, 20 * percent - 10) * Mathf.Sin((20 * percent - 11.125f) * c)) / 2
            : (Mathf.Pow(2, -20 * percent + 10) * Mathf.Sin((20 * percent - 11.125f) * c)) / 2 + 1;
        }


        private float EaseInBack(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            float c1 = 1.70158f;
            float c2 = c1 + 1f;

            return c2 * percent * percent * percent - c1 * percent * percent;
        }


        private float EaseOutBack(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            float c1 = 1.70158f;
            float c2 = c1 + 1f;

            return 1 + c2 * Mathf.Pow(percent - 1, 3) + c1 * Mathf.Pow(percent - 1, 2);
        }


        private float EaseInOutBack(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            float c1 = 1.70158f;
            float c2 = c1 * 1.525f;

            return percent < 0.5f
               ? (Mathf.Pow(2 * percent, 2) * ((c2 + 1) * 2 * percent - c2)) / 2
               : (Mathf.Pow(2 * percent - 2, 2) * ((c2 + 1) * (percent * 2 - 2) + c2) + 2) / 2;
        }


        private float EaseInBounce(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            return 1 - EaseOutBounce(1 - percent);
        }


        private float EaseOutBounce(float percent)
        {
            float n1 = 7.5625f;
            float d1 = 2.75f;
            if (percent < 1 / d1)
            {
                return n1 * percent * percent;
            }
            else if (percent < 2 / d1)
            {
                return n1 * (percent -= 1.5f / d1) * percent + 0.75f;
            }
            else if (percent < 2.5f / d1)
            {
                return n1 * (percent -= 2.25f / d1) * percent + 0.9375f;
            }
            else
            {
                return n1 * (percent -= 2.625f / d1) * percent + 0.984375f;
            }
        }


        private float EaseOutBounce(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            float n1 = 7.5625f;
            float d1 = 2.75f;
            if (percent < 1 / d1)
            {
                return n1 * percent * percent;
            }
            else if (percent < 2 / d1)
            {
                return n1 * (percent -= 1.5f / d1) * percent + 0.75f;
            }
            else if (percent < 2.5f / d1)
            {
                return n1 * (percent -= 2.25f / d1) * percent + 0.9375f;
            }
            else
            {
                return n1 * (percent -= 2.625f / d1) * percent + 0.984375f;
            }
        }


        private float EaseInOutBounce(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;

            return percent < 0.5f
               ? (1 - EaseOutBounce(1 - 2 * percent)) / 2
               : (1 + EaseOutBounce(2 * percent - 1)) / 2;
        }


        //sin그래프처럼 움직임
        private float Sinerp(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            percent = Mathf.Sin(percent * Mathf.PI * 0.5f);

            return percent;
        }


        //cos그래프처럼 움직임
        private float Coserp(float elapsedDuration, float totalDuration)
        {
            float percent = elapsedDuration / totalDuration;
            percent = Mathf.Cos(percent * Mathf.PI * 0.5f);

            return percent;
        }
    }
}
