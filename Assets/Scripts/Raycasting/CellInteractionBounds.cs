using System.Collections.Generic;
using UnityEngine;

public class CellInteractionBounds : MonoBehaviour
{
    [Header("Allowed Room Area")]
    [SerializeField] private List<Collider> allowedAreaColliders = new List<Collider>();
    [SerializeField] private bool allowAllWhenNoBoundsAssigned = true;

    [Header("Temperature Cell Rule")]
    [Range(0f, 1f)]
    [SerializeField] private float minInsideFractionForTemperature = 0.20f;

    [Header("CO2 Cell Rule")]
    [Tooltip("Koliki dio CO2 ćelije mora biti unutar sobe da bi se smjela kliknuti. Za djelomične ćelije stavi 0.15-0.25.")]
    [Range(0f, 1f)]
    [SerializeField] private float minInsideFractionForCO2 = 0.20f;

    [Tooltip("Ako je true, centar CO2 ćelije mora biti unutar sobe. Za dopuštanje djelomičnih ćelija treba biti false.")]
    [SerializeField] private bool requireCO2CellCenterInside = false;

    [Header("CO2 Graph Anchor Adjustment")]
    [Tooltip("Ako je true, CO2 graf za rubne/djelomične ćelije pomiče se na sigurnu vidljivu poziciju unutar sobe.")]
    [SerializeField] private bool adjustCO2GraphAnchor = true;

    [Tooltip("Koliko graf pokušava biti udaljen od zida. Ovo ne odbija ćeliju, nego traži bolji anchor.")]
    [SerializeField] private float co2GraphClearanceMeters = 0.35f;

    [Tooltip("Ako je ćelija skoro potpuno unutar sobe, graf ostaje u centru ćelije.")]
    [Range(0.80f, 1f)]
    [SerializeField] private float fullCellInsideFraction = 0.98f;

    [Header("Sampling")]
    [Tooltip("Koliko gusto se uzorkuje ćelija. 7 je dobar balans.")]
    [Range(3, 15)]
    [SerializeField] private int sampleResolution = 7;

    [Tooltip("0.46 znači da se uzorkuje skoro cijela ćelija, ali ne baš sama granica.")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float sampleExtent = 0.46f;

    [Header("Debug")]
    [SerializeField] private bool debugRejectedCells = false;
    [SerializeField] private bool debugGraphAnchors = false;

    private void OnValidate()
    {
        minInsideFractionForTemperature = Mathf.Clamp01(minInsideFractionForTemperature);
        minInsideFractionForCO2 = Mathf.Clamp01(minInsideFractionForCO2);

        co2GraphClearanceMeters = Mathf.Max(0f, co2GraphClearanceMeters);
        sampleResolution = Mathf.Clamp(sampleResolution, 3, 15);
        sampleExtent = Mathf.Clamp(sampleExtent, 0.1f, 0.5f);
        fullCellInsideFraction = Mathf.Clamp(fullCellInsideFraction, 0.8f, 1f);
    }

    // ============================================================
    // TEMPERATURE API - NOVI NACIN
    // ============================================================

    public bool IsTemperatureCellAllowed(Vector3 cellCenter, float cellWidth, float cellHeight)
    {
        if (!HasAnyValidCollider())
            return allowAllWhenNoBoundsAssigned;

        float insideFraction = GetInsideFractionXZ(cellCenter, cellWidth, cellHeight);
        bool allowed = insideFraction >= minInsideFractionForTemperature;

        if (!allowed && debugRejectedCells)
        {
            Debug.Log(
                $"Temperature cell rejected. insideFraction={insideFraction:F2}, required={minInsideFractionForTemperature:F2}"
            );
        }

        return allowed;
    }

    // ============================================================
    // TEMPERATURE API - STARI NACIN / BACKWARD COMPATIBILITY
    // Ovo rjesava greske u GridCellCursor.cs i SpaceTimeCubeManager.cs
    // ============================================================

    public bool IsTemperatureCellAllowed(ShaderGridHeatmap heatmap, int gridX, int gridY)
    {
        if (heatmap == null)
            return false;

        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);
        float cellWidth = heatmap.GetCellWidth();
        float cellHeight = heatmap.GetCellHeight();

        return IsTemperatureCellAllowed(cellCenter, cellWidth, cellHeight);
    }

    // ============================================================
    // CO2 API - NOVI NACIN
    // ============================================================

