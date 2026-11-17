"""
-----------------------------------------------------------------------------------------------------------------
Script: map_geom_with_sem.py
Author: Aaron Perez Herrera
Description:    This script generates a "maping.json" file for linkage of the SOFiSTiK Element IDs with
                IFC GlobalIDs by parsing the Information Container property sets.
AI Assistance:  Claude (Anthropic, 2026) was used as a coding assistant to develop the lifc for iterating
                through nested IFC properties sets (usign ifcopenshell) and extracting vañues within the 
                "explore_first_beam" method and assignation in "build_map" method.
-----------------------------------------------------------------------------------------------------------------
"""
import ifcopenshell         # Library to read and manipulate IFC files
import json                 # Library to handle JSON data
import yaml                 # Library to handle YAML configuration files
import pathlib              # Library to handle file paths

def main():
    # Variables in CONFIG.yaml
    [ifc_name, p_set, prop_name]  = load_config_yaml()
    current_dir = pathlib.Path(__file__).parent
    ifc_file = current_dir / "output" / ifc_name
    output_json = current_dir / "output" / "mapping.json"
    
    # Check if the IFC file exists and properties inside beam
    explore_first_beam(ifc_file)

    # Generate the mapping of element IDs to beam GlobalIds based on the specified property set and property name
    mapping = build_map(ifc_file, p_set, prop_name)

    if len(mapping) == 0:
        print(f"No elements found with property set '{p_set}' and property '{prop_name}'.")
        
    # Store the mapping in a JSON file
    with open(output_json, 'w') as json_file:
        json.dump(mapping, json_file, indent=4)
        
def explore_first_beam(ifc_file):
    """
    Search ifcbeams in the IFC file and print their properties. If no beams are found, it will print a message indicating that.
    """
    # Open the IFC file and search for IfcBeam elements
    beams = ifcopenshell.open(ifc_file).by_type("IfcBeam")
    if not beams:
        print("There are no beams in the IFC file. Please check the file and try again.")
        return
 
    # Print the properties of the first beam found to check property sets and properties
    sample = beams[0]
    print(f"Sample beam properties: {sample.GlobalId} - {sample.Name}")
    print("-" * 60)
    
    # AI-Assisted Logic: The following loop structure to access 'IfcRelDefinesByProperties' 
    # and unwrap the 'NominalValue' from the IFC schema was developed with Claude.
    for definition in sample.IsDefinedBy:
        if definition.is_a("IfcRelDefinesByProperties"):
            prop_set = definition.RelatingPropertyDefinition
            if prop_set.is_a("IfcPropertySet"):
                print(f"Pset: {prop_set.Name}")
                for prop in prop_set.HasProperties:
                    value = getattr(prop, "NominalValue", None)
                    value = value.wrappedValue if value else None
                    print(f"   - {prop.Name}: {value}")
                print()
                
def build_map(ifc_file, pset_name, property_name):
    """
    Build a mapping of element IDs to beam GlobalIds based on the specified property set and property name.
    - key: element ID (Name from SOFISTIK P-Set of element beam)
    - value: Global ID of the beam in the IFC file
    """
    mapping = {}
    # Open the IFC file and search for IfcBeam elements
    beams = ifcopenshell.open(ifc_file).by_type("IfcBeam")
 
    # AI-Assisted Logic: The following loop to iterate through each beam and check its property sets for the 
    # specified property was developed with Claude
    for beam in beams:
        for definition in beam.IsDefinedBy:
            if not definition.is_a("IfcRelDefinesByProperties"):
                continue
            prop_set = definition.RelatingPropertyDefinition
            if not prop_set.is_a("IfcPropertySet"):
                continue
            if prop_set.Name != pset_name:
                continue
 
            for prop in prop_set.HasProperties:
                if prop.Name != property_name:
                    continue
                value = prop.NominalValue.wrappedValue if prop.NominalValue else None
                if value is not None:
                    element_id = str(int(value))
                    mapping[element_id] = beam.GlobalId  # Generate Correspondance
    return mapping

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
        p_set_name = config['P_set_name']
        prop_name = config['Prop_name']
        ifc_name = config['ifc_name']

    return ifc_name, p_set_name, prop_name

if __name__ == "__main__":
    # Variables file names
    main()