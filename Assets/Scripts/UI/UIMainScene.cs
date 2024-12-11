using Muks.UI;
using UnityEngine;

[RequireComponent(typeof(UINavigationCoordinator))]
public class UIMainScene : MonoBehaviour
{
    private UINavigationCoordinator _coordinator;

    private void Awake()
    {
        _coordinator = GetComponent<UINavigationCoordinator>();
    }

    private void Update()
    {
        if (!Input.anyKey)
            return;

        if (UserInfo.IsTutorialStart)
            return;

        if (PopupManager.IsShowPopup)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            _coordinator.Pop();
    }
}
