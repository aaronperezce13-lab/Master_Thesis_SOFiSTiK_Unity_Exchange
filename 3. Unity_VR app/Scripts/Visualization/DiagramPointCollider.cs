/*
----------------------------------------------------------------------------------------------------------
Script: DiagramPointCollider.cs
Author: Aaron Perez Herrera
Description:    This script manages interaction logic for individual data points in the 3D diagram.
                Handles point selection, highlighting, grouping by distance tolerance, and distance 
                calculations for the VR interface.
AI Assistance:  The overarching state management and geometric logic (distance calculations, group 
                matching) were designed by the author. Claude (Anthropic, 2026) was used as a syntax 
                assistant to correctly bind events to the Unity XR Interactiong Toolikt API 
                (XRSimpleInteractable, SelectEneterEventArgs) and implement the static caching pattern 
                for UI instances.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class DiagramPointCollider : MonoBehaviour
{
    public float value;
    public int loadCase;
    public int memberId;
    public Vector3 worldPoint; // Global Position of the point in the Structure
    public Vector3 diagramPoint; // Global Position of the point in the Diagram
    public string forceValue; // Unit of the force value (e.g., "KN", "KNm")

    // Static list to keep track of all DiagramPointColliders in the scene
    public static List<DiagramPointCollider> globalPoints = new List<DiagramPointCollider>();

    // Static references to UI components for tooltip and distance display
    private static DiagramDistanceUI _distanceUICache = null;
    private static DiagramDistanceUI DistanceUI => _distanceUICache != null ? _distanceUICache : (_distanceUICache = FindFirstObjectByType<DiagramDistanceUI>());
    
    private static DiagramTooltipUI  _tooltipUICache  = null;
    private static DiagramTooltipUI  TooltipUI  => _tooltipUICache  != null ? _tooltipUICache  : (_tooltipUICache  = FindFirstObjectByType<DiagramTooltipUI>());

    // Status of selection
    private static DiagramPointCollider _firstSelected = null;
    private static DiagramPointCollider _secondSelected = null;
    private static List<DiagramPointCollider> _firstGroup = null;
    private static List<DiagramPointCollider> _secondGroup = null;

    // Mesh components for visual feedback
    private MeshRenderer _sphereRenderer1, _sphereRenderer2;
    private Color _originalColor1, _originalColor2;

    //Tolerance for distance comparison
    public static float distanceTolerance = 0.01f;

    [Header("Selection Highlight")]
    public Color selectedColor = Color.red;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;

    // Input keys for selection and clearing selection
    void Awake()
    {
        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    void Start()
    {
        // AI-Assisted Implementation: Binding event listeners directly to the XR Interaction toolkit
        // API to handle selection events was implemented with syntax assistance from Claude.
        // Suscribe to events
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnXRSelectActivated);
        }

        // Find the child sphere and get its MeshRenderer for color changes
        Transform sphere1 = transform.Find("PointSphere1");
        if (sphere1 != null)
        {
            _sphereRenderer1 = sphere1.GetComponent<MeshRenderer>();
            _originalColor1 = _sphereRenderer1.material.color;
        }

        Transform sphere2 = transform.Find("PointSphere2");
        if (sphere2 != null)
        {
            _sphereRenderer2 = sphere2.GetComponent<MeshRenderer>();
            _originalColor2 = _sphereRenderer2.material.color;
        }
    }

    void OnXRSelectActivated(SelectEnterEventArgs args)
    {
        HandleSelect();
    }
    
    // AI-Assisted Implementation: Syntax for the conditionals for each selection and groups to store selection
    // was designed with help of Claude
    // Method to handle point selection logic, managing first and second selections and updating UI accordingly
    public void HandleSelect()
    {
        // RESTART if both already selected
        if (_firstSelected != null && _secondSelected != null)
            ClearSelection();

        // FIRST SELECTION
        if (_firstSelected == null)
        {
            _firstGroup = FindPointsAtPosition(this.worldPoint);

            if (_firstGroup.Count == 0)
            {
                Debug.LogWarning($"No points found at worldPoint {this.worldPoint}");
                return;
            }

            foreach (var p in _firstGroup)
                p.SetColor(true);

            _firstSelected = this;
            DistanceUI?.HideTooltip();
            return;
        }

        // CLICK ON SAME GROUP AS FIRST -> CLEAR
        _secondGroup = FindPointsAtPosition(this.worldPoint);

        if (_secondGroup.Count == 0)
        {
            Debug.LogWarning($"No points found at worldPoint {this.worldPoint}");
            return;
        }

        // If second click is on the same physical position as first -> clear
        bool clickedSamePosition = Vector3.Distance(this.worldPoint, _firstSelected.worldPoint) < distanceTolerance;
        if (clickedSamePosition)
        {
            ClearSelection();
            return;
        }

        // SECOND SELECTION
        // Find matching memberId between both groups
        DiagramPointCollider resolvedFirst  = null;
        DiagramPointCollider resolvedSecond = null;

        foreach (var i in _secondGroup)
                i.SetColor(true);

        foreach (var p1 in _firstGroup)
        {
            foreach (var p2 in _secondGroup)
            {
                if (p1.memberId == p2.memberId)
                {
                    resolvedFirst  = p1;
                    resolvedSecond = p2;
                    break;
                }
            }
            if (resolvedFirst != null) break;
        }

        // No common memberId found between groups
        if (resolvedFirst == null)
        {
            foreach (var p1 in _firstGroup)
                p1.SetColor(false);
            _firstGroup = null;
            
            foreach (var p2 in _secondGroup)
                p2.SetColor(false);
            _secondGroup = null;

            ClearSelection();
            Debug.LogWarning("ERROR: No common member found between the two selected positions.");
            return;
        }

        _firstSelected  = resolvedFirst;
        _secondSelected = resolvedSecond;

        ShowDistance(_firstSelected, _secondSelected);
    }

    // Method to find all DiagramPointColliders at a given world position within a certain tolerance
    private static List<DiagramPointCollider> FindPointsAtPosition(Vector3 targetWorldPoint)
    {
        var result = new List<DiagramPointCollider>();

        foreach (DiagramPointCollider point in DiagramPointCollider.globalPoints)
        {
            if (point != null &&
                Vector3.Distance(point.worldPoint, targetWorldPoint) < distanceTolerance)
            {
                result.Add(point);
            }
        }

        if (result.Count == 0)
            Debug.LogWarning($"No points found at {targetWorldPoint}. " +
                            $"Total registered: {DiagramPointCollider.globalPoints.Count}");

        return result;
    }

    // Method to show distance information between two points
    private static void ShowDistance(DiagramPointCollider a, DiagramPointCollider b)
    {
        float dx = b.worldPoint.x - a.worldPoint.x;
        float dy = b.worldPoint.y - a.worldPoint.y;
        float dz = b.worldPoint.z - a.worldPoint.z;

        Vector3 midpoint = (a.transform.position + b.transform.position) / 2f;

        // Show distance UI with the calculated values
        DistanceUI?.ShowDistance(dx, dy, dz, midpoint, a.worldPoint, b.worldPoint, a.memberId);
    }

    // Method to clear the current selection and reset highlights and UI
    public static void ClearSelection()
    {

        // Clear entire group highlight if it exists
        if (_firstGroup != null)
        {
            foreach (var p in _firstGroup)
                if (p != null) p.SetColor(false);
            _firstGroup = null;
        }

        if (_secondGroup != null)
        {
            foreach (var p in _secondGroup)
                if (p != null) p.SetColor(false);
            _secondGroup = null;
        }

        _firstSelected  = null;
        _secondSelected = null;

        DistanceUI?.HideTooltip();
    }

    // Method to set the highlight state of the point
    void SetColor(bool on)
    {
        if (_sphereRenderer1 != null)
            _sphereRenderer1.material.color = on ? selectedColor : _originalColor1;
        if (_sphereRenderer2 != null)
            _sphereRenderer2.material.color = on ? selectedColor : _originalColor2;
    }

    // Clear selection if this point is destroyed
    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (_interactable != null)
        {
            _interactable.selectEntered.RemoveListener(OnXRSelectActivated);
        }

        if (_firstSelected  == this) _firstSelected  = null;
        if (_secondSelected == this) _secondSelected = null;
    }
}
