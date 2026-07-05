using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class TutorialSlideController : MonoBehaviour
{
    [Serializable]
    public class TutorialSlide
    {
        [Header("Images")]
        public Sprite imageTop;
        public Sprite imageLeft;
        public Sprite imageRight;

        [Header("Text")]
        [TextArea(3, 10)]
        public string descriptionText;
    }

    [Header("UI Image References")]
    [SerializeField] private Image topImage;
    [SerializeField] private Image leftImage;
    [SerializeField] private Image rightImage;

    [Header("UI Text References")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text slideNumberText;

    [Header("Slides")]
    [SerializeField] private List<TutorialSlide> slides = new List<TutorialSlide>();

    [Header("Console Screens")]
    [Tooltip("Primarni console screen. Ovo je console_screen (2). Aktivan je za slajdove 0-14.")]
    [SerializeField] private GameObject primaryConsoleScreen;

    [Tooltip("Drugi console screen. Ovo je console_screen (5). Aktivan je od slajda 15 nadalje.")]
    [SerializeField] private GameObject secondaryConsoleScreen;

    [Tooltip("Od kojeg indeksa slajda se prebacuje na drugi console screen. Za 16. slajd ovo treba biti 15.")]
    [SerializeField] private int secondaryConsoleStartIndex = 15;

    [Header("Startup")]
    [SerializeField] private int startSlideIndex = 0;
    [SerializeField] private bool showFirstSlideOnStart = true;

    [Header("Input - Keyboard")]
    [SerializeField] private bool enableKeyboardInput = true;

    [Tooltip("Tipka za prethodni slajd. Sada je X.")]
    [SerializeField] private KeyCode previousSlideKeyboardKey = KeyCode.X;

    [Tooltip("Tipka za sljedeći slajd. Sada je Y.")]
    [SerializeField] private KeyCode nextSlideKeyboardKey = KeyCode.Y;

    [Header("Input - Meta / XR Left Controller")]
    [SerializeField] private bool enableLeftControllerInput = true;

    [Tooltip("Na lijevom Meta kontroleru Primary Button je najčešće X. Sada ide na prethodni slajd.")]
    [SerializeField] private bool usePrimaryButtonForPrevious = true;

    [Tooltip("Na lijevom Meta kontroleru Secondary Button je najčešće Y. Sada ide na sljedeći slajd.")]
    [SerializeField] private bool useSecondaryButtonForNext = true;

    [Header("Behaviour")]
    [SerializeField] private bool loopSlides = false;

    [Header("Debug")]
    [SerializeField] private bool logSlideChanges = false;

    private int currentSlideIndex = 0;

    private InputDevice leftController;
    private bool lastPrimaryButtonState = false;
    private bool lastSecondaryButtonState = false;

    private void Start()
    {
        currentSlideIndex = Mathf.Clamp(startSlideIndex, 0, Mathf.Max(0, slides.Count - 1));

        TryFindLeftController();

        if (showFirstSlideOnStart)
            ShowSlide(currentSlideIndex);
        else
            UpdateConsoleScreensForSlide(currentSlideIndex);
    }

    private void Update()
    {
        HandleKeyboardInput();
        HandleLeftControllerInput();
    }

    private void HandleKeyboardInput()
    {
        if (!enableKeyboardInput)
            return;

        // X na tipkovnici = prethodni slajd
        if (Input.GetKeyDown(previousSlideKeyboardKey))
            PreviousSlide();

        // Y na tipkovnici = sljedeći slajd
        if (Input.GetKeyDown(nextSlideKeyboardKey))
            NextSlide();
    }

    private void HandleLeftControllerInput()
    {
        if (!enableLeftControllerInput)
            return;

        if (!leftController.isValid)
            TryFindLeftController();

        if (!leftController.isValid)
            return;

        // Primary Button na lijevom kontroleru = X = prethodni slajd
        if (usePrimaryButtonForPrevious)
        {
            bool primaryPressed = false;

            if (leftController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed))
            {
                if (primaryPressed && !lastPrimaryButtonState)
                    PreviousSlide();

                lastPrimaryButtonState = primaryPressed;
            }
        }

        // Secondary Button na lijevom kontroleru = Y = sljedeći slajd
        if (useSecondaryButtonForNext)
        {
            bool secondaryPressed = false;

            if (leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed))
            {
                if (secondaryPressed && !lastSecondaryButtonState)
                    NextSlide();

                lastSecondaryButtonState = secondaryPressed;
            }
        }
    }

    private void TryFindLeftController()
    {
        List<InputDevice> devices = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left |
            InputDeviceCharacteristics.Controller,
            devices
        );

        if (devices.Count > 0)
            leftController = devices[0];
    }

    public void NextSlide()
    {
        if (slides == null || slides.Count == 0)
            return;

        int nextIndex = currentSlideIndex + 1;

        if (nextIndex >= slides.Count)
        {
            if (loopSlides)
                nextIndex = 0;
            else
                nextIndex = slides.Count - 1;
        }

        ShowSlide(nextIndex);
    }

    public void PreviousSlide()
    {
        if (slides == null || slides.Count == 0)
            return;

        int previousIndex = currentSlideIndex - 1;

        if (previousIndex < 0)
        {
            if (loopSlides)
                previousIndex = slides.Count - 1;
            else
                previousIndex = 0;
        }

        ShowSlide(previousIndex);
    }

    public void ShowSlide(int index)
    {
        if (slides == null || slides.Count == 0)
        {
            ClearUI();
            return;
        }

        index = Mathf.Clamp(index, 0, slides.Count - 1);
        currentSlideIndex = index;

        TutorialSlide slide = slides[currentSlideIndex];

        ApplyImage(topImage, slide.imageTop);
        ApplyImage(leftImage, slide.imageLeft);
        ApplyImage(rightImage, slide.imageRight);

        if (descriptionText != null)
            descriptionText.text = slide.descriptionText;

        if (slideNumberText != null)
            slideNumberText.text = $"{currentSlideIndex + 1}/{slides.Count}";

        UpdateConsoleScreensForSlide(currentSlideIndex);

        if (logSlideChanges)
            Debug.Log($"Tutorial slide changed: {currentSlideIndex + 1}/{slides.Count}");
    }

    private void UpdateConsoleScreensForSlide(int slideIndex)
    {
        bool useSecondaryConsole = slideIndex >= secondaryConsoleStartIndex;

        if (primaryConsoleScreen != null)
            primaryConsoleScreen.SetActive(!useSecondaryConsole);

        if (secondaryConsoleScreen != null)
            secondaryConsoleScreen.SetActive(useSecondaryConsole);
    }

    private void ApplyImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = sprite;

        // Ako za neki slajd ne želiš sliku na tom canvasu, ostavi Sprite prazan.
        targetImage.enabled = sprite != null;
    }

    private void ClearUI()
    {
        ApplyImage(topImage, null);
        ApplyImage(leftImage, null);
        ApplyImage(rightImage, null);

        if (descriptionText != null)
            descriptionText.text = "";

        if (slideNumberText != null)
            slideNumberText.text = "0/0";

        UpdateConsoleScreensForSlide(0);
    }
}