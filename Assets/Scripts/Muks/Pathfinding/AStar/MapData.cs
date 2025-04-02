
using UnityEngine;

namespace Muks.PathFinding.AStar
{
    public class MapData
    {
        public Vector2 MapBottomLeft;
        public Vector2 MapSize;
        public Node[,] Nodes;
        public int SizeX;
        public int SizeY;

        public MapData(Vector2 mapBottomLeft, Vector2 mapSize)
        {
            float nodeSize = AStar.Instance.NodeSize;

            MapBottomLeft = mapBottomLeft;
            MapSize = mapSize;
            SizeX = Mathf.CeilToInt(mapSize.x / nodeSize);
            SizeY = Mathf.CeilToInt(mapSize.y / nodeSize);
            Nodes = new Node[SizeX, SizeY];
        }

        public Vector2Int WorldToNodePos(Vector2 pos)
        {
            float nodeSize = AStar.Instance.NodeSize;
            int x = Mathf.FloorToInt((pos.x - MapBottomLeft.x) / nodeSize);
            int y = Mathf.FloorToInt((pos.y - MapBottomLeft.y) / nodeSize);
            return new Vector2Int(Mathf.Clamp(x, 0, SizeX - 1), Mathf.Clamp(y, 0, SizeY - 1));
        }
    }

}


