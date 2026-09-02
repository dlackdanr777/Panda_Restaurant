using UnityEngine;

/// <summary>
/// 가챠 시스템에서 사용할 스탭 데이터 래퍼 클래스
/// StaffData를 GachaData 형태로 래핑하여 가챠 시스템과 호환되도록 함
/// </summary>
public class GachaStaffData : GachaData
{
    private StaffData _staffData;
    public StaffData StaffData => _staffData;

    /// <summary>
    /// 가챠 가중치 (등급 기반 자동 계산)
    /// </summary>
    public float GachaWeight => _staffData.GachaWeight;

    public GachaStaffData(StaffData staffData)
    {
        _staffData = staffData;
        
        // StaffData의 속성을 GachaData에 복사
        _id = staffData.Id;
        _name = staffData.Name;
        _description = staffData.Description;
        _rank = staffData.Rank;
        _sprite = staffData.Sprite;
        _thumbnailSprite = staffData.ThumbnailSprite;
    }
}
