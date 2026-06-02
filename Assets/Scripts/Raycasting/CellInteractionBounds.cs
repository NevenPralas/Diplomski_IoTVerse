using UnityEngine;

public class CellInteractionBounds : MonoBehaviour
{
    public enum CellCheckMode
    {
        TemperatureSpaceTimeCube,
        CO2LineGraph
    }

    [Header("Allowed Room Area")]
    [Tooltip("Dodaj jedan ili više BoxCollidera koji pokrivaju stvarni prostor sobe u kojem smiješ otvarati vizualizacije. Najbolje: jedan trigger BoxCollider preko poda/sobe.")]
    [SerializeField] private Collider[] allowedAreaColliders;

    [Tooltip("Ako nema nijedan collider u listi, sve ćelije se dopuštaju da ti projekt ne pukne dok ne spojiš bounds.")]
    [SerializeField] private bool allowAllWhenNoBoundsAssigned = true;

    [Header("Temperature Cell Rule")]
    [Tooltip("Za temperaturu je dopušteno da je ćelija djelomično u sobi. 0.20 znači da barem oko 20% sample točaka ćelije mora biti unutar Allowed Room Area.")]
    [Range(0f, 1f)]
    [SerializeField] private float minInsideFractionForTemperature = 0.20f;

    [Header("CO2 Cell Rule")]
    [Tooltip("Za CO2 line graph preporuka je strože pravilo jer se canvas lako sakrije iza zida. 1.0 znači da cijela ćelija mora biti unutar Allowed Room Area.")]
    [Range(0f, 1f)]
    [SerializeField] private float minInsideFractionForCO2 = 1.0f;

    [Tooltip("Ako je uključeno, centar CO2 ćelije mora biti unutar sobe.")]
    [SerializeField] private bool requireCO2CellCenterInside = true;

    [Tooltip("Ako je uključeno, za CO2 se provjerava i dodatni margin oko centra da graf ne bude preblizu zidu.")]
    [SerializeField] private bool requireExtraCO2GraphClearance = true;

    [Tooltip("Minimalni horizontalni razmak od zida za CO2 graf. Povećaj ako graf i dalje ulazi u zid.")]
    [SerializeField] private float co2GraphClearanceMeters = 0.35f;

    [Header("Sampling")]
    [Tooltip("0.49 znači skoro rub ćelije. Nemoj staviti 0.5 jer može biti točno na granici collidersa.")]
    [Range(0.1f, 0.49f)]
    [SerializeField] private float sampleExtent = 0.46f;

    [Header("Debug")]
    [SerializeField] private bool debugRejectedCells = false;

    public bool IsTemperatureCellAllowed(ShaderGridHeatmap heatmap, int gridX, int gridY)
    {
        if (heatmap == null)
            return false;

        Vector3 center = heatmap.GetCellCenterWorld(gridX, gridY);
        float cellW = heatmap.GetCellWidth();
        float cellH = heatmap.GetCellHeight();

        bool allowed = IsCellAllowed(center, cellW, cellH, minInsideFractionForTemperature, false, 0f);

        if (!allowed && debugRejectedCells)
            Debug.Log($"Temperature cell rejected by room bounds: ({gridX}, {gridY})");

        return allowed;
    }

    public bool IsCO2CellAllowed(CO2GridLineGraph co2Grid, int gridX, int gridY)
    {
        if (co2Grid == null)
            return false;

        Vector3 center = co2Grid.GetCellCenterWorld(gridX, gridY);
        float cellW = co2Grid.GetCellWidth();
        float cellH = co2Grid.GetCellHeight();

        float clearance = requireExtraCO2GraphClearance ? co2GraphClearanceMeters : 0f;

        bool allowed = IsCellAllowed(
            center,
            cellW,
            cellH,
            minInsideFractionForCO2,
            requireCO2CellCenterInside,
            clearance
        );

        if (!allowed && debugRejectedCells)
            Debug.Log($"CO2 cell rejected by room bounds: ({gridX}, {gridY})");

        return allowed;
    }

    public bool TryGetSafeCO2GraphAnchor(
        CO2GridLineGraph co2Grid,
        int gridX,
        int gridY,
        float graphHorizontalClearance,
        out Vector3 safeAnchor)
    {
        safeAnchor = co2Grid != null
            ? co2Grid.GetCellCenterWorld(gridX, gridY)
            : Vector3.zero;

        if (!HasBounds())
            return false;

        BoxCollider bestBox = FindContainingOrClosestBox(safeAnchor);

        if (bestBox == null)
            return false;

        float clearance = Mathf.Max(co2GraphClearanceMeters, graphHorizontalClearance);

        safeAnchor = ClampPointInsideBoxXZ(bestBox, safeAnchor, clearance);
        return true;
    }

