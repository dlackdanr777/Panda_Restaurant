using UnityEngine;

public class StaffCleaner : Staff
{
    [Header("Cleaner Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _cleanSound;
    [SerializeField] private GameObject _cleanerItem;
    [SerializeField] private GameObject _cleanParticle;


    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (!(staffData is CleanerData))
            throw new System.Exception("û�Һ� ���ǿ��� û�Һ� �����Ͱ� ������ �ʾҽ��ϴ�.");

        base.SetStaffData(staffData, equipFloorType, tableManager, kitchenSystem, customerController);
        _cleanerItem.gameObject.SetActive(true);
        _cleanParticle.gameObject.SetActive(false);
    }


    public void PlayCleanSound()
    {
        _audio.PlayOneShot(_cleanSound);
    }

    public void StopSound()
    {
        if (!_audio.isPlaying)
            return;

        _audio.Stop();
    }

    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _animator.SetInteger("State", (int)_state);
    }

}

