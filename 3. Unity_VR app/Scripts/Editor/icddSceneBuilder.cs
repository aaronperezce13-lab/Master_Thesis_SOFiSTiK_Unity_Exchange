/*
----------------------------------------------------------------------------------------------------------
Script: icddSceneBuilder.cs
Author: Aaron Perez Herrera
Description:    An Editor-time automation script that automates the deployment of the ICDD container in 
                Unity. It extracts the ZIP archive, triggers the IFC geometry import, parses the JSON 
                manifest, and recursively attaches and configures the "Get_Plot.cs" component to the 
                imported structural elements.
AI Assistance:  The overarching deployment logic, mapping algorithm, and UI configuration were developed 
                by the author. Google Gemini (2026) was utilized as a syntax assistant to understand and
                correctly implement Unity Editor-specifics APIs, specifically using the #if 
                (UNITY_EDITOR) preprocessor directivie, the [MenuItem] attribute, and tracking 
                instantiated GameObjects across the active scene.
----------------------------------------------------------------------------------------------------------
*/

#if (UNITY_EDITOR)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GluonGui.Dialog;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// This script is responsible for building the scene in Unity based on the ICDD container and the mapping information (Automatically).
/// It loads the icdd container doing next steps:
/// 1. Extract the .icdd file information [IFC model + CSV results] and read unity_manifest.json
/// 2. Trigger import of the IFC model into Unity (using the existing IFC importer plugin)
/// 3. Move across imported IFC elements and assign them the "Get_Plot.cs" script.
/// </summary>

public static class icddSceneBuilder
{
    // NOTE - Names of the ICDD Container and manifest
    private const string ICDD_CONTAINER_NAME = "ICDD Container/SteelFrame_CV_Final.ifc.icdd";
    private const string MANIFEST_FILE_NAME = "unity_manifest.json";
    private const string RESOURCES_PATH = "Assets/Resources";
    
    // AI-Assited Implementation: The usage of the [MenuItem] attribute and Unity scene tracking 
    // (GetActiveScene().GetRootGameObjects) to isolate newly imported IFC hierarchies was guided 
    // by Gemini.

    [MenuItem("ICDD Container/Import ICDD Container")]
    public static void ImportIcddContainer()
    {
        // Paths of the ICDD container and the extraction folder
        string icddPath = Path.Combine(RESOURCES_PATH, ICDD_CONTAINER_NAME);
        string extractPath = Path.Combine(RESOURCES_PATH, "icdd_extracted");
        if (!File.Exists(icddPath))
        {
            Debug.LogError($"ICDD Container not found at path: {icddPath}");
            return;
        }

       // Delete the extraction folder if it exists, then extract the ICDD container
       if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, recursive: true);
        System.IO.Compression.ZipFile.ExtractToDirectory(icddPath, extractPath);
 
        // Step 1: Extract the IFC file and check it exists in the extracted folder and get names csv files
        string docsFolder = Path.Combine(extractPath, "Payload Documents");
        string ifcFile = Directory.GetFiles(docsFolder, "*.ifc").FirstOrDefault();

        if (ifcFile == null)
        {
            Debug.LogError($"IFC file not found in folder: {docsFolder}");
            return;
        }

        var namesCsv = new List<string>();
        string[] csvFiles = Directory.GetFiles(docsFolder, "*.csv");
        foreach (var csvDoc in csvFiles)
        {
            Debug.Log($"Found CSV file: {Path.GetFileName(csvDoc)}");
            string name = Path.GetFileName(csvDoc);
            name = Path.GetFileNameWithoutExtension(name);
            namesCsv.Add(name);
        }


        // step 2: Extract the unity_manifest.json file and check it exists
        var manifestFile = LoadManifest();

