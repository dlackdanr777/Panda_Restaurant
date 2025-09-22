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
            var startMap = FindBestMatchingMap(start);
            var endMap = FindBestMatchingMap(end);
            
            // ? 시작점과 목적지 모두 체크
            Vector2 correctedStart = start;
            Vector2 correctedEnd = end;
            MapData selectedMap = null;

            // 시작점이 맵 밖에 있는 경우
            if (startMap == null)
            {
                var closestStartInfo = FindClosestMapAndTile(start);
                if (closestStartInfo.map == null)
                {
                    Debug.LogError($"No available maps found for start position: {start}");
                    callback(new List<Vector2>());
                    return;
                }
                startMap = closestStartInfo.map;
                correctedStart = closestStartInfo.tilePosition;
                Debug.LogWarning($"Start position was outside map. Moved to closest tile: {correctedStart}");
            }

            // 목적지가 맵 밖에 있는 경우
            if (endMap == null)
            {
                var closestEndInfo = FindClosestMapAndTile(end);
                if (closestEndInfo.map == null)
                {
                    Debug.LogError($"No available maps found for end position: {end}");
                    callback(new List<Vector2>());
                    return;
                }
                endMap = closestEndInfo.map;
                correctedEnd = closestEndInfo.tilePosition;
                Debug.LogWarning($"End position was outside map. Moved to closest tile: {correctedEnd}");
            }

            // ? 시작점과 목적지가 같은 맵에 있는지 확인
            if (startMap == endMap)
            {
                selectedMap = startMap;
            }
            else
            {
                // 다른 맵에 있다면, 목적지 맵을 우선 선택
                selectedMap = endMap;
                
                // 시작점을 목적지 맵 내 가장 가까운 위치로 이동
                Vector2 newStart = FindClosestWalkableTileInMap(selectedMap, correctedStart);
                if (newStart != Vector2.zero)
                {
                    correctedStart = newStart;
                    Debug.LogWarning($"Start and end are in different maps. Moved start to: {correctedStart}");
                }
                else
                {
                    // 목적지 맵에 이동 가능한 타일이 없다면 시작점 맵 사용
                    selectedMap = startMap;
                    Vector2 newEnd = FindClosestWalkableTileInMap(selectedMap, correctedEnd);
                    if (newEnd != Vector2.zero)
                    {
                        correctedEnd = newEnd;
                        Debug.LogWarning($"No walkable tiles in end map. Moved end to: {correctedEnd}");
                    }
                }
            }

            PathfindingQueue.Instance.Enqueue(() =>
            {
                var result = PathFinding(selectedMap, correctedStart, correctedEnd);
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

        /// <summary>위치에서 가장 가까운 맵과 그 맵에서 가장 가까운 이동 가능한 타일 찾기</summary>
        private (MapData map, Vector2 tilePosition) FindClosestMapAndTile(Vector2 position)
        {
            MapData closestMap = null;
            Vector2 closestTilePos = Vector2.zero;
            float closestDistance = float.MaxValue;

            foreach (var map in _maps.Values)
            {
                // 각 맵에서 가장 가까운 이동 가능한 타일 찾기
                Vector2 closestTileInMap = FindClosestWalkableTileInMap(map, position);
                
                if (closestTileInMap != Vector2.zero) // 이동 가능한 타일이 있다면
                {
                    float distance = Vector2.SqrMagnitude(position - closestTileInMap);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestMap = map;
                        closestTilePos = closestTileInMap;
                    }
                }
            }

            return (closestMap, closestTilePos);
        }

        /// <summary>특정 맵에서 주어진 위치에 가장 가까운 이동 가능한 타일 찾기</summary>
        private Vector2 FindClosestWalkableTileInMap(MapData map, Vector2 position)
        {
            float nodeSize = NodeSize;
            
            // 주어진 위치를 노드 좌표로 변환 (맵 밖이어도 계산)
            Vector2Int centerNodePos = new Vector2Int(
                Mathf.RoundToInt((position.x - map.MapBottomLeft.x) / nodeSize - 0.5f),
                Mathf.RoundToInt((position.y - map.MapBottomLeft.y) / nodeSize - 0.5f)
            );

            // 나선형으로 검색하여 가장 가까운 이동 가능한 타일 찾기
            int maxRadius = Mathf.Max(map.SizeX, map.SizeY);
            
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                // 현재 반지름에서 모든 위치 검사
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // 현재 반지름의 경계에 있는 점들만 검사 (이미 검사한 내부는 제외)
                        if (radius > 0 && Mathf.Abs(dx) < radius && Mathf.Abs(dy) < radius)
                            continue;

                        int nodeX = centerNodePos.x + dx;
                        int nodeY = centerNodePos.y + dy;

                        // 맵 범위 내인지 확인
                        if (nodeX >= 0 && nodeX < map.SizeX && nodeY >= 0 && nodeY < map.SizeY)
                        {
                            Node node = map.Nodes[nodeX, nodeY];
                            
                            // 이동 가능한 타일인지 확인
                            if (!node.IsWall && node.IsGround)
                            {
                                Vector2 tileWorldPos = new Vector2(
                                    map.MapBottomLeft.x + (nodeX + 0.5f) * nodeSize,
                                    map.MapBottomLeft.y + (nodeY + 0.5f) * nodeSize
                                );
                                return tileWorldPos;
                            }
                        }
                    }
                }
            }

            return Vector2.zero; // 이동 가능한 타일을 찾지 못함
        }

        private List<Vector2> PathFinding(MapData map, Vector2 start, Vector2 end)
        {
            Vector2Int sPos = map.WorldToNodePos(start);
            Vector2Int tPos = map.WorldToNodePos(end);

            // ? 노드 위치가 맵 범위 내인지 확인
            if (sPos.x < 0 || sPos.x >= map.SizeX || sPos.y < 0 || sPos.y >= map.SizeY)
            {
                Debug.LogError($"Start position {start} -> node {sPos} is outside map bounds");
                return new List<Vector2>();
            }
            
            if (tPos.x < 0 || tPos.x >= map.SizeX || tPos.y < 0 || tPos.y >= map.SizeY)
            {
                Debug.LogError($"End position {end} -> node {tPos} is outside map bounds");
                return new List<Vector2>();
            }

            // ? 시작점과 목적지가 이동 가능한 타일인지 확인
            var startNode = map.Nodes[sPos.x, sPos.y];
            var targetNode = map.Nodes[tPos.x, tPos.y];

            if (startNode.IsWall || !startNode.IsGround)
            {
                Debug.LogError($"Start position {start} is not walkable (Wall: {startNode.IsWall}, Ground: {startNode.IsGround})");
                return new List<Vector2>();
            }

            if (targetNode.IsWall || !targetNode.IsGround)
            {
                Debug.LogError($"End position {end} is not walkable (Wall: {targetNode.IsWall}, Ground: {targetNode.IsGround})");
                return new List<Vector2>();
            }

            foreach (var node in map.Nodes)
            {
                node.G = int.MaxValue;
                node.H = 0;
                node.ParentNode = null;
            }

            startNode.G = 0;
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

            Debug.LogWarning($"No path found from {start} to {end}");
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
