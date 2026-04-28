using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class WatchSwitcher : MonoBehaviour
{
    public Image temperatureIcon;
    public Image megaphoneIcon;
    public Image humidityIcon;
    public Image CO2Icon;

    private int currentIndex = 0;
    private bool wasAPressed = false;

    private readonly Color green = new Color(0f, 1f, 0f, 1f);
    private readonly Color red = new Color(1f, 0f, 0f, 1f);

    private void Start()
    {
        SetActiveIcon(0);
    }

    private void Update()
    {
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out bool isAPressed))
        {
            if (isAPressed && !wasAPressed)
            {
                SwitchToNextIcon();
            }

            wasAPressed = isAPressed;
        }
    }

    private void SwitchToNextIcon()
    {
        currentIndex++;

        if (currentIndex > 3)
        {
            currentIndex = 0;
        }

        SetActiveIcon(currentIndex);
    }

    private void SetActiveIcon(int index)
    {
        temperatureIcon.color = red;
        megaphoneIcon.color = red;
        humidityIcon.color = red;
        CO2Icon.color = red;

        switch (index)
        {
            case 0:
                temperatureIcon.color = green;
                break;

            case 1:
                megaphoneIcon.color = green;
                break;

            case 2:
                humidityIcon.color = green;
                break;

            case 3:
                CO2Icon.color = green;
                break;
        }
    }
}