using System.Collections.Generic;
using UnityEngine;

public class UIBouncingBall : MonoBehaviour
{
    [System.Serializable]
    public class Ball
    {
        [SerializeField] private RectTransform _ballRect;
        public RectTransform BallRect => _ballRect;

        [SerializeField] private float _radius;
        public float Radius => _radius;

        [HideInInspector] public float RotationSpeed;
        [HideInInspector] public Vector2 Velocity;
        [HideInInspector] public Vector2 Direction;

    }

    [SerializeField] private bool _noSpeedDamping; // �ӵ� ������ ������ bool ��
    public bool NoSpeedDamping
    {
        get { return _noSpeedDamping; }
        set { _noSpeedDamping = value; }
    }

    [SerializeField] private List<Ball> _ballList; // ���� ���� ���� ������ ����Ʈ
    [SerializeField] private float _speed = 300f; // ���� �ʱ� �ӵ�
    [SerializeField] private float _gravity = -9.8f; // �߷��� ũ�� (UI �󿡼��� �߷�)
    [SerializeField] private float _circleBoundaryRadius = 200f; // ���� ������
    [SerializeField] private float _bounceDamping = 0.8f; // �ݻ� �� �ӵ� ���� (������ �ս�)
    [SerializeField] private float _rotationFactor = 0.05f; // �浹�� ���� ȸ�� �ӵ� ����ġ
    [SerializeField] private float _rotationDamping = 0.99f; // ȸ�� ���� ���� (ȸ�� ����)

    private RectTransform _rect;

    public void StartBounce()
    {
        // ������ �ʱ� �ӵ��� ���� ����
        foreach (var ball in _ballList)
        {
            // ���� ����(0������ 180�� ����)���� ���� ���� ����
            float randomAngle = Random.Range(0f, 180f);
            // ������ �������� ��ȯ�ϰ�, ���� ���� ���
            float angleRad = randomAngle * Mathf.Deg2Rad;
            ball.Direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)); // ���� 180�� ����

            // �ʱ� �ӵ� ���� (���⿡ ���� �ӵ� ����)
            ball.Velocity = ball.Direction * _speed;

