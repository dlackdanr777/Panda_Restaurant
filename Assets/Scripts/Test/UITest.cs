using UnityEngine;
using UnityEngine.UI;

public class UITest : MonoBehaviour
{
    [SerializeField] private Button _miniGameButton;
    [SerializeField] private UIMiniGame _miniGame;

    private void Awake()
    {
#if !UNITY_EDITOR
            gameObject.SetActive(false);
            return;
#endif

        FoodMiniGameData data = FoodDataManager.Instance.GetFoodData("FOOD08").FoodMiniGameData;
        _miniGameButton.onClick.AddListener(() => _miniGame.StartMiniGame(data));
    }
}
