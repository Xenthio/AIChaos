# Saved Payloads

This document explains how saved payloads work in AIChaos Brain.

## Overview

Saved payloads allow you to save frequently used commands for reuse in random chaos mode or manual execution. Each payload contains:
- A unique ID
- A descriptive name
- The original user prompt
- The generated execution code (Lua)
- Optional undo code to reverse the effects
- Timestamp of when it was saved

## File Format

### Individual JSON Files (Current Format)

Each saved payload is stored as a separate JSON file in the `saved_payloads/` directory. The filename format is:

```
<sanitized_name>_<id>.json
```

For example:
- `Spawn_Barrels_1.json`
- `Change_Gravity_2.json`
- `Rainbow_Players_3.json`

#### Benefits:
- **Easy sharing**: Share individual payload files with others
- **Git-friendly**: Easier to merge changes from multiple contributors
- **Modularity**: Add or remove payloads without affecting others
- **Readability**: Clear what each file contains from its name

### Example Payload File

```json
{
  "Id": 1,
  "Name": "Spawn Barrels",
  "UserPrompt": "spawn 10 explosive barrels",
  "ExecutionCode": "for i=1,10 do\n    local ent = ents.Create(\"prop_physics\")\n    ent:SetModel(\"models/props_c17/oildrum001_explosive.mdl\")\n    local ply = player.GetAll()[1]\n    if IsValid(ply) then\n        ent:SetPos(ply:GetPos() + Vector(0, 0, 100) + VectorRand() * 200)\n    end\n    ent:Spawn()\nend",
  "UndoCode": "for k,v in pairs(ents.FindByClass(\"prop_physics\")) do\n    if v:GetModel() == \"models/props_c17/oildrum001_explosive.mdl\" then\n        v:Remove()\n    end\nend",
  "SavedAt": "2024-12-15T03:00:00Z"
}
```

## Migration from Old Format

If you have an existing `payloads.json` file (old format), the system will automatically migrate it to individual files when AIChaos Brain starts. The old file will be deleted after successful migration.

### Old Format (Deprecated)

Previously, all payloads were stored in a single `saved_payloads/payloads.json` file:

```json
[
  {
    "Id": 1,
    "Name": "Spawn Barrels",
    "UserPrompt": "spawn 10 explosive barrels",
    "ExecutionCode": "...",
    "UndoCode": "...",
    "SavedAt": "2024-12-15T03:00:00Z"
  },
  {
    "Id": 2,
    "Name": "Change Gravity",
    "UserPrompt": "set gravity to low",
    "ExecutionCode": "...",
    "UndoCode": "...",
    "SavedAt": "2024-12-15T03:05:00Z"
  }
]
```

## Sharing Payloads

To share a payload with someone:

1. Navigate to the `saved_payloads/` directory
2. Copy the desired `.json` file(s)
3. Share the file(s) with others
4. Recipients can place the files in their `saved_payloads/` directory
5. Restart AIChaos Brain or it will be loaded automatically

## Creating Custom Payloads

You can manually create payload files by:

1. Creating a new `.json` file in the `saved_payloads/` directory
2. Following the structure shown in the example above
3. Ensuring the `Id` is unique (higher than any existing payload)
4. Using a descriptive filename (special characters will be replaced with underscores)

## File Naming Rules

When saving payloads through the UI, the system automatically sanitizes the name:
- Invalid filename characters (`:`, `/`, `\`, `<`, `>`, `"`, `|`, `?`, `*`) are replaced with `_`
- Length is limited to 50 characters
- Multiple consecutive underscores are collapsed to a single underscore
- Whitespace is trimmed
- The payload ID is appended to ensure uniqueness

## Technical Details

### Loading Process

On startup, AIChaos Brain:
1. Checks for the old `payloads.json` file and migrates it if found
2. Scans the `saved_payloads/` directory for all `.json` files
3. Loads each file and adds it to the in-memory payload list
4. Determines the next available ID based on the highest existing ID

### Saving Process

When a payload is saved:
1. A new `SavedPayload` object is created with a unique ID
2. The name is sanitized for filesystem safety
3. The payload is written to `<sanitized_name>_<id>.json`
4. The file is stored with pretty-printed JSON for readability

### Deleting Process

When a payload is deleted:
1. The payload is removed from the in-memory list
2. All files matching `*_<id>.json` are deleted from the filesystem

## Directory Structure

```
AIChaos.Brain/
├── saved_payloads/          # Saved payload files (gitignored)
│   ├── Spawn_Barrels_1.json
│   ├── Change_Gravity_2.json
│   └── Rainbow_Players_3.json
└── example_payloads/        # Example payloads (in git)
    ├── Spawn_Barrels_1.json
    ├── Change_Gravity_2.json
    └── Rainbow_Players_3.json
```

## Example Payloads

The repository includes example payloads in the `example_payloads/` directory. You can copy these to your `saved_payloads/` directory to get started quickly.

## Troubleshooting

### Payloads Not Loading

1. Check that files are in the `saved_payloads/` directory (not `example_payloads/`)
2. Verify the JSON format is valid (use a JSON validator)
3. Ensure each payload has a unique `Id` field
4. Check the AIChaos Brain logs for parsing errors

### Migration Issues

If migration from the old format fails:
1. Make a backup of your `payloads.json` file
2. Delete the file and restart AIChaos Brain
3. Manually create individual files from your backup

### Duplicate IDs

If you encounter duplicate ID errors:
1. Open each payload file
2. Manually assign unique IDs (starting from 1)
3. Restart AIChaos Brain
