# ADO Mirror Architecture: Per-Feature Design

## Overview

The ADO mirror system has been refactored from a monolithic single-file architecture to a per-feature distributed architecture for better scalability, performance, and maintainability.

## Architecture

### Before (Monolithic)
```
.speckit/
  ado-mirror.json  (2809 lines, 76KB - ALL work items for ALL features)
```

### After (Per-Feature)
```
.speckit/
  ado-mirror.json  (Epic-level work items only)
  ado-mirror.json.backup  (backup of original monolithic file)

specs/
  002-login/
    .speckit/
      ado-mirror.json  (Feature 3058 + 71 descendants)

  [future features will follow same pattern]
```

## Benefits

1. **Performance**: Smaller files mean faster read/write operations
2. **Isolation**: Changes to one feature don't affect other features
3. **Clarity**: Each feature's mirror is colocated with its specs
4. **Scalability**: Can handle hundreds of features without degradation
5. **Reduced Conflicts**: Multiple developers can work on different features without mirror conflicts

## File Structure

### Root Mirror (`.speckit/ado-mirror.json`)

Contains only Epic-level work items and feature mappings:

```json
{
  "lastSync": "timestamp",
  "organization": "qcellsces",
  "project": "test-project-tony",
  "workItems": {
    "2894": { /* Epic work item */ },
    "2922": { /* Epic work item */ }
  },
  "features": {
    "3058": "002-login"
  },
  "changelog": []
}
```

### Feature Mirror (`specs/{feature}/.speckit/ado-mirror.json`)

Contains Feature and all descendants (User Stories, Tasks):

```json
{
  "lastSync": "timestamp",
  "featureId": "002-login",
  "organization": "qcellsces",
  "project": "test-project-tony",
  "workItems": {
    "3058": { /* Feature */ },
    "3211": { /* User Story 0 */ },
    "3067": { /* User Story 1 */ },
    "3103": { /* Task T001 */ },
    ... /* 71 total work items */
  },
  "drift": {
    "intentDrift": [],
    "executionDrift": []
  },
  "changelog": []
}
```

## Utilities

### `scripts/mirror-utils.py`

Provides helper functions for working with mirrors:

- `get_root_mirror_path()` - Returns path to root mirror
- `get_feature_mirror_path(feature_id)` - Returns path to feature mirror
- `load_root_mirror()` - Loads root mirror
- `load_feature_mirror(feature_id)` - Loads feature mirror
- `save_root_mirror(data)` - Saves root mirror
- `save_feature_mirror(feature_id, data)` - Saves feature mirror
- `find_feature_by_work_item_id(work_item_id)` - Searches all mirrors for a work item
- `get_all_feature_ids()` - Returns list of all feature IDs

### `scripts/migrate-mirrors.py`

One-time migration script that splits the monolithic mirror into per-feature mirrors.

## Migration History

**Date**: January 20, 2026

**Changes**:
- Split monolithic `.speckit/ado-mirror.json` (165 work items) into:
  - Root mirror: 1 Epic-level work item
  - Feature 002-login mirror: 72 work items (Feature 3058 + descendants)
- Created backup at `.speckit/ado-mirror.json.backup`
- Feature 001 (blob-copy) work items remain in root mirror pending proper spec creation

**Note**: Feature 001 (blob-copy) has 91 work items that couldn't be automatically migrated because it doesn't have a spec.md with Feature ADO ID. These remain in the root mirror and can be migrated manually once the spec is properly set up.

## Usage Examples

### Loading a Feature Mirror

```python
from scripts.mirror_utils import load_feature_mirror

# Load Feature 002 mirror
mirror = load_feature_mirror("002-login")
print(f"Loaded {len(mirror['workItems'])} work items")
```

### Finding Which Feature Contains a Work Item

```python
from scripts.mirror_utils import find_feature_by_work_item_id

feature_id = find_feature_by_work_item_id(3211)  # User Story 0
print(f"Work item 3211 belongs to feature: {feature_id}")  # "002-login"
```

### Updating a Feature Mirror

```python
from scripts.mirror_utils import load_feature_mirror, save_feature_mirror
from datetime import datetime, timezone

# Load mirror
mirror = load_feature_mirror("002-login")

# Make changes
mirror['workItems']['3211']['state'] = 'In Progress'
mirror['lastSync'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')

# Save
save_feature_mirror("002-login", mirror)
```

## Integration with Speckit Commands

Future updates to speckit commands (`.claude/commands/speckit.*.md`) will use `mirror-utils.py` to:
- Auto-detect which feature mirror to use based on current context
- Load/save the correct mirror file
- Handle cross-feature references (via root mirror)

## Rollback Procedure

If needed, the original monolithic mirror can be restored:

```bash
cp .speckit/ado-mirror.json.backup .speckit/ado-mirror.json
rm -rf specs/*/. speckit/
```

## Future Enhancements

1. **Command Integration**: Update speckit commands to use mirror-utils
2. **Feature 001 Migration**: Create proper spec for blob-copy and migrate its work items
3. **Cross-Feature References**: Add utilities for handling work items that span features
4. **Drift Detection**: Per-feature drift detection and reporting
5. **Sync Optimization**: Only sync changed features instead of entire workspace
