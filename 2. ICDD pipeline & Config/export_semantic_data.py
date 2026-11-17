"""
-----------------------------------------------------------------------------------------------------------------
Script: export_semantic_data.py
Author: Aaron Perez Herrera (Extended from original SOFiSTiK AG example)
Description:    This script extracts structural analysis data (nodes, beams and forces) from the SOFiSTiK
                CDB and exports it to CSV files for linkage with geometry in the ICDD Container.
Acknowledgments: The core routine to access CDB via ctypes and extract node properties are based on official 
                 SOFiSTiK Python examples.
AI Assistance:  Claude (Anthropic, 2026) was used as a coding assistant to develop the logic for dynamically 
                structuring and exporting the final pandas DataFrame into multiple CSV file with constant columns. 
-----------------------------------------------------------------------------------------------------------------
"""
# +============================================================================+
# | Company:   SOFiSTiK AG                                                     |
# | Version:   SOFiSTIK 2024                                                   |
# +============================================================================+

import os                                   # for the environment variable necessary, this is a great tool
import platform                             # checks the python platform
from ctypes import *                        # read the functions from the cdb
from config_files.sofistik_daten import *   # import DLL functions from sofistik_daten.py
import pandas as pd                         # for data manipulation and analysis in table format
import numpy as np                          # for numerical operations
import yaml                                 # for reading YAML configuration files
import pathlib                              # for handling file paths

