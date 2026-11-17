/*
----------------------------------------------------------------------------------------------------------
Script: PlotUIController.cs
Author: Aaron Perez Herrera
Description:    This script manages interaction between the Unity Canvas UI elements (dropdowns, toggles, 
                sliders) and the "Get_Plot" controller. It maps the selections of user in UI to 
                dynamically update the 3D structural diagrams and filter properties.
AI Assistance:  The logic linking UI components to diagram updates was defined by the author. Claude 
                (Anthropic, 2026) was used as a syntax assistant to properly implement Unity events 
                subscription model (onValueChanged.AddListener / RemoveListener) to prevent memory leaks 
                during runtime. 
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlotUIController : MonoBehaviour
{
    [Header("Referenced Get_Plot Member")]
    [SerializeField] public Get_Plot m_GetPlot;

    [Header("Dropdowns")]
    [SerializeField] public TMP_Dropdown TypeForceDropdown;
    [SerializeField] public TMP_Dropdown LoadCaseDropdown;
    [SerializeField] public TMP_Dropdown FilterMemberDropdown;

    [Header("Toggles")]
    [SerializeField] public Toggle MiddleLinePlotToggle;
    [SerializeField] public Toggle ExtremeValuesToggle;
    [SerializeField] public Toggle ViewXZToggle;
    [SerializeField] public Toggle ViewYZToggle;

    [Header("Sliders")]
    [SerializeField] public Slider MomentScaleSlider;
    [SerializeField] public Slider InterpolationStepsSlider;
    [SerializeField] public Slider CenterLineWidthSlider;
    [SerializeField] public Slider PointAlphaSlider;
    [SerializeField] public Slider PointSizeSlider;

    // AI-Assisted Implementation: The syntax for safely subscribing and unsubscribing to Unity
    // UI events guided by Claude
    void Start()
    {
        // Suscribe to events
        TypeForceDropdown.onValueChanged.AddListener(OnTypeForceChanged);
        LoadCaseDropdown.onValueChanged.AddListener(OnLoadCaseChanged);
        FilterMemberDropdown.onValueChanged.AddListener(OnFilterMemberChanged);

        MiddleLinePlotToggle.onValueChanged.AddListener(OnMiddleLinePlotChanged);
        ExtremeValuesToggle.onValueChanged.AddListener(OnShowExtremeValuesChanged);
        ViewXZToggle.onValueChanged.AddListener(OnViewXZChanged);
        ViewYZToggle.onValueChanged.AddListener(OnViewYZChanged);

        MomentScaleSlider.onValueChanged.AddListener(OnMomentScaleChanged);
        CenterLineWidthSlider.onValueChanged.AddListener(OnCenterLineWidthChanged);
        PointAlphaSlider.onValueChanged.AddListener(OnPointAlphaChanged);
        PointSizeSlider.onValueChanged.AddListener(OnPointSizeChanged);
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        TypeForceDropdown.onValueChanged.RemoveListener(OnTypeForceChanged);
        LoadCaseDropdown.onValueChanged.RemoveListener(OnLoadCaseChanged);
        FilterMemberDropdown.onValueChanged.RemoveListener(OnFilterMemberChanged);

        MiddleLinePlotToggle.onValueChanged.RemoveListener(OnMiddleLinePlotChanged);
        ExtremeValuesToggle.onValueChanged.RemoveListener(OnShowExtremeValuesChanged);
        ViewXZToggle.onValueChanged.RemoveListener(OnViewXZChanged);
        ViewYZToggle.onValueChanged.RemoveListener(OnViewYZChanged);

        MomentScaleSlider.onValueChanged.RemoveListener(OnMomentScaleChanged);
        CenterLineWidthSlider.onValueChanged.RemoveListener(OnCenterLineWidthChanged);
        PointAlphaSlider.onValueChanged.RemoveListener(OnPointAlphaChanged);
        PointSizeSlider.onValueChanged.RemoveListener(OnPointSizeChanged);
    }

    void OnTypeForceChanged(int index)
    {
        // Check if the dropdown and m_GetPlot are assigned
        if(TypeForceDropdown == null || m_GetPlot == null)
        {
            Debug.LogError("TypeForceDropdown or m_GetPlot is not assigned.");
            return;
        }

        string force_selected = TypeForceDropdown.options[index].text;
        m_GetPlot.csvFileName = force_selected;
    }

    void OnLoadCaseChanged(int index)
    {
        // Check if the dropdown and m_GetPlot are assigned
        if(LoadCaseDropdown == null || m_GetPlot == null)
        {
            Debug.LogError("LoadCaseDropdown or m_GetPlot is not assigned.");
            return;
        }
        
        int loadCase = int.Parse(LoadCaseDropdown.options[index].text);
        m_GetPlot.loadCaseName = loadCase;
    }

    void OnFilterMemberChanged(int index)
    {
        // Check if the dropdown and m_GetPlot are assigned
        if(FilterMemberDropdown == null || m_GetPlot == null)
        {
            Debug.LogError("FilterMemberDropdown or m_GetPlot is not assigned.");
            return;
        }

        string memberId = FilterMemberDropdown.options[index].text;

        if (memberId == "All Structure")
        {
            m_GetPlot.filterMemberId = -1; // Use a special value to indicate no filtering
        }
        else
        {
            m_GetPlot.filterMemberId = int.Parse(memberId); // Set the selected member ID for filtering
        }
    }

    void OnMiddleLinePlotChanged(bool value)
    {
        m_GetPlot.middleLinePlot = value;
    }

    void OnShowExtremeValuesChanged(bool value)
    {
        m_GetPlot.showExtremeValues = value;
    }

    void OnViewXZChanged(bool value)
    {
        m_GetPlot.viewXZ = value;
        // Ensure XZ and YZ are mutually exclusive
        if (value && m_GetPlot.viewYZ)
        {
            m_GetPlot.viewYZ = false;
            ViewYZToggle.SetIsOnWithoutNotify(false);
        }
    }

    void OnViewYZChanged(bool value)
    {
        m_GetPlot.viewYZ = value;
        //Ensure XZ and YZ are mutually exclusive
        if (value && m_GetPlot.viewXZ)
        {
            m_GetPlot.viewXZ = false;
            ViewXZToggle.SetIsOnWithoutNotify(false);
        }
    }

    void OnMomentScaleChanged(float value)
    {
        m_GetPlot.momentScale = value;
    }   

    void OnCenterLineWidthChanged(float value)
    {
        m_GetPlot.centerLineWidth = value;
    }

    void OnPointAlphaChanged(float value)
    {
        m_GetPlot.pointAlpha = value;
    }

    void OnPointSizeChanged(float value)
    {
        m_GetPlot.pointCircleRadius = value;
    }

}