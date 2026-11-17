/*
----------------------------------------------------------------------------------------------------------
Script: DiagramTooltipUI.cs
Author: Aaron Perez Herrera
Description:    This script controls 3D user interface tooltip for individual structural data points in 
                the XR environment. Ensures the text dynamically faces the main camera and displays 
                real-time semantic data (force value, load case, element ID) upon selection.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using TMPro;

public class DiagramTooltipUI : MonoBehaviour
{
    [Header("UI Variables")]
    public GameObject tooltipRoot;
    public TextMeshProUGUI TT_Value;
    public TextMeshProUGUI TT_LoadCase;
    public TextMeshProUGUI TT_MemberId;
    public Camera mainCamera;
    public float tooltipOffset = 0.3f;
    public float cameraOffset = 0.3f;

    void Awake()
    {
        mainCamera = Camera.main;
        HideTooltip();
    }

    // Ensure the tooltip always faces the camera
    void LateUpdate()
    {
        if (tooltipRoot != null && tooltipRoot.activeSelf)
            tooltipRoot.transform.rotation = mainCamera.transform.rotation;
    }

    // Method to display the tooltip with the given information
    public void ShowTooltip(float value, int loadCase, int memberId, Vector3 tooltipPos, string forceValue)
    {
        if (tooltipRoot == null) { Debug.LogError("tooltipRoot es NULL"); return; }
        if (TT_Value == null)    { Debug.LogError("TT_Value es NULL"); return; }
        if (TT_LoadCase == null) { Debug.LogError("TT_LoadCase es NULL"); return; }
        if (TT_MemberId == null) { Debug.LogError("TT_MemberId es NULL"); return; }

        TT_Value.text    = $"Value:  {value:F2} {forceValue}";
        TT_LoadCase.text = $"LC:  {loadCase}";
        TT_MemberId.text = $"ID:  {memberId}";

        tooltipRoot.transform.position = tooltipPos + Vector3.up * tooltipOffset + mainCamera.transform.forward * -cameraOffset;
        tooltipRoot.SetActive(true);
    }

    // Method to hide the tooltip
    public void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }
}