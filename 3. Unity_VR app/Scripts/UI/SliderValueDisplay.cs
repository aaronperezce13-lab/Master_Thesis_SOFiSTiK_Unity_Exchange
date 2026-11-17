/*
----------------------------------------------------------------------------------------------------------
Script: SliderValueDisplay.cs
Author: Aaron Perez Herrera
Description:    A UI utility script taht connects Unity Canvas Sliders to TextMeshPro text elements, 
                dynamically updating the displayed numerical values in real-time during user interaction.
AI Assistance:  The UI tracking logic was degined by the author. Claude (Anthropic, 2026) assisted with 
                the C# syntax for subscribing to the Unity event listener using a lambda expression.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderValueDisplay : MonoBehaviour
{
    [SerializeField]  private Slider GO_Slider;
    [SerializeField] private TextMeshProUGUI ValueText_Slider;

    // AI-Assisted Implementation: The syntax for the lambda expression linking the slider float value 
    // directly to the text update method was provided by Claude.
    // Access the Text component of the TextMeshProUGUI
    void Start()
    {
        GO_Slider.onValueChanged.AddListener((value) => UpdateText(value));
    }

    void UpdateText(float value)
    {
        string text = value.ToString("0.00"); 
        ValueText_Slider.text = text;
    }
}