"""
-----------------------------------------------------------------------------------------------------------------
Script: Validation_ICDD_Container.py
Author: Aaron Perez Herrera
Description:    This script automates the validation of the generated ICDD container against the conformance 
                requirements specified in ISO 21597-1. It inspects ZIP structure, mandatory folders, Index.rdf,
                linksets, and performs RDF/OWL semantic graph validations using rdflib.
AI Assistance:  The conceptual validation framework and regulatory checklist derived from ISO 21597-1 were 
                designed by the author. Google Gemini (2026) was utilized as a syntax and implementation 
                assistant for rdflib graph parsing operations, in-memory zip stream reading, and dynamic triple
                assertions for namespace extensibility checks. 
-----------------------------------------------------------------------------------------------------------------
"""

# Validation of the ICDD container in accordance with the established 
# requirements in section “5. Conformance requirements” of ISO 21597
# --------------------------------------------------------------------------
from pathlib import Path
import zipfile
from rdflib import Graph, Namespace, RDF, OWL, Literal, RDFS, URIRef, XSD

ICDD_PATH = Path(__file__).parent / "SteelFrame_CV_Final.ifc.icdd"
ICDD_LINKSET = "Link-Content.rdf"
CT = Namespace("https://standards.iso.org/iso/21597/-1/ed-1/en/Container#")
LS = Namespace("https://standards.iso.org/iso/21597/-1/ed-1/en/Linkset#")
BASE = Namespace("https://aaron.bauhaus-weimar.de/icdd/MasterThesis#")
REQUIRED_FOLDERS = ["Ontology resources", "Payload documents", "Payload triples"]
CONTAINER_ONTOLOGY = ("https://standards.iso.org/iso/21597/-1/ed-1/en/Container.rdf")

# Allowed vocabulary according ISO 21597 (Methods in Container.rdf and Linkset.rdf)
KNOWN_CT_TERMS = {
    "ContainerDescription", "InternalDocument", "ExternalDocument",
    "Linkset", "containsDocument", "containsLinkset", "filename",
    "format", "conformanceIndicator",
}
KNOWN_LS_TERMS = {
    "LinkElement", "Link", "DirectedLink", "DirectedBinaryLink", "Directed1toNLink",
    "Identifier", "StringBasedIdentifier", "URIBasedIdentifier", "QueryBasedIdentifier",
    "hasDocument", "hasIdentifier", "hasFromLinkElement", "hasToLinkElement",
    "hasLinkElement", "identifier", "identifierField", "queryExpression", "queryLanguage", "uri"
}

class Report:
    def __init__(self):
        self.results = []
 
    def check(self, requirement, passed, detail=""):
        status = "PASS" if passed else "FAIL"
        self.results.append((requirement, status, detail))
 
    def warn(self, requirement, detail=""):
        self.results.append((requirement, "MANUAL CHECK REQUIRED", detail))
 
    def print_report(self):
        print("=" * 90)
        for requirement, status, detail in self.results:
            print(f"[{status:<22}] {requirement}")
            if detail:
                print(f"    -> {detail}")
        print("=" * 90)
        n_pass = sum(1 for _, s, _ in self.results if s == "PASS")
        n_fail = sum(1 for _, s, _ in self.results if s == "FAIL")
        n_manual = sum(1 for _, s, _ in self.results if s == "MANUAL CHECK REQUIRED")
        print(f"Total: {n_pass} PASS, {n_fail} FAIL, {n_manual} MANUAL CHECK REQUIRED")
        