        // step 3: Register already existing GameObjects to detect the new one after the import and assign the Get_Plot.cs script to the new GameObject and its children 
        var rootsBefore = new HashSet<GameObject>(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
 
        // step 4: Trigger the IFC import using the existing IfcFileLoader class (from MenuEntries.cs)
        var fileLoader = new IfcFileLoader();
        fileLoader.LoadIfcFile(ifcFile);

        // Step 5: After the import, find the new root GameObject and assign the Get_Plot.cs script to it and its children
        var rootsAfter = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var newRoot = rootsAfter.FirstOrDefault(go => !rootsBefore.Contains(go));
 
        if (newRoot == null)
        {
            Debug.LogError("It was not possible to detect the new root GameObject after the import. " +
                            "Check if the IFC import is Async.");
            return;
        }
 
        // Step 6: Read CSV file and get names for dropdown options (for the UI) and assign them to the Get_Plot.cs script
        var csvFileName = namesCsv[0];
        var points = LoadDataFromCSV(csvFileName);
        var loadCaseNames = points.Select(p => p.x.ToString()).Distinct().ToList();
        var memberIds = points.Select(p => p.y.ToString()).Distinct().ToList();
        memberIds.Insert(0, "All Structure"); // Add "All" option at the beginning of the list

        // Step 7: Recursively traverse the hierarchy of GameObjects and assign the Get_Plot.cs script to each element that has a matching entry in the manifest.
        AttachGetPlotRecursive(newRoot.transform, manifestFile, loadCaseNames, memberIds, namesCsv);
    }
 
    private static void AttachGetPlotRecursive(Transform node, Dictionary<string, ManifestElement> manifest, List<string> loadCaseNames, List<string> memberIds, List<string> namesCsv)
    {
        int attached = 0, skipped = 0;
        AttachRecursiveInternal(node, manifest, loadCaseNames, memberIds, namesCsv, ref attached, ref skipped);
        Debug.Log($"ICDD import completed. Get_Plot assigned to {attached} elements " +
                  $"({skipped} IFC elements without results in the manifest were skipped).");
    }
 
