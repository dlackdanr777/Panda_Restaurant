using UnityEngine;
using UnityEngine.UI;

public class UIGachaButton : MonoBehaviour
{
    [SerializeField] private Button _gachaButton;
    [SerializeField] private Material _noneMaterial;
    [SerializeField] private Material _grayMaterial;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnChallengeClear();
        UserInfo.OnClearChallengeHandler += OnChallengeClear;
    }
    
    private void OnChallengeClear()
    {
        _gachaButton.image.material = UserInfo.GetIsClearChallenge("MainReward12") ? _noneMaterial : _grayMaterial;
    }

    private void OnDestroy()
    {
        UserInfo.OnClearChallengeHandler -= OnChallengeClear;
    }
}
