using UnityEngine;

public class SpaceTimeSliceData : MonoBehaviour
{
    public float Temperature { get; private set; }
    public float RelativeTime { get; private set; }
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int SecondIndex { get; private set; }

    public void Init(float temperature, float relativeTime, int gridX, int gridY, int secondIndex)
    {
        Temperature = temperature;
        RelativeTime = relativeTime;
        GridX = gridX;
        GridY = gridY;
        SecondIndex = secondIndex;
    }
}