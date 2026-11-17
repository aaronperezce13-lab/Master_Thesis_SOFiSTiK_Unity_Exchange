/*
----------------------------------------------------------------------------------------------------------
Script: SemanticDataLoader.cs
Author: Aaron Perez Herrera
Description:    This script connects semantic manifest mapping with the parsed CSV structural data. 
                Translates coordinates from SOFiSTiK to Unity space and constructs the ordered physical 
                element geometry.
AI Assistance:  The data flow and coordinate mapping architecture were designed by the author. Google 
                Gemini (2026) was used as coding assistant to refactor the nodal sorting algorithm 
                (replacing spatial heuristics with explicit topological indexing via "Position Node") and
                implementation of "LocalAxisDirection" vector calculation.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class SemanticDataLoader : MonoBehaviour
{
    public List<Vector4> _pointsOriginal = new List<Vector4>();
    public List<int> _pointsOriginalNodes = new List<int>();
    public List<SampleStructuralData> loadCaseData = new List<SampleStructuralData>();
    public bool _diagramDirty;
    public bool _extremeValuesDirty;
    public List<Vector4> ExportPoints = new List<Vector4>();
    // Variable to Store local axis
    public Vector3 localAxisDirection;

    // Method to load points from the specified CSV file based on the current load case and member ID
    public void LoadPointsFromCSV(string csvFileName, string icddElementId, int loadCaseName, ManifestElement manifestElement, List<Vector4> Points, float DiscontinuityEpsilon)
    {
        Debug.Log($"Loading CSV file from Resources + {csvFileName} in member {icddElementId} for load case {loadCaseName}");

        string csvPath = $"icdd_extracted/Payload documents/{csvFileName}";

        // Load the CSV file from the Resources folder using the specified file name
        TextAsset csvAsset = Resources.Load<TextAsset>(csvPath);
        if (csvAsset == null)
        {
            Debug.LogError($"CSV file '{csvFileName}' not found in {csvPath}. Please check the file name and ensure it is placed in the Resources folder.");
            return;
        }

        // Load from CSV file and parse the data into a list of SampleStructuralData objects
        loadCaseData = CSVParser.ParseCSV(csvAsset.text);

        // Store the current member ID from the ICDD element ID for filtering the data
        var currentMemberId = int.Parse(icddElementId.Trim());

        // Allways clear points before loading new data to avoid duplicates
        Points.Clear();

        // Store Load Case as string and csv file with the .csv extension added
        string targetLC = loadCaseName.ToString().Trim();
        string targetCSV = csvFileName.Trim() + ".csv";
        
        // List to store coord and node number
        List<Vector4> pts = new List<Vector4>();
        List<int> nodes = new List<int>();

        // Debug: Check if the manifest contains the current member ID
        if (manifestElement == null)
        {
            Debug.LogError($"Manifest data not found for Member ID {currentMemberId}.");
            return;
        }
        
        // Search indices that match the current load case and CSV file in the manifest
        List<int> rowIndices = null;

        Debug.Log($"Entries for Member ID {currentMemberId} in manifest: " + string.Join(" | ", manifestElement.results.Select(r => $"[{r.csvFileName}/{r.loadCase}]")));
            
        foreach (var manifestEl in manifestElement.results)
        {
            if (manifestEl.loadCase == targetLC && manifestEl.csvFileName == targetCSV)
            {
                // Store the row indices from the manifest for the current load case and CSV file
                rowIndices = manifestEl.rowIndices;
                break; // Exit the loop once the relevant row indices are found
            }
        }
        // Check if any row indices were found for the current load case and CSV file in the manifest
        if (rowIndices == null)
        {
            Debug.LogError($"No row indices found for Member ID {currentMemberId} in Load Case {loadCaseName} and CSV file {targetCSV}.");
            _pointsOriginal.Clear();
            _pointsOriginalNodes.Clear();
            _diagramDirty = true;
            _extremeValuesDirty = true;
            return;
        }

        // Temporal list to store raw data
        List<(Vector4 p, int n, int pos)> rawData = new List<(Vector4, int, int)>();

        foreach (var index in rowIndices)
        {
            // Check index is in range of values
            if (index < 0 || index >= loadCaseData.Count)
            {
                Debug.LogError($"Index {index} is out of range for the load case data.");
                continue;
            }

            var data = loadCaseData[index];
            
            // Fix coordenate import values (Diferent axis for vertical values and direction)
            float unityx = -data.X;
            float unityy = -data.Z;
            float unityz =  data.Y;

            rawData.Add((new Vector4(unityx, unityy, unityz, data.RESULT_F), data.NODE_NUMBER, data.POSITION));

            pts.Add(new Vector4(unityx, unityy, unityz, data.RESULT_F));
            nodes.Add(data.NODE_NUMBER);
        }

        Points = pts;

        // Check if any points were found for the current member ID and load case, and log a warning if none were found
        if (Points.Count == 0)
        {
            Debug.LogWarning($"No data found for Member ID {icddElementId} in Load Case {loadCaseName}. Please check the CSV file and ensure the data is available.");
            _pointsOriginal.Clear();
            _pointsOriginalNodes.Clear();
            _diagramDirty = true;   
            _extremeValuesDirty = true;
            return;
        }

        // AI-Assisted Implementation: The explicit topological sorting logic utilizing the imported
        // Position sequence, and the subsequent local axis calculation, were refactored with help of
        // Gemini
        
        // Sort based Position of each node
        var sortedData = rawData.OrderBy(c => c.pos).ToList();
        // Obtain Local axis of element
        Vector3 startPos = new Vector3(sortedData[0].p.x, sortedData[0].p.y, sortedData[0].p.z);
        Vector3 endPos = new Vector3(sortedData[sortedData.Count - 1].p.x, sortedData[sortedData.Count - 1].p.y, sortedData[sortedData.Count - 1].p.z);

        if (Vector3.Distance(startPos, endPos) > 0.0001f)
        {
            localAxisDirection = (endPos - startPos).normalized;
        }
        else
        {
            localAxisDirection = Vector3.zero;
        }

        // Store the sorted points back into the Points list for further processing and plotting
        Points = sortedData.Select(c => c.p).ToList();
        Debug.Log($"Vectors inside: {Points}");
        ExportPoints = Points;

        // Store the original points
        _pointsOriginalNodes = sortedData.Select(c => c.n).ToList();
        _pointsOriginal = new List<Vector4>(Points);

        // Mark the diagram and extreme values as dirty to trigger re-plotting and updating of extreme value indicators
        _diagramDirty = true;
        _extremeValuesDirty = true;
    }

}
