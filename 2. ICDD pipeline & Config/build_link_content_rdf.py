"""
-----------------------------------------------------------------------------------------------------------------
Script: build_link_content_rdf.py
Author: Aaron Perez Herrera
Description:    This script generates the "Link-Content.rdf" file for the ICDD Container, establishing 
                DirectBinaryLinks between IFC structural elements and specific rows in CSV result files based
                on Element ID and Load Case.
AI Assistance:  The relational logic and ICDD hierarchical structure were defined by the author. Claude 
                (Anthropic, 2026) was used as a syntax assistant to translate this logic into the Python 
                "rdflib" library, specifically for instanting Namepaces, URIRefs, BNodes, and serilizing final
                RDF/XML graph. 
-----------------------------------------------------------------------------------------------------------------
"""

import json                                                             # Import the json module to handle JSON files
import os                                                               # Import the os module to interact with the operating system
import pathlib                                                          # Import the pathlib module to handle file paths
import pandas as pd                                                     # Import the pandas module to handle CSV files and dataframes
from rdflib import Graph, Namespace, URIRef, Literal, BNode, graph      # Import the rdflib module to handle RDF graphs and related classes
from rdflib.namespace import RDF, RDFS, XSD, OWL                        # Import the RDF, RDFS, and XSD namespaces from rdflib for RDF serialization and validation
import yaml                                                             # Import the yaml module to handle YAML files

def main():
    # Load configuration from YAML file
    [ifc_file_name, base, ls, ct] = load_config_yaml()
    current_dir = pathlib.Path(__file__).parent
    mapping_json = current_dir / "output" / "mapping.json"
    csv_folder = current_dir / "output"
    output_content_rdf = current_dir / "output" / "Link-Content.rdf"

    # Load mapping and CSV data to build the RDF graph
    mapping = load_mapping(mapping_json)
    csv_data = csv_files_in_folder(csv_folder)
 
    # Debugging information: Print the number of elements in mapping.json and the list of CSV files found
    print(f"Elements in mapping.json: {len(mapping)}")
    print(f"CSV files found in folder: {list(csv_data.keys())}")
 
    # Build the RDF graph and serialize it to a Turtle file
    rdf_graph = build_graph(mapping, csv_data, ls, ct, base, ifc_file_name)
    rdf_graph.serialize(destination=output_content_rdf, format="xml")
    print(f"File generated! Total of triples inside: {len(rdf_graph)}")
 
def load_mapping(path):
    """"
    Load mapping [Element ID: IFC GlobalId] from a JSON file.
    """
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)
 
def csv_files_in_folder(folder):
    """
    Load all CSV files in the desired folder into a dictionary of DataFrames of pandas.
    """
    data = {}
    for filename in os.listdir(folder):
        if filename.lower().endswith(".csv"):
            path = os.path.join(folder, filename)
            data[filename] = pd.read_csv(path)
    return data
 
