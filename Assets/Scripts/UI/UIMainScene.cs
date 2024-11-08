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
        if (UserInfo.IsTutorialStart)
            return;

        if (!Input.anyKey)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            _coordinator.Pop();
    }
}
