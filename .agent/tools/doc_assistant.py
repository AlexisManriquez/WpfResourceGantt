import os
import sys
import re

def check_documentation(root_dir="."):
    pm_dir = os.path.join(root_dir, "ProjectManagement")
    features_dir = os.path.join(pm_dir, "Features")
    
    app_breakdown_path = os.path.join(root_dir, "Binder", "APP_BREAKDOWN.md")
    project_structure_path = os.path.join(root_dir, "Binder", "PROJECT_STRUCTURE.md")
    
    app_content = ""
    struct_content = ""
    
    reports = []

    # 1. Check APP_BREAKDOWN.md for Features
    if os.path.exists(app_breakdown_path) and os.path.isdir(features_dir):
        with open(app_breakdown_path, 'r', encoding='utf-8', errors='replace') as f:
            app_content = f.read()
        
        features = [d for d in os.listdir(features_dir) if os.path.isdir(os.path.join(features_dir, d))]
        missing_features = []
        for feature in features:
            if feature not in app_content:
                missing_features.append(feature)
        
        if missing_features:
            reports.append(f"⚠️ Missing Features in APP_BREAKDOWN.md: {', '.join(missing_features)}")
        else:
            reports.append("✅ All code features are documented in APP_BREAKDOWN.md")

    # 2. Check PROJECT_STRUCTURE.md for Files
    if os.path.exists(project_structure_path) and os.path.isdir(pm_dir):
        with open(project_structure_path, 'r', encoding='utf-8', errors='replace') as f:
            struct_content = f.read()
        
        # Files at root of ProjectManagement
        pm_files = [f for f in os.listdir(pm_dir) if os.path.isfile(os.path.join(pm_dir, f))]
        missing_files = []
        for file in pm_files:
            if file not in struct_content:
                missing_files.append(file)
        
        if missing_files:
            reports.append(f"⚠️ Missing Files in PROJECT_STRUCTURE.md: {', '.join(missing_files)}")
        else:
            reports.append("✅ All core files are documented in PROJECT_STRUCTURE.md")

    # 3. Check for ORPHANED documentation (References to things that don't exist)
    # This is a bit more complex, let's look for common patterns like "Folders" or "Views"
    orphaned = []
    # Search for bullet points with folder icons or names in the MD
    folder_refs = re.findall(r'📁 (\w+)', struct_content)
    for ref in folder_refs:
        # Check if folder exists anywhere in ProjectManagement
        found = False
        for root, dirs, _ in os.walk(pm_dir):
            if ref in dirs:
                found = True
                break
        if not found:
            orphaned.append(ref)
            
    if orphaned:
        reports.append(f"🔴 Orphaned References in PROJET_STRUCTURE.md (Folder not found in code): {', '.join(orphaned)}")

    return "\n".join(reports)

if __name__ == "__main__":
    print(check_documentation())