def main():
    """
    Main function to extract node and beam information from SOFiSTiK CDB and export to CSV.
    """
    # Variables in CONFIG.yaml
    [instalation_path, pathCDB, load_cases_id, RoundVal]  = load_config_yaml()
    current_dir = pathlib.Path(__file__).parent
    csv_folder = current_dir / "output"
    
    # Check the python platform (32bit or 64bit)
    print ("The path variable=", os.environ["Path"])
    print ("Python architecture=", platform.architecture())
    sofPlatform = str(platform.architecture())

    ## Set environment variable for the dll files
    print ("Hint: 64bit DLLs are used")

    # Set DLL dir path - new in PY 3.8 for ctypes
    # See: https://docs.python.org/3/whatsnew/3.8.html#ctypes
    os.add_dll_directory(os.path.join(instalation_path, "interfaces", "64bit"))
    os.add_dll_directory(instalation_path)

    # Get the DLL functions
    myDLL = cdll.LoadLibrary("sof_cdb_w_edu-2024.dll")
    py_sof_cdb_get = cdll.LoadLibrary("sof_cdb_w_edu-2024.dll").sof_cdb_get
    py_sof_cdb_get.restype = c_int

    py_sof_cdb_kenq = cdll.LoadLibrary("sof_cdb_w_edu-2024.dll").sof_cdb_kenq_ex
    
    # Obtain nodes info
    nodes = store_nodes_coord(pathCDB, myDLL, py_sof_cdb_get, RoundVal)
    #nodes.style.format({"Node Number": "{:.0f}", "X Coordinate": "{:.2f}", "Y Coordinate": "{:.2f}", "Z Coordinate": "{:.2f}"})
    
    # Obtain beams info
    Beams = BeamNodes(pathCDB, myDLL, py_sof_cdb_get)
    Beams = Beams[Beams["Beam ID"] != 0]        # Delete Beam ID = 0
    #Beams.style.format({"Beam ID": "{:.0f}", "Node I": "{:.0f}", "Node J": "{:.0f}"})
    
    # method to find the element ID for each row in the Beams DataFrame
    def find_element_id(row):
        node_i = row['Node I']
        node_j = row['Node J']
        
        for element_id, nodes in element_to_nodes_set.items():
            if node_i in nodes and node_j in nodes:
                return element_id
        
        return None
    
    # Obtain beam to line mapping
    nodes_id = get_element_node_id(pathCDB, myDLL, py_sof_cdb_get)
    # Map each element ID to its corresponding list of node numbers
    element_to_nodes = nodes_id.groupby('Element ID')['Node Number'].apply(list).to_dict()
    # Convert the lists to sets for faster lookups
    element_to_nodes_set = {k: set(v) for k, v in element_to_nodes.items()}
    # Add the 'element_id' column to the Beams DataFrame by applying the find_element_id function
    Beams['Element ID'] = Beams.apply(find_element_id, axis=1)
    
    # Obtain node forces info
    force_data = node_forces(pathCDB, load_cases_id, myDLL, py_sof_cdb_get, RoundVal)
    force_data = force_data[force_data["Beam ID"] != 0] # Delete Beam ID = 0
    #force_data.style.format({"Node Number": "{:.0f}", "N": "{:.2f}", "Vy": "{:.2f}", "Vz": "{:.2f}", "My": "{:.2f}", "Mz": "{:.2f}"})
    
    # Fix format forces_beam
    force_data = force_data.reset_index(drop=True)

    for i in range(len(force_data) - 1):
        current_row = force_data.iloc[i]
        next_row = force_data.iloc[i + 1]
        
        if current_row["Beam Position"] == "END":
            
            if current_row["Beam ID"] < 0:
                force_data.loc[i, "Beam ID"] = abs(current_row["Beam ID"])
                current_row["Beam ID"] = abs(current_row["Beam ID"])
            
            if current_row["Beam ID"] == next_row["Beam ID"] and current_row["Beam Position"] == next_row["Beam Position"]:
                force_data.loc[i, "Beam Position"] = "MID"
                
    # Add nodes id to "START" and "End" positions
    for i in range(len(force_data)):
        if force_data.loc[i, "Beam Position"] == "START":
            beam_id = force_data.loc[i, "Beam ID"]
            node_i = Beams.loc[Beams["Beam ID"] == beam_id, "Node I"].values[0]
            force_data.loc[i, "Node Number"] = int(node_i)
        elif force_data.loc[i, "Beam Position"] == "END":
            beam_id = force_data.loc[i, "Beam ID"]
            node_j = Beams.loc[Beams["Beam ID"] == beam_id, "Node J"].values[0]
            force_data.loc[i, "Node Number"] = int(node_j)
        else:
            force_data.loc[i, "Node Number"] = None
    
    # Add coordinates to nodes "START" and "END" positions
    for i in range(len(force_data)):
        node_id = force_data.loc[i, "Node Number"]
        
        if node_id is not None:
            node_row = nodes.loc[nodes["Node Number"] == node_id]
            
            if not node_row.empty:
                P = node_row.iloc[0][["X Coordinate", "Y Coordinate", "Z Coordinate"]].values
                force_data.loc[i, "X Coordinate"] = P[0]
                force_data.loc[i, "Y Coordinate"] = P[1]
                force_data.loc[i, "Z Coordinate"] = P[2]
                
    # Add coordinate and ID to "MID" positions
    max_node_id = force_data["Node Number"].max()
    for i in range(len(force_data)):
        if force_data.loc[i, "Beam Position"] == "MID":
            beam_id = force_data.loc[i, "Beam ID"]
            node_i = Beams.loc[Beams["Beam ID"] == beam_id, "Node I"].values[0]
            node_j = Beams.loc[Beams["Beam ID"] == beam_id, "Node J"].values[0]
            
            # Get coordinates of Node I and Node J
            x_i = nodes.loc[nodes["Node Number"] == node_i, "X Coordinate"].values[0]
            y_i = nodes.loc[nodes["Node Number"] == node_i, "Y Coordinate"].values[0]
            z_i = nodes.loc[nodes["Node Number"] == node_i, "Z Coordinate"].values[0]
            
            x_j = nodes.loc[nodes["Node Number"] == node_j, "X Coordinate"].values[0]
            y_j = nodes.loc[nodes["Node Number"] == node_j, "Y Coordinate"].values[0]
            z_j = nodes.loc[nodes["Node Number"] == node_j, "Z Coordinate"].values[0]
            
            # Obtain id for the new node
            new_node_id = max_node_id + 1
            max_node_id += 1
            
            # Calculate new point coordinates
            Pi = np.array([x_i, y_i, z_i])  # Point I coordinates
            Pj = np.array([x_j, y_j, z_j])  # Point J coordinates

            L = np.linalg.norm(Pj - Pi)     # Length of the beam

            t = force_data.loc[i, "Beam Length"] / L    # Parameter t for interpolation

            P = Pi + t*(Pj-Pi)

            force_data.loc[i, "X Coordinate"] = P[0]
            force_data.loc[i, "Y Coordinate"] = P[1]
            force_data.loc[i, "Z Coordinate"] = P[2]
            force_data.loc[i, "Node Number"] = new_node_id
            
    force_data = force_data.drop("Beam Length", axis=1) # Drop Beam Length column since it is no longer needed

    # Add to force_data the Element ID
    force_data = force_data.merge( Beams[['Beam ID', 'Element ID']], on='Beam ID', how='left')

    # Drop not useful fields
    force_data = force_data.drop("Length", axis=1)
    force_data = force_data.drop("Beam ID", axis=1)

    # Delete duplicates in same position (no longer needed START and END)
    force_data = force_data.drop_duplicates(subset=['Element ID', 'Load Case', 'X Coordinate', 'Y Coordinate', 'Z Coordinate', 'N', 'Vy', 'Vz', 'My', 'Mz'], keep='first')
    force_data = force_data.reset_index(drop=True)
    
    # Add Positions on element for each node (Help later in sorting and provide local axis to Unity)
    force_data = force_data.merge(nodes_id[["Node Number", "Element ID", "Position Node"]], on=["Node Number", "Element ID"], how="left")
            
    # Folder to Store CSV files
    output_folder = csv_folder
    os.makedirs(output_folder, exist_ok=True)

    # AI-Assisted Logic: The following data export workflow, which defines constant columns and dynamically 
    # generates independent CSV files for each load case result, was developed with Claude.
    cte_cols = [
        "Load Case", 
        "Node Number", 
        "X Coordinate", "Y Coordinate", 
        "Z Coordinate", 
        "Element ID",
        "Position Node"
        ]

    result_names = {
        "Normal_Force": "N",
        "Shear_Force_Y": "Vy",
        "Shear_Force_Z": "Vz",
        "Bending_Moment_Y": "My",
        "Bending_Moment_Z": "Mz"
        }

    # Generate CSV files for each Result Type
    for filename, result_column in result_names.items():
        columns = cte_cols + [result_column]
        df_export = force_data[columns]
        csv_path = os.path.join(
            output_folder,
            f"{filename}.csv"
        )

        # Delete repeated rows in force_data DataFrame
        df_export = df_export.drop_duplicates()       
        df_export.to_csv(csv_path, index=False)
        print(f"Saved: {csv_path}")
    
