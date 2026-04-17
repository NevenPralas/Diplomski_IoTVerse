using UnityEngine;
using UnityEngine.InputSystem;

public class InputActionDebugReader : MonoBehaviour
{
    public InputActionProperty actionToRead;
    public string label = "Action";

    private void OnEnable()
    {
        actionToRead.action?.Enable();
    }

    private void OnDisable()
    {
        actionToRead.action?.Disable();
    }

    private void Update()
    {
        if (actionToRead.action == null)
            return;

        float value = 0f;

        try
        {
            value = actionToRead.action.ReadValue<float>();
        }
        catch
        {
            return;
        }

        if (value > 0.01f)
        {
            Debug.Log($"{label}: {value}");
        }
    }
}