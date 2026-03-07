# .agent/tools/smart_search.py
import os
import argparse
import re

def search_codebase(query, extensions, context_lines=2, search_dir="."):
    """Searches the codebase and returns token-efficient results with context."""
    ext_list = tuple(ext.strip() for ext in extensions.split(','))
    
    # Folders to completely ignore
    ignore_dirs = {'bin', 'obj', '.vs', '.git', 'packages', 'node_modules'}
    
    results_found = 0
    
    print(f"Searching for '{query}' in {ext_list} files...\n")
    
    for root, dirs, files in os.walk(search_dir):
        # Mutate dirs in-place to skip ignored directories
        dirs[:] = [d for d in dirs if d not in ignore_dirs]
        
        for file in files:
            if file.endswith(ext_list):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
                        lines = f.readlines()
                        
                    match_indices =[i for i, line in enumerate(lines) if re.search(query, line, re.IGNORECASE)]
                    
                    if match_indices:
                        results_found += len(match_indices)
                        print(f"📄 {file_path}")
                        
                        # Print with context, avoiding overlaps
                        printed_lines = set()
                        for idx in match_indices:
                            start = max(0, idx - context_lines)
                            end = min(len(lines), idx + context_lines + 1)
                            
                            for i in range(start, end):
                                if i not in printed_lines:
                                    prefix = ">> " if i == idx else "   "
                                    print(f"{prefix}{i + 1}: {lines[i].rstrip()}")
                                    printed_lines.add(i)
                        print("-" * 50)
                except Exception as e:
                    # Silently skip unreadable files (e.g., weird encodings)
                    pass

    if results_found == 0:
        print("No matches found.")
    else:
        print(f"\n✅ Found {results_found} matching lines.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("query", help="Text or regex to search for")
    parser.add_argument("--ext", default=".cs,.xaml", help="Comma-separated extensions")
    parser.add_argument("--context", type=int, default=2, help="Lines of context around match")
    args = parser.parse_args()
    
    search_codebase(args.query, args.ext, args.context)