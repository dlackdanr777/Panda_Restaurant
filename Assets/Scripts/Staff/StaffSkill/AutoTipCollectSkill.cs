using UnityEngine;

[CreateAssetMenu(fileName = "AutoTipCollectSkill", menuName = "Scriptable Object/Skill/AutoTipCollectSkill")]
public class AutoTipCollectSkill : SkillBase
{

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (GameManager.Instance.MaxTipValue <= GameManager.Instance.Tip)
        {
            GameManager.Instance.AppendTip(-GameManager.Instance.Tip);
        }
    }
}
