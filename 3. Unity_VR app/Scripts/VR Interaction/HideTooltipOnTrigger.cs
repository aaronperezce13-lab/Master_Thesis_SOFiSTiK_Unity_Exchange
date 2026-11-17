/*
----------------------------------------------------------------------------------------------------------
Script: HideTooltipOnTrigger.cs
Author: Aaron Perez Herrera
Description:    Utility script designed to automatically hide the 3D diagram tootips whr the user presses 
                the select/trigger button on their VR controller, utilizing Unity Input System.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using UnityEngine.InputSystem;

public class HideTooltipOnTrigger : MonoBehaviour
{
    [Header("Tooltip UI")]
    [SerializeField] private DiagramTooltipUI tooltipUI;

    [Header("Trigger / Select Input")]
    [SerializeField] private InputActionReference selectAction;

    private void OnEnable()
    {
        if (selectAction != null)
        {
            selectAction.action.performed += OnTriggerPressed;
            selectAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (selectAction != null)
        {
            selectAction.action.performed -= OnTriggerPressed;
            selectAction.action.Disable();
        }
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (tooltipUI != null)
        {
            tooltipUI.HideTooltip();
        }
    }
}