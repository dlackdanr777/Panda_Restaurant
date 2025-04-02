using UnityEngine;

namespace Muks.PathFinding.AStar
{
    /// <summary>AStar���� ����ϴ� Node Class</summary>
    public class Node
    {
        public int F => G + H;
        public int H;
        public int G;
        public int Y;
        public int X;
        public bool IsWall;
        public bool IsGround;
        public Node ParentNode;

        public Node(bool isWall, bool isGround, int x, int y)
        {
            IsWall = isWall;
            IsGround = isGround;
            X = x;
            Y = y;
        }


        /// <summary> ��� ��ǥ�� Vector2 �������� ��ȯ</summary>
        public Vector2 toVector2()
        {
            return new Vector2(X, Y);
        }


        public Vector2 toWorldPosition(Vector2 mapBottomLeft)
        {
            float nodeSize = AStar.Instance.NodeSize;
            return new Vector2(mapBottomLeft.x + (X + 0.5f) * nodeSize,
                               mapBottomLeft.y + (Y + 0.5f) * nodeSize);
        }

    }
}
