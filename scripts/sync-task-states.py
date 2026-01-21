"""
Sync task states from mirror to Azure DevOps.

This script implements the governance model for task state transitions:
- Spec-Kit owns task state and assignments
- Completed tasks in mirror should have state "Closed" in ADO
- Changes are synced via Azure DevOps REST API
"""

import json
import sys
import subprocess
from pathlib import Path
from datetime import datetime, timezone
from typing import Dict, List, Any, Optional

def get_azure_token() -> str:
    """Get Azure AD token for Azure DevOps API."""
    print("Getting Azure AD token for Azure DevOps...")

    # Try PowerShell first (Windows), then az CLI (Linux/Mac)
    try:
        # PowerShell command
        ps_command = (
            'az account get-access-token '
            '--resource 499b84ac-1321-427f-aa17-267ca6975798 '
            '--query accessToken -o tsv'
        )
        result = subprocess.run(
            ["powershell", "-Command", ps_command],
            capture_output=True,
            text=True,
            check=True
        )
        token = result.stdout.strip()
        if token:
            return token
    except (subprocess.CalledProcessError, FileNotFoundError):
        pass

    # Fallback to az CLI directly
    try:
        result = subprocess.run(
            ["az", "account", "get-access-token",
             "--resource", "499b84ac-1321-427f-aa17-267ca6975798",
             "--query", "accessToken", "-o", "tsv"],
            capture_output=True,
            text=True,
            check=True
        )
        token = result.stdout.strip()
        if token:
            return token
    except (subprocess.CalledProcessError, FileNotFoundError):
        pass

    raise RuntimeError("Failed to get Azure AD token. Make sure you're logged in with 'az login'")

