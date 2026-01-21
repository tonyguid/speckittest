"""
Mirror Utilities for Per-Feature ADO Mirror Architecture

This module provides utilities for working with per-feature ADO mirror files.
Mirrors are stored at:
- Root: .speckit/ado-mirror.json (Epic-level work items only)
- Features: specs/{feature-id}-{feature-name}/.speckit/ado-mirror.json
"""

import json
import os
from pathlib import Path
from typing import Dict, Optional, List, Any
from datetime import datetime, timezone


def get_root_mirror_path() -> Path:
    """Returns path to root (Epic-level) mirror file."""
    return Path(".speckit/ado-mirror.json")


def get_feature_mirror_path(feature_id: str) -> Optional[Path]:
    """
    Returns path to feature mirror file.

    Args:
        feature_id: Feature ID (e.g., "002-login")

    Returns:
        Path to feature mirror, or None if feature directory doesn't exist
    """
    specs_dir = Path("specs")

    # Find the feature directory
    for feature_dir in specs_dir.glob(f"{feature_id}-*"):
        if feature_dir.is_dir():
            mirror_path = feature_dir / ".speckit" / "ado-mirror.json"
            return mirror_path

    return None


def load_root_mirror() -> Dict[str, Any]:
    """
    Loads the root (Epic-level) mirror file.

    Returns:
        Dictionary containing root mirror data

    Raises:
        FileNotFoundError: If root mirror doesn't exist
    """
    mirror_path = get_root_mirror_path()

    if not mirror_path.exists():
        raise FileNotFoundError(f"Root mirror not found at {mirror_path}")

    with open(mirror_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def load_feature_mirror(feature_id: str) -> Dict[str, Any]:
    """
    Loads a feature-specific mirror file.

    Args:
        feature_id: Feature ID (e.g., "002-login")

    Returns:
        Dictionary containing feature mirror data

    Raises:
        FileNotFoundError: If feature mirror doesn't exist
    """
    mirror_path = get_feature_mirror_path(feature_id)

    if mirror_path is None or not mirror_path.exists():
        raise FileNotFoundError(f"Feature mirror not found for {feature_id}")

    with open(mirror_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def save_root_mirror(data: Dict[str, Any]) -> None:
    """
    Saves the root (Epic-level) mirror file.

    Args:
        data: Mirror data to save
    """
    mirror_path = get_root_mirror_path()
    mirror_path.parent.mkdir(parents=True, exist_ok=True)

    with open(mirror_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def save_feature_mirror(feature_id: str, data: Dict[str, Any]) -> None:
    """
    Saves a feature-specific mirror file.

    Args:
        feature_id: Feature ID (e.g., "002-login")
        data: Mirror data to save

    Raises:
        FileNotFoundError: If feature directory doesn't exist
    """
    mirror_path = get_feature_mirror_path(feature_id)

    if mirror_path is None:
        raise FileNotFoundError(f"Feature directory not found for {feature_id}")

    # Create .speckit directory if it doesn't exist
    mirror_path.parent.mkdir(parents=True, exist_ok=True)

    with open(mirror_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def find_feature_by_work_item_id(work_item_id: int) -> Optional[str]:
    """
    Searches all feature mirrors to find which feature contains a work item.

    Args:
        work_item_id: Work item ID to search for

    Returns:
        Feature ID if found, None otherwise
    """
    specs_dir = Path("specs")

    if not specs_dir.exists():
        return None

    for feature_dir in specs_dir.iterdir():
        if not feature_dir.is_dir():
            continue

        mirror_path = feature_dir / ".speckit" / "ado-mirror.json"
        if not mirror_path.exists():
            continue

        try:
            with open(mirror_path, 'r', encoding='utf-8-sig') as f:
                mirror = json.load(f)

            if str(work_item_id) in mirror.get('workItems', {}):
                # Extract feature ID from directory name (e.g., "002-login" from "specs/002-login")
                return feature_dir.name
        except (json.JSONDecodeError, IOError):
            continue

    return None


def get_all_feature_ids() -> List[str]:
    """
    Returns list of all feature IDs that have specs directories.

    Returns:
        List of feature IDs (e.g., ["001-blob-copy", "002-login"])
    """
    specs_dir = Path("specs")

    if not specs_dir.exists():
        return []

    feature_ids = []
    for feature_dir in specs_dir.iterdir():
        if feature_dir.is_dir():
            feature_ids.append(feature_dir.name)

    return sorted(feature_ids)


def create_empty_feature_mirror(feature_id: str, organization: str, project: str) -> Dict[str, Any]:
    """
    Creates an empty feature mirror structure.

    Args:
        feature_id: Feature ID (e.g., "002-login")
        organization: ADO organization name
        project: ADO project name

    Returns:
        Empty feature mirror dictionary
    """
    return {
        "lastSync": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
        "featureId": feature_id,
        "organization": organization,
        "project": project,
        "workItems": {},
        "drift": {
            "intentDrift": [],
            "executionDrift": []
        },
        "changelog": []
    }


def get_feature_id_from_work_item(work_item: Dict[str, Any], all_work_items: Dict[str, Dict]) -> Optional[int]:
    """
    Extracts the feature ID from a work item by traversing up the hierarchy.

    Args:
        work_item: Work item to analyze
        all_work_items: All work items dictionary for hierarchy traversal

    Returns:
        Feature work item ID, or None if not found
    """
    current_item = work_item

    while current_item:
        if current_item.get('type') == 'Feature':
            return current_item.get('id')

        # Traverse up to parent
        parent_id = current_item.get('parent')
        if parent_id is None:
            break

        current_item = all_work_items.get(str(parent_id))

    return None
