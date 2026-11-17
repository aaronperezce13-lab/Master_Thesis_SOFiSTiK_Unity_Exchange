/*
----------------------------------------------------------------------------------------------------------
Script: WristMenuController.cs
Author: Aaron Perez Herrera
Description:    Manages a wrist-attached virtual reality menu system, allowing users to toggle, navigate,
                and interact with UI elements (buttons, toggles, sliders, and dropdowns) using VR 
                controller thumbsticks and action buttons.
AI Assistance:  The overarching wrist-menu UX architecture and navigation flow were designed by the 
                author. Claude (Anthropic, 2026) was utilized as a syntax assistant to implement the 
                helper methods for Unity EventSystem (AxisEventData, ExecuteEvenets), courtine-based 
                focus restoration, and TMP_Dropdown state tracking.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class WristMenuController : MonoBehaviour
{
    [Header("Necessary References")]
    [SerializeField] GameObject      MenuCanvas_1;
    [SerializeField] GameObject      MenuCanvas_2;
    [SerializeField] Transform       m_LeftController;

    [Header("Offset relative to the left controller")]
    [SerializeField] Vector3         m_PositionOffset = new Vector3(0f, 0.1f, 0.05f);
    [SerializeField] Vector3         m_RotationOffset = new Vector3(45f, 0f, 0f);

    [Header("Input Actions")]
    [SerializeField] InputActionReference m_ToggleMenuAction;  // X button
    [SerializeField] InputActionReference m_NavigateAction;    // Left thumbstick
    [SerializeField] InputActionReference m_ClickAction;      // A button

    [Header("Navigation")]
    [SerializeField] float m_NavigationCooldown = 0.25f; // seconds between navigations
    [SerializeField] float m_DeadZone           = 0.5f;  // thumbstick threshold

    // State for dropdown handling
    private bool _visibleDropdown = false;
    private TMPro.TMP_Dropdown m_ActiveDropdown = null;
    private GameObject m_LastSelectedBeforeDropdown = null;

    // Internal state
    private bool  m_MenuVisible      = false;
    private float m_LastNavigateTime = 0f;
    private GameObject CurrentMenuSelected  = null;

    // UI navigation system
    private EventSystem    m_EventSystem;
    private GameObject     m_CurrentSelected;

    // Lifecycle

    void Awake()
    {
        m_EventSystem = EventSystem.current;

        // The menu starts hidden
        MenuCanvas_1.SetActive(false);
        MenuCanvas_2.SetActive(false);
    }

    void OnEnable()
    {
        if (m_ToggleMenuAction != null)
            m_ToggleMenuAction.action.performed += OnToggleMenu;

        if (m_NavigateAction != null)
            m_NavigateAction.action.performed += OnNavigate;

        if (m_ClickAction != null)
            m_ClickAction.action.performed += OnClick;
    }

    void OnDisable()
    {
        if (m_ToggleMenuAction != null)
            m_ToggleMenuAction.action.performed -= OnToggleMenu;
        
        if (m_NavigateAction != null)
            m_NavigateAction.action.performed -= OnNavigate;

        if (m_ClickAction != null)
            m_ClickAction.action.performed -= OnClick;
    }

    void Update()
    {
        if (!m_MenuVisible || m_LeftController == null)
            return;

        // The 2 canvas follows the left controller
        MenuCanvas_1.transform.position = m_LeftController.TransformPoint(m_PositionOffset);
        MenuCanvas_1.transform.rotation = m_LeftController.rotation *
                                          Quaternion.Euler(m_RotationOffset);

        MenuCanvas_2.transform.position = m_LeftController.TransformPoint(m_PositionOffset);
        MenuCanvas_2.transform.rotation = m_LeftController.rotation *
                                          Quaternion.Euler(m_RotationOffset);
        
        // Handle dropdown visibility: if we had a dropdown open but it's now closed, restore focus to the last selected element
        if (_visibleDropdown == true && !IsTMPDropdownExpanded(m_ActiveDropdown))
        {
            _visibleDropdown = false;
            GameObject target = m_LastSelectedBeforeDropdown;
            m_ActiveDropdown = null;
            StartCoroutine(RestoreFocusNextFrame(target));
        }
    }

    void OnToggleMenu(InputAction.CallbackContext ctx)
    {
        SetMenuVisible(!m_MenuVisible);
    }

    public void SetMenuVisible(bool visible)
    {
        m_MenuVisible = visible;
        MenuCanvas_1.SetActive(visible);
        MenuCanvas_2.SetActive(false);

        // When opening, select the first interactable element
        if (visible)
        {
            CurrentMenuSelected = MenuCanvas_1;
            SelectFirstElement();
        }
        else
        {
            CurrentMenuSelected = null;
        }  
    }

    // Utility to switch between canvases (called from buttons)
    public void SwitchToCanvas(GameObject targetCanvas)
    {
        // Clear current selection to prevent issues when switching canvases
        if (m_EventSystem != null)
        {
            m_EventSystem.SetSelectedGameObject(null);
        }

        MenuCanvas_1.SetActive(targetCanvas == MenuCanvas_1);
        MenuCanvas_2.SetActive(targetCanvas == MenuCanvas_2);
        CurrentMenuSelected = targetCanvas;
        StartCoroutine(RestoreFocusNextFrame(null));
    }

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (!m_MenuVisible) return;

        // Cooldown to prevent too fast navigation
        if (Time.unscaledTime - m_LastNavigateTime < m_NavigationCooldown)
            return;

        var input = ctx.ReadValue<Vector2>();

        // Dead zone: ignore small inputs
        if (input.magnitude < m_DeadZone) return;

        m_LastNavigateTime = Time.unscaledTime;

        // Build navigation event for the EventSystem
        var axisEventData = new AxisEventData(m_EventSystem);

        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            // Vertical navigation
            axisEventData.moveDir = input.y > 0
                ? MoveDirection.Up
                : MoveDirection.Down;
        }
        else
        {
            // Horizontal navigation (useful for sliders and dropdowns)
            axisEventData.moveDir = input.x > 0
                ? MoveDirection.Right
                : MoveDirection.Left;
        }

        // Send the event to the currently selected element
        if (m_EventSystem.currentSelectedGameObject != null)
            ExecuteEvents.Execute(
                m_EventSystem.currentSelectedGameObject,
                axisEventData,
                ExecuteEvents.moveHandler);
        else
            SelectFirstElement();
    }

    void OnClick(InputAction.CallbackContext ctx)
    {
        if (!m_MenuVisible) return;

        // Get the currently selected UI element
        var selected = m_EventSystem.currentSelectedGameObject;
        if (selected == null) return;

        // If it's a dropdown, open it
        var dropdown = selected.GetComponent<TMPro.TMP_Dropdown>();
        if (dropdown != null)
        {
            m_ActiveDropdown = dropdown;
            m_LastSelectedBeforeDropdown = selected;
            dropdown.Show();
            StartCoroutine(RestoreFocusNextFrame(selected)); // Re-select
            _visibleDropdown = true;
            return;
        }

        // If it's a toggle, change its state
        var toggle = selected.GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = !toggle.isOn;
            StartCoroutine(RestoreFocusNextFrame(selected)); // Re-select
            return;
        }

        // If it's a slider, do nothing on click
        var slider = selected.GetComponent<Slider>();
        if (slider != null)
            return;

        // If it's a button or any other interactable, trigger its onClick
        var pointerData = new PointerEventData(m_EventSystem);
        ExecuteEvents.Execute(selected, pointerData, ExecuteEvents.submitHandler);
        ExecuteEvents.Execute(selected, pointerData, ExecuteEvents.pointerClickHandler);

        // Re-select the previously selected element to maintain navigation flow
        // StartCoroutine(RestoreFocusNextFrame(selected));
    }

    // AI-Assisted Implementation: The helper methods for traversing selectables, managing EventSystem
    // focus via coroutines, and checking TMP_Dropdown expansion were implemented with syntax assistance 
    // from Claude.
    // Helpers

    void SelectFirstElement()
    {
        Selectable validSelectable = null;
        GameObject activeCanvas = null;

        // Check which canvas is active and search for the first selectable
        if (MenuCanvas_1.activeInHierarchy)
            activeCanvas = MenuCanvas_1;
        else if (MenuCanvas_2.activeInHierarchy)
            activeCanvas = MenuCanvas_2;

        if(activeCanvas != null)
        {
            // Pick all selectables in the active canvas and its children
            Selectable[] selectables = activeCanvas.GetComponentsInChildren<Selectable>(false);

            // Find the first interactable and enabled selectable
            foreach (Selectable sel in selectables)
            {
                if (sel.IsInteractable() && sel.enabled)
                {
                    validSelectable = sel;
                    break;
                }
            }
        }

        // If no valid selectable found, clear selection
        if (validSelectable != null)
        {
            m_EventSystem.SetSelectedGameObject(null);
            m_EventSystem.SetSelectedGameObject(validSelectable.gameObject);
            m_CurrentSelected = validSelectable.gameObject;
        }
    }

    // Utility to restore focus to a specific element after an interaction
    private IEnumerator RestoreFocusNextFrame(GameObject target)
    {
        // wait 2 frames to ensure the dropdown or toggle has finished processing
        yield return null;
        yield return null;

        if (target != null)
        {
            m_EventSystem.SetSelectedGameObject(null);  // force the EventSystem to recognize the change
            m_EventSystem.SetSelectedGameObject(target);
            m_CurrentSelected = target;
        }
        // If target is null, select the first element of the current canvas
        else
        {
            m_EventSystem.SetSelectedGameObject(null);
            SelectFirstElement();
        }
    }

    private bool IsTMPDropdownExpanded(TMPro.TMP_Dropdown dropdown)
    {
        // TMP_Dropdown creates an object called "Dropdown List" when it's open
        return dropdown.transform.Find("Dropdown List") != null;
    }
}