def store_nodes_coord(pathCDB, myDLL, py_sof_cdb_get, round_val):
    """
    Store nodes coordinates from SOFiSTiK CDB.
    """
    # Connect to CDB
    Index = c_int()
    cdbIndex = 99

    # Path CDB file
    fileName = pathCDB

    # important: Unicode call!
    Index.value = myDLL.sof_cdb_init(fileName.encode('utf8'), cdbIndex)

    # get the CDB status
    cdbStat = c_int()
    cdbStat.value = myDLL.sof_cdb_status(Index.value)

    # Print the Status of the CDB
    print ("CDB Status:", cdbStat.value)

    pos = c_int(0)
    datalen = c_int(0)

    a = c_int()
    ie = c_int(0)
    datalen.value = sizeof(CNODE)
    RecLen = c_int(sizeof(cnode))

    # Lists to store the data
    data_list = []

    while ie.value < 2:
        ie.value = py_sof_cdb_get(Index, 20, 0, byref(cnode), byref(RecLen), 1)
        
        # Store the data in a pandas DataFrame - Node
        NodeNum = cnode.m_nr
        Xcoord = round(cnode.m_xyz[0], round_val)
        Ycoord = round(cnode.m_xyz[1], round_val)
        Zcoord = round(cnode.m_xyz[2], round_val)
        
        dataRow = {"Node Number": NodeNum, "X Coordinate": Xcoord, "Y Coordinate": Ycoord, "Z Coordinate": Zcoord}
        data_list.append(dataRow)

        # Always read the length of record before sof_cdb_get is called
        RecLen = c_int(sizeof(cnode))
    
    # Close the CDB, 0 - will close all the files
    myDLL.sof_cdb_close(0)
    
    # Print again the status of the CDB, if status = 0 -> CDB Closed successfully
    cdbStat.value = myDLL.sof_cdb_status(Index.value)
    if cdbStat.value == 0:
        print("CDB closed successfully, status = 0")
        
    # Delete duplicate points in the DataFrame based on Node Number
    df = pd.DataFrame(data_list)
    df.drop_duplicates(subset='Node Number', keep='first', inplace=True)
        
    return pd.DataFrame(df)

