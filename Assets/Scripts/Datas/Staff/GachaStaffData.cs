using UnityEngine;

/// <summary>
/// 가챠 시스템에서 사용할 스탭 데이터 래퍼 클래스
/// StaffData를 GachaData 형태로 래핑하여 가챠 시스템과 호환되도록 함
/// </summary>
public class GachaStaffData : GachaData
{
    private StaffData _staffData;
    public StaffData StaffData => _staffData;

    public static GachaStaffData Create(StaffData staffData)
    {
        GachaStaffData data = CreateInstance<GachaStaffData>();
        data.Initialize(staffData);
        return data;
    }

    private void Initialize(StaffData staffData)
    {
        _staffData = staffData;

        if (staffData == null)
        {
            DebugLog.LogError("가챠 직원 래퍼에 빈 직원 데이터가 전달되었습니다.");
            _name = string.Empty;
            _id = string.Empty;
            _description = string.Empty;
            _rank = Rank.Normal1;
            return;
        }
        
        // StaffData의 속성을 GachaData에 복사
        _id = staffData.Id;
        _name = string.IsNullOrWhiteSpace(staffData.Name) ? staffData.Id : staffData.Name;
        _description = staffData.Description ?? string.Empty;
        _rank = staffData.Rank;
        _sprite = staffData.Sprite == null ? staffData.ThumbnailSprite : staffData.Sprite;
        _thumbnailSprite = staffData.ThumbnailSprite == null ? staffData.Sprite : staffData.ThumbnailSprite;
    }
}
