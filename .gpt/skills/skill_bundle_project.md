# Skill — Project Bundle for Chat

## Purpose
Bundle the git-tracked files of a project into a single zip for upload to Claude chat on mobile or desktop.

Respects `.gitignore` automatically via `git ls-files`.
Drops the zip to `C:/claude_temp/` for easy retrieval.
No manifest. No extras.

---

## Trigger
Run this skill when you want to upload the current project source to a chat session.

Suggested trigger name: **Bundle project for chat**

---

## Prerequisites
- Git must be installed and available on PATH
- The project folder must be a git repository
- `C:/claude_temp/` must exist, or the script will create it

---

## Configuration
Edit these two lines at the top of the script before first use:

```python
PROJECT_ROOT = r"C:/path/to/your/project"   # absolute path to the git repo root
OUTPUT_DIR   = r"C:/claude_temp"             # where the zip gets dropped
```

---

## Script

```python
import subprocess
import zipfile
import os
from pathlib import Path
from datetime import datetime

# --- Configuration ---
PROJECT_ROOT = r"C:/path/to/your/project"
OUTPUT_DIR   = r"C:/claude_temp"

def bundle_project():
    root = Path(PROJECT_ROOT).resolve()
    out  = Path(OUTPUT_DIR)
    out.mkdir(parents=True, exist_ok=True)

    # Get git-tracked files (respects .gitignore automatically)
    result = subprocess.run(
        ["git", "ls-files"],
        cwd=root,
        capture_output=True,
        text=True,
        check=True
    )

    tracked_files = [
        f.strip() for f in result.stdout.splitlines() if f.strip()
    ]

    if not tracked_files:
        print("No tracked files found. Is this a git repo with committed files?")
        return

    # Name the zip after the folder and current timestamp
    timestamp = datetime.now().strftime("%Y%m%d_%H%M")
    zip_name  = f"{root.name}_{timestamp}.zip"
    zip_path  = out / zip_name

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for rel_path in tracked_files:
            abs_path = root / rel_path
            if abs_path.is_file():
                zf.write(abs_path, rel_path)

    file_count = len(tracked_files)
    size_kb    = zip_path.stat().st_size // 1024

    print(f"Bundled {file_count} files → {zip_path} ({size_kb} KB)")

if __name__ == "__main__":
    bundle_project()
```

---

## Output
- Zip file at `C:/claude_temp/<projectname>_<timestamp>.zip`
- Contains all files tracked by git at time of run
- Untracked files and anything in `.gitignore` are excluded automatically

---

## Usage in Chat
After running, attach the zip to your Claude chat session.
Claude will read the file tree from the zip as project context.
Pair with a Feature Brief and a Code Brief for a full session handoff.

---

## Notes
- Run `git add` on any new files before bundling if you want them included — untracked files are excluded
- The zip preserves relative paths from the project root, so the structure is readable in chat
- If the project is large, consider adding a path filter to limit to `src/` or `assets/` only — edit the `tracked_files` list comprehension to add a prefix check