    public bool IsCO2CellAllowed(Vector3 cellCenter, float cellWidth, float cellHeight)
    {
        if (!HasAnyValidCollider())
            return allowAllWhenNoBoundsAssigned;

        float insideFraction = GetInsideFractionXZ(cellCenter, cellWidth, cellHeight);

        if (insideFraction < minInsideFractionForCO2)
        {
            if (debugRejectedCells)
            {
                Debug.Log(
                    $"CO2 cell rejected. insideFraction={insideFraction:F2}, required={minInsideFractionForCO2:F2}"
                );
            }

            return false;
        }

        if (requireCO2CellCenterInside && !IsPointInsideAllowedAreaXZ(cellCenter))
        {
            if (debugRejectedCells)
                Debug.Log("CO2 cell rejected because center is outside room bounds.");

            return false;
        }

        return true;
    }

    // ============================================================
    // CO2 API - STARI NACIN / BACKWARD COMPATIBILITY
    // Ovo rjesava gresku u CO2GridCellCursor.cs
    // ============================================================

    public bool IsCO2CellAllowed(CO2GridLineGraph co2Grid, int gridX, int gridY)
    {
        if (co2Grid == null)
            return false;

        Vector3 cellCenter = co2Grid.GetCellCenterWorld(gridX, gridY);
        float cellWidth = co2Grid.GetCellWidth();
        float cellHeight = co2Grid.GetCellHeight();

        return IsCO2CellAllowed(cellCenter, cellWidth, cellHeight);
    }

    // ============================================================
    // CO2 GRAPH ANCHOR - NOVI NACIN
    // ============================================================

    public bool TryGetCO2GraphAnchor(
        Vector3 cellCenter,
        float cellWidth,
        float cellHeight,
        out Vector3 graphAnchor)
    {
        graphAnchor = cellCenter;

        if (!HasAnyValidCollider())
            return allowAllWhenNoBoundsAssigned;

        if (!IsCO2CellAllowed(cellCenter, cellWidth, cellHeight))
            return false;

        if (!adjustCO2GraphAnchor)
        {
            graphAnchor = cellCenter;
            return true;
        }

        float insideFraction = GetInsideFractionXZ(cellCenter, cellWidth, cellHeight);
        bool centerInside = IsPointInsideAllowedAreaXZ(cellCenter);
        bool centerHasClearance = GetBestClearanceXZ(cellCenter) >= co2GraphClearanceMeters;

        bool canUseOriginalCenter =
            insideFraction >= fullCellInsideFraction &&
            centerInside &&
            centerHasClearance;

        if (canUseOriginalCenter)
        {
            graphAnchor = cellCenter;
            return true;
        }

        if (TryFindBestInteriorAnchor(cellCenter, cellWidth, cellHeight, out Vector3 adjustedAnchor))
        {
            graphAnchor = adjustedAnchor;
            graphAnchor.y = cellCenter.y;

            if (debugGraphAnchors)
            {
                Debug.DrawLine(
                    cellCenter + Vector3.up * 0.05f,
                    graphAnchor + Vector3.up * 0.05f,
                    Color.yellow,
                    1.5f
                );

                Debug.Log($"CO2 graph anchor adjusted from {cellCenter} to {graphAnchor}");
            }

            return true;
        }

        graphAnchor = cellCenter;
        return true;
    }

    // ============================================================
    // CO2 GRAPH ANCHOR - STARI NACIN / BACKWARD COMPATIBILITY
    // Ako negdje imas poziv s co2Grid, gridX, gridY, i to ce raditi.
    // ============================================================

    public bool TryGetCO2GraphAnchor(
        CO2GridLineGraph co2Grid,
        int gridX,
        int gridY,
        out Vector3 graphAnchor)
    {
        graphAnchor = Vector3.zero;

        if (co2Grid == null)
            return false;

        Vector3 cellCenter = co2Grid.GetCellCenterWorld(gridX, gridY);
        float cellWidth = co2Grid.GetCellWidth();
        float cellHeight = co2Grid.GetCellHeight();

        return TryGetCO2GraphAnchor(cellCenter, cellWidth, cellHeight, out graphAnchor);
    }

    // ============================================================
    // INTERNAL SAMPLING
    // ============================================================