def main():
    report = Report()
    path = Path(ICDD_PATH)
    
    # CONTAINER REQUIREMENTS -------------------------------------------------
    # 1. The Container shall be a valid ZIP64 file
    is_valid_zip = zipfile.is_zipfile(path)
    report.check("Container 1: Valid ZIP file", is_valid_zip)
    # Need manual verification -> Python cannot tell the difference berween "ZIP64"  and "ZIP"
    report.warn("Container 1b: Explicit ZIP64 flag (vs. standard ZIP)",
                "Python's zipfile does not expose this distinction directly; "
                "verify manually with a hex/zip inspection tool if strict "
                "ZIP64 compliance must be demonstrated.")
    
    if not is_valid_zip:
        report.print_report()
        return
 
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
    
        # 2. The Container shall have an Index.rdf file in the root
        has_index_root = "Index.rdf" in names
        report.check("Container 2: 'Index.rdf' present in root", has_index_root,
                        "Note: case-sensitive check per ISO Figure 7 (capital I)")
        
        # 3. The Container shall have following folders: ["Ontology resources", 
        #    "Payload documents", "Payload triples"]
        for folder in REQUIRED_FOLDERS:
            present = any(n.startswith(f"{folder}/") for n in names)
            report.check(f"Container 3: '{folder}/' folder present", present)
                
        # 4. If the ontology file Container.rdf and/or Linkset.rdf is included in the 
        #    container, then it shall be located in the “Ontology resources” folder. 
        for onto_file in ["Container.rdf", "Linkset.rdf"]:
            matches = [n for n in names if n.endswith(onto_file)]
            if not matches:
                report.warn(f"Container 4: '{onto_file}' location",
                            f"'{onto_file}' not found in container at all.")
            else:
                correctly_placed = all(
                    n == f"Ontology resources/{onto_file}" for n in matches)
                report.check(f"Container 4: '{onto_file}' inside 'Ontology resources/'",
                                correctly_placed, f"Found at: {matches}")

        # 5. The Container shall have the ".icdd" filename extension
        report.check("Container 5: '.icdd' file extension",
                        path.suffix == ".icdd", f"Found extension: {path.suffix}")
            
        # INDEX.RDF REQUIREMENTS ----------------------------------------------
        g_index = None
        if has_index_root:
            index_bytes = z.read("Index.rdf")
            g_index = Graph()
            
            try:
                g_index.parse(data=index_bytes, format="xml")
                index_format_ok = True
                
                i_rdf_terms = list(g_index.triples((None, RDF.type, None)))
                i_rdfs_terms = list(g_index.triples((None, None, RDFS.Resource)))
                i_owl_terms = list(g_index.triples((None, OWL.imports, None)))
                i_ontology_terms = list(g_index.triples((None, RDF.type, OWL.Ontology)))
                i_terms = len(i_rdf_terms) + len(i_rdfs_terms) + len(i_owl_terms) + len(i_ontology_terms)
                
                # 1. It shall comply with RDF(S)/OWL.
                if (i_terms > 0):
                    report.check("Index 1: Index.rdf shall comply with RDF(S)/OWL", True,
                                    f"Uses RDF/OWL vocabulary. RDF triples: {len(i_rdf_terms)}; RDFS triples: {len(i_rdfs_terms)}; OWL triples: {len(i_owl_terms)}; OWL Ontology: {len(i_ontology_terms)}"
                                    f"-> It requires manual validation to ensure proper use of them")
                
                # 2. It shall be serialized in RDF/XML [W3C-RDF11-XML] or any other 
                #    equivalent RDF serialization recommended by W3C
                report.check("Index 2: Serialized in RDF/XML (or W3C-equivalent)",
                    index_format_ok)
                
            except Exception as e_xml:
                index_format_ok = False
                parse_error_msg = f"XML Error: {e_xml}"
                g_index = None
                
                report.check("Index 1: Index.rdf parses as valid RDF(S)/OWL", False)
                
                report.check("Index 2: Serialized in RDF/XML (or W3C-equivalent)",
                    index_format_ok, parse_error_msg)
            
        if g_index is not None:
            # 3. It shall comply with the Container.rdf file as specified in this part of 
            #    the ISO 21597 series.
            # --> Is out of Scope, require a manual check
            report.warn(
                            "Index 3: Full compliance with Container.rdf ontology",
                            "Full OWL/SHACL validation against the ontology is outside "
                            "the scope of this script. Require manual Validation."
                        )
            
            # 4. It shall import the Container.rdf ontology using owl: imports predicate.
            imports = list(g_index.triples((None, OWL.imports, None)))
            
            has_container_import = any(str(o) == CONTAINER_ONTOLOGY for _, _, o in imports)
            
            report.check("Index 4: declares owl:imports of Container.rdf ontology",
                            has_container_import,
                            f"Found imports: {[str(o) for _, _, o in imports]}")

            # 5. It shall list all internal documents and external documents.
            internal_docs = list(g_index.triples((None, RDF.type, CT.InternalDocument)))
            external_docs = list(g_index.triples((None, RDF.type, CT.ExternalDocument)))
            report.check("Index 5: lists internal and/or external documents",
                            len(internal_docs) + len(external_docs) > 0,
                            f"{len(internal_docs)} internal, {len(external_docs)} external")
            
            # 6. It shall list all datasets.
            linksets = list(g_index.triples((None, CT.containsLinkset, None)))
            report.check("Index 6: lists all linksets (datasets)",
                            len(linksets) > 0, f"Found: {len(linksets)} linkset reference(s)")
            
            # 7. It shall contain the value “ICDD-Part1-Container” for the ct: conformanceIndicator property.
            conformance = list(g_index.triples((None, CT.conformanceIndicator, None)))
            has_correct_value = any(
                str(o) == "ICDD-Part1-Container" for _, _, o in conformance)
            report.check(
                "Index 7: ct:conformanceIndicator = 'ICDD-Part1-Container'",
                has_correct_value,
                f"Found values: {[str(o) for _, _, o in conformance]}" if conformance
                else "Property ct:conformanceIndicator not found -- likely missing, add to build_index_rdf.py"
            )
        
        # LINK-DATASET FILE REQUIREMENTS -------------------------------
        path_linkset_zip = f"Payload triples/{ICDD_LINKSET}"
        has_linkset_file = path_linkset_zip in names
        g_linkset = None
        
        if has_linkset_file:
            linkset_bytes = z.read(path_linkset_zip)
            g_linkset = Graph()
            # Flags
            linkset_format_ok = False
            turtle_ok_linkset = False
            parse_error_msg = ""
        
        try:
            g_linkset.parse(data=linkset_bytes, format="xml")
            linkset_format_ok = True
            
            rdf_terms = list(g_linkset.triples((None, RDF.type, None)))
            rdfs_terms = list(g_linkset.triples((None, None, RDFS.Resource)))
            owl_terms = list(g_linkset.triples((None, OWL.imports, None)))
            ontology_terms = list(g_linkset.triples((None, RDF.type, OWL.Ontology)))
            terms = len(rdf_terms) + len(rdfs_terms) + len(owl_terms) + len(ontology_terms)
            
            # 1. It shall comply with RDF(S)/OWL.
            if (terms > 0):
                report.check("Linkset 1: Link-Content.rdf shall comply with RDF(S)/OWL", True,
                             f"Uses RDF/OWL vocabulary. RDF triples: {len(rdf_terms)}; RDFS triples: {len(rdfs_terms)}; OWL triples: {len(owl_terms)}; OWL Ontology: {len(ontology_terms)}."
                             f"-> It requires manual validation to ensure proper use of them")
            
            # 2. It shall be serialized in RDF/XML [W3C-RDF11-XML] or any other 
            #    equivalent RDF serialisation recommended by W3C
            report.check("Linkset 2: Serialized in RDF/XML (or W3C-equivalent)",
                linkset_format_ok)
            
        except Exception as e_xml:
            linkset_format_ok = False
            parse_error_msg = f"XML Error: {e_xml}"
            g_linkset = None
            
            report.check("Linkset 1: Link-Content.rdf parses as valid RDF(S)/OWL", False)
            
            report.check("Linkset 2: Serialized in RDF/XML (or W3C-equivalent)",
                linkset_format_ok, parse_error_msg)
            
        if g_linkset is not None:
            # 3. It shall comply with the Linkset.rdf file as specified in this part of 
            #    the ISO 21597 series.
            # --> Is out of Scope, require a manual check
            report.warn(
                            "Linkset 3: Full compliance with Linkset.rdf ontology",
                            "Full OWL/SHACL validation against the ontology is outside "
                            "the scope of this script. Require manual Validation."
                        )
        
        # 4. It shall be stored in the “Payload triples” folder.
        if has_linkset_file:
            report.check("Linkset 4: Linkset file (Link-Content.rdf) is inside 'Payload triples/'", True)
        else:
            report.check("Linkset 4: Linkset file (Link-Content.rdf) is inside 'Payload triples/'", False)
            
        # EVERY DOCUMENTS REQUIREMENTS ---------------------------------
        # 1.Each document (to transfer) contained in the container, shall be stored 
        #   in the “Payload documents” folder. 
        payload_docs = [n for n in names
                         if n.lower().endswith((".ifc", ".csv"))]
        misplaced = [n for n in payload_docs
                     if not n.startswith("Payload documents/")]
        report.check("Documents 1: all payload documents inside 'Payload documents/'",
                     len(misplaced) == 0, f"Misplaced: {misplaced}" if misplaced else "")
        
        # EXTENDABILITY OF ONTOLOGIES REQUIREMENTS ---------------------
        # No extensions are permitted, therefore the Index.rdf file and any Link 
        # dataset file may only contain individuals that are in compliance with 
        # the classes as specified by Container.rdf and Linkset.rdf.
        g_container_ontology = Graph()
        g_linkset_ontology = Graph()

        # Load Container ontology
        container_ontology_path = "Ontology resources/Container.rdf"
        if container_ontology_path in names:
            try:
                g_container_ontology.parse(
                    data=z.read(container_ontology_path),
                    format="xml")

                report.check("Extensibility 1: Container ontology", True,
                    "Container.rdf loaded successfully.")

            except Exception as e:
                report.check(
                    "Extensibility 1: Container ontology", False,
                    f"Could not parse Container.rdf: {e}")

        # Load Linkset ontology
        linkset_ontology_path = "Ontology resources/Linkset.rdf"
        if linkset_ontology_path in names:
            try:
                g_linkset_ontology.parse(
                    data=z.read(linkset_ontology_path),
                    format="xml")

                report.check(
                    "Extensibility 1: Linkset ontology", True,
                    "Linkset.rdf loaded successfully.")

            except Exception as e:
                report.check(
                    "Extensibility 1: Linkset ontology", False,
                    f"Could not parse Linkset.rdf: {e}")
    
        # Get all terms defined in the ICDD ontologies
        container_terms = set()
        for subject, predicate, obj in g_container_ontology:
            for term in (subject, predicate, obj):
                if isinstance(term, URIRef) and str(term).startswith(str(CT)):
                    container_terms.add(term)


        linkset_terms = set()
        for subject, predicate, obj in g_linkset_ontology:
            for term in (subject, predicate, obj):
                if isinstance(term, URIRef) and str(term).startswith(str(LS)):
                    linkset_terms.add(term)

        # Check RDF files contained in the ICDD container
        for rdf_file in names:
            # Skip ontology resources.
            if rdf_file.startswith("Ontology resources/"):
                continue

            if not rdf_file.lower().endswith(".rdf"):
                continue

            try:
                graph = Graph()
                graph.parse(
                    data=z.read(rdf_file),
                    format="xml"
                )

            except Exception as e:
                report.check(
                    f"Extensibility 1: parse {rdf_file}",
                    False,
                    f"Could not parse RDF file: {e}"
                )
                continue
            
            # Check every URI term used in the RDF graph
            unknown_terms = []
            for subject, predicate, obj in graph:
                for term in (subject, predicate, obj):
                    if not isinstance(term, URIRef):
                        continue
                    
                    uri = str(term)
                    
                    # RDF / RDFS / OWL / XSD vocabulary
                    if (
                        uri.startswith(str(RDF)) or
                        uri.startswith(str(RDFS)) or
                        uri.startswith(str(OWL)) or
                        uri.startswith(str(XSD))):
                        continue
                        
                    # Container vocabulary
                    if uri.startswith(str(CT)):
                        if (
                            term not in container_terms):
                            unknown_terms.append(term)

                        continue
                    
                    # Linkset vocabulary
                    if uri.startswith(str(LS)):

                        if (
                            term not in linkset_terms):
                            unknown_terms.append(term)

                        continue
                    
                    # Project-specific vocabulary
                    if uri.startswith(str(BASE)):
                        continue
                    
            # Report unknown ICDD terms
            if unknown_terms:
                unknown_terms = sorted(
                    set(unknown_terms),
                    key=str)

                for term in unknown_terms:
                    report.check(
                        f"Extensibility 1: {rdf_file}",
                        False,
                        f"Unknown ICDD vocabulary term: {term}")

            else:
                report.check(
                    f"Extensibility 1: {rdf_file}",
                    True,
                    "All Container and Linkset vocabulary terms are "
                    "defined by the corresponding ICDD ontologies.")
                    
        # Project-specific extensions
        # -> base:hasStructuralResult
        if g_linkset is not None:
            structural_result = BASE.hasStructuralResult

            is_owl_class = (structural_result, RDF.type, OWL.Class) in g_linkset
            is_subclass = (structural_result, RDFS.subClassOf, LS.DirectedBinaryLink) in g_linkset

            report.check(
                "Extensibility 1: base:hasStructuralResult",
                is_owl_class and is_subclass,
                (
                    "Project-specific relationship type is correctly declared as "
                    "owl:Class and rdfs:subClassOf ls:DirectedBinaryLink."
                    if (is_owl_class and is_subclass)
                    else
                    "base:hasStructuralResult is NOT correctly declared as an owl:Class inheriting from ls:DirectedBinaryLink."
                )
            )

            # -> base:hasRowIndex
            row_index = BASE.hasRowIndex

            is_rdf_property = (row_index, RDF.type, RDF.Property) in g_linkset
            has_correct_domain = (row_index, RDFS.domain, LS.LinkElement) in g_linkset
            has_correct_range = (row_index, RDFS.range,XSD.integer) in g_linkset

            report.check(
                "Extensibility 1: base:hasRowIndex",
                is_rdf_property and has_correct_domain and has_correct_range,
                (
                    "Project-specific property is declared as rdf:Property "
                    "with domain ls:LinkElement and range xsd:integer."
                    if (
                        is_rdf_property and
                        has_correct_domain and
                        has_correct_range
                    )
                    else
                    "base:hasRowIndex does not have the expected RDF property definition."
                )
            )
        
        report.warn(
            "Extensibility 1: project-specific vocabulary",
            "Project-specific terms use the BASE namespace. "
            "Their normative compliance with ISO 21597-1 requires manual assessment.")
 
    report.print_report()  
    
if __name__ == "__main__":
    main()