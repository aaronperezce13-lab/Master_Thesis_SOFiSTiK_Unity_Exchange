/*
----------------------------------------------------------------------------------------------------------
Script: CSVParser.cs
Author: Aaron Perez Herrera
Description:    This script parses structural data from CSV files, extracting load cases, nodal 
                coordinates, elements IDs, Position Nodes, and force results into a robust data Structure
                for Unity.
AI Assistance:  The core parsing logic, error handling, and data structuring were develped by the author.
                Google Gemini (2026) assisted in refactoring the validation logic to store all errors
                during parsing process in "errors" list.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

public class CSVParser
{
    // Define a data structure to hold the parsed data
    public static List<SampleStructuralData> ParseCSV(string textData)
    {
        // List to hold the parsed data
        List<SampleStructuralData> dataList = new List<SampleStructuralData>();
        
        // AI-Assisted Implementation: The error list generation and final plot to user for validation
        // of the process was provided by Gemini 
        // List to store errors in Parsing process
        List<string> errors = new List<string>();
        
        // Check if the input text data is null or empty
        if (string.IsNullOrWhiteSpace(textData))
        {
            Debug.LogError("CSVParser: Input text data is null or empty.");
            return dataList;
        }

        // Read all lines from the CSV file
        try
        {
            // Split the text data into lines, handling different newline characters
            string[] lines = textData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++) // skip the header line
            {
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                // Split the line into values based on commas
                string[] values = lines[i].Split(',');
                
                // Ensure there are enough values in the line - we expect at least 8 based on the data structure
                if (values.Length != 8)
                {
                    errors.Add($"Error in line {i + 1}, expected exactly 8 values, but found {values.Length}.");
                    continue;
                }

                // Parse each value accordingly
                (float f_lc, bool lc_correct) = ParseFloat(values[0]);
                int lc = (int)f_lc;
                if (lc_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for Load Case");
                }

                (float f_nodeNumber, bool nodeNumber_correct) = ParseFloat(values[1]);
                int nodeNumber = Mathf.RoundToInt(f_nodeNumber);
                if (nodeNumber_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for Node Number");
                }

                (float x, bool x_correct) = ParseFloat(values[2]);
                if (x_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for X coordinate");
                }

                (float y, bool y_correct) = ParseFloat(values[3]);
                if (y_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for Y coordinate");
                }

                (float z, bool z_correct) = ParseFloat(values[4]);
                if (z_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for Z coordinate");
                }
                
                (float f_elementId, bool elementID_correct) = ParseFloat(values[5]);
                int elementId = (int)f_elementId;
                if (elementID_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for Element Id");
                }

                (float f_posNode, bool posNode_correct) = ParseFloat(values[6]);
                int positionNode = (int)f_posNode;
                if (!posNode_correct)
                {
                    errors.Add($"Line {i+1}: Invalid value for Position Node");
                }

                (float result_f, bool result_correct) = ParseFloat(values[7]); 
                if (result_correct != true)
                {
                    errors.Add($"Line {i+1}: Invalid value for result force");
                }

                // Create a new SampleStructuralData object and add it to the list
                dataList.Add(new SampleStructuralData(lc, nodeNumber, x, y ,z, elementId, positionNode, result_f));
            }
        }
        
        // Print error message if there's an issue in parsing the CSV file
        catch (Exception e)
        {
            errors.Add($"Error parsing CSV: {e.Message}");
        }

        // Report all errors after the complete parsing process
        if (errors.Count > 0)
        {
            Debug.LogError(
                $"CSVParser: Parsing completed with {errors.Count} error(s):\n" +
                string.Join("\n", errors)
            );
            // Prevent use of incomplete/invalid data
            return new List<SampleStructuralData>();
        }

        return dataList;
    }

    // Method to parse float values 
    private static (float value, bool isCorrect) ParseFloat(string value)
    {   
        // Use InvariantCulture to ensure consistent parsing
        if (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return (result, true);
        
        // if there is an error send an error message

        return (0f, false);
    }
}
