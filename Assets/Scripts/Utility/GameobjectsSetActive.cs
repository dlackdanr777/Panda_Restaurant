using UnityEngine;

public class GameobjectsSetActive : MonoBehaviour
{
    [SerializeField] private GameObject[] _gameObjects;

    public void SetActiveAll(bool isActive)
    {
        foreach (var gameObject in _gameObjects)
        {
            if (gameObject == null)
                continue;

            gameObject.SetActive(isActive);
        }
    }
}