    private bool TryFindBestInteriorAnchor(
        Vector3 cellCenter,
        float cellWidth,
        float cellHeight,
        out Vector3 bestPoint)
    {
        bestPoint = cellCenter;

        bool found = false;
        float bestScore = float.NegativeInfinity;

        int resolution = Mathf.Max(3, sampleResolution);

        for (int ix = 0; ix < resolution; ix++)
        {
            float tx = resolution == 1 ? 0.5f : ix / (float)(resolution - 1);
            float offsetX = Mathf.Lerp(-cellWidth * sampleExtent, cellWidth * sampleExtent, tx);

            for (int iz = 0; iz < resolution; iz++)
            {
                float tz = resolution == 1 ? 0.5f : iz / (float)(resolution - 1);
                float offsetZ = Mathf.Lerp(-cellHeight * sampleExtent, cellHeight * sampleExtent, tz);

                Vector3 p = new Vector3(
                    cellCenter.x + offsetX,
                    cellCenter.y,
                    cellCenter.z + offsetZ
                );

                if (!IsPointInsideAllowedAreaXZ(p))
                    continue;

                float clearance = GetBestClearanceXZ(p);

                float distancePenalty = Vector3.Distance(
                    new Vector3(cellCenter.x, 0f, cellCenter.z),
                    new Vector3(p.x, 0f, p.z)
                ) * 0.15f;

                float score = clearance - distancePenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = p;
                    found = true;
                }
            }
        }

        return found;
    }

    private float GetInsideFractionXZ(Vector3 cellCenter, float cellWidth, float cellHeight)
    {
        if (!HasAnyValidCollider())
            return allowAllWhenNoBoundsAssigned ? 1f : 0f;

        int insideCount = 0;
        int totalCount = 0;

        int resolution = Mathf.Max(3, sampleResolution);

        for (int ix = 0; ix < resolution; ix++)
        {
            float tx = resolution == 1 ? 0.5f : ix / (float)(resolution - 1);
            float offsetX = Mathf.Lerp(-cellWidth * sampleExtent, cellWidth * sampleExtent, tx);

            for (int iz = 0; iz < resolution; iz++)
            {
                float tz = resolution == 1 ? 0.5f : iz / (float)(resolution - 1);
                float offsetZ = Mathf.Lerp(-cellHeight * sampleExtent, cellHeight * sampleExtent, tz);

                Vector3 p = new Vector3(
                    cellCenter.x + offsetX,
                    cellCenter.y,
                    cellCenter.z + offsetZ
                );

                totalCount++;

                if (IsPointInsideAllowedAreaXZ(p))
                    insideCount++;
            }
        }

        if (totalCount == 0)
            return 0f;

        return insideCount / (float)totalCount;
    }

    private bool IsPointInsideAllowedAreaXZ(Vector3 point)
    {
        for (int i = 0; i < allowedAreaColliders.Count; i++)
        {
            Collider c = allowedAreaColliders[i];

            if (c == null)
                continue;

            if (IsPointInsideColliderXZ(c, point))
                return true;
        }

        return false;
    }

    private bool IsPointInsideColliderXZ(Collider collider, Vector3 worldPoint)
    {
        BoxCollider box = collider as BoxCollider;

        if (box != null)
        {
            Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;

            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.z) <= half.z;
        }

        Bounds b = collider.bounds;

        return worldPoint.x >= b.min.x &&
               worldPoint.x <= b.max.x &&
               worldPoint.z >= b.min.z &&
               worldPoint.z <= b.max.z;
    }

    private float GetBestClearanceXZ(Vector3 point)
    {
        float best = 0f;

        for (int i = 0; i < allowedAreaColliders.Count; i++)
        {
            Collider c = allowedAreaColliders[i];

            if (c == null)
                continue;

            if (!IsPointInsideColliderXZ(c, point))
                continue;

            float clearance = GetClearanceInsideColliderXZ(c, point);

            if (clearance > best)
                best = clearance;
        }

        return best;
    }

    private float GetClearanceInsideColliderXZ(Collider collider, Vector3 worldPoint)
    {
        BoxCollider box = collider as BoxCollider;

        if (box != null)
        {
            Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;

            float localClearanceX = half.x - Mathf.Abs(local.x);
            float localClearanceZ = half.z - Mathf.Abs(local.z);

            float scaleX = Mathf.Abs(box.transform.lossyScale.x);
            float scaleZ = Mathf.Abs(box.transform.lossyScale.z);

            float worldClearanceX = localClearanceX * scaleX;
            float worldClearanceZ = localClearanceZ * scaleZ;

            return Mathf.Max(0f, Mathf.Min(worldClearanceX, worldClearanceZ));
        }

        Bounds b = collider.bounds;

        float clearanceX = Mathf.Min(
            Mathf.Abs(worldPoint.x - b.min.x),
            Mathf.Abs(b.max.x - worldPoint.x)
        );

        float clearanceZ = Mathf.Min(
            Mathf.Abs(worldPoint.z - b.min.z),
            Mathf.Abs(b.max.z - worldPoint.z)
        );

        return Mathf.Max(0f, Mathf.Min(clearanceX, clearanceZ));
    }

    private bool HasAnyValidCollider()
    {
        for (int i = 0; i < allowedAreaColliders.Count; i++)
        {
            if (allowedAreaColliders[i] != null)
                return true;
        }

        return false;
    }
}