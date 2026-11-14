using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ForestDoor : MonoBehaviour
{
    public string sceneToLoad = "Kitchen";
    public Transform player;
    public float interactRadius = 2.5f;
    public GameObject prompt;             // "[E] Back to kitchen" text

    void Update()
    {
        bool dayOver = GameManager.I != null && GameManager.I.DayOver;
        Vector3 d = player ? player.position - transform.position : Vector3.positiveInfinity;
        d.y = 0;
        bool near = !dayOver && d.magnitude < interactRadius;

        if (prompt) prompt.SetActive(near);
        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            SceneManager.LoadScene(sceneToLoad);
    }
}