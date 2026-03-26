using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RaycastSelector : MonoBehaviour
{
    private NearFarInteractor nearFarInteractor;

    void Awake()
    {
        nearFarInteractor = GetComponent<NearFarInteractor>();
    }

    void OnEnable()
    {
        nearFarInteractor.selectEntered.AddListener(OnSelectEntered);
        nearFarInteractor.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        nearFarInteractor.selectEntered.RemoveListener(OnSelectEntered);
        nearFarInteractor.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        GameObject hit = args.interactableObject.transform.gameObject;
        Debug.Log("Kliknuto: " + hit.name);
        // ovdje dodaj svoju logiku
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        Debug.Log("Pušteno");
    }
}