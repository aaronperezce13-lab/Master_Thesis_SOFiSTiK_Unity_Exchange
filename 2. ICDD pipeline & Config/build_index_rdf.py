"""
----------------------------------------------------------------------------------------------------------
Script: build_index_rdf.py
Author: Aaron Perez Herrera
Description:    This script generates the "Index.rdf" file, functioning as the central manifest of the ICDD
                Container. It declares the container ontology (ISO 21597-1) and registers all Payload documents 
                (IFC file and resulting CSV files) and Payload triples (Link-Content.rdf)
AI Assistance:  The ICDD Conformance logic and document registry architecture were defined by author. Claude 
                (Anthropic, 2026) was utilized as a syntax assistant to build the semantic RDF graph using 
                the "rdflib" library.
----------------------------------------------------------------------------------------------------------
"""

import os                                                       # Import the os module to interact with the operating system
import pathlib                                                  # Import the pathlib module to handle file paths                          
import yaml                                                     # Import the yaml module to handle YAML files
from rdflib import Graph, Namespace, Literal, RDF, OWL, URIRef  # Import the rdflib module to handle RDF graphs and related classes
from rdflib.namespace import XSD                                # Import the XSD namespace from rdflib for RDF serialization and validation


def main():
    # Load configuration from YAML file
    [ifc_file_name, base, ct, ls] = load_config_yaml()
    current_dir = pathlib.Path(__file__).parent
    output_folder = current_dir / "output"
    content_rdf_name = "Link-Content.rdf"
    content_rdf_path = output_folder / content_rdf_name
    output_index_rdf = output_folder / "Index.rdf"

    # Obtain csv file names
    csv_filenames = [f for f in os.listdir(output_folder) if f.lower().endswith(".csv")]

    # Debugging: Check if all the files were found
    print(f"CSV files found in folder: {csv_filenames}")
    print(f"IFC file: {ifc_file_name}")
    print(f"Linkset (content.rdf) file: {content_rdf_path}")

    # Build index.rdf file
    graph = build_graph(csv_filenames, ifc_file_name, content_rdf_name, base, ct)
    graph.serialize(destination=output_index_rdf, format="xml")
    print(f"File generated! Total of triples inside: {len(graph)}")


def build_graph(csv_filenames, ifc_file_name, content_rdf_name, base, ct):
    """
    Build the RDF graph for the index.rdf file, which describes the container and its contents.
    """
    # Initialize the RDF graph and bind namespaces
    graph = Graph()
    graph.bind("base", base)
    graph.bind("ct", ct)
    graph.bind("owl", OWL)
    
    # AI-Assisted Implementation: The definition of the ContainerDescription and subsequent mapping of 
    # documents as InteralDocuments using "rdflib" -> triplets assembly waas aided by Claude.

    # Define the container and its type
    container = base.ContainerDescription
    graph.add((container, RDF.type, ct.ContainerDescription))

    # Declare the Index dataset as a OWL Ontology
    graph.add((container, RDF.type, OWL.Ontology))
    
    # Import ISO 21597-1 Container Ontology
    container_ontology = URIRef("https://standards.iso.org/iso/21597/-1/ed-1/en/Container.rdf")
    graph.add((container, OWL.imports, container_ontology))
    
    # Declare ISO 21597-1 Conformance
    graph.add((container, ct.conformanceIndicator, Literal("ICDD-Part1-Container")))
    
    # Define the IFC document and its properties
    ifc_doc = base[ifc_file_name]
    graph.add((ifc_doc, RDF.type, ct.InternalDocument))
    graph.add((ifc_doc, ct.filename, Literal(f"Payload documents/{ifc_file_name}")))
    graph.add((ifc_doc, ct["format"], Literal("application/x-step", datatype=XSD.string)))
    graph.add((container, ct.containsDocument, ifc_doc))

    # Define CSV documents and their properties
    for csv_filename in csv_filenames:
        csv_doc = base[csv_filename]
        graph.add((csv_doc, RDF.type, ct.InternalDocument))
        graph.add((csv_doc, ct.filename, Literal(f"Payload documents/{csv_filename}")))
        graph.add((csv_doc, ct["format"], Literal("text/csv", datatype=XSD.string)))
        graph.add((container, ct.containsDocument, csv_doc))

    # Define the linkset (content.rdf) and its properties
    linkset = base[pathlib.Path(content_rdf_name).name]
    graph.add((linkset, RDF.type, ct.Linkset))
    graph.add((linkset, ct.filename, Literal(f"Payload triples/{pathlib.Path(content_rdf_name).name}")))
    graph.add((container, ct.containsLinkset, linkset))

    return graph


def load_config_yaml():
    """
        Load variables from a YAML file.
    """
    current_dir = pathlib.Path(__file__).parent
    config_path = current_dir / "config_files" / "Config.yaml"

    with open(config_path, "r", encoding="utf-8") as file:
        config = yaml.safe_load(file)
        ifc_file_name = config['ifc_name']
        base_namespace = Namespace(config['Base'])
        content_namespace = Namespace(config['ct'])
        linkset_namespace = Namespace(config['ls'])

    return ifc_file_name, base_namespace, content_namespace, linkset_namespace


if __name__ == "__main__":
    main()