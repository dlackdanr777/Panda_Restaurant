using UnityEngine;
using UnityEngine.UI;

public class IntroSceneCanvas : MonoBehaviour
{
    [SerializeField] private Button _skipButton;

    public void Init(IntroScene introScene)
    {
        _skipButton.onClick.AddListener(() => introScene.SkipIntroVideo());
    }
}
