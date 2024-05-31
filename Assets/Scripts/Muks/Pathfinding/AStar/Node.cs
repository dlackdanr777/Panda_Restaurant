using UnityEngine;

namespace Muks.PathFinding.AStar
{
    /// <summary>AStar에서 사용하는 Node Class</summary>
    public class Node
    {
        public int F => G + H;
        public int H;
        public int G;
        public int Y;
        public int X;
        public bool IsWall;
        public Node ParentNode;

        private float _nodeSize => AStar.Instance.NodeSize;
        private Vector2 _mapBottomLeft => AStar.Instance.MapBottomLeft;

        public Node(bool isWall, int x, int y) { IsWall = isWall; X = x; Y = y; }


        /// <summary> 노드 좌표를 Vector2 형식으로 반환</summary>
        public Vector2 toVector2()
        {
            return new Vector2(X, Y);
        }


        /// <summary> 노드 좌표를 월드 좌표로 변환해 반환</summary>
        public Vector2 toWorldPosition()
        {
            float posX = _mapBottomLeft.x + (X + 0.5f) * _nodeSize;
            float posY = _mapBottomLeft.y + (Y + 0.5f) * _nodeSize;
            return new Vector2(posX, posY);
        }
    }
}
