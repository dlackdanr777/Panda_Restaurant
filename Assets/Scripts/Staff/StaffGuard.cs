using UnityEngine;

public class StaffGuard : Staff
{
    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _animator.SetInteger("State", (int)_state);
    }
}

