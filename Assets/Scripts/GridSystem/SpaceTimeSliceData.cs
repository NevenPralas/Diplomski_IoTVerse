using UnityEngine;

public class SpaceTimeSliceData : MonoBehaviour
{
    // Backward compatible property name: existing scripts can still read Temperature.
    public float Temperature { get; private set; }

    public float Value => Temperature;
    public float RelativeTime { get; private set; }
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int SecondIndex { get; private set; }

    public string ValueTitle { get; private set; } = "Temperature";
    public string ValueUnit { get; private set; } = "°C";
    public int ValueDecimals { get; private set; } = 1;

    public void Init(float temperature, float relativeTime, int gridX, int gridY, int secondIndex)
    {
        Init(temperature, relativeTime, gridX, gridY, secondIndex, "Temperature", "°C", 1);
    }

    public void Init(
        float value,
        float relativeTime,
        int gridX,
        int gridY,
        int secondIndex,
        string valueTitle,
        string valueUnit,
        int valueDecimals)
    {
        Temperature = value;
        RelativeTime = relativeTime;
        GridX = gridX;
        GridY = gridY;
        SecondIndex = secondIndex;

        ValueTitle = string.IsNullOrWhiteSpace(valueTitle) ? "Value" : valueTitle.Trim();
        ValueUnit = valueUnit == null ? string.Empty : valueUnit.Trim();
        ValueDecimals = Mathf.Clamp(valueDecimals, 0, 3);
    }

    public string FormatValue()
    {
        string number = Value.ToString("F" + ValueDecimals);

        if (string.IsNullOrWhiteSpace(ValueUnit))
            return number;

        return number + " " + ValueUnit;
    }

    public string FormatValueWithTitle()
    {
        return ValueTitle + ": " + FormatValue();
    }
}
