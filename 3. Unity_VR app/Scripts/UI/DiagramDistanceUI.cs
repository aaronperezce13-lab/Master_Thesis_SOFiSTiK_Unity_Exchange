/*
----------------------------------------------------------------------------------------------------------
Script: DiagramDistanceUI.cs
Author: Aaron Perez Herrera
Description:    This script controls the 3D user interface for displaying spatial distances between
                selected data points in the XR environment. It dynamically calculates offsets and 
                positions a LineRenderer and TextMeshPro tooltip facing the main camera perspective.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System;
using TMPro;

public class DiagramDistanceUI : MonoBehaviour
{
    [Header("UI Variables")]
    public GameObject tooltipRoot;
    public TextMeshProUGUI TT_Dist;
    public TextMeshProUGUI TT_MemberId;
    public Camera mainCamera;
    public float tooltipOffset = 0.3f;
    public float cameraOffset = 0.3f;
    public float CameralineOffset = 0.1f;
    public float lineOffset = 0.3f;
    public GameObject lineDistance;
    private LineRenderer lDist;

    void Start()
    {
        mainCamera = Camera.main;

        // If the LineRenderer component does not exist, add it to the line objects
        lDist = lineDistance.GetComponent<LineRenderer>();

        if (lDist == null)
            lDist = lineDistance.AddComponent<LineRenderer>();
        
        lDist.useWorldSpace   = true;
        lDist.positionCount   = 4;
        lDist.widthMultiplier = 0.06f;

        if (lDist.material == null || lDist.material.shader.name != "Universal Render Pipeline/Unlit")
        {
            lDist.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lDist.material.color = Color.black;
        }
        //lineDistance.SetActive(false);
        HideTooltip();
    }

    // LateUpdate is called after all Update functions have been called, ensuring that the UI updates after all other game logic has been processed
    void LateUpdate()
    {
        if (tooltipRoot == null || !tooltipRoot.activeSelf) return;
        tooltipRoot.transform.rotation = mainCamera.transform.rotation;
    }

    // Method to display distance information
    public void ShowDistance(float dx, float dy, float dz,
                             Vector3 midpoint, Vector3 worldA, Vector3 worldB,int memberID)
    {
        // Show the UI with distance
        tooltipRoot.SetActive(true);

        // Display only significant values (bigger than a small threshold -> eps)
        float eps = 0.0001f;
        float total = Vector3.Distance(worldA, worldB);
        Vector3 offset_p1, offset_p2;

        // Create a list of active lines to display based on the values that exceed the threshold
        if (total > eps)
        {
            decimal DecDx = Math.Round((decimal)dx, 4);
            decimal DecDy = Math.Round((decimal)dy, 4);
            decimal DecDz = Math.Round((decimal)dz, 4);
            decimal DecTotal = Math.Round((decimal)total, 4);

            // Show distance for x members
            if ((DecDy == 0 && DecDz == 0) || (DecDy == 0 && DecDx == 0))
            {
                TT_Dist.text    =  $"Dist:  {DecTotal} m";
                TT_MemberId.text = $"ID:  {memberID}";
                tooltipRoot.transform.position = midpoint + Vector3.up * tooltipOffset + mainCamera.transform.forward * -cameraOffset;

                // offset for line to camera
                Vector3 camOffset = mainCamera.transform.forward * -CameralineOffset;

                // Visualize the line between the two points
                offset_p1 = worldA + Vector3.up * lineOffset + camOffset;
                offset_p2 = worldB + Vector3.up * lineOffset + camOffset;

                lDist.SetPosition(0, worldA + camOffset);
                lDist.SetPosition(1, offset_p1);
                lDist.SetPosition(2, offset_p2);
                lDist.SetPosition(3, worldB + camOffset);
            }

            // Show distance for y members
            else if(DecDx == 0 && DecDz == 0)
            {
                TT_Dist.text    =  $"Dist:  {DecTotal} m";
                TT_MemberId.text = $"ID:  {memberID}";
                tooltipRoot.transform.position = midpoint + Vector3.left * tooltipOffset + mainCamera.transform.forward * -cameraOffset;

                // offset for line to camera
                Vector3 camOffset = mainCamera.transform.forward * -CameralineOffset;

                // Visualize the line between the two points
                offset_p1 = worldA + Vector3.left * lineOffset + camOffset;
                offset_p2 = worldB + Vector3.left * lineOffset + camOffset;

                lDist.SetPosition(0, worldA + camOffset);
                lDist.SetPosition(1, offset_p1);
                lDist.SetPosition(2, offset_p2);
                lDist.SetPosition(3, worldB + camOffset);
            }

            else
            {
                TT_Dist.text    =  $"Dist:  {DecTotal} m";
                TT_MemberId.text = $"ID:  {memberID}";
                tooltipRoot.transform.position = midpoint + Vector3.left * tooltipOffset + Vector3.up * tooltipOffset + mainCamera.transform.forward * -cameraOffset;

                // offset for line to camera
                Vector3 camOffset = mainCamera.transform.forward * -CameralineOffset;

                // Visualize the line between the two points
                offset_p1 = worldA + Vector3.left * lineOffset + Vector3.up * lineOffset + camOffset;
                offset_p2 = worldB + Vector3.left * lineOffset + Vector3.up * lineOffset + camOffset;

                lDist.SetPosition(0, worldA + camOffset);
                lDist.SetPosition(1, offset_p1);
                lDist.SetPosition(2, offset_p2);
                lDist.SetPosition(3, worldB + camOffset);
            }
        }
        else
        {
            Debug.Log($"Distance Between elements is too Short");
        }
    }

     // Method to hide the tooltip
    public void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }
}