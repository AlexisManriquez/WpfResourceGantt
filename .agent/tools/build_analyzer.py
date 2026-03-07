# .gemini/tools/build_analyzer.py
import subprocess
import re
import sys
import argparse

def run_build(project_path="."):
    """Runs dotnet build and parses the output for errors and warnings."""
    print(f"Running dotnet build on '{project_path}'...")
    
    # We use specific MSBuild loggers to reduce noise natively
    result = subprocess.run(['dotnet', 'build', project_path, '/clp:NoSummary;NoItemAndPropertyList;ErrorsOnly'],
        capture_output=True,
        text=True
    )
    
    output = result.stdout + result.stderr
    
    # Regex to catch standard MSBuild error formats:
    # e.g., C:\Path\File.cs(15,30): error CS1002: ; expected [C:\Path\Proj.csproj]
    pattern = re.compile(r"^(.*?)\((\d+),(\d+)\):\s+(error|warning)\s+([A-Z0-9]+):\s+(.*?)\s+\[(.*?)\]", re.MULTILINE)
    
    matches = pattern.findall(output)
    
    if not matches and result.returncode == 0:
        print("\n✅ BUILD SUCCESSFUL. No errors found.")
        return

    if not matches and result.returncode != 0:
        print("\n❌ BUILD FAILED with an unknown error format. Raw output:")
        print(output[:1000] + "\n...[truncated]")
        return

    print(f"\n❌ BUILD FAILED. Found {len(matches)} issues:\n")
    
    # Group by file to make it token-efficient and easy to read
    issues_by_file = {}
    for match in matches:
        file_path, line, col, severity, code, message, proj = match
        if file_path not in issues_by_file:
            issues_by_file[file_path] = []
        issues_by_file[file_path].append(f"  Line {line}, Col {col} | {code}: {message}")

    for file_path, issues in issues_by_file.items():
        print(f"📄 {file_path}")
        for issue in issues:
            print(issue)
        print("-" * 40)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Clean build analyzer for AI")
    parser.add_argument("--path", default=".", help="Path to solution or project")
    args = parser.parse_args()
    run_build(args.path)