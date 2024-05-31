using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Muks.PathFinding
{
    /// <summary> AStar 길찾기 계산을 멀티 스레드 환경에서 순차적으로 실행시키는 Class  </summary>
    public class PathfindingQueue : MonoBehaviour
    {
        public static PathfindingQueue Instance => _instance;
        private static PathfindingQueue _instance;

        private ConcurrentQueue<Action> _pathfindingQueue = new ConcurrentQueue<Action>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateObj()
        {
            if (_instance != null)
                return;

            GameObject obj = new GameObject("PathfindingQueue");
            _instance = obj.AddComponent<PathfindingQueue>();
            DontDestroyOnLoad(obj);
        }

        private void Awake()
        {
            StartProcessingQueue();
        }


        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary> 큐에 있는 대리자를 순차 실행시키는 함수 </summary>
        private void StartProcessingQueue()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            _processingTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_pathfindingQueue.TryDequeue(out Action action))
                    {
                        action.Invoke();
                    }
                    else
                    {
                        // 큐가 비어 있을 때 잠시 대기한다.
                        await Task.Delay(10, token); 
                    }
                }
            }, token);
        }


        /// <summary> 대리자를 큐에 적재시키는 함수 </summary>
        public void Enqueue(Action action)
        {
            _pathfindingQueue.Enqueue(action);
        }
    }
}
