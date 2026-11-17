/*
----------------------------------------------------------------------------------------------------------
Script: DiagramGenerator.cs
Author: Aaron Perez Herrera
Description:    This script generats dynamic 3D structural diagrams (moment, shear, normal) by projecting
                force values along local axis of the IFC geometry. Handles discontinuity segmentation and
                instantiates interactable XR data points.
AI Assistance:  The core calculation of the transversal planes from global coordinates (Universal FEM 
                cross-product algorithm) was developed with the assistance of Google Gemini (2026).
                Claue (Anthropic, 2026) provided syntax support for generating the mesh triangulation in 
                "PlotSegment" and dynamically applying XR interactables, materials, and custom colliders
                to the instantiated data spheres.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit;

public class DiagramGenerator : MonoBehaviour
{
    public int currentMemberId;
    public int filterMemberId;
    public int loadCaseName;
    public float pointCircleRadius;
    public float pointAlpha;
    public string forceValue;

    public Transform parentTransform;
    public List<DiagramPointCollider> allSelectablePoints = new List<DiagramPointCollider>();
    public XRInteractionManager interactionManager;
    public Material diagCircleMaterial;
    public Material elementCircleMaterial;
    
    // Method to plot the moment diagram area based on the provided value points, material, and scaling factor
    public void PlotMomentDiagramArea(List<Vector4> valuePoints, Vector3 localAxisDir,Material areaMaterial, float momentScale, DiagramTooltipUI tooltipUI, DiagramPointCollider selectedPoint)
    {
        // Determine if the current member passes the filter
        bool passesFilter = filterMemberId == -1 || currentMemberId == filterMemberId;
        if (!passesFilter) return;

        // Check if there are enough points to plot the diagram; if not, return early
        if (valuePoints == null || valuePoints.Count < 2) return;

        // Remove all child objects in the diagram container to clear previous meshes and points
        DiagramPointCollider.globalPoints.RemoveAll(p => p == null || allSelectablePoints.Contains(p));
        // Clear previous diagram points and meshes
        allSelectablePoints.Clear();
        DiagramPointCollider.ClearSelection();

        // Find or create the container for the diagram meshes
        GameObject diagramContainer = parentTransform.Find("ShearDiagramContainer")?.gameObject;
        if (diagramContainer != null)
            DestroyImmediate(diagramContainer);

        diagramContainer = new GameObject("ShearDiagramContainer");
        diagramContainer.transform.SetParent(parentTransform, false);

        // AI-Assisted Logic (Gemini): The following cross-product methodology to determine the structural
        // diagram's transversal plane relative to Unity gravity vector was developed with AI assistance
        // Calculate the normal direction perpendicular to the element's axis in 3D
        Vector3 lStart = parentTransform.InverseTransformPoint(new Vector3(valuePoints[0].x, valuePoints[0].y, valuePoints[0].z));
        Vector3 globalUp = Vector3.up;
        Vector3 globalNormalDir;
        string element;

        // Check if element is a Column (Vertical)
        if (Mathf.Abs(Vector3.Dot(localAxisDir, globalUp)) > 0.999f)
        {
            // Use Z axis (Foward) for drawing
            globalNormalDir = -Vector3.Cross(localAxisDir, Vector3.forward).normalized;
            element = "column";
        }
        
        // For horizontal or diagonal beams
        else
        {
            // Find a vector that points out of the plane
            Vector3 globalHorizontal = Vector3.Cross(localAxisDir, Vector3.right).normalized;
            globalNormalDir = (Vector3.Cross(globalHorizontal, localAxisDir).normalized);
            element = "beam";
        }

        // In case vector direction input is null assign safe option up
        if (globalNormalDir == Vector3.zero)
        {
           globalNormalDir = Vector3.up;
           element = "beam"; 
        } 

        // consider rotation fo element in Unity
        Vector3 normalDir = parentTransform.InverseTransformDirection(globalNormalDir).normalized;

        // Segment the points to detect discontinuities
        List<List<Vector4>> segments = new List<List<Vector4>>();
        List<Vector4> currentSegment = new List<Vector4> { valuePoints[0] };

        // Iterate through the value points to identify discontinuities based on position and effort value
        for (int i = 1; i < valuePoints.Count; i++)
        {
            Vector3 prevPos = new Vector3(valuePoints[i - 1].x, valuePoints[i - 1].y, valuePoints[i - 1].z);
            Vector3 currPos = new Vector3(valuePoints[i].x, valuePoints[i].y, valuePoints[i].z);

            // Check for discontinuity based on the distance between consecutive points; if they are too close, consider it a discontinuity
            bool isDiscontinuity = Vector3.Distance(prevPos, currPos) < 0.0001f;

            if (isDiscontinuity)
            {
                segments.Add(currentSegment);
                currentSegment = new List<Vector4>();
            }
            currentSegment.Add(valuePoints[i]);
        }
        segments.Add(currentSegment);

        // Plot each segment of the diagram
        for (int i = 0; i < segments.Count; i++)
        {
            // Get the current segment to plot
            List<Vector4> seg = segments[i];
            // Skip segments that have less than 2 points, as they cannot form a valid area
            if (seg.Count < 2) continue;

            PlotSegment(seg, i, diagramContainer, areaMaterial, localAxisDir, normalDir, Vector3.one, lStart, 1f, -momentScale, 0.01f, tooltipUI, selectedPoint, element);
        }
    }

    // AI-Assisted Implementation (Claude): The syntax for creating the dynamic Unity mesh (Calculation of vertices and 
    // triange for the curve area) was guided by Claude.
    // Method to plot a segment of the moment diagram area based on the provided points, material, and scaling factors
    private void PlotSegment(List<Vector4> seg, int segIndex, GameObject container, Material areaMaterial, Vector3 axisDir, Vector3 normalDir, 
                                Vector3 thicknessDir, Vector3 startPoint, float scaleFactor, float momentScale, float thickness, 
                                DiagramTooltipUI tooltipUI, DiagramPointCollider selectedPoint, string element)
    {
        // Create a new GameObject for the segment's diagram area and set up its MeshFilter and MeshRenderer components
        GameObject diagramObj = new GameObject($"ShearDiagramArea_{segIndex}");
        diagramObj.transform.SetParent(container.transform, false);

        MeshFilter mf_diag   = diagramObj.AddComponent<MeshFilter>();
        MeshRenderer mr_diag = diagramObj.AddComponent<MeshRenderer>();
        mr_diag.material = areaMaterial;

        // Create a new mesh to represent the area under the curve for this segment
        Mesh mesh = new Mesh();
        Vector3[] vertices  = new Vector3[seg.Count * 2];
        int[]     triangles = new int[(seg.Count - 1) * 12];

        // Iterate through the points in the segment and create vertices for the base and peak of the diagram, as well as triangles to form the area under the curve
        for (int i = 0; i < seg.Count; i++)
        {
            // Convert the global point to local space for accurate positioning in the diagram
            Vector3 globalBasePoint = new Vector3(seg[i].x, seg[i].y, seg[i].z);
            Vector3 basePoint = parentTransform.InverseTransformPoint(globalBasePoint);

            // define Force Value in the diagram
            float value = seg[i].w;

            // Define direction Bending
            float offset;
            if(forceValue == "KNm")
            {
                offset = value * momentScale;
            }
            // Define direction Normal
            else if (forceValue == "KN " && element == "beam")
            {
                offset = value * momentScale;
            }
            else if (forceValue == "KN " && element == "column")
            {
                offset = value * (-momentScale);
            }
            // Define direction Shear
            else
            {
               offset = value * (-momentScale); 
            }
            
            // Calculate the peak point of the diagram by offsetting the base point
            Vector3 peakPoint = basePoint + normalDir * offset;

            vertices[i * 2]     = basePoint;
            vertices[i * 2 + 1] = peakPoint;

            CreateSelectablePoint( container.transform, peakPoint, basePoint, value, loadCaseName, currentMemberId, tooltipUI, selectedPoint);
        }

        // Create triangles to form the area under the curve
        for (int i = 0; i < seg.Count - 1; i++)
        {
            int r = i * 2;
            int t = i * 12;

            // Front face triangles
            triangles[t+0]=r; triangles[t+1]=r+1; triangles[t+2]=r+2;
            triangles[t+3]=r+1; triangles[t+4]=r+3; triangles[t+5]=r+2;

            // Back face triangles
            triangles[t+6]=r; triangles[t+7]=r+2; triangles[t+8]=r+1;
            triangles[t+9]=r+1; triangles[t+10]=r+2; triangles[t+11]=r+3;
        }

        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mf_diag.mesh = mesh;
    }

    // AI-Assisted Implementation (Claude): The dynamic attachment of XR components (XRSimpleInteractable) and custom
    // data colliders to runtime-instantiated spheres was implemented using Claude
    // Method to create a selectable point at the peak of the diagram
    private void CreateSelectablePoint(Transform parent, Vector3 localPeak, Vector3 localBase, float value, int lcName, 
                                        int mId, DiagramTooltipUI tooltipUI, DiagramPointCollider selectedPoint)
    {
        // Interaction Layer for points
        int pointsLayerIndex = InteractionLayerMask.NameToLayer("Points");

        // Create a new GameObject for the point (one in diagram + one in element)
        GameObject pt_1 = new GameObject("DiagramPoint");
        pt_1.transform.SetParent(parent, false);
        pt_1.transform.localPosition = localPeak;

        GameObject pt_2 = new GameObject("ElementPoint");
        pt_2.transform.SetParent(parent, false);
        pt_2.transform.localPosition = localBase;

        // Create a sphere_1 to visually represent the selectable point and configure its material for transparency if needed
        GameObject sphere_1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere_1.name = "PointSphere1";
        sphere_1.transform.SetParent(pt_1.transform, false);
        sphere_1.transform.localPosition = Vector3.zero;
        sphere_1.transform.localScale    = Vector3.one * pointCircleRadius * 2f;
        Material sphereMat_1 = diagCircleMaterial != null // Use the provided material for the point circles if available; otherwise, create a default material
            ? diagCircleMaterial
            : new Material(Shader.Find("Unlit/Color"));
        
        GameObject sphere_2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere_2.name = "PointSphere2";
        sphere_2.transform.SetParent(pt_2.transform, false);
        sphere_2.transform.localPosition = Vector3.zero;
        sphere_2.transform.localScale    = Vector3.one * pointCircleRadius * 2f;
        Material sphereMat_2 = elementCircleMaterial != null // Use the provided material for the point circles if available; otherwise, create a default material
            ? elementCircleMaterial
            : new Material(Shader.Find("Unlit/Color"));

        // Update the alpha of the material color based on the pointAlpha
        float clamped = Mathf.Clamp01(pointAlpha);
        
        Color c1 = sphereMat_1.color;
        c1.a = clamped;
        sphereMat_1.color = c1;

        Color c2 = sphereMat_2.color;
        c2.a = clamped;
        sphereMat_2.color = c2;

        // Configure the material for transparency if alpha is less than 1
        if (clamped < 1f)
        {
            sphereMat_1.SetFloat("_Mode", 3);
            sphereMat_1.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sphereMat_1.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sphereMat_1.SetInt("_ZWrite", 0);
            sphereMat_1.DisableKeyword("_ALPHATEST_ON");
            sphereMat_1.EnableKeyword("_ALPHABLEND_ON");
            sphereMat_1.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sphereMat_1.renderQueue = 3000;

            sphereMat_2.SetFloat("_Mode", 3);
            sphereMat_2.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sphereMat_2.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sphereMat_2.SetInt("_ZWrite", 0);
            sphereMat_2.DisableKeyword("_ALPHATEST_ON");
            sphereMat_2.EnableKeyword("_ALPHABLEND_ON");
            sphereMat_2.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sphereMat_2.renderQueue = 3000;
        }

        sphere_1.GetComponent<MeshRenderer>().material = sphereMat_1;
        DestroyImmediate(sphere_1.GetComponent<Collider>());

        sphere_2.GetComponent<MeshRenderer>().material = sphereMat_2;
        DestroyImmediate(sphere_2.GetComponent<Collider>());

        SphereCollider col_1 = pt_1.AddComponent<SphereCollider>();
        col_1.radius = pointCircleRadius * 1.5f;

        SphereCollider col_2 = pt_2.AddComponent<SphereCollider>();
        col_2.radius = pointCircleRadius * 1.5f;

        // Convert local coordinates to world coordinates for the point colliders
        Vector3 worldBase = parentTransform.TransformPoint(localBase);
        Vector3 worldPeak = parentTransform.TransformPoint(localPeak);

        // Add XRSimpleInteractable component to make the point interactable in XR
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable_1 = pt_1.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        // Assig to InteractionMananger
        interactable_1.interactionManager = interactionManager;
        // Set the select mode to single to one point be selected at time
        interactable_1.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;
        // Move points to Points Interaction Layer
        if (pointsLayerIndex < 0)
            Debug.LogError("There is no Interaction Layer 'Points'");
        else
            interactable_1.interactionLayers = InteractionLayerMask.GetMask("Points");
        
        // Add a custom collider component to store the value, load case name, member ID, and world position of the point for interaction purposes
        DiagramPointCollider dpc_1 = pt_1.AddComponent<DiagramPointCollider>();
        dpc_1.value      = value;
        dpc_1.loadCase   = lcName;
        dpc_1.memberId   = mId;
        dpc_1.worldPoint = worldBase;
        dpc_1.diagramPoint = worldPeak;
        dpc_1.forceValue = forceValue;
        
        // Suscribe to the select event of the interactable to handle point selection
        interactable_1.hoverEntered.AddListener((args) => 
        {
            if(tooltipUI != null)
            {
                if (selectedPoint != dpc_1)
                {
                    tooltipUI.HideTooltip();
                }

                tooltipUI.ShowTooltip(dpc_1.value, dpc_1.loadCase, dpc_1.memberId, dpc_1.diagramPoint, dpc_1.forceValue);
                // Store selected point
                selectedPoint = dpc_1;  
            }                 
        });

        // Add XRSimpleInteractable component to make the point interactable in XR
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable_2 = pt_2.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        // Assig to InteractionMananger
        interactable_2.interactionManager = interactionManager;
        // Set the select mode to single to one point be selected at time
        interactable_2.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;
        // Move points to Points Interaction Layer
        interactable_2.interactionLayers  = InteractionLayerMask.GetMask("Points");

        // Add a custom collider component to store the value, load case name, member ID, and world position of the point for interaction purposes
        DiagramPointCollider dpc_2 = pt_2.AddComponent<DiagramPointCollider>();
        dpc_2.value      = value;
        dpc_2.loadCase   = lcName;
        dpc_2.memberId   = mId;
        dpc_2.worldPoint = worldBase;
        dpc_2.diagramPoint = worldPeak;
        dpc_2.forceValue = forceValue;
        
        // Suscribe to the select event of the interactable to handle point selection
        interactable_2.hoverEntered.AddListener((args) =>
        {
            if (tooltipUI != null)
                if (selectedPoint != dpc_2)
                {
                    tooltipUI.HideTooltip();
                }

                tooltipUI.ShowTooltip(dpc_2.value, dpc_2.loadCase, dpc_2.memberId, dpc_2.worldPoint, dpc_2.forceValue);
                // Store selected point
                selectedPoint = dpc_2;        
        });

        // save point into the list
        allSelectablePoints.Add(dpc_1);
        allSelectablePoints.Add(dpc_2);

        // Add the created DiagramPointColliders to the global list for selection management
        DiagramPointCollider.globalPoints.Add(dpc_1);
        DiagramPointCollider.globalPoints.Add(dpc_2);
    }
}