def BeamNodes(pathCDB, myDLL, py_sof_cdb_get):
    """
    Store beam nodes from SOFiSTiK CDB.
    """
    
    # Connect to CDB
    Index = c_int()
    cdbIndex = 99

    # Path CDB file
    fileName = pathCDB

    # important: Unicode call!
    Index.value = myDLL.sof_cdb_init(fileName.encode('utf8'), cdbIndex)

    # get the CDB status
    cdbStat = c_int()
    cdbStat.value = myDLL.sof_cdb_status(Index.value)

    # Print the Status of the CDB
    print ("CDB Status:", cdbStat.value)

    pos = c_int(0)
    datalen = c_int(0)

    a = c_int()
    ie = c_int(0)
    datalen.value = sizeof(CBEAM)
    RecLen = c_int(sizeof(cbeam))

    # Lists to store the data
    data_list = []
    
    while ie.value < 2:
        ie.value = py_sof_cdb_get(Index, 100, 0, byref(cbeam), byref(RecLen), 1)
        
        # Store the data in a pandas DataFrame - Beam
        BeamID = cbeam.m_nr      # Node Number
        NodeI = cbeam.m_node[0]  # Node Start
        NodeJ = cbeam.m_node[1]  # Node End
        
        dataRow = {"Beam ID": BeamID, "Node I": NodeI, "Node J": NodeJ}
        data_list.append(dataRow)

        # Always read the length of record before sof_cdb_get is called
        RecLen = c_int(sizeof(cbeam))
        
    # Close the CDB, 0 - will close all the files
    myDLL.sof_cdb_close(0)
    
    # Print again the status of the CDB, if status = 0 -> CDB Closed successfully
    cdbStat.value = myDLL.sof_cdb_status(Index.value)
    if cdbStat.value == 0:
        print("CDB closed successfully, status = 0")
        
    return pd.DataFrame(data_list)

def node_forces(pathCDB, load_cases_list, myDLL, py_sof_cdb_get, round_val):
    """
    Store node forces from SOFiSTiK CDB for specified load cases.
    """
    
    # Connect to CDB
    Index = c_int()
    cdbIndex = 99

    # Path CDB file
    fileName = pathCDB

    # important: Unicode call!
    Index.value = myDLL.sof_cdb_init(fileName.encode('utf8'), cdbIndex)

    # get the CDB status
    cdbStat = c_int()
    cdbStat.value = myDLL.sof_cdb_status(Index.value)

    # Print the Status of the CDB
    print ("CDB Status:", cdbStat.value)
    pos = c_int(0)
    datalen = c_int(0)
    
    a = c_int()
    datalen.value = sizeof(CBEAM_FOR)
    RecLen = c_int(sizeof(cbeam_for))
    
    data_list = []
    
    # Previous values to avoid duplicates
    prev_BeamId = None
    prev_BeamPos = None
    prev_m_n = None
    prev_m_vy = None
    prev_m_vz = None
    prev_m_my = None
    prev_m_mz = None
    
    for load_case in load_cases_list:
        ie = c_int(0)
        
        while ie.value < 2:
            ie.value = py_sof_cdb_get(Index, 102, load_case, byref(cbeam_for), byref(RecLen), 1)
            
            # Store the data in a pandas DataFrame - Node
            BeamId = cbeam_for.m_nr
            m_n = round(cbeam_for.m_n, round_val)
            m_vy = round(cbeam_for.m_vy, round_val)
            m_vz = round(cbeam_for.m_vz, round_val)
            m_my = round(cbeam_for.m_my, round_val)
            m_mz = round(cbeam_for.m_mz, round_val)
            BeamLength = cbeam_for.m_x
            if BeamLength == 0:
                BeamPos = "START"
            else:
                BeamPos = "END"
            
            if (BeamId == prev_BeamId and BeamPos == prev_BeamPos and m_n == prev_m_n and m_vy == prev_m_vy and m_vz == prev_m_vz and m_my == prev_m_my and m_mz == prev_m_mz):
                # Always read the length of record before sof_cdb_get is called
                RecLen = c_int(sizeof(cbeam_for))
                continue
            
            # Update previous values
            prev_BeamId = BeamId
            prev_BeamPos = BeamPos
            prev_m_n = m_n
            prev_m_vy = m_vy
            prev_m_vz = m_vz
            prev_m_my = m_my
            prev_m_mz = m_mz
            
            dataRow = {"Beam ID": BeamId, "Load Case": load_case, "Length": BeamLength, "N": m_n, "Vy": m_vy, "Vz": m_vz, "My": m_my, "Mz": m_mz, "Beam Position": BeamPos, "Beam Length": BeamLength}
            data_list.append(dataRow)

            # Always read the length of record before sof_cdb_get is called
            RecLen = c_int(sizeof(cbeam_for))
        
    # Close the CDB, 0 - will close all the files
    myDLL.sof_cdb_close(0)
    
    # Print again the status of the CDB, if status = 0 -> CDB Closed successfully
    cdbStat.value = myDLL.sof_cdb_status(Index.value)
    if cdbStat.value == 0:
        print("CDB closed successfully, status = 0")
        
    return pd.DataFrame(data_list)

