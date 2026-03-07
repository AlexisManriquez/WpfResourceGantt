# .agent/tools/verify_edit.py
import argparse
import os
import sys

def verify_content(file_path, start_line, end_line, expected_content):
    if not os.path.exists(file_path):
        print(f"❌ Error: File '{file_path}' not found.")
        return False

    try:
        try:
            # utf-8-sig handles the BOM correctly on Windows
            with open(file_path, 'r', encoding='utf-8-sig', newline='') as f:
                lines = f.readlines()
        except UnicodeDecodeError:
            with open(file_path, 'r', encoding='utf-8', newline='') as f:
                lines = f.readlines()
        except Exception:
            with open(file_path, 'r', encoding='latin-1', newline='') as f:
                lines = f.readlines()
            
        total_lines = len(lines)
        if start_line < 1 or start_line > total_lines:
            print(f"❌ Error: Start line {start_line} is out of bounds (1-{total_lines}).")
            return False
            
        actual_end = end_line if end_line and end_line <= total_lines else total_lines
        
        # Extract lines for the range (1-based to 0-based slice)
        target_lines = lines[start_line-1:actual_end]
        actual_content = "".join(target_lines)
        
        # Normalize line endings for comparison if needed, 
        # but the goal is to be exact, so we show diffs if they don't match.
        if actual_content == expected_content:
            print(f"✅ MATCH: The content in {file_path} at lines {start_line}-{actual_end} exactly matches.")
            return True
        else:
            print(f"❌ MISMATCH at {file_path} lines {start_line}-{actual_end}")
            print("\n--- EXPECTED (Escaped) ---")
            print(repr(expected_content))
            print("\n--- ACTUAL (Escaped) ---")
            print(repr(actual_content))
            print("\n--- ACTUAL (Raw) ---")
            print("```")
            print(actual_content, end='')
            print("```")
            return False
            
    except Exception as e:
        print(f"❌ Error: {str(e)}")
        return False

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Verify if a file chunk matches expected content.")
    parser.add_argument("file", help="Path to the file")
    parser.add_argument("--start", type=int, required=True, help="Start line number (1-based)")
    parser.add_argument("--end", type=int, required=True, help="End line number")
    parser.add_argument("--content-file", help="Path to a file containing the expected content")
    
    args = parser.parse_args()
    
    expected = ""
    if args.content_file:
        if os.path.exists(args.content_file):
            try:
                with open(args.content_file, 'r', encoding='utf-8-sig', newline='') as f:
                    expected = f.read()
            except UnicodeDecodeError:
                # Fallback for Windows-1252 or UTF-16 (common on Windows PowerShell)
                try:
                    with open(args.content_file, 'r', encoding='utf-16', newline='') as f:
                        expected = f.read()
                except:
                    with open(args.content_file, 'r', encoding='latin-1', newline='') as f:
                        expected = f.read()
        else:
            print(f"❌ Error: Content file '{args.content_file}' not found.")
            sys.exit(1)
    else:
        # If no file provided, read from stdin (allows piping content)
        print("⏳ Reading expected content from stdin... (Ctrl+Z and Enter on Windows to finish)")
        expected = sys.stdin.read()
    
    if verify_content(args.file, args.start, args.end, expected):
        sys.exit(0)
    else:
        sys.exit(1)
