/*
----------------------------------------------------------------------------------------------------------
Script: SampleStructuralData.cs
Author: Aaron Perez Herrera
Description:    This script defines the primary data structure used to encapsulate structural analysis 
                results parsed from the CSV files. It stores nodal coordinates, load cases, element IDs, 
                topological position, and force values for seamless integration into the Unity 
                environment.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System;
using UnityEngine.UIElements;
public class SampleStructuralData
{    
    // Define the fields for the structural data
    public int LC, NODE_NUMBER, ELEMENT_ID, POSITION;
    public float X, Y, Z, RESULT_F;

    // Constructor to initialize the structural data
    public SampleStructuralData(int lc, int nodeNumber, float x, float y, float z, int elementId, int position, float value_f)
    {
        LC = lc;
        NODE_NUMBER = nodeNumber;
        X = x;
        Y = y;
        Z = z;
        ELEMENT_ID = elementId;
        POSITION = position;
        RESULT_F = value_f;
    }
    
}
