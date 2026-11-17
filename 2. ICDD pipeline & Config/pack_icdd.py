"""
----------------------------------------------------------------------------------------------------------
Script: pack_icdd.py
Author: Aaron Perez Herrera
Description:    This script assembles the generated files (IFC, CSVs, RDF graphs and Ontologies) into a
                standarized ICDD Container in .zip format, adhering to the IDO 21597-1 folder structure
                requirements.
----------------------------------------------------------------------------------------------------------
"""

import os
import pathlib
import zipfile
import yaml
#Force ZIP64 compression even if the container is small
zipfile.ZIP64_LIMIT = 0
 
def main():
    # load config yaml information
    ifc_name, path_icdd = load_config_yaml()
    # get the current directory of the script
    root = pathlib.Path(__file__).parent
    path_icdd = pathlib.Path(path_icdd)
    
    # define paths for required files and folders
    ifc_path = root / "output" / ifc_name
    csv_folder = root / "output"
    link_index_rdf_path = root / "output" / "Index.rdf"
    link_content_rdf_path = root / "output" / "Link-Content.rdf"
    ontology_folder = root / "Ontology resources"
    output_folder = path_icdd / "ICDD Container"
    output_icdd_path = output_folder / f"{ifc_name}.icdd"
 
    # debugging information: Check if the required files exist and print their paths
    required = [ifc_path, link_index_rdf_path, link_content_rdf_path]
    for path in required:
        if not path.exists():
            raise FileNotFoundError(f"Required file not found: {path}")
 
    csv_files = list(csv_folder.glob("*.csv"))
    if not csv_files:
        raise FileNotFoundError(f"No CSV files found in: {csv_folder}")
    print(f"CSV files found: {[file.name for file in csv_files]}")
 
    ontology_files = list(ontology_folder.glob("*.rdf"))
    if not ontology_files:
        raise FileNotFoundError(
            f"No ontology files (.rdf) found in: {ontology_folder}. "
            "Place actual copies of Container.rdf and Linkset.rdf there before packaging."
        )
 
    # Create the output folder if it doesn't exist
    output_folder.mkdir(parents=True, exist_ok=True)
 
    # Zip the files into an ICDD container
    with zipfile.ZipFile(output_icdd_path, "w", compression=zipfile.ZIP_DEFLATED, allowZip64=True) as zf:
        # index.rdf need to be at the root of the zip file (ISO 21597-1 require it)
        zf.write(link_index_rdf_path, arcname="Index.rdf")
 
        # Ontology Resources
        for onto_file in ontology_files:
            zf.write(onto_file, arcname=f"Ontology resources/{onto_file.name}")
 
        # Payload Documents (IFC + all the CSV files)
        zf.write(ifc_path, arcname=f"Payload documents/{ifc_path.name}")
        for csv_file in csv_files:
            zf.write(csv_file, arcname=f"Payload documents/{csv_file.name}")
 
        # Payload Triples
        zf.write(link_content_rdf_path, arcname=f"Payload triples/{link_content_rdf_path.name}")
 
    # Debugging information: Print summary of the ICDD container creation
    print(f"ICDD Container created: {output_icdd_path}")
    print(f"  - Index.rdf: 1")
    print(f"  - Ontology Resources: {len(ontology_files)} file(s)")
    print(f"  - Payload Documents: {1 + len(csv_files)} file(s) (1 IFC + {len(csv_files)} CSV)")
    print(f"  - Payload Triples: 1 file (Link-Content.rdf)")
 
 
def load_config_yaml():
    # Obtain the current directory of the script
    current_dir = pathlib.Path(__file__).parent
    
    # config file path
    config_path = current_dir / "config_files" / "Config.yaml"
    # load configuration from YAML file
    with open(config_path, "r", encoding="utf-8") as file:
        config = yaml.safe_load(file)
        # store each variable from the config file into a separate variable
        ifc_name = config['ifc_name']
        path_icdd = config['ICCD_path']
    
    return ifc_name, path_icdd
 
if __name__ == "__main__":
    main()