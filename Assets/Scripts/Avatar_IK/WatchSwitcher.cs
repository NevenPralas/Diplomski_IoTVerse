using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class WatchSwitcher : MonoBehaviour
{
    public enum SwitchButton
    {
        A_PrimaryButton,
        B_SecondaryButton
    }

    [Header("Input")]
    [Tooltip("Left/sensor watch should use A_PrimaryButton. Right/visualization watch should use B_SecondaryButton.")]
    [SerializeField] private SwitchButton switchButton = SwitchButton.A_PrimaryButton;

    [Header("Input - Keyboard Simulation")]
    [Tooltip("Ako je uključeno, tipkovnica simulira A/B tipke s Meta kontrolera.")]
    [SerializeField] private bool enableKeyboardSimulation = true;

    [Header("Icons / Slots")]
    [Tooltip("Slot 0. On sensor watch this is Temperature. On visualization watch this is Space-time cubes.")]
    public Image temperatureIcon;

    [Tooltip("Slot 1. On sensor watch this is Noise. On visualization watch this is Bubble grid.")]
    public Image megaphoneIcon;

    [Tooltip("Slot 2. On sensor watch this is Humidity. On visualization watch this is Spatio-temporal trail.")]
    public Image humidityIcon;

    [Tooltip("Slot 3. On sensor watch this is CO2. On visualization watch this is Line graph.")]
    public Image CO2Icon;

    [Header("Startup")]
    [SerializeField] private bool setInitialIconOnStart = true;

    [Range(0, 3)]
    [SerializeField] private int startIndex = 0;

    [Header("Colors")]
    [SerializeField] private Color activeColor = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0f, 0f, 1f);

    [Header("Debug")]
    [SerializeField] private bool logSwitches = false;

    private int currentIndex = 0;
    private bool wasButtonPressed = false;

    public int CurrentIndex => currentIndex;
    public SwitchButton Button => switchButton;

    private void Start()
    {
        currentIndex = Mathf.Clamp(startIndex, 0, 3);

        if (setInitialIconOnStart)
            SetActiveIcon(currentIndex);
        else
            currentIndex = DetectCurrentIndexFromGreenIcon();
    }

    private void Update()
    {
        bool isPressed = false;

        // Meta Quest desni kontroler:
        // A = primaryButton
        // B = secondaryButton
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightHand.isValid)
        {
            bool controllerPressed = false;

            if (switchButton == SwitchButton.A_PrimaryButton)
                rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out controllerPressed);
            else
                rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out controllerPressed);

            isPressed = controllerPressed;
        }

        // Tipkovnica simulira isti odabrani gumb:
        // ako je ovaj WatchSwitcher podešen na A, reagira i na tipku A
        // ako je ovaj WatchSwitcher podešen na B, reagira i na tipku B
        if (enableKeyboardSimulation)
        {
            bool keyboardPressed = false;

            if (switchButton == SwitchButton.A_PrimaryButton)
                keyboardPressed = Input.GetKey(KeyCode.A);
            else
                keyboardPressed = Input.GetKey(KeyCode.B);

            isPressed = isPressed || keyboardPressed;
        }

        if (isPressed && !wasButtonPressed)
            SwitchToNextIcon();

        wasButtonPressed = isPressed;
    }

    public void SwitchToNextIcon()
    {
        currentIndex++;

        if (currentIndex > 3)
            currentIndex = 0;

        SetActiveIcon(currentIndex);

        if (logSwitches)
            Debug.Log($"WatchSwitcher {name} switched to index {currentIndex} using {switchButton}");
    }

    public void SetActiveIcon(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, 3);

        SetIconColor(temperatureIcon, inactiveColor);
        SetIconColor(megaphoneIcon, inactiveColor);
        SetIconColor(humidityIcon, inactiveColor);
        SetIconColor(CO2Icon, inactiveColor);

        switch (currentIndex)
        {
            case 0:
                SetIconColor(temperatureIcon, activeColor);
                break;

            case 1:
                SetIconColor(megaphoneIcon, activeColor);
                break;

            case 2:
                SetIconColor(humidityIcon, activeColor);
                break;

            case 3:
                SetIconColor(CO2Icon, activeColor);
                break;
        }
    }

    public bool IsIndexActive(int index)
    {
        return currentIndex == index;
    }

    private void SetIconColor(Image icon, Color color)
    {
        if (icon != null)
            icon.color = color;
    }

    private int DetectCurrentIndexFromGreenIcon()
    {
        if (IsIconGreen(temperatureIcon)) return 0;
        if (IsIconGreen(megaphoneIcon)) return 1;
        if (IsIconGreen(humidityIcon)) return 2;
        if (IsIconGreen(CO2Icon)) return 3;

        return Mathf.Clamp(startIndex, 0, 3);
    }

    private bool IsIconGreen(Image icon)
    {
        if (icon == null)
            return false;

        Color c = icon.color;
        return c == Color.green || (c.g > 0.65f && c.r < 0.45f && c.b < 0.45f);
    }
}