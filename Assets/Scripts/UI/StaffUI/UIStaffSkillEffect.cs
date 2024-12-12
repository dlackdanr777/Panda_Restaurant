using UnityEngine;
using UnityEngine.UI;

public class UIStaffSkillEffect : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UITextAndText _skillEffectGroup1;
    [SerializeField] private UITextAndText _skillEffectGroup2;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;

    public void Init()
    {
        _leftArrowButton.onClick.AddListener(OnArrowButtonClicked);
        _rightArrowButton.onClick.AddListener(OnArrowButtonClicked);
    }

    public void SetData(StaffData data)
    {
        _skillEffectGroup1.gameObject.SetActive(true);
        _skillEffectGroup2.gameObject.SetActive(false);

        if (data == null)
        {
            _skillEffectGroup1.SetText1("???");
            _skillEffectGroup2.SetText1("???");
            _skillEffectGroup2.SetText2("???");
            return;
        }

        _skillEffectGroup1.SetText1(Utility.GetStaffEffectDescription(data));
        _skillEffectGroup2.SetText1(Utility.GetStaffSkillDescription(data));
        _skillEffectGroup2.SetText2(data.Skill == null ? string.Empty : data.Skill.Cooldown + "s");
    }

    private void OnArrowButtonClicked()
    {
        if(_skillEffectGroup1.gameObject.activeSelf)
        {
            _skillEffectGroup1.gameObject.SetActive(false);
            _skillEffectGroup2.gameObject.SetActive(true);
        }
        else
        {
            _skillEffectGroup1.gameObject.SetActive(true);
            _skillEffectGroup2.gameObject.SetActive(false);
        }
    }
}
