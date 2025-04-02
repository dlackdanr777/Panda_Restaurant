using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.PathFinding.AStar
{
    public class AStar : MonoBehaviour
    {
        public static AStar Instance { get; private set; }

        [Header("맵 설정")]
        [SerializeField] private bool _drawGizmos;
        [Range(0.01f, 100000f)] [SerializeField] private float _nodeSize = 1f;
        public float NodeSize => _nodeSize;
        [SerializeField] private MapInfo[] _mapInfos;

        private Dictionary<string, MapData> _maps = new();
        private readonly int[] _dirX = { 0, 0, 1, -1, 1, 1, -1, -1 };
        private readonly int[] _dirY = { 1, -1, 0, 0, 1, -1, -1, 1 };
        private readonly int[] _cost = { 10, 10, 10, 10, 14, 14, 14, 14 };

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;

            foreach (var info in _mapInfos)
                AddMap(info);
        }

        private void AddMap(MapInfo info)
        {
            var map = new MapData(info.MapBottomLeft, info.MapSize);
            GenerateNodes(map);
            _maps[info.MapKey] = map;
        }

        private void GenerateNodes(MapData map)
        {
            float nodeSize = NodeSize;

            for (int i = 0; i < map.SizeX; i++)
            {
                float posX = map.MapBottomLeft.x + (i + 0.5f) * nodeSize;
                for (int j = 0; j < map.SizeY; j++)
                {
                    float posY = map.MapBottomLeft.y + (j + 0.5f) * nodeSize;

                    bool isWall = false, isGround = false;
                    foreach (Collider2D col in Physics2D.OverlapCircleAll(new Vector2(posX, posY), nodeSize * 0.4f))
                        if (col.gameObject.layer == LayerMask.NameToLayer("Wall"))
                            isWall = true;

                    if (!isWall && j != 0)
                    {
                        float groundY = map.MapBottomLeft.y + (j - 1 + 0.5f) * nodeSize;
                        foreach (Collider2D col in Physics2D.OverlapCircleAll(new Vector2(posX, groundY), nodeSize * 0.4f))
                            if (col.gameObject.layer == LayerMask.NameToLayer("Ground"))
                                isGround = true;
                    }

                    map.Nodes[i, j] = new Node(isWall, isGround, i, j);
                }
            }
        }


        /// <summary>맵 키 없이 자동으로 포함된 맵에서 길찾기</summary>
        public void RequestPath(Vector2 start, Vector2 end, Action<List<Vector2>> callback)
        {
            var map = FindBestMatchingMap(start);

            if (map == null)
            {
                Debug.LogError($"No map contains start position: {start}");
                callback(new List<Vector2>());
                return;
            }

            PathfindingQueue.Instance.Enqueue(() =>
            {
                var result = PathFinding(map, start, end);
                MainThreadDispatcher.Instance.Enqueue(() => callback(result));
            });
        }

        /// <summary>위치가 포함된 맵들 중, 중심에 가장 가까운 맵 반환</summary>
        private MapData FindBestMatchingMap(Vector2 pos)
        {
            MapData bestMap = null;
            float bestDistance = float.MaxValue;

            foreach (var map in _maps.Values)
            {
                Rect rect = new Rect(map.MapBottomLeft, map.MapSize);
                if (rect.Contains(pos))
                {
                    Vector2 center = map.MapBottomLeft + new Vector2(map.MapSize.x, map.MapSize.y) * 0.5f;
                    float dist = Vector2.SqrMagnitude(pos - center); // 거리의 제곱
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestMap = map;
                    }
                }
            }

            return bestMap;
        }

        private List<Vector2> PathFinding(MapData map, Vector2 start, Vector2 end)
        {
            Vector2Int sPos = map.WorldToNodePos(start);
            Vector2Int tPos = map.WorldToNodePos(end);

            foreach (var node in map.Nodes)
            {
                node.G = int.MaxValue;
                node.H = 0;
                node.ParentNode = null;
            }

            var startNode = map.Nodes[sPos.x, sPos.y];
            var targetNode = map.Nodes[tPos.x, tPos.y];

            var openList = new List<Node> { startNode };
            var closedSet = new HashSet<Node>();

            while (openList.Count > 0)
            {
                Node current = openList[0];
                for (int i = 1; i < openList.Count; i++)
                    if (openList[i].F < current.F || (openList[i].F == current.F && openList[i].H < current.H))
                        current = openList[i];

                openList.Remove(current);
                closedSet.Add(current);

                if (current == targetNode)
                {
                    var path = new List<Vector2>();
                    Node node = targetNode;
                    while (node != startNode)
                    {
                        path.Add(node.toWorldPosition(map.MapBottomLeft));
                        node = node.ParentNode;
                    }

                    path.Add(startNode.toWorldPosition(map.MapBottomLeft));
                    path.Reverse();
                    return path;
                }

                for (int d = 0; d < 4; d++)
                {
                    int nx = current.X + _dirX[d];
                    int ny = current.Y + _dirY[d];
                    if (nx < 0 || ny < 0 || nx >= map.SizeX || ny >= map.SizeY)
                        continue;

                    Node next = map.Nodes[nx, ny];
                    if (next.IsWall || !next.IsGround || closedSet.Contains(next))
                        continue;

                    int moveCost = current.G + _cost[d];
                    if (moveCost < next.G || !openList.Contains(next))
                    {
                        next.G = moveCost;
                        next.H = (Mathf.Abs(nx - tPos.x) + Mathf.Abs(ny - tPos.y)) * 10;
                        next.ParentNode = current;
                        if (!openList.Contains(next)) openList.Add(next);
                    }
                }
            }

            return new List<Vector2>();
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;

            float nodeSize = NodeSize;

            foreach (var info in _mapInfos)
            {
                // 맵 외곽 박스 표시
                Vector2 mapCenter = info.MapBottomLeft + new Vector2(info.MapSize.x, info.MapSize.y) * 0.5f;
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(mapCenter, info.MapSize);

                int sizeX = Mathf.CeilToInt(info.MapSize.x / nodeSize);
                int sizeY = Mathf.CeilToInt(info.MapSize.y / nodeSize);

                for (int i = 0; i < sizeX; i++)
                {
                    float posX = info.MapBottomLeft.x + (i + 0.5f) * nodeSize;

                    for (int j = 0; j < sizeY; j++)
                    {
                        float posY = info.MapBottomLeft.y + (j + 0.5f) * nodeSize;
                        Vector2 nodePos = new Vector2(posX, posY);

                        Color color = Color.white;

                        // 게임 실행 중이고 노드가 초기화되어 있으면 색상 구분
                        if (_maps.TryGetValue(info.MapKey, out var map) && map.Nodes != null &&
                            i < map.SizeX && j < map.SizeY)
                        {
                            Node node = map.Nodes[i, j];
                            if (node != null)
                            {
                                color = node.IsWall ? Color.red :
                                        node.IsGround ? Color.green :
                                        Color.white;
                            }
                        }

                        Gizmos.color = color;
                        Gizmos.DrawWireCube(nodePos, Vector2.one * nodeSize * 0.95f);
                    }
                }
            }
        }

    }
}
