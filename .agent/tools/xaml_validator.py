import os
import sys
import xml.etree.ElementTree as ET
import re

def validate_xaml(file_path):
    if not os.path.exists(file_path):
        return f"Error: File '{file_path}' not found."

    with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
        content = f.read()

    # 1. Check for common diff markers (+ or - at start of lines)
    diff_markers = []
    lines = content.splitlines()
    for i, line in enumerate(lines, 1):
        if line.strip().startswith('+') or line.strip().startswith('-'):
            # Only count as error if it looks like a stray diff marker, not part of a string
            if not any(quote in line for quote in ['"', "'"]):
                diff_markers.append(f"Line {i}: Potential stray diff marker found: '{line.strip()}'")

    # 2. Basic XML Parsing
    try:
        # Pre-process content to remove common XAML-breaking characters if needed, 
        # but here we just want to catch the error.
        root = ET.fromstring(content)
    except ET.ParseError as e:
        line, column = e.position
        return f"❌ XML Syntax Error: {str(e)}\nContext: Line {line}, Column {column}\n" + "\n".join(diff_markers)

    # 3. Hierarchy Rules
    # Tags that can only have one child
    single_child_tags = ['Border', 'ScrollViewer', 'UserControl', 'Window', 'ContentControl', 'Frame', 'GroupBox']
    errors = []

    # Map namespaces to handle XAML prefixes
    # This is a bit simplified but covers common cases
    namespaces = dict([
        node for _, node in ET.iterparse(file_path, events=['start-ns'])
    ])

    for elem in root.iter():
        # Get tag name without namespace
        tag = elem.tag.split('}')[-1] if '}' in elem.tag else elem.tag
        
        if tag in single_child_tags:
            # Count elements that are not properties (don't contain a dot)
            children = [child for child in elem if '.' not in child.tag]
            if len(children) > 1:
                errors.append(f"❌ Hierarchy Error: <{tag}> at line {get_line_number(content, elem)} contains {len(children)} children. It only supports ONE child.")

    if diff_markers:
        errors.extend(diff_markers)

    if errors:
        return "\n".join(errors)
    
    return "✅ XAML structure is valid."

def get_line_number(content, element):
    # Rough estimate of line number for an element
    # ET doesn't store line numbers by default, so we search for a unique snippet
    tag_only = element.tag.split('}')[-1]
    search_str = f"<{tag_only}"
    # This is imperfect for duplicates but better than nothing
    lines = content.splitlines()
    for i, line in enumerate(lines, 1):
        if search_str in line:
            return i
    return "unknown"

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python xaml_validator.py <file_path>")
        sys.exit(1)
    
    result = validate_xaml(sys.argv[1])
    print(result)
