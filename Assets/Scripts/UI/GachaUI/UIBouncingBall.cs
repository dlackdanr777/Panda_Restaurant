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

    [SerializeField] private bool _noSpeedDamping; // 속도 감속을 제어할 bool 값
    public bool NoSpeedDamping
    {
        get { return _noSpeedDamping; }
        set { _noSpeedDamping = value; }
    }

    [SerializeField] private List<Ball> _ballList; // 여러 개의 공을 저장할 리스트
    [SerializeField] private float _speed = 300f; // 공의 초기 속도
    [SerializeField] private float _gravity = -9.8f; // 중력의 크기 (UI 상에서의 중력)
    [SerializeField] private float _circleBoundaryRadius = 200f; // 원의 반지름
    [SerializeField] private float _bounceDamping = 0.8f; // 반사 시 속도 감소 (에너지 손실)
    [SerializeField] private float _rotationFactor = 0.05f; // 충돌에 따른 회전 속도 가중치
    [SerializeField] private float _rotationDamping = 0.99f; // 회전 감속 비율 (회전 감속)

    private RectTransform _rect;

    public void StartBounce()
    {
        // 공마다 초기 속도와 방향 설정
        foreach (var ball in _ballList)
        {
            // 위쪽 방향(0도에서 180도 범위)에서 랜덤 각도 생성
            float randomAngle = Random.Range(0f, 180f);
            // 각도를 라디안으로 변환하고, 방향 벡터 계산
            float angleRad = randomAngle * Mathf.Deg2Rad;
            ball.Direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)); // 위쪽 180도 범위

            // 초기 속도 설정 (방향에 따라 속도 적용)
            ball.Velocity = ball.Direction * _speed;

            // 회전 속도 초기화
            ball.RotationSpeed = 0;
        }
    }

    // 공의 리셋 함수: 공을 아래쪽에 모아서 위치시킴
    public void ResetBalls()
    {
        float bottomPosition = -_circleBoundaryRadius + 50f; // 공들이 아래쪽에 모여있도록 설정
        float randomXPosition;

        foreach (var ball in _ballList)
        {
            // 공의 초기 위치를 원의 아래쪽에 랜덤하게 배치
            randomXPosition = Random.Range(-_circleBoundaryRadius + ball.Radius, _circleBoundaryRadius - ball.Radius);
            ball.BallRect.anchoredPosition = new Vector2(randomXPosition, bottomPosition);

            // 초기 속도는 0으로 설정 (바운스 시작 전)
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

            // 중력 적용: y축 속도에 중력을 추가
            ball.Velocity += new Vector2(0, _gravity * Time.deltaTime);

            // 공 이동
            ball.BallRect.anchoredPosition += ball.Velocity * Time.deltaTime;

            // 공 회전 (Z축을 기준으로 회전) 및 회전 감속 적용
            ball.BallRect.Rotate(0, 0, ball.RotationSpeed * Time.deltaTime);
            ball.RotationSpeed *= _rotationDamping; // 회전 감속 적용

            if (ball.RotationSpeed < 0.5f && -0.5f < ball.RotationSpeed)
                ball.RotationSpeed = 0;

            // 원의 중심으로부터 공의 현재 위치까지의 거리 계산 (원 경계와 충돌 체크)
            if (Vector2.Distance(Vector2.zero, ball.BallRect.anchoredPosition) >= _circleBoundaryRadius - ball.Radius)
            {
                // 경계에 도달하면 반사
                Vector2 normal = ball.BallRect.anchoredPosition.normalized; // 법선 벡터 계산
                ball.Velocity = Vector2.Reflect(ball.Velocity, normal); // 반사 벡터 계산

                // NoSpeedDamping이 false일 때만 속도 감속을 적용
                if (_noSpeedDamping)
                {
                    // 반사 시 에너지 손실 (속도 감소)
                    ball.Velocity *= _bounceDamping;
                }

                // 경계를 넘지 않도록 공 위치 조정
                ball.BallRect.anchoredPosition = normal * (_circleBoundaryRadius - ball.Radius);
            }

            // 다른 공들과 충돌 체크
            for (int j = i + 1; j < _ballList.Count; j++)
            {
                var otherBall = _ballList[j];

                // 두 공 사이의 거리 계산
                float distance = Vector2.Distance(ball.BallRect.anchoredPosition, otherBall.BallRect.anchoredPosition);

                // 충돌 감지 (두 공의 반지름 합보다 거리가 짧으면 충돌)
                if (distance < ball.Radius + otherBall.Radius)
                {
                    // 충돌 시 두 공이 반사됨 (충돌 법선 벡터 계산)
                    Vector2 collisionNormal = (ball.BallRect.anchoredPosition - otherBall.BallRect.anchoredPosition).normalized;

                    // 각 공의 속도를 반사 벡터로 변경
                    ball.Velocity = Vector2.Reflect(ball.Velocity, collisionNormal);
                    otherBall.Velocity = Vector2.Reflect(otherBall.Velocity, -collisionNormal);

                    // NoSpeedDamping이 false일 때만 속도 감속을 적용
                    if (_noSpeedDamping)
                    {
                        ball.Velocity *= _bounceDamping;
                    }

                    if (_noSpeedDamping)
                    {
                        otherBall.Velocity *= _bounceDamping;
                    }

                    // 두 공의 회전 속도를 충돌 방향에 맞게 설정 (충돌 방향에 따라 회전 방향 랜덤)
                    float relativeVelocity = (ball.Velocity - otherBall.Velocity).magnitude;
                    float rotationDirection = Random.Range(0, 2) == 0 ? -1 : 1; // 랜덤하게 회전 방향 결정
                    ball.RotationSpeed += relativeVelocity * _rotationFactor * rotationDirection;
                    otherBall.RotationSpeed += relativeVelocity * _rotationFactor * -rotationDirection; // 반대 방향 회전

                    // 두 공이 겹치지 않도록 최소 거리로 위치 조정
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

        // 부모 RectTransform의 중심을 기준으로 원 경계 그리기 (circleBoundaryRadius)
        Gizmos.color = Color.yellow;
        Vector3 parentPosition = _rect.position; // 부모 RectTransform의 월드 위치
        Gizmos.DrawWireSphere(parentPosition, _circleBoundaryRadius);

        // 각 공의 반지름 그리기
        Gizmos.color = Color.green;
        foreach (var ball in _ballList)
        {
            if (ball.BallRect != null)
            {
                // 공의 월드 위치 (RectTransform의 로컬 좌표를 월드 좌표로 변환)
                Vector3 ballWorldPosition = ball.BallRect.position;

                // 공의 반지름을 가진 원을 그린다
                Gizmos.DrawWireSphere(ballWorldPosition, ball.Radius);
            }
        }
    }
}