    // Method to recursively traverse the hierarchy of GameObjects and assign the Get_Plot.cs script to each element that has a matching entry in the manifest.
    private static void AttachRecursiveInternal(
        Transform node, Dictionary<string, ManifestElement> manifest,
        List<string> loadCaseNames, List<string> memberIds, List<string> namesCsv,
        ref int attached, ref int skipped)
    {
        var productData = node.GetComponent<IfcProductData>();
 
        if (productData != null && productData.IfcClass == "IfcBeam")
        {
            // Obtain curren ManifestElement
            if (manifest.TryGetValue(productData.Id, out var resultInfo))
            {
                var getPlot = node.GetComponent<Get_Plot>();
                if (getPlot == null)
                    getPlot = node.gameObject.AddComponent<Get_Plot>();
 
                var UiController = node.GetComponent<PlotUIController>();
                if (UiController == null)
                    UiController = node.gameObject.AddComponent<PlotUIController>();

                var teleportController = node.GetComponent<TeleportController>();
                if(teleportController == null)
                    teleportController = node.gameObject.AddComponent<TeleportController>();
                
                // NOTE: Variables inside Get_Plot.cs to be assigned with the information from the manifest
                // UI Settings
                // Assign InfoUI
                GameObject infoUI = GameObject.Find("Info_UI");
                DiagramTooltipUI tooltipUI = infoUI.GetComponent<DiagramTooltipUI>();
                getPlot.tooltipUI = tooltipUI;
                // Assing Max and Min tooltip prefabs from Prefabs folder to the Get_Plot script
                GameObject maxTooltipPrefab = Resources.Load<GameObject>("Prefabs/Max_UI");
                getPlot.maxtooltipUIPrefab = maxTooltipPrefab;
                GameObject minTooltipPrefab = Resources.Load<GameObject>("Prefabs/Min_UI");
                getPlot.mintooltipUIPrefab = minTooltipPrefab;

                // ICDD Settings
                getPlot.icddElementId = resultInfo.elementId;

                // Send the linkset for current element and index position for each load case and csv file
                getPlot.manifestElement = resultInfo;

                // Selectable Points Settings
                getPlot.pointCircleRadius = 0.02f;
                Material diagCircleMaterial = Resources.Load<Material>("Materials/Diagram Point Material");
                getPlot.diagCircleMaterial = diagCircleMaterial;
                Material elementCircleMaterial = Resources.Load<Material>("Materials/Element Point Material");
                getPlot.elementCircleMaterial = elementCircleMaterial;
                getPlot.pointAlpha = 0.5f;

                // Filter Settings (Only for control current state)
                getPlot.filterMemberId = -1;

                // CSV file Settings
                getPlot.csvFileName = "Bending_Moment_Y";
                getPlot.forceValue = "KNm";
                getPlot.loadCaseName = 1;

                // Plot Settings
                getPlot.momentScale = 0.04f;
                Material plotMaterial = Resources.Load<Material>("Materials/Line Material");
                getPlot.plotMaterial = plotMaterial;

                // Middle Line Settings
                getPlot.middleLinePlot = false;
                Material CenterLineMaterial = Resources.Load<Material>("Materials/MiddleLine Material");
                getPlot.centerLineMaterial = CenterLineMaterial;
                getPlot.centerLineWidth = 0.05f;

                // Teleport Settings
                getPlot.distanceMultiplier = 0.9f;
                getPlot.viewXZ = true;
                getPlot.viewYZ = false;
                getPlot.flightDuration = 2f;
                getPlot.arcHeightFactor = 0.3f;

                // NOTE: Variables inside PlotUIController.cs
                UiController.m_GetPlot = getPlot;

                TMP_Dropdown typeForceDropdown = GameObject.Find("Dropdown_TypeForce").GetComponent<TMP_Dropdown>();
                PopulateDropdown(typeForceDropdown, namesCsv, 0);
                UiController.TypeForceDropdown = typeForceDropdown;

                TMP_Dropdown loadCaseDropdown = GameObject.Find("Dropdown_LoadCase").GetComponent<TMP_Dropdown>();
                PopulateDropdown(loadCaseDropdown, loadCaseNames, 0);
                UiController.LoadCaseDropdown = loadCaseDropdown;

                var filterMemberDropdown = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "Dropdown_FilterID");
                if (filterMemberDropdown != null)
                {
                    var dropdown = filterMemberDropdown.GetComponent<TMP_Dropdown>();
                    PopulateDropdown(dropdown, memberIds, 0);
                    UiController.FilterMemberDropdown = dropdown;
                }

                Toggle middleLineToggle = GameObject.Find("Toggle_MiddleLine").GetComponent<Toggle>();
                UiController.MiddleLinePlotToggle = middleLineToggle;

                var extremeValuesToggleObj = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "Toggle_ExValue");
                if (extremeValuesToggleObj != null)
                {
                    var toggle = extremeValuesToggleObj.GetComponent<Toggle>();
                    UiController.ExtremeValuesToggle = toggle;
                }

                var xzToggle = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "Toggle_XZ");
                if (xzToggle != null)
                {
                    var toggle = xzToggle.GetComponent<Toggle>();
                    UiController.ViewXZToggle = toggle;
                }

