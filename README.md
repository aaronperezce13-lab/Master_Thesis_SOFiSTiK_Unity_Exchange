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
