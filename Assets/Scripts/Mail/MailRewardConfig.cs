using UnityEngine;

/// <summary>
/// 편지함 보상 우편 설정 ScriptableObject.
/// 뒤끝 게임 테이블의 특정 행을 아이템으로 참조합니다.
/// 에디터 우클릭 → Create/Mail/Reward Config 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "MailRewardConfig", menuName = "Mail/Reward Config")]
public class MailRewardConfig : ScriptableObject
{
    [Header("보상 정보 (뒤끝 게임 테이블 참조)")]
    [Tooltip("뒤끝 콘솔의 게임 테이블 이름 (예: Items, Rewards)")]
    public string TableName;

    [Tooltip("테이블 컬럼 이름 (예: item)")]
    public string Column;

    [Tooltip("해당 행의 inDate (뒤끝 콘솔에서 확인)")]
    public string RowInDate;

    [Header("메타 정보 (로그/확인용)")]
    [Tooltip("어느 이벤트에서 발송하는 보상인지 설명")]
    public string Description;
}
