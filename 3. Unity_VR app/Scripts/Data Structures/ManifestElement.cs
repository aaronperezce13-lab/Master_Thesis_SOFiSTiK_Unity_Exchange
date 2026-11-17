/*
----------------------------------------------------------------------------------------------------------
Script: ManifestElement.cs
Author: Aaron Perez Herrera
Description:    This script defines the serializable data structures used to parse and store the JSON 
                manifest "unity_manifest.json" within Unity. It bridges the semantic ICDD element IDs 
                with their respective load cases and CSV row indices.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections.Generic;

// Data Structure to be used to asociate each Element Id with relevant data and positions
[System.Serializable]
public class ManifestElement
{
    public string elementId;
    public List<ManifestResult> results;
}

// Data Structure to store the results associated with a manifest element
[System.Serializable]
public class ManifestResult
{
    public string csvFileName;
    public string loadCase;
    public List<int> rowIndices;
}