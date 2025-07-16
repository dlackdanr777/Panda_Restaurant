using UnityEngine;
using UnityEngine.UI;

public class UITest : MonoBehaviour
{
    [Header("Test MiniGame")]
    [SerializeField] private Button _miniGameButton;
    [SerializeField] private UIMiniGame _miniGame;


    [Space]
    [Header("Satisfaction System")]
    [SerializeField] private Button _satisfactionUpButton;
    [SerializeField] private Button _satisfactionDownButton;

    private void Awake()
    {
        // #if !UNITY_EDITOR
        //             gameObject.SetActive(false);
        //             return;
        // #endif

        FoodMiniGameData data = FoodDataManager.Instance.GetFoodData("FOOD08").FoodMiniGameData;
        _miniGameButton.onClick.AddListener(() => _miniGame.StartMiniGame(data));

        _satisfactionUpButton.onClick.AddListener(() => UserInfo.AddSatisfaction(UserInfo.CurrentStage, 10));
        _satisfactionDownButton.onClick.AddListener(() => UserInfo.AddSatisfaction(UserInfo.CurrentStage, -10));
    }
}
