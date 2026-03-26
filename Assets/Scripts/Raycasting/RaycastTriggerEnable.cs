using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RaycastTriggerEnable : MonoBehaviour
{
    private NearFarInteractor nearFarInteractor;
    private InputAction rightTrigger;

    void Awake()
    {
        nearFarInteractor = GetComponent<NearFarInteractor>();

        rightTrigger = new InputAction(
            binding: "<XRController>{RightHand}/trigger"
        );
        rightTrigger.Enable();
    }

    void Update()
    {
        float val = rightTrigger.ReadValue<float>();
        Debug.Log("Trigger: " + val);
        nearFarInteractor.enableFarCasting = val > 0.1f;
    }

    void OnDestroy()
    {
        rightTrigger.Disable();
    }
}