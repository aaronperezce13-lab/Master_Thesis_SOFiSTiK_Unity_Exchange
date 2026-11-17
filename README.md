# Master_Thesis_SOFiSTiK_Unity_Exchange
A pipeline for immersive visualization of structural data from SOFiSTiK, using C# coding in Unity (program without licensing cost), to improve the interpretation and manipulation of results in multidisciplinary work environments.

Developed as part of a Master's Thesis in Digital Engineering at **Bauhaus-Universität Weimar**.

---

## 📦 Download Full Unity VR Project (Release)

Due to GitHub's file size limitations, the complete ready-to-open Unity VR project package (including scenes, prefabs, and configured plugins) is hosted in the repository's **Releases** section:

* 👉 **[Download Complete Unity VR Project](https://github.com/aaronperezce13-lab/Master_Thesis_SOFiSTiK_Unity_Exchange/releases/tag/v1.0.0)**

*(Extract the downloaded `.zip` archive which contain all necessary codes and projects).*

---

## 📁 Repository Structure

```text
├── 1. SOFiSTiK Teddy/
│   └── Export_IFC_and_Results.dat    # Main CADINP/Teddy routine to trigger export & container generation
├── 2. ICDD Container and Config/
│   ├── config_files/
│   │   └── Config.yaml               # Central configuration file (paths, SOFiSTiK version, load cases)
│   └── ...                           # Python scripts for CDB extraction, RDF graph generation & JSON manifest
└── 3. Unity/VR app/
    └── ...                           # Unity C# scripts (icddSceneBuilder.cs, Get_Plot.cs) & VR project files
```

## Prerequisites & Software Environment
To execute the complete workflow, ensure the following tools are installed:
- SOFiSTiK Structural Desktop / FEA (with CDB Interfaces / API available)
- Python 3.x with required libraries (ifcopenshell, rdflib, pyyaml, numpy)
- Unity Hub & Unity Editor (configured with XR Interaction Toolkit and OpenXR)
- Meta Quest 3 (or compatible OpenXR headset) for standalone VR deployment (optional: can also be tested directly in the Unity Editor via the XR Device Simulator, turn off simulator GameObject to use real VR lenses)

## Step-by-Step Execution WorkflowStep 0: 
# Step 0: Configure Project Paths (First-Time Setup or New Project)
Before running the pipeline on a new machine or with a different structural model:
- Navigate to 2. ICDD Container and Config/config_files/ and open Config.yaml.
- Update the SOFiSTiK installation directory, project-specific file paths, and the target Load Cases to be exported.

# Step 1: Export & ICDD Container Generation in SOFiSTiK
1. Open your calculated structural model in SOFiSTiK.
2. Load Export_IFC_and_Results.dat (located in 1. SOFiSTiK Teddy/) into the Project Navigator and run it as the final calculation step.
3. Executing this Teddy routine automatically performs the following operations:Exports the structural geometry in IFC2x3 format using Coordination View 2.0 (CV 2.0).Extracts nodal internal forces from the SOFiSTiK CDB into structured .csv files. Maps internal SOFiSTiK Element IDs to their corresponding IFC GlobalId identifiers.Generates the ISO 21597 RDF metadata files (Index.rdf and linkset .rdf) defining sub-document semantic links.Packages and compresses all payload documents and linksets into a standardized ICDD container (.icdd / .zip) at the configured output directory.Generates and exports unity_manifest.json so Unity can parse the semantic links without a native SPARQL engine.
# Step 2: Automated Scene Reconstruction in Unity
1. Download and extract the Unity project from the Releases section (Tags section) and open it via Unity Hub.
2. In the Unity Editor top menu bar, navigate to ICDD Container $\rightarrow$ Import ICDD Container.
3. This custom editor tool automatically decompresses the ICDD container, imports and triangulates the IFC geometry, attaches the visualization scripts to each structural member, and binds the corresponding analytical result datasets via the JSON manifest.
# Step 3: Interactive VR Visualization & Deployment
- In-Editor Testing: Press the Play button in the Unity Editor to inspect the 3D force diagrams, filter load cases, highlight local maxima/minima, and measure distances using the simulated XR rig.
- Standalone Headset Deployment: Before generating the Buld or running the app go to hierarchy of objects in Scene and turn off the "XR Device Simulator".

## Author
Aaron Perez HerreraM.Sc. Digital Engineering — Bauhaus-Universität Weimar