    private bool IsCellAllowed(
        Vector3 center,
        float cellW,
        float cellH,
        float requiredInsideFraction,
        bool requireCenterInside,
        float extraClearance)
    {
        if (!HasBounds())
            return allowAllWhenNoBoundsAssigned;

        if (requireCenterInside && !IsPointInsideAnyAllowedArea(center))
            return false;

        int insideCount = 0;
        int totalCount = 0;

        float dx = cellW * sampleExtent;
        float dz = cellH * sampleExtent;

        Sample(center, ref insideCount, ref totalCount);
        Sample(center + new Vector3(-dx, 0f, -dz), ref insideCount, ref totalCount);
        Sample(center + new Vector3(-dx, 0f, dz), ref insideCount, ref totalCount);
        Sample(center + new Vector3(dx, 0f, -dz), ref insideCount, ref totalCount);
        Sample(center + new Vector3(dx, 0f, dz), ref insideCount, ref totalCount);
        Sample(center + new Vector3(-dx, 0f, 0f), ref insideCount, ref totalCount);
        Sample(center + new Vector3(dx, 0f, 0f), ref insideCount, ref totalCount);
        Sample(center + new Vector3(0f, 0f, -dz), ref insideCount, ref totalCount);
        Sample(center + new Vector3(0f, 0f, dz), ref insideCount, ref totalCount);

        float fraction = totalCount <= 0 ? 0f : insideCount / (float)totalCount;

        if (fraction + 0.0001f < requiredInsideFraction)
            return false;

        if (extraClearance > 0f)
        {
            if (!IsPointInsideAnyAllowedArea(center + new Vector3(extraClearance, 0f, 0f)))
                return false;

            if (!IsPointInsideAnyAllowedArea(center + new Vector3(-extraClearance, 0f, 0f)))
                return false;

            if (!IsPointInsideAnyAllowedArea(center + new Vector3(0f, 0f, extraClearance)))
                return false;

            if (!IsPointInsideAnyAllowedArea(center + new Vector3(0f, 0f, -extraClearance)))
                return false;
        }

        return true;
    }

    private void Sample(Vector3 point, ref int insideCount, ref int totalCount)
    {
        totalCount++;

        if (IsPointInsideAnyAllowedArea(point))
            insideCount++;
    }

    public bool IsPointInsideAnyAllowedArea(Vector3 point)
    {
        if (!HasBounds())
            return allowAllWhenNoBoundsAssigned;

        for (int i = 0; i < allowedAreaColliders.Length; i++)
        {
            Collider col = allowedAreaColliders[i];

            if (col == null)
                continue;

            if (IsPointInsideCollider(col, point))
                return true;
        }

        return false;
    }

    private bool IsPointInsideCollider(Collider col, Vector3 point)
    {
        if (col is BoxCollider box)
            return IsPointInsideBox(box, point);

        Vector3 closest = col.ClosestPoint(point);
        return (closest - point).sqrMagnitude < 0.000001f;
    }

    private bool IsPointInsideBox(BoxCollider box, Vector3 worldPoint)
    {
        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    private bool HasBounds()
    {
        if (allowedAreaColliders == null || allowedAreaColliders.Length == 0)
            return false;

        for (int i = 0; i < allowedAreaColliders.Length; i++)
        {
            if (allowedAreaColliders[i] != null)
                return true;
        }

        return false;
    }

    private BoxCollider FindContainingOrClosestBox(Vector3 point)
    {
        BoxCollider bestBox = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < allowedAreaColliders.Length; i++)
        {
            BoxCollider box = allowedAreaColliders[i] as BoxCollider;

            if (box == null)
                continue;

            if (IsPointInsideBox(box, point))
                return box;

            Vector3 closest = box.ClosestPoint(point);
            float distanceSqr = (closest - point).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestBox = box;
            }
        }

        return bestBox;
    }

    private Vector3 ClampPointInsideBoxXZ(BoxCollider box, Vector3 worldPoint, float margin)
    {
        Vector3 local = box.transform.InverseTransformPoint(worldPoint);
        Vector3 localCenterRelative = local - box.center;
        Vector3 half = box.size * 0.5f;

        float safeX = Mathf.Max(0.01f, half.x - margin);
        float safeZ = Mathf.Max(0.01f, half.z - margin);

        localCenterRelative.x = Mathf.Clamp(localCenterRelative.x, -safeX, safeX);
        localCenterRelative.z = Mathf.Clamp(localCenterRelative.z, -safeZ, safeZ);

        Vector3 clampedLocal = box.center + localCenterRelative;
        return box.transform.TransformPoint(clampedLocal);
    }
}
