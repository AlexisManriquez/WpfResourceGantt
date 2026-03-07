# .agent/tools/read_chunk.py
import argparse
import os

def read_file_chunk(file_path, start_line=1, end_line=None):
    if not os.path.exists(file_path):
        print(f"❌ Error: File '{file_path}' not found.")
        return

    try:
        with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
            lines = f.readlines()
            
        total_lines = len(lines)
        actual_end = end_line if end_line and end_line <= total_lines else total_lines
        
        # Ensure 1-based indexing logic is safe
        start_idx = max(0, start_line - 1)
        end_idx = actual_end
        
        print(f"📄 Reading {file_path} (Lines {start_line} to {actual_end} of {total_lines}):\n")
        print("```")
        for i in range(start_idx, end_idx):
            # Prepend the line number, e.g., "15 | public void..."
            print(f"{i + 1:4} | {lines[i]}", end='')
        print("\n```")
        
    except Exception as e:
        print(f"❌ Error reading file: {str(e)}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("file", help="Path to the file")
    parser.add_argument("--start", type=int, default=1, help="Start line number (1-based)")
    parser.add_argument("--end", type=int, default=None, help="End line number")
    args = parser.parse_args()
    
    read_file_chunk(args.file, args.start, args.end)