def build_graph(mapping, csv_data, ls, ct, base, ifc_file_name):
    """
    Generate a Graph with the expected hierarchy for the links between ifc and csv witn following structure:
 
    Element ID
        -> csv_file_name
            -> Load Case
                -> [ row 0
                     row 5
                     ... ] (All rows that have same Load Case and Element ID in CSV file)
 
   """
    
    # Generate Graph and bind it with prefix to use
    graph = Graph()
    graph.bind("ls", ls)
    graph.bind("ct", ct)
    graph.bind("base", base)
    graph.bind("owl", OWL)
 
    # AI-Assisted Implementation: The syntatic structure of adding RDF triples
    # (subject, Predicate, Object) using "rdflib" framework was guided by Claude.
    
    # Create the custom link TYPE (hasStructuralResult) as a subclass of DirectedBinaryLink
    # Asociate CSV files with the IFC model
    has_structural_result = base.hasStructuralResult
    # Declare and add triplet (Structure: Subject -> Predicate -> Object) 
    graph.add((has_structural_result, RDF.type, OWL.Class))
    # Declare direction Link
    graph.add((has_structural_result, RDFS.subClassOf, ls.DirectedBinaryLink))
    # Add label "hasStructuralResult" (human readable)
    graph.add((has_structural_result, RDFS.label, Literal("hasStructuralResult", lang="en")))
    # Add a comment to explain purpose of the Linkset
    graph.add((has_structural_result, RDFS.comment, Literal(
        "Relates an IFC structural element to the specific rows in a "
        "results CSV document that correspond to it, filtered jointly by "
        "the 'Element ID' and 'Load Case' columns. Each Link points to a "
        "LinkElement whose ct:hasRowIndex values enumerate the exact "
        "0-based data-row indices (header excluded) that match that "
        "Element ID + Load Case combination in the CSV.", lang="en")))
 
    # Create Custom property "hasRowIndex" to link rows of CSV with corresponding Element IFC
    has_row_index = base.hasRowIndex
    # Declare and add triplet indicating that it is an RDF property
    graph.add((has_row_index, RDF.type, RDF.Property))
    # Add label "hasRowIndex" (human readable)
    graph.add((has_row_index, RDFS.label, Literal("hasRowIndex", lang="en")))
    # Define the domain on which the property is used (in this case, it is used on LinkElement objects)
    graph.add((has_row_index, RDFS.domain, ls.LinkElement))
    # Define the range the property can be used (this case only with integer values)
    graph.add((has_row_index, RDFS.range, XSD.integer))
    # Add a comment to explain purpose of property
    graph.add((has_row_index, RDFS.comment, Literal(
        "0-based index of a data row within "
        "the CSV document referenced by ls:hasDocument on the same "
        "LinkElement. Multiple values on the same LinkElement mean all "
        "those rows share the Element ID + Load Case identified by that "
        "LinkElement's ls:hasIdentifier values.", lang="en")))
 

    # Create the LinkElements for each element in IFC file
    ifc_link_elements = {}
    # Move through the mapping between IFC geometry and SOFISTIK ID 
    for element_id, global_id in mapping.items():
        # Create the identifier of the IFC element
        le = base[f"ifcElement_{element_id}"]
        # Declare and add the identifier as a LinkElement
        graph.add((le, RDF.type, ls.LinkElement))
        # Declare and add the asociated IFC document (Indicate element Id belongs to the ifc)
        graph.add((le, ls.hasDocument, base[ifc_file_name]))
        # Store and add the GlobalId in the Linkset
        ident_node = base[f"idcIdent_{element_id}"]
        graph.add((ident_node, RDF.type, ls.StringBasedIdentifier))
        graph.add((ident_node, ls.identifier, Literal(global_id, datatype=XSD.string)))
        graph.add((le, ls.hasIdentifier, ident_node))
        # Store it in the dictionary in python
        ifc_link_elements[element_id] = le
 
    # Move through all csv file documents in pandas DataFrame format
    for csv_filename, df in csv_data.items():
        # Ensures that csv file rows has the columns "Element ID" and "Load Case" columns -> If it does not have it, skip the row  
        if "Element ID" not in df.columns or "Load Case" not in df.columns:
            print(f"Warning!: '{csv_filename}' does not have columns 'Element ID' " "and/or 'Load Case' ")
            continue
 
        # Store in a variable the reference to csv file
        csv_doc_ref = base[csv_filename]
 
        # Delete rows without an Element ID or Load Case before grouping
        df_clean = df.dropna(subset=["Element ID", "Load Case"]).copy()
        # Transform to int the "Element ID" and "Load Case"
        df_clean["Element ID"] = df_clean["Element ID"].astype(int).astype(str)
        df_clean["Load Case"] = df_clean["Load Case"].astype(int).astype(str)
 
        # Group rows by combinations of “Element ID” and “Load Case”
        grouped = df_clean.groupby(["Element ID", "Load Case"]).groups
 
        # Move across each group
        for (element_id, load_case), row_indices in grouped.items():
            # Ensure that the element exists in the IFC file
            if element_id not in mapping:
                print(f"Warning!: Element ID {element_id} in '{csv_filename}' is not present in the mapping.json.")
                continue

            # Create the LinkElement for the CSV file
            csv_le = base[f"csvRows_{csv_filename}_{element_id}_{load_case}"]
            # Declare it and add the LinkElement
            graph.add((csv_le, RDF.type, ls.LinkElement))
            # Declare and add the asociation with the CSV file
            graph.add((csv_le, ls.hasDocument, csv_doc_ref))
            # Declare and store the identifier 1: Element ID of the current element
            csv_ident_elem = base[f"csvIdent_{csv_filename}_{element_id}_{load_case}_elem"]
            graph.add((csv_ident_elem, RDF.type, ls.StringBasedIdentifier))
            graph.add((csv_ident_elem, ls.identifier, Literal(element_id, datatype=XSD.string)))
            graph.add((csv_ident_elem, ls.identifierField, Literal("Element ID", datatype=XSD.string)))
            graph.add((csv_le, ls.hasIdentifier, csv_ident_elem))
            # Declare and store the identifier 2: Load Case of the current element
            csv_ident_load = base[f"csvIdent_{csv_filename}_{element_id}_{load_case}_load"]
            graph.add((csv_ident_load, RDF.type, ls.StringBasedIdentifier))
            graph.add((csv_ident_load, ls.identifier, Literal(load_case, datatype=XSD.string)))
            graph.add((csv_ident_load, ls.identifierField, Literal("Load Case", datatype=XSD.string)))
            graph.add((csv_le, ls.hasIdentifier, csv_ident_load))
 

            # Declare and store all the rows that have the combination of "Element ID" and "Load Case" for the current "CSV file"
            for row_idx in row_indices:
                graph.add((csv_le, has_row_index, Literal(int(row_idx), datatype=XSD.integer)))
 
            # Create the directional Link that connects the IFC file with each CSV file
            link = BNode()
            # Declare that it is DirectedBinaryLink
            graph.add((link, RDF.type, has_structural_result))
            # Declare From: IFC File
            graph.add((link, ls.hasFromLinkElement, ifc_link_elements[element_id]))
            # Declare To: CSV File
            graph.add((link, ls.hasToLinkElement, csv_le))
 
    return graph
 
def load_config_yaml():
    """
    Load variables from a YAML file.
    """
    # Obtain the current directory of the script
    current_dir = pathlib.Path(__file__).parent
    
    # Config file path
    config_path = current_dir / "config_files" / "Config.yaml"
    # Load configuration from YAML file
    with open(config_path, "r", encoding="utf-8") as file:
        config = yaml.safe_load(file)
        # Store each variable from the config file into a separate variable
        ifc_name = config['ifc_name']
        base_namespace = Namespace(config['Base'])
        linkset_namespace = Namespace(config['ls'])
        content_namespace = Namespace(config['ct'])
        
    return ifc_name, base_namespace, linkset_namespace, content_namespace

if __name__ == "__main__":
    main()