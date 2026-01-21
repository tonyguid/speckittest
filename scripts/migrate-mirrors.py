"""
Migrate Monolithic ADO Mirror to Per-Feature Architecture

This script splits the existing .speckit/ado-mirror.json into:
1. Root mirror (.speckit/ado-mirror.json) - Contains only Epic-level work items
2. Feature mirrors (specs/{feature}/.speckit/ado-mirror.json) - Contains Feature and all descendants

Usage:
    python scripts/migrate-mirrors.py
"""

import json
import sys
from pathlib import Path
from datetime import datetime, timezone
from collections import defaultdict
import shutil


def load_current_mirror():
    """Load the existing monolithic mirror file."""
    mirror_path = Path(".speckit/ado-mirror.json")

    if not mirror_path.exists():
        print(f"Error: Mirror file not found at {mirror_path}")
        sys.exit(1)

    print(f"Loading current mirror from {mirror_path}")
    with open(mirror_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def find_feature_directories():
    """Find all feature directories in specs/."""
    specs_dir = Path("specs")

    if not specs_dir.exists():
        print("Error: specs/ directory not found")
        sys.exit(1)

    features = {}
    for feature_dir in specs_dir.iterdir():
        if feature_dir.is_dir():
            # Read spec.md to find Feature ADO ID
            spec_file = feature_dir / "spec.md"
            if spec_file.exists():
                with open(spec_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    # Look for **Feature ADO ID**: {id}
                    for line in content.split('\n'):
                        if '**Feature ADO ID**:' in line:
                            ado_id = line.split(':')[1].strip()
                            features[ado_id] = feature_dir.name
                            break

    print(f"Found {len(features)} features: {features}")
    return features


def get_work_item_feature(work_item_id, work_items):
    """Traverse up the hierarchy to find the Feature ID for a work item."""
    current_id = str(work_item_id)

    visited = set()
    while current_id in work_items and current_id not in visited:
        visited.add(current_id)
        item = work_items[current_id]

        if item.get('type') == 'Feature':
            return current_id

        parent_id = item.get('parent')
        if parent_id is None:
            break

        current_id = str(parent_id)

    return None


def split_mirror_by_feature(mirror, feature_map):
    """Split work items by feature."""
    work_items = mirror.get('workItems', {})

    # Group work items by feature
    feature_work_items = defaultdict(dict)
    epic_work_items = {}

    for work_item_id, work_item in work_items.items():
        item_type = work_item.get('type')

        if item_type == 'Epic':
            # Epics go to root mirror
            epic_work_items[work_item_id] = work_item
        elif item_type == 'Feature':
            # Features go to their respective feature mirrors
            if work_item_id in feature_map:
                feature_work_items[work_item_id][work_item_id] = work_item
        else:
            # Find which feature this belongs to
            feature_id = get_work_item_feature(work_item_id, work_items)
            if feature_id and feature_id in feature_map:
                feature_work_items[feature_id][work_item_id] = work_item
            else:
                print(f"Warning: Could not find feature for work item {work_item_id} ({work_item.get('title', 'Unknown')})")

    return epic_work_items, feature_work_items


def create_root_mirror(mirror, epic_work_items, feature_map):
    """Create the root mirror with Epics only."""
    return {
        "lastSync": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
        "organization": mirror.get('organization'),
        "project": mirror.get('project'),
        "workItems": epic_work_items,
        "features": {fid: fname for fid, fname in feature_map.items()},
        "changelog": [
            {
                "timestamp": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                "action": "migrate",
                "note": f"Migrated from monolithic mirror to per-feature architecture. Split into {len(feature_map)} feature mirrors."
            }
        ]
    }


def create_feature_mirror(feature_id, feature_name, work_items, mirror, changelog_note):
    """Create a feature-specific mirror."""
    # Filter drift and changelog for this feature's work items
    work_item_ids = set(work_items.keys())

    feature_drift = {
        "intentDrift": [],
        "executionDrift": []
    }

    # Try to filter drift if it exists
    if 'drift' in mirror:
        if 'intentDrift' in mirror['drift']:
            feature_drift['intentDrift'] = [
                d for d in mirror['drift']['intentDrift']
                if str(d.get('workItemId')) in work_item_ids
            ]
        if 'executionDrift' in mirror['drift']:
            feature_drift['executionDrift'] = [
                d for d in mirror['drift']['executionDrift']
                if str(d.get('workItemId')) in work_item_ids
            ]

    # Filter changelog
    feature_changelog = []
    if 'changelog' in mirror:
        feature_changelog = [
            entry for entry in mirror['changelog']
            if str(entry.get('workItemId')) in work_item_ids
        ]

    # Add migration note
    feature_changelog.append({
        "timestamp": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
        "action": "migrate",
        "note": changelog_note
    })

    return {
        "lastSync": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
        "featureId": feature_name,
        "organization": mirror.get('organization'),
        "project": mirror.get('project'),
        "workItems": work_items,
        "drift": feature_drift,
        "changelog": feature_changelog
    }


def main():
    print("=" * 60)
    print("ADO Mirror Migration: Monolithic to Per-Feature")
    print("=" * 60)
    print()

    # Load current mirror
    mirror = load_current_mirror()
    print(f"[OK] Loaded mirror with {len(mirror.get('workItems', {}))} work items")
    print()

    # Find features
    feature_map = find_feature_directories()
    print()

    # Backup original mirror
    backup_path = Path(".speckit/ado-mirror.json.backup")
    print(f"Creating backup at {backup_path}")
    shutil.copy(".speckit/ado-mirror.json", backup_path)
    print("[OK] Backup created")
    print()

    # Split work items
    print("Splitting work items by feature...")
    epic_work_items, feature_work_items = split_mirror_by_feature(mirror, feature_map)
    print(f"[OK] Found {len(epic_work_items)} Epic-level work items")
    print(f"[OK] Split into {len(feature_work_items)} feature groups")
    print()

    # Create root mirror
    print("Creating root mirror (Epic-level only)...")
    root_mirror = create_root_mirror(mirror, epic_work_items, feature_map)
    root_path = Path(".speckit/ado-mirror.json")
    with open(root_path, 'w', encoding='utf-8') as f:
        json.dump(root_mirror, f, indent=2, ensure_ascii=False)
    print(f"[OK] Root mirror saved to {root_path}")
    print()

    # Create feature mirrors
    print("Creating per-feature mirrors...")
    for feature_id, feature_name in feature_map.items():
        if feature_id in feature_work_items:
            work_items = feature_work_items[feature_id]
            feature_dir = Path("specs") / feature_name
            mirror_dir = feature_dir / ".speckit"
            mirror_path = mirror_dir / "ado-mirror.json"

            # Create .speckit directory
            mirror_dir.mkdir(parents=True, exist_ok=True)

            # Create feature mirror
            feature_mirror = create_feature_mirror(
                feature_id,
                feature_name,
                work_items,
                mirror,
                f"Migrated from monolithic mirror. Contains {len(work_items)} work items for feature {feature_name}."
            )

            with open(mirror_path, 'w', encoding='utf-8') as f:
                json.dump(feature_mirror, f, indent=2, ensure_ascii=False)

            print(f"[OK] Created {mirror_path} ({len(work_items)} work items)")

    print()
    print("=" * 60)
    print("Migration Complete!")
    print("=" * 60)
    print()
    print("Summary:")
    print(f"  - Root mirror: {len(epic_work_items)} Epic-level work items")
    print(f"  - Feature mirrors: {len(feature_map)}")
    for feature_id, feature_name in feature_map.items():
        if feature_id in feature_work_items:
            count = len(feature_work_items[feature_id])
            print(f"    - {feature_name}: {count} work items")
    print()
    print(f"  - Backup saved at: {backup_path}")
    print()


if __name__ == "__main__":
    main()
