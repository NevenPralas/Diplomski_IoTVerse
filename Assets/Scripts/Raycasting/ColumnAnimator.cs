using UnityEngine;

public class ColumnAnimator : MonoBehaviour
{
    [Header("Rise Animation")]
    [SerializeField] private float riseDuration = 0.8f;

    [Header("Pulse Animation")]
    [SerializeField] private float pulseAmount = 0.04f;
    [SerializeField] private float pulseSpeed = 1.8f;

    [Header("Glow Animation")]
    [SerializeField] private float glowMin = 0.05f;
    [SerializeField] private float glowMax = 0.2f;
    [SerializeField] private float glowSpeed = 1.2f;

    private float targetScaleX;
    private float targetScaleY;
    private float targetScaleZ;

    private float riseTimer = 0f;
    private bool isRising = true;
    private float basePulseY;

    private Renderer columnRenderer;
    private Material columnMaterial;
    private Color baseEmissionColor;

    public void Init(float scaleX, float scaleY, float scaleZ)
    {
        targetScaleX = scaleX;
        targetScaleY = scaleY;
        targetScaleZ = scaleZ;
        basePulseY = scaleY;

        transform.localScale = new Vector3(scaleX, 0f, scaleZ);

        Vector3 pos = transform.position;
        pos.y -= scaleY / 2f;
        transform.position = pos;

        riseTimer = 0f;
        isRising = true;

        // Dohvati renderer i napravi instancu materijala da ne mijenjamo originalni asset
        columnRenderer = GetComponent<Renderer>();
        if (columnRenderer != null)
        {
            columnMaterial = columnRenderer.material;

            // Uključi emission keyword ako već nije
            columnMaterial.EnableKeyword("_EMISSION");

            // Zapamti baznu emission boju iz materijala
            baseEmissionColor = columnMaterial.GetColor("_EmissionColor");

            // Ako emission nije postavljen na materijalu, uzmi base color kao fallback
            if (baseEmissionColor == Color.black)
            {
                Color baseColor = columnMaterial.GetColor("_BaseColor");
                baseEmissionColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            }
        }
    }

    private void Update()
    {
        if (isRising)
        {
            riseTimer += Time.deltaTime;
            float t = Mathf.Clamp01(riseTimer / riseDuration);

            float easedT = EaseOutBack(t);

            float currentY = Mathf.Lerp(0f, targetScaleY, easedT);
            transform.localScale = new Vector3(targetScaleX, currentY, targetScaleZ);

            if (t >= 1f)
            {
                transform.localScale = new Vector3(targetScaleX, targetScaleY, targetScaleZ);
                isRising = false;
            }
        }
        else
        {
            // Pulsiranje scale-a
            float pulse = Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) * pulseAmount;
            float newY = basePulseY + pulse;
            transform.localScale = new Vector3(targetScaleX, newY, targetScaleZ);
        }

        // Animacija sjaja — radi i dok niče i dok pulsira
        AnimateGlow();
    }

    private void AnimateGlow()
    {
        if (columnMaterial == null) return;

        // Sin između glowMin i glowMax — lagano treperi
        float t = (Mathf.Sin(Time.time * glowSpeed * Mathf.PI) + 1f) / 2f;
        float intensity = Mathf.Lerp(glowMin, glowMax, t);

        // HDR boja za emission — množimo baznu boju s intenzitetom
        Color emissionColor = baseEmissionColor * intensity;
        columnMaterial.SetColor("_EmissionColor", emissionColor);
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}