                var yzToggle = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "Toggle_YZ");
                if (yzToggle != null)
                {
                    var toggle = yzToggle.GetComponent<Toggle>();
                    UiController.ViewYZToggle = toggle;
                }

                Slider momentScaleSlider = GameObject.Find("Slider_Scale").GetComponent<Slider>();
                UiController.MomentScaleSlider = momentScaleSlider;

                var interpolationStepsSlider = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "Slider_Interpol");
                if (interpolationStepsSlider != null)
                {
                    var slider = interpolationStepsSlider.GetComponent<Slider>();
                    UiController.InterpolationStepsSlider = slider;
                }

                Slider centerLineWidthSlider = GameObject.Find("Slider_LineWidth").GetComponent<Slider>();
                UiController.CenterLineWidthSlider = centerLineWidthSlider;

                Slider pointAlphaSlider = GameObject.Find("Slider_Opacity").GetComponent<Slider>();
                UiController.PointAlphaSlider = pointAlphaSlider;

                Slider pointSizeSlider = GameObject.Find("Slider_Point").GetComponent<Slider>();
                UiController.PointSizeSlider = pointSizeSlider;

                EditorUtility.SetDirty(getPlot);
                attached++;
            }
            else
            {
                skipped++;
            }
        }
 
        foreach (Transform child in node)
            AttachRecursiveInternal(child, manifest, loadCaseNames, memberIds, namesCsv, ref attached, ref skipped);
    }
 
    // Load the unity_manifest.json file and return a dictionary mapping IFC GlobalIds to their corresponding ManifestElement.
    private static Dictionary<string, ManifestElement> LoadManifest()
    {
        // Obtain path to the manifest file in Resources and read it
        string path = Path.Combine(RESOURCES_PATH, MANIFEST_FILE_NAME);
        string json = File.ReadAllText(path);

        // Transform it to an Object
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        // Store all elements inside the mapping file (based Element ID)
        var elements = (Newtonsoft.Json.Linq.JObject)root["elements"];
 
        // Generate the empty final dictionary
        var result = new Dictionary<string, ManifestElement>();
        // Move across the entire mapping file
        foreach (var pair in elements)
        {
            // Obtain the data of the current element
            var data = (Newtonsoft.Json.Linq.JObject)pair.Value;
            // Create the ManifestElement to store the values
            var el = new ManifestElement
            {
                // Save as separate variable the current elementId
                elementId = data["elementId"]?.ToString(),
                // Generate the results variable where resides LoadCase and Indexes
                results = new List<ManifestResult>()
            };

            // Level 1: csvFileName to Load Case
            var resultsObj = (Newtonsoft.Json.Linq.JObject)data["results"];
            if (resultsObj != null)
            {
                // Move across csv files in the current ElementId
                foreach (var csvEntry in resultsObj)
                {
                    // Store as variables current csv file name and Load Cases in it
                    string csvFileName = csvEntry.Key;
                    var loadCaseObj = (Newtonsoft.Json.Linq.JObject)csvEntry.Value;

                    // Level 2: Load Case to List. Storing LoadCases in a new dictionary with index positions in csv files as lists inside
                    var loadCaseDict = new Dictionary<string, List<int>>();
                    if (loadCaseObj != null)
                    {
                        // Move across all Load Cases
                        foreach (var lcEntry in loadCaseObj)
                        {
                            // Save as variable current load case
                            string loadCase = lcEntry.Key;
                            // Transfor the list of index of JSON in a List<int>
                            List<int> rowIndex = lcEntry.Value.ToObject<List<int>>();
                            
                            // Store index of positions an all data for ManifestResult
                            el.results.Add(new ManifestResult
                            {
                                csvFileName = csvFileName,
                                loadCase = loadCase,
                                rowIndices = rowIndex
                            });
                        }
                    }
                }
            }

            // Store the complete element (for current Element Id) in final dictionary
            result[pair.Key] = el;
        }

        return result;
    }

    // Method to populate a TMP_Dropdown with a list of options and set a default selected index.
    private static void PopulateDropdown(TMP_Dropdown dropdown, List<string> options, int defaultIndex = 0)
    {
        if (dropdown == null) return;

        dropdown.ClearOptions(); // Clear existing options in the dropdown
        dropdown.AddOptions(options); // Add the list of strings passed as a parameter
        dropdown.value = defaultIndex; // Select the default option
        dropdown.RefreshShownValue(); // Update the visible text in the UI

        EditorUtility.SetDirty(dropdown); // Mark the object as modified in the Unity Editor
    }

    // Method to load points from the specified CSV file and Obtain the variables for dropboxes
    private static List<Vector2> LoadDataFromCSV(string csvFileName)
    {
        // Get the path of the current csv file
        string csvPath = $"icdd_extracted/Payload documents/{csvFileName}";

        // Store

        // Load the CSV file from the Resources folder using the specified file name
        TextAsset csvAsset = Resources.Load<TextAsset>(csvPath);
        if (csvAsset == null)
        {
            Debug.LogError($"CSV file '{csvFileName}' not found in {csvPath}. Please check the file name and ensure it is placed in the Resources folder.");
            return new List<Vector2>();
        }

        // NOTE: Check if current Structure document is well imported
        var CsvData = CSVParser.ParseCSV(csvAsset.text);
        var Points = new List<Vector2>();
        

        foreach (var data in CsvData)
        {
            Points.Add(new Vector2(data.LC, data.ELEMENT_ID));
        }

        if (CsvData.Count == 0)
        {
            Debug.LogWarning($"No data found in CSV file '{csvFileName}'. Please check the file content.");
        }

        return Points;
    }
}
#endif
