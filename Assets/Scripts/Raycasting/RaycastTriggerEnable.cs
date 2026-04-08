using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RaycastTriggerEnable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NearFarInteractor nearFarInteractor;

    [Header("Input")]
    [SerializeField] private InputActionReference farCastValueAction;

    [Header("Settings")]
    [SerializeField] private float threshold = 0.1f;
    [SerializeField] private bool logDebugValue = false;

    private void Awake()
    {
        if (nearFarInteractor == null)
            nearFarInteractor = GetComponent<NearFarInteractor>();
    }

    private void OnEnable()
    {
        if (farCastValueAction != null)
            farCastValueAction.action.Enable();
    }

    private void OnDisable()
    {
        if (farCastValueAction != null)
            farCastValueAction.action.Disable();
    }

    private void Update()
    {
        if (nearFarInteractor == null || farCastValueAction == null)
            return;

        float value = farCastValueAction.action.ReadValue<float>();

        if (logDebugValue)
            Debug.Log($"{name} FarCast value: {value}");

        nearFarInteractor.enableFarCasting = value > threshold;
    }
}