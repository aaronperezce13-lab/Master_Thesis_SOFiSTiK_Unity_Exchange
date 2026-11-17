"""
----------------------------------------------------------------------------------------------------------
Script: export_Unity_manifest.py
Author: Aaron Perez Herrera
Description:    This script parses the RDF graph (Index.rdf and Link-Content.rdf) of the ICDD container
                to extract semantic links and serializes them into a likewise JSON mapping 
                "unity_manifest.json". This step acts as a bridge between the ICDD container and Unity 
                (Unity lacks native RDF support)
AI Assistance:  The overall architecture and JSON mapping logic were designed by the author. Google 
                Gemini (2026) was used as a coding assistant to refine the "rdflib" syntax for querying 
                the graph. 
----------------------------------------------------------------------------------------------------------
"""

import json                             # Import the json module for working with JSON data
import os                               # Import the os module for interacting with the operating system
import pathlib                          # Import the pathlib module for working with file paths
import re                               # Import the re module for regular expression operations
import yaml                             # Import the yaml module for working with YAML files
from rdflib import Graph, Namespace     # Import Graph and Namespace classes from the rdflib module for working with RDF graphs and namespaces
from rdflib.namespace import RDF        # Import RDF namespace for RDF.type lookups
 
def main():
    """
    Main function to generate a Unity manifest JSON file from RDF data that can be readed in Unity.
    """
    # Load configuration from YAML file
    [ifc_name, icdd_path, base, ct, ls] = load_config_yaml()
    current_dir = pathlib.Path(__file__).parent
    icdd_path = pathlib.Path(icdd_path)
 
    # Define paths for the required RDF files and the output manifest JSON file
    content_rdf_path = current_dir / "output" / "Link-Content.rdf"
    index_rdf_path = current_dir / "output" / "Index.rdf"
    output_path = icdd_path / "unity_manifest.json"
 
    # Generate the RDF graph by parsing the content and index RDF files
    graph = Graph()
    graph.parse(content_rdf_path, format="xml")
    graph.parse(index_rdf_path, format="xml")
 
    # Build the manifest dictionary from the RDF graph and write it to a JSON file
    manifest = build_manifest(graph, ls, base, ifc_name)
 
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
 
    # Debugging information: Print summary of the manifest generation
    elements = manifest["elements"]
    total_csv_docs = sum(len(e["results"]) for e in elements.values())
    total_load_case_links = sum(
        len(load_cases)
        for e in elements.values()
        for load_cases in e["results"].values()
    )
    total_rows = sum(
        len(rows)
        for e in elements.values()
        for load_cases in e["results"].values()
        for rows in load_cases.values()
    )
 
    print(f"Manifest Generated: {output_path}")
    print(f"  - Elements: {len(elements)}")
    print(f"  - CSV documents linked (Element x CSV): {total_csv_docs}")
    print(f"  - Load Case links (Element x CSV x Load Case): {total_load_case_links}")
    print(f"  - Total rows referenced: {total_rows}")
 
 
def build_manifest(g, ls, base, ifc_file_name):
    """
    Read the .rdf document, build the Graph and store the data in a dictionary.
    """
 
    # Generate empty manifest to store python dictionary
    manifest = {"ifcFile": ifc_file_name, "elements": {}}
    
    # AI-Assisted Implementation: The syntax for querying the loaded rdflib graph to
    # traverse the custom directed links was refined with the assistance of Gemini
 
    # Search for instances of your custom link "hasStructuralResult"
    for link in g.subjects(RDF.type, base.hasStructuralResult):
        # Retrieve directions of Link
        ifc_element = g.value(link, ls.hasFromLinkElement)
        csv_element = g.value(link, ls.hasToLinkElement)
        
        # Ignore elements that not have link direction
        if ifc_element is None or csv_element is None:
            continue
 
        # Store the current IFC element id
        ifc_ident_node = g.value(ifc_element, ls.hasIdentifier)
        if ifc_ident_node is None:
            continue
        
        ifc_guid_literal = g.value(ifc_ident_node, ls.identifier)
        # Ignore elements that does not have IFC element id
        if ifc_guid_literal is None:
            continue
        # Transform IFC element id in a string type
        ifc_guid = str(ifc_guid_literal)
 
        # Store the current CSV file and store only the csv name in string format
        csv_doc_ref = g.value(csv_element, ls.hasDocument)
        csv_doc = str(csv_doc_ref).split("#")[-1] if csv_doc_ref is not None else ""
 
        # On each csvElement search and store the 2 identifiers "Element ID=X" y "Load Case=Y"
        element_id = ""
        load_case = ""
        
        for ident_node in g.objects(csv_element, ls.hasIdentifier):
            field_name = str(g.value(ident_node, ls.identifierField))
            ident_value = str(g.value(ident_node, ls.identifier))
            
            if field_name == "Element ID":
                element_id = ident_value
            elif field_name == "Load Case":
                load_case = ident_value

        # Obtain and sort all the rows of the file for the current combination "Element ID" and "Load Case" for the current element
        row_indices = sorted(int(v) for v in g.objects(csv_element, base.hasRowIndex))
 
        # If element is not present in manifest create it
        if ifc_guid not in manifest["elements"]:
            manifest["elements"][ifc_guid] = {
                "elementId": element_id,
                "results": {}
            }
 
        # Obtain entry of current element
        element_entry = manifest["elements"][ifc_guid]
        
        # Ensure exists current element in manifest
        if not element_entry["elementId"] and element_id:
            element_entry["elementId"] = element_id
 
        # Obtain current results
        results = element_entry["results"]
        
        # Ensure that csv dile exists
        if csv_doc not in results:
            results[csv_doc] = {}
 
        # Store Load Case + Rows in the dictionary
        results[csv_doc][load_case] = row_indices
 
    return manifest
 
 
def load_config_yaml():
    """
        Load variables from a YAML file.
    """
    current_dir = pathlib.Path(__file__).parent
    config_path = current_dir / "config_files" / "Config.yaml"

    with open(config_path, "r", encoding="utf-8") as file:
        config = yaml.safe_load(file)
        ifc_name = config['ifc_name']
        icdd_path = config['ICCD_path']
        base_namespace = Namespace(config['Base'])
        content_namespace = Namespace(config['ct'])
        linkset_namespace = Namespace(config['ls'])

    return ifc_name, icdd_path, base_namespace, content_namespace, linkset_namespace
 
 
if __name__ == "__main__":
    main()