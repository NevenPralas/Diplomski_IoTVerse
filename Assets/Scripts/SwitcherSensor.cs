using UnityEngine;
using UnityEngine.UI;

public class SwitcherSensor : MonoBehaviour
{
    public Image temperatureIcon;
    public Image megaphoneIcon;
    public Image humidityIcon;
    public Image co2Icon;

    public GameObject TemperatureTracker;
    public GameObject NoiseTracker;

    private const string SpaceTimeColumnRootPrefix = "SpaceTimeColumnRoot";

    void Update()
    {
        if (temperatureIcon.color == Color.green)
        {
            Debug.Log("TEMPERATURA");

            if (TemperatureTracker.activeSelf == false)
            {
                TemperatureTracker.SetActive(true);
            }

            if (NoiseTracker.activeSelf == true)
            {
                NoiseTracker.SetActive(false);
            }

            SetSpaceTimeColumnRootsActive(true);
        }
        else if (megaphoneIcon.color == Color.green)
        {
            Debug.Log("ZVUK");

            if (NoiseTracker.activeSelf == false)
            {
                NoiseTracker.SetActive(true);
            }

            if (TemperatureTracker.activeSelf == true)
            {
                TemperatureTracker.SetActive(false);
            }

            SetSpaceTimeColumnRootsActive(false);
        }
        else if (humidityIcon.color == Color.green)
        {
            Debug.Log("VLAGA");

            if (TemperatureTracker.activeSelf == true)
            {
                TemperatureTracker.SetActive(false);
            }

            if (NoiseTracker.activeSelf == true)
            {
                NoiseTracker.SetActive(false);
            }

            SetSpaceTimeColumnRootsActive(false);
        }
        else if (co2Icon.color == Color.green)
        {
            Debug.Log("CO2");

            if (TemperatureTracker.activeSelf == true)
            {
                TemperatureTracker.SetActive(false);
            }

            if (NoiseTracker.activeSelf == true)
            {
                NoiseTracker.SetActive(false);
            }

            SetSpaceTimeColumnRootsActive(false);
        }
    }

    private void SetSpaceTimeColumnRootsActive(bool isActive)
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);

        foreach (Transform t in allTransforms)
        {
            if (t.name.StartsWith(SpaceTimeColumnRootPrefix))
            {
                if (t.gameObject.activeSelf != isActive)
                {
                    t.gameObject.SetActive(isActive);
                }
            }
        }
    }
}