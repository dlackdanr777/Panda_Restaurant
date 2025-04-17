using Muks.Tween;
using System;
using System.Collections.Generic;
using UnityEngine;
using Muks.PathFinding.AStar;

public class StaffWaiter : Staff
{
    [Header("Waiter Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Animator _bowlAnimator;
    [SerializeField] private SpriteRenderer _topBowl;
    [SerializeField] private SpriteRenderer _bottomBowl;
    [SerializeField] private SpriteRenderer _effect;
    [SerializeField] private SpriteRenderer _foodRenderer;


    public override void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        base.Init(type, tableManager, kitchenSystem, customerController, feverSystem);
    }

    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        base.SetStaffData(staffData, equipFloorType);
        if(staffData == null)
            return;
            
        if (!(staffData is WaiterData))
            throw new System.Exception("웨이터 스탭에게 웨이터 데이터가 들어오지 않았습니다.");

        _bowlAnimator.enabled = true;

        int order = (int)_staffType - (int)EquipStaffType.Waiter1;
        _spriteRenderer.sortingOrder = order * 3;
        _foodRenderer.sortingOrder = (order * 3) + 1;
        _bottomBowl.sortingOrder = (order * 3) + 1;
        _topBowl.sortingOrder = (order * 3) + 2;
        _effect.sortingOrder = (order * 3) + 3;
        _topBowl.color = Color.white;
        _bottomBowl.color = Color.white;
        BowlSetActive(false);
    }

    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _animator.SetInteger("State", (int)_state);
    }

    public void BowlSetActive(bool active)
    {
        _bowlAnimator.gameObject.SetActive(active);
        HideFood();
    }

    public void BowlSetAction()
    {
        _bowlAnimator.SetTrigger("Action");
    }

    public void ShowFood(FoodData data)
    {
        _foodRenderer.gameObject.SetActive(true);
        _foodRenderer.sprite = data.Sprite;

        if (data.Sprite == null) return;

        float spriteHeight = data.Sprite.bounds.size.y;

        float scaleY = _foodRenderer.transform.localScale.y;
        float adjustedHeight = spriteHeight * scaleY;

        _foodRenderer.transform.localPosition = new Vector3(
            0f,
            adjustedHeight / 2f,
            0f
        );
    }

    public void HideFood()
    {
        _foodRenderer.gameObject.SetActive(false);
    }

    public override void TweenAlpha(float alpha, float duration, Ease ease, Action onCompleted = null)
    {
        _bottomBowl.TweenAlpha(alpha, duration, ease);
        _topBowl.TweenAlpha(alpha, duration, ease);
        base.TweenAlpha(alpha, duration, ease, onCompleted);
    }

    protected override void StairsMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _isStairsMove = true;

        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList, () =>
        {
            _bowlAnimator.enabled = false;
            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() =>
            {
                AStar.Instance.RequestPath(_tableManager.GetDoorPos(RestaurantType.Hall, _targetPos), _targetPos, TargetMove);
                _bowlAnimator.enabled = true;
            }));
        }
        ));
    }

}

