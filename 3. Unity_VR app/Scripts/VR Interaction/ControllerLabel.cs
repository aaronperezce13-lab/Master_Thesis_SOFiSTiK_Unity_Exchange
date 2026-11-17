/*
----------------------------------------------------------------------------------------------------------
Script: ControllerLabel.cs
Author: Aaron Perez Herrera
Description:    Controls the rendering, billboarding, and smooth fading animations of floating UI labels
                and guide lines in the XR environment to highlight controller buttons or interactive 
                features.
AI Assistance:  The conceptual desing and UI layout requirements were defined by the author. Claude 
                (Anthropic, 2026) was utilized as a syntax assistant to implement the alpha fade 
                coroutine using AnimationCurves and the camera-facing orientation math in LateUpdate
----------------------------------------------------------------------------------------------------------
*/

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ControllerLabel : MonoBehaviour
{
    [Header("Text in floating label")]
    public string labelText = "Action";
    [TextArea] public string descriptionText = "";

    [Header("UI References")]
    public Canvas labelCanvas;          // Child World Space Canvas
    public TextMeshProUGUI titleTMP;
    public TextMeshProUGUI descTMP;
    [Header("Guide Line")]
    public bool showGuideLine = false;
    public LineRenderer guideLine;
    public GameObject RealButtonGuide;

    [Header("Label Position")]
    [Tooltip("Local offset from the button anchor")]
    public Vector3 labelOffset = new Vector3(0.08f, 0.06f, 0f);

    [Header("Animation")]
    public float fadeDuration = 0.25f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;
    private bool _isInitialized = false; // Flag to ensure initialization happens only once

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        // Automatically find the Canvas if not assigned
        if (labelCanvas == null)
            labelCanvas = GetComponentInChildren<Canvas>();

        // Ensure the Canvas has a CanvasGroup for fading
        if (labelCanvas != null)
        {
            _canvasGroup = labelCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = labelCanvas.gameObject.AddComponent<CanvasGroup>();

            // Position the canvas according to the offset
            labelCanvas.transform.localPosition = labelOffset;
            
            // Hide at start
            _canvasGroup.alpha = 0f;
            labelCanvas.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError($"[ControllerLabel] ¡Falta asignar el Canvas en {gameObject.name}!", this);
        }

        // Apply the text
        if (titleTMP) titleTMP.text = labelText;
        if (descTMP)  descTMP.text  = descriptionText;

        // Create guide line if enabled
        if (showGuideLine && guideLine == null)
            CreateGuideLine();

        _isInitialized = true;
    }

    private void CreateGuideLine()
    {
        // Add LineRenderer in same GameObject
        guideLine = gameObject.AddComponent<LineRenderer>();
        guideLine.positionCount = 2;
        guideLine.useWorldSpace = true;

        // thickness
        guideLine.startWidth = 0.001f;
        guideLine.endWidth   = 0.001f;

        // Use simple material
        guideLine.material = new Material(Shader.Find("Sprites/Default"));
        guideLine.material.color = Color.white;

        // No shadow or lighting
        guideLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        guideLine.receiveShadows = false;

        guideLine.enabled = false; // empieza oculto
    }

    // AI-Assisted Implementation: The billboarding rotation math and the fad courtine execution logic
    // were stuctured with syntax help from Claude.
    void LateUpdate()
    {
        // The label always faces the camera (billboard)
        if (Camera.main != null)
        {
            labelCanvas.transform.LookAt(
                labelCanvas.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up
            );
        }

        // Update the guide line
        if (guideLine != null && guideLine.enabled)
        {
            guideLine.SetPosition(0, RealButtonGuide.transform.position);
            guideLine.SetPosition(1, labelCanvas.transform.position);
        }
    }

    public void SetVisible(bool visible, bool instant = false)
    {
        Initialize(); // Ensure initialization before changing visibility

        // Check if the Canvas and CanvasGroup are assigned
        if (labelCanvas == null || _canvasGroup == null)
        {
            Debug.LogWarning($"[ControllerLabel] Canvas or CanvasGroup is missing in {gameObject.name}. Cannot change visibility.", this);
            return;
        }

        // Stop any ongoing fade coroutine before starting a new one
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

        if (instant)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            labelCanvas.gameObject.SetActive(visible);
            if (guideLine) guideLine.enabled = visible;
            return;
        }

        _fadeCoroutine = StartCoroutine(FadeRoutine(visible));
    }

    private IEnumerator FadeRoutine(bool fadeIn)
    {
        labelCanvas.gameObject.SetActive(true);
        if (guideLine) guideLine.enabled = true;

        float start  = _canvasGroup.alpha;
        float target = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeCurve.Evaluate(elapsed / fadeDuration);
            _canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }

        _canvasGroup.alpha = target;
        if (!fadeIn)
        {
            labelCanvas.gameObject.SetActive(false);
            if (guideLine) guideLine.enabled = false;
        }
    }
}