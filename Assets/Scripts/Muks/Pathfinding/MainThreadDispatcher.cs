using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.PathFinding
{
    /// <summary> 큐에 저장시켜놓고 해당 큐를 순차적으로 실행하는 클래스 </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        public static MainThreadDispatcher Instance => _instance;
        private static MainThreadDispatcher _instance;

        [SerializeField] private int _maxActionsPerFrame = 5; // 프레임당 최대 처리 개수
        [SerializeField] private float _maxExecutionTimeMs = 5f; // 프레임당 최대 실행 시간 (밀리초)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateObj()
        {
            if (_instance != null)
                return;

            GameObject obj = new GameObject("MainThreadDispatcher");
            _instance = obj.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }


        public void Update()
        {
            lock (_executionQueue)
            {
                int processedCount = 0;
                float startTime = Time.realtimeSinceStartup;
                
                while (_executionQueue.Count > 0)
                {
                    // 프레임당 최대 개수 체크
                    if (processedCount >= _maxActionsPerFrame)
                        break;
                    
                    // 실행 시간 체크 (밀리초 단위)
                    float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                    if (elapsedMs >= _maxExecutionTimeMs)
                        break;
                    
                    _executionQueue.Dequeue().Invoke();
                    processedCount++;
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
    }
}

