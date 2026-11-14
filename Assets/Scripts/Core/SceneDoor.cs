using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneDoor : MonoBehaviour
{
    public string sceneToLoad = "Forest";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) Go();
    }

    public void Go()
    {
        if (GameManager.I != null && GameManager.I.DayOver) return;
        SceneManager.LoadScene(sceneToLoad);
    }
}