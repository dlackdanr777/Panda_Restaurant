using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Muks.PathFinding
{
    /// <summary> AStar ��ã�� ����� ��Ƽ ������ ȯ�濡�� ���������� �����Ű�� Class  </summary>
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

        /// <summary> ť�� �ִ� �븮�ڸ� ���� �����Ű�� �Լ� </summary>
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
                        // ť�� ��� ���� �� ��� ����Ѵ�.
                        await Task.Delay(10, token); 
                    }
                }
            }, token);
        }


        /// <summary> �븮�ڸ� ť�� �����Ű�� �Լ� </summary>
        public void Enqueue(Action action)
        {
            _pathfindingQueue.Enqueue(action);
        }
    }
}
