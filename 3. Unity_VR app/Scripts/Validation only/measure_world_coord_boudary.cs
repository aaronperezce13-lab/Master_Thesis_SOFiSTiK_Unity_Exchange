/*
----------------------------------------------------------------------------------------------------------
Script: measure_world_coord_boundary.cs
Author: Aaron Perez Herrera
Description:    Utility script to calculate and display in console the global wolrd coordinates of a 
                structural element at start and end nodes. It evaluates the element mesh bounds and 
                isolates the longest local axis to determine the physical boundaries.
AI Assistance:  The geometrical logic to isolate the principal axis was defined by the author. As 
                indicated in the methodology tools table, AI assistance Google Gemini (2026) was utilized
                for syntax optimization regrading Unity rendering pipelines, specifically for extracting 
                "Mesh.bounds" and transforming local to world space coordinates.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;

public class measure_world_coord_boudary : MonoBehaviour
{
    void Start()
    {
    // 1. Get local mesh of the current element
    MeshFilter meshFilter = GetComponent<MeshFilter>();
    string elementId = GetComponent<Get_Plot>().icddElementId;

    if (meshFilter == null || meshFilter.sharedMesh == null)
    {
        Debug.LogWarning($"Element {elementId} does not have a MeshFilter.");
        return;
    }

    // AI-Assisted Implementation: The syntax for accessing Unity Mesh.bounds API and performing the 
    // tranform.TransformPoint coordinate translation was implemented with AI assistance
    // 2. Obtain local boundaries of element
    Bounds localBounds = meshFilter.sharedMesh.bounds;
    Vector3 size = localBounds.size;
    Vector3 center = localBounds.center;

    // Initial points in centroid of mesh
    Vector3 localStart = center;
    Vector3 localEnd = center;

    // 3. Identify the length axis (assumed largest axis of element)
    if (size.x > size.y && size.x > size.z)
    {
        localStart.x = localBounds.min.x;
        localEnd.x = localBounds.max.x;
    }
    else if (size.y > size.x && size.y > size.z)
    {
        localStart.y = localBounds.min.y;
        localEnd.y = localBounds.max.y;
    }
    else
    {
        localStart.z = localBounds.min.z;
        localEnd.z = localBounds.max.z;
    }

    // 4. Transform local coordinates to Global coordinates of Unity
    Vector3 worldStart = transform.TransformPoint(localStart);
    Vector3 worldEnd = transform.TransformPoint(localEnd);

    // Print results of validation
    Debug.Log($"Structural Element: {elementId}");
    Debug.Log($"Initial Node (World): {worldStart}");
    Debug.Log($"End Node (World): {worldEnd}");
    }
}