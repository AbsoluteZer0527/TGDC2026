using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class KitchenCameraLook : MonoBehaviour
{
    [SerializeField] Vector2 maxTurnDegrees = new(2f, 1.2f);
    [SerializeField] Vector2 maxShift = new(0.06f, 0.03f);
    Camera view;
    Vector3 restPosition;
    Quaternion restRotation;
    Vector2 look;

    void Awake()
    {
        view = GetComponent<Camera>();
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        Vector2 target = Vector2.zero;
        var mouse = Mouse.current;
        if (Application.isFocused && mouse != null)
        {
            Vector2 pointer = mouse.position.ReadValue();
            if (view.pixelRect.Contains(pointer))
                target = (Vector2)view.ScreenToViewportPoint(pointer) * 2f - Vector2.one;
        }

        look = Vector2.Lerp(look, target, 1f - Mathf.Exp(-7f * Time.deltaTime));
        transform.localRotation = restRotation * Quaternion.Euler(-look.y * maxTurnDegrees.y, look.x * maxTurnDegrees.x, 0f);
        transform.localPosition = restPosition + restRotation * new Vector3(look.x * maxShift.x, look.y * maxShift.y, 0f);
    }

    void OnDisable()
    {
        look = Vector2.zero;
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
    }
}