def get_element_node_id(pathCDB, myDLL, py_sof_cdb_get):
    """
    Provide te ID of the element to each node in CDB
    """
    # Connect to CDB
    Index = c_int()
    cdbIndex = 99

    # Path CDB file
    fileName = pathCDB

    # important: Unicode call!
    Index.value = myDLL.sof_cdb_init(fileName.encode('utf8'), cdbIndex)

    # get the CDB status
    cdbStat = c_int()
    cdbStat.value = myDLL.sof_cdb_status(Index.value)

    # Print the Status of the CDB
    print ("CDB Status:", cdbStat.value)

    pos = c_int(0)
    datalen = c_int(0)

    a = c_int()
    ie = c_int(0)
    datalen.value = sizeof(CNODE_EDG)
    RecLen = c_int(sizeof(cnode_edg))

    # Lists to store the data
    data_list = []
    # Position of node in element
    number_in_element = 1
    
    while ie.value < 2:
        ie.value = py_sof_cdb_get(Index, 20, 1, byref(cnode_edg), byref(RecLen), 1)
    
        # Store the data in a pandas DataFrame - Node with ID of the element
        num_ids = (RecLen.value - sizeof(c_int)) // sizeof(c_int) # Use only the real number of nodes

        for i in range(num_ids):

            if len(data_list) > 0:
                if data_list[-1]["Element ID"] != cnode_edg.m_nr:
                    number_in_element = 1
            
            data_list.append({
                "Element ID": cnode_edg.m_nr,
                "Node Number": cnode_edg.m_id[i],
                "Position Node": number_in_element
            })
            number_in_element += 1

        # Always read the length of record before sof_cdb_get is called 
        RecLen.value = sizeof(cnode_edg)
        
    # Close the CDB, 0 - will close all the files
    myDLL.sof_cdb_close(0)
    
    # Print again the status of the CDB, if status = 0 -> CDB Closed successfully
    cdbStat.value = myDLL.sof_cdb_status(Index.value)
    if cdbStat.value == 0:
        print("CDB closed successfully, status = 0")
        
    return pd.DataFrame(data_list)


def load_config_yaml():
    """
    Load variables from a YAML file.
    """
    # Obtain the current directory of the script
    current_dir = pathlib.Path(__file__).parent
    
    # Config file path
    config_path = current_dir / "config_files" / "config.yaml"
    # Load configuration from YAML file
    with open(config_path, "r", encoding="utf-8") as file:
        config = yaml.safe_load(file)
        # Store each variable from the config file into a separate variable
        instalation_path = config['InstallationPath']
        pathCDB = config['pathCBD']
        LoadCases = config['LoadCasesNumber']
        RoundVal = config['RoundVal']

    return instalation_path, pathCDB, LoadCases, RoundVal

# Run main app
if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("An error occurred:", str(e))