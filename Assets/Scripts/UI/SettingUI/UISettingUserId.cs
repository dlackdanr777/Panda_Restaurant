using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISettingUserId : MonoBehaviour
{
    [SerializeField] private Button _copyButton;
    [SerializeField] private TextMeshProUGUI _idText;


    public void Init(string userId)
    {
        _idText.text = userId;
        _copyButton.onClick.AddListener(OnCopyButtonClicked);
    }


    private void OnCopyButtonClicked()
    {
        string copyStr = _idText.text;
        if(string.IsNullOrWhiteSpace(copyStr))
        {

            return;
        }

        GUIUtility.systemCopyBuffer = copyStr;
        PopupManager.Instance.ShowDisplayText("UserID 복사가 완료됬습니다.");
    }
}