def load_feature_mirror(feature_path: Path) -> Dict[str, Any]:
    """Load feature mirror file."""
    mirror_path = feature_path / ".speckit" / "ado-mirror.json"
    if not mirror_path.exists():
        raise FileNotFoundError(f"Mirror not found: {mirror_path}")

    with open(mirror_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def save_feature_mirror(feature_path: Path, mirror: Dict[str, Any]):
    """Save feature mirror file."""
    mirror_path = feature_path / ".speckit" / "ado-mirror.json"
    with open(mirror_path, 'w', encoding='utf-8') as f:
        json.dump(mirror, f, indent=2, ensure_ascii=False)

def find_tasks_needing_state_update(mirror: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    Find tasks that are marked completed in tags but have state != Closed.

    Returns list of tasks that need state updates.
    """
    tasks_to_update = []
    work_items = mirror.get('workItems', {})

    for work_item_id, work_item in work_items.items():
        if work_item.get('type') != 'Task':
            continue

        tags = work_item.get('tags', [])
        current_state = work_item.get('state', 'New')

        # Check if task is marked completed in tags
        is_completed = 'Completed' in tags

        if is_completed and current_state != 'Closed':
            tasks_to_update.append({
                'id': work_item['id'],
                'title': work_item.get('title', 'Unknown'),
                'current_state': current_state,
                'target_state': 'Closed'
            })

    return tasks_to_update

def update_task_state_in_ado(
    task_id: int,
    organization: str,
    project: str,
    token: str
) -> bool:
    """
    Update task state to Closed in Azure DevOps.

    Uses Azure DevOps REST API v7.0 with JSON Patch format.
    """
    import requests

    url = f"https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/{task_id}"
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json-patch+json"
    }

    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")

    # JSON Patch operations
    patch_operations = [
        {
            "op": "add",
            "path": "/fields/System.State",
            "value": "Closed"
        },
        {
            "op": "add",
            "path": "/fields/System.History",
            "value": f"State updated to Closed by Spec-Kit sync at {timestamp}"
        }
    ]

    try:
        response = requests.patch(
            f"{url}?api-version=7.0",
            headers=headers,
            json=patch_operations,
            timeout=30
        )
        response.raise_for_status()
        return True
    except requests.exceptions.RequestException as e:
        print(f"  [ERROR] Failed to update task {task_id}: {e}")
        return False

def update_task_state_in_mirror(
    mirror: Dict[str, Any],
    task_id: int
) -> bool:
    """Update task state to Closed in local mirror."""
    work_items = mirror.get('workItems', {})
    task_id_str = str(task_id)

    if task_id_str not in work_items:
        return False

    work_items[task_id_str]['state'] = 'Closed'
    work_items[task_id_str]['syncedAt'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')

    return True

def add_changelog_entry(
    mirror: Dict[str, Any],
    task_updates: List[Dict[str, Any]]
):
    """Add changelog entry for task state updates."""
    if 'changelog' not in mirror:
        mirror['changelog'] = []

    timestamp = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')

    changelog_entry = {
        "timestamp": timestamp,
        "action": "sync_task_states",
        "changes": {
            "task_ids": [task['id'] for task in task_updates],
            "state_transition": "New -> Closed",
            "reason": "Syncing completed tasks from Spec-Kit to ADO"
        },
        "note": f"Updated {len(task_updates)} completed tasks to Closed state in ADO"
    }

    mirror['changelog'].append(changelog_entry)

def main():
    """Main execution function."""
    # Determine feature directory
    specs_dir = Path("specs")

    # Get current git branch to determine which feature
    result = subprocess.run(
        ["git", "rev-parse", "--abbrev-ref", "HEAD"],
        capture_output=True,
        text=True,
        check=True
    )
    current_branch = result.stdout.strip()

    # Find feature directory matching branch name
    feature_path = None
    for feature_dir in specs_dir.iterdir():
        if feature_dir.is_dir() and feature_dir.name in current_branch:
            feature_path = feature_dir
            break

    if not feature_path:
        print(f"[ERROR] No feature directory found matching branch '{current_branch}'")
        print("Available features:")
        for feature_dir in specs_dir.iterdir():
            if feature_dir.is_dir():
                print(f"  - {feature_dir.name}")
        sys.exit(1)

    print(f"Working with feature: {feature_path.name}")

    # Load mirror
    try:
        mirror = load_feature_mirror(feature_path)
    except FileNotFoundError as e:
        print(f"[ERROR] {e}")
        sys.exit(1)

    organization = mirror.get('organization')
    project = mirror.get('project')

    if not organization or not project:
        print("[ERROR] Mirror missing organization or project")
        sys.exit(1)

    # Find tasks needing state updates
    tasks_to_update = find_tasks_needing_state_update(mirror)

    if not tasks_to_update:
        print("[OK] No tasks need state updates")
        return

    print(f"\nFound {len(tasks_to_update)} tasks needing state updates:")
    for task in tasks_to_update:
        print(f"  - Task {task['id']}: {task['title']}")
        print(f"    Current state: {task['current_state']} -> Target state: {task['target_state']}")

    # Get Azure token
    try:
        token = get_azure_token()
    except Exception as e:
        print(f"[ERROR] Failed to get Azure token: {e}")
        sys.exit(1)

    # Update tasks in ADO
    print("\nUpdating tasks in Azure DevOps...")
    success_count = 0
    fail_count = 0

    for task in tasks_to_update:
        task_id = task['id']
        print(f"\n  Updating Task {task_id}: {task['title']}")

        # Update in ADO
        if update_task_state_in_ado(task_id, organization, project, token):
            print(f"    [OK] Successfully updated in ADO")

            # Update in mirror
            if update_task_state_in_mirror(mirror, task_id):
                print(f"    [OK] Successfully updated in mirror")
                success_count += 1
            else:
                print(f"    [ERROR] Failed to update in mirror")
                fail_count += 1
        else:
            fail_count += 1

    # Add changelog entry
    if success_count > 0:
        add_changelog_entry(mirror, [t for t in tasks_to_update if t['id']])

        # Update lastSync
        mirror['lastSync'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')

        # Save mirror
        save_feature_mirror(feature_path, mirror)
        print(f"\n[OK] Mirror updated and saved")

    # Summary
    print("\n" + "=" * 60)
    print("Task State Sync Complete")
    print(f"  [OK] Success: {success_count}")
    print(f"  [ERROR] Failed: {fail_count}")
    print("=" * 60)

if __name__ == "__main__":
    main()
