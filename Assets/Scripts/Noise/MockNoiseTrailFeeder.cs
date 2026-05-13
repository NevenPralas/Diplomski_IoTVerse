using UnityEngine;

public class MockNoiseTrailFeeder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpatioTemporalNoiseTrail noiseTrail;

    [Header("Mock Movement")]
    [SerializeField] private Transform robotTransform;
    [SerializeField] private bool moveRobot = true;
    [SerializeField] private float movementRadiusX = 2.5f;
    [SerializeField] private float movementRadiusZ = 1.2f;
    [SerializeField] private float movementSpeed = 0.35f;
    [SerializeField] private float robotHeight = 0.1f;

    [Header("Mock Noise")]
    [SerializeField] private float minNoiseDb = 35f;
    [SerializeField] private float maxNoiseDb = 82f;
    [SerializeField] private float noiseSpeed = 0.8f;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 0.35f;

    private float nextSampleTime;
    private Vector3 startPosition;

    private void Start()
    {
        if (robotTransform != null)
        {
            startPosition = robotTransform.position;
        }
        else
        {
            startPosition = transform.position;
        }
    }

    private void Update()
    {
        Vector3 currentPosition = GetCurrentPosition();

        if (moveRobot && robotTransform != null)
        {
            robotTransform.position = currentPosition;
        }

        if (Time.time >= nextSampleTime)
        {
            nextSampleTime = Time.time + sampleInterval;

            float noiseDb = GenerateNoiseValue();
            noiseTrail.AddSample(currentPosition, noiseDb);
        }
    }

    private Vector3 GetCurrentPosition()
    {
        float t = Time.time * movementSpeed;

        float x = Mathf.Sin(t) * movementRadiusX;
        float z = Mathf.Sin(t * 1.7f) * movementRadiusZ;

        Vector3 pos = startPosition + new Vector3(x, 0f, z);
        pos.y = robotHeight;

        return pos;
    }

    private float GenerateNoiseValue()
    {
        float waveA = Mathf.Sin(Time.time * noiseSpeed);
        float waveB = Mathf.Sin(Time.time * noiseSpeed * 2.35f + 1.4f);

        float normalized = Mathf.InverseLerp(-2f, 2f, waveA + waveB);
        return Mathf.Lerp(minNoiseDb, maxNoiseDb, normalized);
    }
}