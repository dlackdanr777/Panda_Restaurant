using UnityEngine;

public class UILoadingScene : MonoBehaviour
{
    [SerializeField] private UILoadingBar _loadingBar;


    public void Init()
    {
        _loadingBar.Init();
        _loadingBar.SetFillAmount(0);
        
    }

    public void SetLoadingBarFillAmount(float amount)
    {
        _loadingBar.SetFillAmount(amount);
    }
}
