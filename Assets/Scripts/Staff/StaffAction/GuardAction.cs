
using Muks.Tween;
using UnityEngine;

public class GuardAction : IStaffAction
{
    private CustomerController _customerController;
    private TableManager _tableManager;
    private GatecrasherCustomer _gatecrasherCustomer;

    private float _actionTime = 1f;
    private float _actionTimer = 0;
    private bool _startAction;
    private Vector3 _tmpScale;

    public GuardAction(CustomerController customerController, TableManager tableManager)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _actionTimer = _actionTime;
        _startAction = false;
    }


    public void Destructor()
    {
    }


    public void PerformAction(Staff staff)
    {
        if (_startAction)
            return;

        float speedMul = staff.SpeedMul;
        _actionTimer -= Time.deltaTime * speedMul;
        if (0 < _actionTimer)
            return;

        _actionTimer = _actionTime;

        // 진상손님 상태 체크 및 갱신
        if(_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
        {
            _gatecrasherCustomer = null;
            _startAction = false;
            
            // 새로운 진상손님 찾기
            if (_customerController.GatecrasherCustomer[(int)staff.EquipFloorType] == null)
                return;

            _gatecrasherCustomer = _customerController.GatecrasherCustomer[(int)staff.EquipFloorType];
            
            // 새로 찾은 진상손님도 유효하지 않으면 리턴
            if (_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
            {
                _gatecrasherCustomer = null;
                return;
            }
        }
        
        // 진상손님이 같은 층에 있는지 확인
        if (_gatecrasherCustomer.VisitFloor != staff.EquipFloorType)
        {
            _gatecrasherCustomer = null;
            _startAction = false;
            return;
        }

        // 진상손님이 위장 상태인지 체크
        if (_gatecrasherCustomer.IsInDisguise)
        {
            _startAction = false;
            return;
        }

        // 액션 시작
        _startAction = true;
        Tween.Wait(1 / speedMul, () =>
        {
            if (!staff.gameObject.activeInHierarchy)
            {
                _startAction = false;
                return;
            }

            // 다시 한번 진상손님 상태 체크
            if (_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
            {
                _gatecrasherCustomer = null;
                _startAction = false;
                return;
            }

            staff.SpriteRenderer.TweenAlpha(0, 0.25f / speedMul).OnComplete(() =>
            {
                if (!staff.gameObject.activeInHierarchy)
                {
                    _startAction = false;
                    staff.SetAlpha(1f);
                    return;
                }

                // 진상손님이 여전히 유효한지 최종 체크
                if (_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
                {
                    // 원래 위치로 복귀
                    staff.transform.position = _tableManager.GetStaffPos(staff.EquipFloorType, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 0.25f / speedMul).OnComplete(() =>
                    {
                        _startAction = false;
                        _gatecrasherCustomer = null;
                    });
                    return;
                }

                staff.transform.position = _gatecrasherCustomer.transform.position + new Vector3(0.2f, 0);
                staff.SetSpriteDir(-1);
                staff.SpriteRenderer.TweenAlpha(1, 0.25f / speedMul).OnComplete(() =>
                {
                    if (_gatecrasherCustomer == null || _gatecrasherCustomer.IsEndEvent)
                    {
                        _startAction = false;
                        staff.SetAlpha(1f);
                        return;
                    }

                    _tmpScale = staff.SpriteRenderer.transform.localScale;
                    EliminatingGatecrasherCustomer2(staff);
                });
            });
        });

        return;
    }


    private void EliminatingGatecrasherCustomer2(Staff staff)
    {
        float speedMul = staff.SpeedMul;
        
        // 진상손님이 사라졌거나 이벤트가 끝났을 때
        if (_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
        {
            _gatecrasherCustomer = null;
            Tween.Wait(1f / speedMul, () =>
            {
                if (!staff.gameObject.activeInHierarchy)
                {
                    staff.SetAlpha(1f);
                    return;
                }

                staff.SpriteRenderer.TweenAlpha(0, 0.3f / speedMul).OnComplete(() =>
                {
                    if (!staff.gameObject.activeInHierarchy)
                    {
                        staff.SetAlpha(1f);
                        return;
                    }
                    
                    staff.transform.position = _tableManager.GetStaffPos(staff.EquipFloorType, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 0.3f / speedMul).OnComplete(() =>
                    {
                        _startAction = false;
                    });
                });
            });
            return;
        }

        // 진상손님 공격 처리
        float duration = (staff.GetActionValue() / staff.SpeedMul) / Mathf.Max(_gatecrasherCustomer.TotalTouchCount, 1);
        Tween.Wait(duration, () =>
        {
            if (!staff.gameObject.activeInHierarchy)
            {
                _startAction = false;
                return;
            }

            // 공격 직전에 다시 한번 진상손님 상태 체크
            if (_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy || _gatecrasherCustomer.IsEndEvent)
            {
                _gatecrasherCustomer = null;
                _startAction = false;
                
                // 원래 위치로 복귀
                staff.SpriteRenderer.TweenAlpha(0, 0.3f / speedMul).OnComplete(() =>
                {
                    if (!staff.gameObject.activeInHierarchy)
                    {
                        staff.SetAlpha(1f);
                        return;
                    }
                    
                    staff.transform.position = _tableManager.GetStaffPos(staff.EquipFloorType, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 0.3f / speedMul);
                });
                return;
            }

            staff.SetSpriteDir(_gatecrasherCustomer.transform.position.x < staff.transform.position.x ? -1 : 1);
            _gatecrasherCustomer.OnTouchEvent();
            staff.transform.position = _gatecrasherCustomer.transform.position + new Vector3(0.2f, 0);
            staff.SpriteRenderer.transform.localScale = _tmpScale;
            ObjectPoolManager.Instance.SpawnSmokeParticle(_gatecrasherCustomer.transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity).Play();
            staff.SpriteRenderer.TweenScale(_tmpScale * 0.99f, duration * 0.5f, Ease.OutBack).OnComplete(() =>
            {
                staff.SpriteRenderer.transform.localScale = _tmpScale * 0.99f;
                staff.SpriteRenderer.TweenScale(_tmpScale, duration * 0.5f, Ease.OutBack);
            });
            
            // 재귀 호출
            EliminatingGatecrasherCustomer2(staff);
        });
    }
}
