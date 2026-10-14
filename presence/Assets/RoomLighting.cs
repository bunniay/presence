using UnityEngine;
using UnityEngine.InputSystem;   // the NEW Input System

// Press E to make the room darker, one step at a time.
// Other scripts (like SimpleAI) can read Darkness to react to it.
public class RoomLighting : MonoBehaviour
{
    [Header("Lights")]
    [Tooltip("Lights to dim. Leave empty to dim every light in the scene.")]
    public Light[] lights;
    [Tooltip("Also dim the scene's ambient (background) light.")]
    public bool dimAmbientLight = true;

    [Header("Darkness")]
    [Tooltip("How many E presses to go from fully lit to darkest.")]
    [Range(1, 10)] public int steps = 4;
    [Tooltip("Brightness at the darkest step. 0 = pitch black, 1 = no change.")]
    [Range(0f, 1f)] public float darkestBrightness = 0.05f;
    [Tooltip("How fast the lights fade between steps.")]
    public float fadeSpeed = 2f;
    [Tooltip("After the darkest step, pressing E again turns the lights back up.")]
    public bool wrapAround = true;

    // 0 = fully lit, 1 = darkest. Fades smoothly, so other scripts ramp up gradually.
    // "private set" means other scripts can READ it but only this script can change it.
    public float Darkness { get; private set; }

    private int currentStep;              // 0 = fully lit, steps = darkest
    private float[] originalIntensities;  // each light's brightness at the start
    private float originalAmbientIntensity;
    private Color originalAmbientColor;

    void Awake()
    {
        if (lights == null || lights.Length == 0)
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        // Remember starting values so we always dim relative to the original,
        // instead of dimming an already-dimmed value over and over
        originalIntensities = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            originalIntensities[i] = lights[i].intensity;

        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalAmbientColor = RenderSettings.ambientLight;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            Darken();

        // Fade Darkness toward the current step's value
        float targetDarkness = (float)currentStep / steps;
        Darkness = Mathf.MoveTowards(Darkness, targetDarkness, fadeSpeed * Time.deltaTime);

        ApplyLighting();
    }

    // Public so other things (a light switch, a trigger zone) can call it too
    public void Darken()
    {
        if (currentStep < steps)
            currentStep++;
        else if (wrapAround)
            currentStep = 0;
    }

    private void ApplyLighting()
    {
        // Darkness 0 -> full brightness (1), Darkness 1 -> darkestBrightness
        float brightness = Mathf.Lerp(1f, darkestBrightness, Darkness);

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].intensity = originalIntensities[i] * brightness;
        }

        if (dimAmbientLight)
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity * brightness;
            RenderSettings.ambientLight = originalAmbientColor * brightness;
        }
    }

    // Put the ambient light back if this script is turned off or removed
    void OnDisable()
    {
        if (dimAmbientLight)
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.ambientLight = originalAmbientColor;
        }
    }
}
