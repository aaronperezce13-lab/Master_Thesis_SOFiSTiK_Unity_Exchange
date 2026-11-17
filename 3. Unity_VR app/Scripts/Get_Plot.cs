/*
----------------------------------------------------------------------------------------------------------
Script: Get_Plot.cs
Author: Aaron Perez Herrera
Description:    This script is the main controller attached to the structural elements in Unity. It
                manages the user interface settings, component filtering, teleportation, and updates the
                state of the DiagramGenerator based on the user selections on VR menu.
AI Assistance:  The current state and user logic of this script were developed by the author. Claude 
                (Anthropic, 2026) was used as a coding assistant to refine specific Unity API syntaxes, 
                notably: manipulating material rendering quesues for point trasparency, transforming local 
                mesh boundaries to word space, and implementing camera-facing text method.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using System.ComponentModel;
using System;

public class Get_Plot : MonoBehaviour
{
    // Variables for external classes
    private XRInteractionManager interactionManager;
    private SemanticDataLoader dataLoader;
    private DiagramGenerator diagGenerator;
    private TeleportController teleportController;

    [Header("UI Settings")]
    public DiagramTooltipUI tooltipUI;
    public GameObject maxtooltipUIPrefab;
    public GameObject mintooltipUIPrefab;

    // Local instances of the max and min tooltips
    private GameObject maxTooltipCopy;
    private GameObject minTooltipCopy;
    private DiagramTooltipUI maxDiagUI;
    private DiagramTooltipUI minDiagUI;
    
    [Header("ICDD Container Settings")]
    // element ID of the element in SOFiSTiK results
    public string icddElementId;
    // Store the current member ID from the ICDD element ID for filtering the data
    private int currentMemberId;
    // Variable where is the dictionary with index positions for each load case
    public ManifestElement manifestElement; 

    [Header("Selectable Points Settings")]
    public float pointCircleRadius = 0.02f;
    public Material diagCircleMaterial;
    public Material elementCircleMaterial;
    public float pointAlpha = 1f;

    [Header("Filter Settings")]
    public int filterMemberId = -1;
    private int _prevFilterMemberId = -2;

    [Header("Csv Settings")]
    public string csvFileName = "Bending_Moment_Y";
    public string forceValue = "KNm";
    public int loadCaseName = 1;

    [Header("Plot Settings")]
    //public bool invertDiagram = false;
    public float momentScale = 1.00f;
    public Material plotMaterial;
    public bool showExtremeValues = false;
    
    [Header("Middle Line Settings")]
    public bool middleLinePlot = false;
    public Material centerLineMaterial;
    public float centerLineWidth = 0.02f;

    [Header("Teleport Settings")]
    public float distanceMultiplier = 1.5f;
    public bool viewXZ = true;
    public bool viewYZ = false;
    public float flightDuration = 2.0f; //seconds
    public float arcHeightFactor = 0.3f; // 0 = straight line, 1 = high arc

    [Header("Input Settings")]
    // Private variables to track state and opt_1imize updates
    private const float DiscontinuityEpsilon = 0.0001f;
    public List<DiagramPointCollider> allSelectablePoints = new List<DiagramPointCollider>(); // List to keep track of all selectable points
    private List<Vector4> Points = new List<Vector4>();
    private MeshRenderer _elementRenderer;
    private GameObject _centerLineObj;
    private GameObject _maxLine;
    private GameObject _minLine;
    private bool _prevMiddleLinePlot;
    private float _prevCenterLineWidth = -1f;
    private float _prevRadius = -1f;
    private string _prevExcelFile;
    private int _prevLoadCaseName;
    private float _prevPointAlpha = -1f;
    private float _prevMomentScale = -1f;
    private Material _prevPlotMaterial = null;
    public List<Vector4> _pointsOriginal = new List<Vector4>();
    public List<int> _pointsOriginalNodes = new List<int>();
    private bool _diagramDirty = true; // Flag to indicate if the diagram needs to be re-plotted due to data changes
    private Coroutine _TeleportCoroutine;  // Coroutine to smoothly teleport the player
    private bool _prevViewXZ;
    private bool _prevViewYZ;
    private bool _prevShowExtremeValues;
    private bool _extremeValuesDirty = false;
    private DiagramPointCollider selectedPoint = null;

    public Vector3 localAxisDirection;

    [System.Obsolete]
    void Start()
    {
        // Search for the XRInteractionManager in the scene to manage interactions
        interactionManager = FindObjectOfType<XRInteractionManager>();
        if(interactionManager == null)
            Debug.LogWarning("No XRInteractionManager found in the scene. XR interactions will not work.");

        // Get the MeshRenderer of the structural element to control its visibility based on filtering and middle line mode
        _elementRenderer = GetComponent<MeshRenderer>();
        _prevMiddleLinePlot = !middleLinePlot;

        // Store the current member ID from the ICDD element ID for filtering the data
        currentMemberId = int.Parse(icddElementId.Trim());

        // Load data and initialize points based on the initial CSV file and load case
        _prevExcelFile    = csvFileName;
        _prevLoadCaseName = loadCaseName;

        // Load points from CSV for the initial setup using SemanticLoader Class
        dataLoader = new SemanticDataLoader();
        dataLoader.LoadPointsFromCSV(csvFileName, icddElementId, loadCaseName, manifestElement, Points, DiscontinuityEpsilon);
        
        // Assign all variables
        Points = dataLoader.ExportPoints;
        localAxisDirection = dataLoader.localAxisDirection;
        _pointsOriginal = dataLoader._pointsOriginal;
        _pointsOriginalNodes = dataLoader._pointsOriginalNodes;
        _diagramDirty = dataLoader._diagramDirty;
        _extremeValuesDirty = dataLoader._extremeValuesDirty;

        // Initialize class Diagram Generation and assign initial values
        diagGenerator = new DiagramGenerator
        {
            interactionManager = interactionManager
        };

        // Assign component to the Couroutine teleport in filtering
        teleportController = GetComponent<TeleportController>();
    }

    [System.Obsolete]
    void Update()
    {
        ApplyExcelFile();
        ApplyLoadCase();
        ApplyPlotSettings();
        ApplyPointAlpha();
        ApplyMemberFilter();
        ApplyMiddleLineMode();
        ApplyRadiusChange();

        // Re-plot the diagram if marked as dirty due to any changes in data or settings
        if (_diagramDirty)
        {
            _diagramDirty = false;

            // Assign variables to Diagram Generator Class to update visualization and update variables inside
            diagGenerator.currentMemberId = currentMemberId;
            diagGenerator.filterMemberId = filterMemberId;
            diagGenerator.loadCaseName = loadCaseName;
            diagGenerator.pointCircleRadius = pointCircleRadius;
            diagGenerator.pointAlpha = pointAlpha;
            diagGenerator.forceValue = forceValue;
            diagGenerator.allSelectablePoints = allSelectablePoints;
            diagGenerator.interactionManager = interactionManager;
            diagGenerator.diagCircleMaterial = diagCircleMaterial;
            diagGenerator.elementCircleMaterial = elementCircleMaterial;
            diagGenerator.parentTransform = this.transform;

            // Update visaulization using DiagramGenerator
            diagGenerator.PlotMomentDiagramArea(Points, localAxisDirection, plotMaterial, momentScale, tooltipUI, selectedPoint);
            
            // Update selectable points to keep track of them - Show Max/Min values
            allSelectablePoints = diagGenerator.allSelectablePoints;
        }

        ApplyShowMaxValue();
    }

    // Method to apply middle line mode, which shows a central line and hides the structural element when enabled
    void ApplyMiddleLineMode()
    {
        bool modeChanged  = middleLinePlot  != _prevMiddleLinePlot;
        bool widthChanged = centerLineWidth != _prevCenterLineWidth;

        if (!modeChanged && !widthChanged) return;

        _prevMiddleLinePlot  = middleLinePlot;
        _prevCenterLineWidth = centerLineWidth;

        // Determine if the current member passes the filter
        bool passesFilter = filterMemberId == -1 || currentMemberId == filterMemberId;

        if (_elementRenderer != null)
            _elementRenderer.enabled = !middleLinePlot && passesFilter;

        if (middleLinePlot && passesFilter)
            ShowCenterLine();
        else
            HideCenterLine();
    }
    
    // Method to apply changes when a new Excel file is selected, which triggers reloading of data and updating of points
    void ApplyExcelFile()
    {
        if (csvFileName == _prevExcelFile) return;
        _prevExcelFile = csvFileName;
        _prevLoadCaseName = loadCaseName;
        
        // NOTE: Change force value depending on the selected CSV file (When adding more calculation forces, this should be updated)
        if (csvFileName == "Bending_Moment_Y" || csvFileName == "Bending_Moment_Z")
            forceValue = "KNm";
        else if(csvFileName == "Normal_Force")
            forceValue = "KN ";
        else
            forceValue = "KN";

        // Reload points from CSV for the initial setup using SemanticLoader Class
        dataLoader = new SemanticDataLoader();
        dataLoader.LoadPointsFromCSV(csvFileName, icddElementId, loadCaseName, manifestElement, Points, DiscontinuityEpsilon);
        // Assign all variables
        Points = dataLoader.ExportPoints;
        localAxisDirection = dataLoader.localAxisDirection;
        _pointsOriginal = dataLoader._pointsOriginal;
        _pointsOriginalNodes = dataLoader._pointsOriginalNodes;
        _diagramDirty = dataLoader._diagramDirty;
        _extremeValuesDirty = dataLoader._extremeValuesDirty;
    }

     // Method to apply changes when the load case is updated
    void ApplyLoadCase()
    {
        if (loadCaseName == _prevLoadCaseName) return;
        _prevLoadCaseName = loadCaseName;

        // Reload points from CSV for the initial setup using SemanticLoader Class
        dataLoader = new SemanticDataLoader();
        dataLoader.LoadPointsFromCSV(csvFileName, icddElementId, loadCaseName, manifestElement, Points, DiscontinuityEpsilon);
        // Assign all variables
        Points = dataLoader.ExportPoints;
        _pointsOriginal = dataLoader._pointsOriginal;
        _pointsOriginalNodes = dataLoader._pointsOriginalNodes;
        _diagramDirty = dataLoader._diagramDirty;
        _extremeValuesDirty = dataLoader._extremeValuesDirty;
    }

    // Method to apply member filtering, which shows or hides the diagram and structural element based on the selected member ID
    [System.Obsolete]
    void ApplyMemberFilter()
    {
        if (filterMemberId == _prevFilterMemberId && viewXZ == _prevViewXZ && viewYZ == _prevViewYZ) return;
        _prevViewXZ = viewXZ;
        _prevViewYZ = viewYZ;
        _prevFilterMemberId = filterMemberId;

        bool shouldShow = filterMemberId == -1 || currentMemberId == filterMemberId;

        // Show/Hide the structural element based on filter and middle line mode
        _elementRenderer.enabled = shouldShow && !middleLinePlot;

        // Show/Hide the diagram container based on filtering
        Transform container = transform.Find("ShearDiagramContainer");
        if (container != null)
            container.gameObject.SetActive(shouldShow);

        // Show/Hide the center line based on filtering and middle line mode
        if (_centerLineObj != null)
            _centerLineObj.SetActive(shouldShow && middleLinePlot);
        
        // Smooth Teleport Player to filtered member
        if (shouldShow && filterMemberId != -1)
        {
            // Find the player object in the scene using its tag
            XROrigin player = FindObjectOfType<XROrigin>();
            if(player == null)
            {
                Debug.LogWarning("Player object with XROrigin component not found in the scene. Teleportation skipped.");
                return;
            }
            
            // Get the center of the member's mesh bounds in world space to teleport the player there
            MeshFilter mf = GetComponent<MeshFilter>();

            // Convert local bounds to world space to get accurate center and size
            Bounds worldBounds = GetWorldBounds(mf);
            Vector3 elementCenter = worldBounds.center;

            // Use the longest dimension to determine how far back the camera should be placed
            float longestSide = Mathf.Max(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);

            // Variable to control position and rotation of the player
            Vector3 targetPosition;
            Quaternion targetRotation;

            float viewDistance = longestSide * distanceMultiplier;

            if (viewXZ == true && viewYZ == false)
            {
                // Front view along Z axis
                targetPosition = new Vector3(elementCenter.x, elementCenter.y, elementCenter.z + viewDistance);
                targetRotation = Quaternion.LookRotation(elementCenter - targetPosition, Vector3.up);
            }

            else if (viewXZ == false && viewYZ == true)
            {
                // Side view along X axis
                targetPosition = new Vector3(elementCenter.x + viewDistance, elementCenter.y, elementCenter.z);
                targetRotation = Quaternion.LookRotation(elementCenter - targetPosition, Vector3.up);
            }

            else return;

            // Start routine to teleport to object filtered
            teleportController.Teleport(player, targetPosition, targetRotation, arcHeightFactor, flightDuration); 
        }
    }

    // AI-Assisted Implementation: The transformation logic to extract mesh bounds from local space
    // space to world space boundaries was refined with Claude in this method.
    private Bounds GetWorldBounds(MeshFilter mf)
    {
        Bounds local = mf.sharedMesh.bounds;
        Vector3[] corners = new Vector3[8];
        corners[0] = local.min;
        corners[1] = local.max;
        corners[2] = new Vector3(local.min.x, local.min.y, local.max.z);
        corners[3] = new Vector3(local.min.x, local.max.y, local.min.z);
        corners[4] = new Vector3(local.max.x, local.min.y, local.min.z);
        corners[5] = new Vector3(local.min.x, local.max.y, local.max.z);
        corners[6] = new Vector3(local.max.x, local.min.y, local.max.z);
        corners[7] = new Vector3(local.max.x, local.max.y, local.min.z);

        // Transform each corner to world space and encapsulate into a world-space Bounds
        Bounds world = new Bounds(transform.TransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            world.Encapsulate(transform.TransformPoint(corners[i]));

        return world;
    }

    void ApplyRadiusChange()
    {
        if (pointCircleRadius == _prevRadius) 
            return;
        
        _prevRadius = pointCircleRadius;

        // Find the container of the diagram points to update them
        Transform container = transform.Find("ShearDiagramContainer");
        if (container == null) return;

        // Iterate through all the points in the diagram
        foreach (Transform child in container)
        {
            foreach (MeshRenderer mr in child.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.gameObject.name != "PointSphere1" && mr.gameObject.name != "PointSphere2")
                    continue;

                mr.transform.localScale = Vector3.one * pointCircleRadius * 2f;
            }

            // Update colliders
            foreach (SphereCollider col in child.GetComponentsInChildren<SphereCollider>())
            {
                col.radius = pointCircleRadius * 1.5f;
            }

            // Update Min and Max Sphere
            Transform maxSphere = transform.Find("MaxSphere");
            if (maxSphere != null)
                maxSphere.localScale = Vector3.one * (pointCircleRadius + 0.01f) * 2f;

            Transform minSphere = transform.Find("MinSphere");
            if (minSphere != null)
                minSphere.localScale = Vector3.one * (pointCircleRadius + 0.01f) * 2f;
    }

    }
    // AI-Assited Implementarion: The Unity rendering properties configuration to dinamically manipulate
    // the alpha transparency channel (ZWrite, SrcBlend) at runtime in this method was guided by Claude.
    void ApplyPointAlpha()
    {
        if (pointAlpha == _prevPointAlpha) 
            return;
        
        _prevPointAlpha = pointAlpha;
        float clamped = Mathf.Clamp01(pointAlpha);

        // Find the container of the diagram points and update the alpha of the point materials
        Transform container = transform.Find("ShearDiagramContainer");
        if (container == null) return;

        // Iterate through all MeshRenderers in the container to find the point spheres
        foreach (MeshRenderer mr in container.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.gameObject.name != "PointSphere1" && mr.gameObject.name != "PointSphere2") continue;

            Material mat = mr.material;

            // Update the alpha of the material color
            Color c = mat.color;
            c.a = clamped;
            mat.color = c;

            // If alpha is less than 1, set the material to be transparent; otherwise, set it to opaque
            if (clamped < 1f)
            {
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetFloat("_Mode", 0); // Opaque
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
            }
        }
    }

    // Method to apply changes to the plot settings, which triggers re-plotting of the diagram if the moment scale or plot material has changed
    void ApplyPlotSettings()
    {
        if (Mathf.Approximately(momentScale, _prevMomentScale) && plotMaterial == _prevPlotMaterial) return;
        _prevMomentScale   = momentScale;
        _prevPlotMaterial  = plotMaterial;
        _diagramDirty = true;
        _extremeValuesDirty  = true;
    }

    void ApplyShowMaxValue()
    {
        // Check if the ShowExtremeValues setting has changed to avoid unnecessary updates
        if (showExtremeValues == _prevShowExtremeValues && !_extremeValuesDirty) 
            return;

        _prevShowExtremeValues = showExtremeValues;
        _extremeValuesDirty    = false;

        // Before showing new extreme value indicators, clean up any existing
        Transform oldMax = transform.Find("MaxSphere");
        if (oldMax != null) DestroyImmediate(oldMax.gameObject);

        Transform oldMin = transform.Find("MinSphere");
        if (oldMin != null) DestroyImmediate(oldMin.gameObject);

        if (_maxLine != null) _maxLine.SetActive(false);
        if (_minLine != null) _minLine.SetActive(false);

        // Delete old tooltips if they exist
        if (maxTooltipCopy != null)
        {
            Destroy(maxTooltipCopy);
            maxTooltipCopy = null;
        }
        if (minTooltipCopy != null)
        {
            Destroy(minTooltipCopy);
            minTooltipCopy = null;
        }

        // If false clean up is enough
        if (!showExtremeValues) return;

        // Check if there are any selectable points to show/hide the extreme value indicators
        if (allSelectablePoints.Count == 0) return;

        // Search max and min values among the selectable points
        float maxValue = allSelectablePoints.Max(p => p.value);
        float minValue = allSelectablePoints.Min(p => p.value);

        // If there are no valid values found (e.g., all points have the same value), log a warning and skip showing extreme value indicators
        if (Mathf.Approximately(maxValue, minValue))
        {
            Debug.Log($"No valid values found among selectable points to determine extreme values in element {currentMemberId}");
            return;
        }

        // Identify the points corresponding to the max and min values to determine where to draw the indicators
        DiagramPointCollider maxPoint = allSelectablePoints.Find(p => p.value == maxValue);
        DiagramPointCollider minPoint = allSelectablePoints.Find(p => p.value == minValue);

        // Check if the maxLine object already exists; if not, create it as a child of the current object
        if (_maxLine == null)
        {
            _maxLine = new GameObject("MaxLine");
            _maxLine.transform.SetParent(this.transform, false);
        }
        
        // create or get the LineRenderer component to draw the lines
        LineRenderer lrmax = _maxLine.GetComponent<LineRenderer>();

        // If the LineRenderer component does not exist, add it to the line objects
        if (lrmax == null)
            lrmax = _maxLine.AddComponent<LineRenderer>();

        Vector3 startMin, endMin, startMax, endMax;

        // Determine the line from element to diagram
        startMax = maxPoint.worldPoint;
        endMax   = maxPoint.diagramPoint;

        // Configure the LineRenderer to draw the max value line
        lrmax.useWorldSpace   = true;
        lrmax.positionCount   = 2;
        lrmax.SetPosition(0, startMax);
        lrmax.SetPosition(1, endMax);
        lrmax.widthMultiplier = 0.06f;
        if (lrmax.material == null || lrmax.material.shader.name != "Universal Render Pipeline/Unlit")
        {
            lrmax.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lrmax.material.color = Color.green;
        }
        _maxLine.SetActive(true);

        // Sphere max value
        GameObject maxSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        maxSphere.name = "MaxSphere";
        maxSphere.transform.SetParent(this.transform, false);
        maxSphere.transform.position = maxPoint.diagramPoint;
        maxSphere.transform.localScale    = Vector3.one * (pointCircleRadius+0.01f) * 2f;
        var renderer = maxSphere.GetComponent<Renderer>();
        if (renderer.material == null || renderer.material.shader.name != "Universal Render Pipeline/Unlit")
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        renderer.material.SetColor("_BaseColor", Color.green);
        
        // 
        if (maxValue != 0f)
        {
            // Show maxValue tooltip
            maxTooltipCopy = Instantiate(maxtooltipUIPrefab);
            maxTooltipCopy.transform.SetParent(maxtooltipUIPrefab.transform.parent, false);
            maxTooltipCopy.SetActive(true);
            maxDiagUI = maxTooltipCopy.GetComponent<DiagramTooltipUI>();
            maxDiagUI.ShowTooltip(maxPoint.value, maxPoint.loadCase, maxPoint.memberId, maxPoint.diagramPoint, maxPoint.forceValue);
        }

        // Configure the LineRenderer to draw the center line
        if (minValue != 0f)
        {
            // // Check if minLine object already exists; if not, create it as a child of the current object
            if (_minLine == null)
            {
                _minLine = new GameObject("MinLine");
                _minLine.transform.SetParent(this.transform, false);
            }

            // create or get the LineRenderer component to draw the lines
            LineRenderer lrmin = _minLine.GetComponent<LineRenderer>();

            // If the LineRenderer component does not exist, add it to the line objects
            if (lrmin == null)
                lrmin = _minLine.AddComponent<LineRenderer>();

            // // Determine the line from element to diagram
            startMin = minPoint.worldPoint;
            endMin   = minPoint.diagramPoint;

            // Configure the LineRenderer to draw the min value line
            lrmin.useWorldSpace   = true;
            lrmin.positionCount   = 2;
            lrmin.SetPosition(0, startMin);
            lrmin.SetPosition(1, endMin);
            lrmin.widthMultiplier = 0.06f;
            if (lrmin.material == null || lrmin.material.shader.name != "Universal Render Pipeline/Unlit")
            {
                lrmin.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lrmin.material.color = Color.blue;
            }
            _minLine.SetActive(true);

            // Sphere min value
            GameObject minSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            minSphere.name = "MinSphere";
            minSphere.transform.SetParent(this.transform, false);
            minSphere.transform.position = minPoint.diagramPoint;
            minSphere.transform.localScale    = Vector3.one * (pointCircleRadius+0.01f) * 2f;
            var renderer2 = minSphere.GetComponent<Renderer>();
            if (renderer2.material == null || renderer2.material.shader.name != "Universal Render Pipeline/Unlit")
            {
                renderer2.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            renderer2.material.SetColor("_BaseColor", Color.red);

            // Show minValue tooltip
            minTooltipCopy = Instantiate(mintooltipUIPrefab) as GameObject;
            minTooltipCopy.transform.SetParent(mintooltipUIPrefab.transform.parent, false);
            minTooltipCopy.SetActive(true);
            minDiagUI = minTooltipCopy.GetComponent<DiagramTooltipUI>();
            minDiagUI.ShowTooltip(minPoint.value, minPoint.loadCase, minPoint.memberId, minPoint.diagramPoint, minPoint.forceValue);
        }
            
        // Display min and max values in the console for each element
        Debug.Log($"Element {currentMemberId} - Max Value: {maxValue} {forceValue} | Min Value: {minValue} {forceValue}");
    }

    // Method to show the center line
    void ShowCenterLine()
    {
        // Check if the center line object already exists; if not, create it as a child of the current object
        if (_centerLineObj == null)
        {
            _centerLineObj = new GameObject("CenterLine");
            _centerLineObj.transform.SetParent(this.transform, false);
        }

        // create or get the LineRenderer component to draw the center line
        LineRenderer lr = _centerLineObj.GetComponent<LineRenderer>();
        
        // If the LineRenderer component does not exist, add it to the center line object
        if (lr == null)
            lr = _centerLineObj.AddComponent<LineRenderer>();

        // Get the bounds of the mesh to determine the start and end points of the center line
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Bounds bounds = mf.sharedMesh.bounds;

        Vector3 start, end;

        // Determine the longest axis of the bounding box to draw the center line along it
        if (bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z)
        {
            start = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
            end   = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
        }
        else if (bounds.size.y >= bounds.size.x && bounds.size.y >= bounds.size.z)
        {
            start = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            end   = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
        else
        {
            start = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            end   = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
        }

        // Configure the LineRenderer to draw the center line
        lr.useWorldSpace   = false;
        lr.positionCount   = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.widthMultiplier = centerLineWidth;
        lr.material = centerLineMaterial != null
            ? centerLineMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        _centerLineObj.SetActive(true);
    }

    // Method to hide the center line
    void HideCenterLine()
    {
        if (_centerLineObj != null)
            _centerLineObj.SetActive(false);
    }

    // AI-Assisted Implementation: The LateUpdate logic to ensure the text consistently faces
    // the main camera was provided by Claude
    public class BillboardText : MonoBehaviour
    {
        private Camera mainCamera;
        void Start() => mainCamera = Camera.main;
        void LateUpdate()
        {
            if (mainCamera != null)
                transform.rotation = mainCamera.transform.rotation;
        }
    }
}