            // ȸ�� �ӵ� �ʱ�ȭ
            ball.RotationSpeed = 0;
        }
    }

    // ���� ���� �Լ�: ���� �Ʒ��ʿ� ��Ƽ� ��ġ��Ŵ
    public void ResetBalls()
    {
        float bottomPosition = -_circleBoundaryRadius + 50f; // ������ �Ʒ��ʿ� ���ֵ��� ����
        float randomXPosition;

        foreach (var ball in _ballList)
        {
            // ���� �ʱ� ��ġ�� ���� �Ʒ��ʿ� �����ϰ� ��ġ
            randomXPosition = Random.Range(-_circleBoundaryRadius + ball.Radius, _circleBoundaryRadius - ball.Radius);
            ball.BallRect.anchoredPosition = new Vector2(randomXPosition, bottomPosition);

            // �ʱ� �ӵ��� 0���� ���� (�ٿ ���� ��)
            ball.Velocity = Vector2.zero;

            float randZ = Random.Range(0f, 360f);
            ball.BallRect.rotation = Quaternion.Euler(0, 0, randZ);
        }
    }


    void Awake()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        ResetBalls();
    }

    private void BounceBalls()
    {
        for (int i = 0; i < _ballList.Count; i++)
        {
            var ball = _ballList[i];

            // �߷� ����: y�� �ӵ��� �߷��� �߰�
            ball.Velocity += new Vector2(0, _gravity * Time.deltaTime);

            // �� �̵�
            ball.BallRect.anchoredPosition += ball.Velocity * Time.deltaTime;

            // �� ȸ�� (Z���� �������� ȸ��) �� ȸ�� ���� ����
            ball.BallRect.Rotate(0, 0, ball.RotationSpeed * Time.deltaTime);
            ball.RotationSpeed *= _rotationDamping; // ȸ�� ���� ����

            if (ball.RotationSpeed < 0.5f && -0.5f < ball.RotationSpeed)
                ball.RotationSpeed = 0;

            // ���� �߽����κ��� ���� ���� ��ġ������ �Ÿ� ��� (�� ���� �浹 üũ)
            if (Vector2.Distance(Vector2.zero, ball.BallRect.anchoredPosition) >= _circleBoundaryRadius - ball.Radius)
            {
                // ��迡 �����ϸ� �ݻ�
                Vector2 normal = ball.BallRect.anchoredPosition.normalized; // ���� ���� ���
                ball.Velocity = Vector2.Reflect(ball.Velocity, normal); // �ݻ� ���� ���

                // NoSpeedDamping�� false�� ���� �ӵ� ������ ����
                if (_noSpeedDamping)
                {
                    // �ݻ� �� ������ �ս� (�ӵ� ����)
                    ball.Velocity *= _bounceDamping;
                }

                // ��踦 ���� �ʵ��� �� ��ġ ����
                ball.BallRect.anchoredPosition = normal * (_circleBoundaryRadius - ball.Radius);
            }

            // �ٸ� ����� �浹 üũ
            for (int j = i + 1; j < _ballList.Count; j++)
            {
                var otherBall = _ballList[j];

                // �� �� ������ �Ÿ� ���
                float distance = Vector2.Distance(ball.BallRect.anchoredPosition, otherBall.BallRect.anchoredPosition);

                // �浹 ���� (�� ���� ������ �պ��� �Ÿ��� ª���� �浹)
                if (distance < ball.Radius + otherBall.Radius)
                {
                    // �浹 �� �� ���� �ݻ�� (�浹 ���� ���� ���)
                    Vector2 collisionNormal = (ball.BallRect.anchoredPosition - otherBall.BallRect.anchoredPosition).normalized;

                    // �� ���� �ӵ��� �ݻ� ���ͷ� ����
                    ball.Velocity = Vector2.Reflect(ball.Velocity, collisionNormal);
                    otherBall.Velocity = Vector2.Reflect(otherBall.Velocity, -collisionNormal);

                    // NoSpeedDamping�� false�� ���� �ӵ� ������ ����
                    if (_noSpeedDamping)
                    {
                        ball.Velocity *= _bounceDamping;
                    }

                    if (_noSpeedDamping)
                    {
                        otherBall.Velocity *= _bounceDamping;
                    }

                    // �� ���� ȸ�� �ӵ��� �浹 ���⿡ �°� ���� (�浹 ���⿡ ���� ȸ�� ���� ����)
                    float relativeVelocity = (ball.Velocity - otherBall.Velocity).magnitude;
                    float rotationDirection = Random.Range(0, 2) == 0 ? -1 : 1; // �����ϰ� ȸ�� ���� ����
                    ball.RotationSpeed += relativeVelocity * _rotationFactor * rotationDirection;
                    otherBall.RotationSpeed += relativeVelocity * _rotationFactor * -rotationDirection; // �ݴ� ���� ȸ��

                    // �� ���� ��ġ�� �ʵ��� �ּ� �Ÿ��� ��ġ ����
                    float overlap = ball.Radius + otherBall.Radius - distance;
                    Vector2 correction = collisionNormal * (overlap / 2);
                    ball.BallRect.anchoredPosition += correction;
                    otherBall.BallRect.anchoredPosition -= correction;
                }
            }
        }
    }


    void Update()
    {
        BounceBalls();
    }


    private void OnDrawGizmosSelected()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        // �θ� RectTransform�� �߽��� �������� �� ��� �׸��� (circleBoundaryRadius)
        Gizmos.color = Color.yellow;
        Vector3 parentPosition = _rect.position; // �θ� RectTransform�� ���� ��ġ
        Gizmos.DrawWireSphere(parentPosition, _circleBoundaryRadius);

        // �� ���� ������ �׸���
        Gizmos.color = Color.green;
        foreach (var ball in _ballList)
        {
            if (ball.BallRect != null)
            {
                // ���� ���� ��ġ (RectTransform�� ���� ��ǥ�� ���� ��ǥ�� ��ȯ)
                Vector3 ballWorldPosition = ball.BallRect.position;

                // ���� �������� ���� ���� �׸���
                Gizmos.DrawWireSphere(ballWorldPosition, ball.Radius);
            }
        }
    }
}

