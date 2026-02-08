using UnityEngine;
using System.Collections.Generic;
using Muks.PathFinding.AStar;

public class StaffCleaner : Staff
{
    [Header("Cleaner Components")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _cleanSound;
    [SerializeField] private GameObject _cleanerItem;
    [SerializeField] private GameObject _cleanParticle;


    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        base.SetStaffData(staffData, equipFloorType);
        if (staffData == null)
            return;

        if (!(staffData is CleanerData))
            throw new System.Exception("청소부 스탭에게 청소부 데이터가 들어오지 않았습니다.");


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

