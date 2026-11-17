/*
----------------------------------------------------------------------------------------------------------
Script: ControllerLabelSystem.cs
Author: Aaron Perez Herrera
Description:    Manages the centralized system for toggling controller instruction labelson and off in 
                response to user input (help button press) using Unity Input System.
AI Assistance:  The overarching feature toggle architecture was designed by the author. Claude 
                (Anthropic, 2026) was utilized as a syntax assistant to correctly configure the Unity 
                Input System "InputActionReference" bindings and event callback registrations (OnEnable / 
                OnDisable).
----------------------------------------------------------------------------------------------------------
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class ControllerLabelSystem : MonoBehaviour
{
    [Header("Input Action — Help Button")]
    public InputActionReference helpButtonAction;

    [Header("Controller Prefab References")]
    public GameObject leftControllerRoot;
    public GameObject rightControllerRoot;

    private List<ControllerLabel> _allLabels = new();
    private bool _labelsVisible = false;

    // AI-Assited Implementation: The syntax for enabling input actions and registering callback events via 
    // InputAction.CallbackContext was implemented with assistance from Claude
    void OnEnable()
    {
        helpButtonAction.action.Enable();
        helpButtonAction.action.performed += OnHelpPressed;
    }

    void OnDisable()
    {
        helpButtonAction.action.performed -= OnHelpPressed;
    }

    void Start()
    {
        // Pick labes of both controllers
        if (leftControllerRoot)
            _allLabels.AddRange(leftControllerRoot.GetComponentsInChildren<ControllerLabel>(true));
        if (rightControllerRoot)
            _allLabels.AddRange(rightControllerRoot.GetComponentsInChildren<ControllerLabel>(true));

        // Start hidded
        SetLabelsVisible(false, instant: true);
    }

    private void OnHelpPressed(InputAction.CallbackContext ctx)
    {
        _labelsVisible = !_labelsVisible;
        SetLabelsVisible(_labelsVisible);
    }

    public void SetLabelsVisible(bool visible, bool instant = false)
    {
        foreach (var label in _allLabels)
            label.SetVisible(visible, instant);
    }

    void Update()
{
    // Check in the console if the action is active
    if (helpButtonAction != null && helpButtonAction.action != null)
    {
        if (helpButtonAction.action.WasPerformedThisFrame())
            Debug.Log("[LabelSystem] Help button detected");
    }
    else
    {
        Debug.LogWarning("[LabelSystem] helpButtonAction not assigned or null");
    }
}
}