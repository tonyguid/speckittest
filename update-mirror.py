import json
from datetime import datetime, timezone

# Read the mirror file
with open('.speckit/ado-mirror.json', 'r', encoding='utf-8-sig') as f:
    mirror = json.load(f)

# Add User Story 0 (3211) with all US0 tasks (T001-T013, T060-T067)
all_us0_tasks = [3103, 3104, 3106, 3107, 3108, 3109, 3110, 3111, 3112, 3113, 3114, 3115, 3116, 3163, 3164, 3165, 3166, 3167, 3168, 3169, 3170]

mirror['workItems']['3211'] = {
    "id": 3211,
    "type": "User Story",
    "title": "Foundational Setup for System Operations",
    "state": "New",
    "description": "The system requires foundational infrastructure, configuration, and environment preparation to support all upcoming functionality. This includes establishing the project structure, initializing required services, configuring authentication and access, and ensuring that the application can reliably run and interact with its dependencies before any user-facing features are implemented.",
    "parent": 3058,
    "children": all_us0_tasks,
    "tags": ["Speckit"],
    "syncedAt": datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
    "createdBy": "Speckit"
}

# Update Feature 3058 to include User Story 0 in its children
if '3058' in mirror['workItems'] and 'children' in mirror['workItems']['3058']:
    if 3211 not in mirror['workItems']['3058']['children']:
        mirror['workItems']['3058']['children'].insert(0, 3211)

# Update the parent for all US0 tasks from 3058 to 3211
task_ids = all_us0_tasks
for task_id in task_ids:
    task_id_str = str(task_id)
    if task_id_str in mirror['workItems']:
        mirror['workItems'][task_id_str]['parent'] = 3211

# Update Feature 3058 children list to remove the US0 tasks
if '3058' in mirror['workItems'] and 'children' in mirror['workItems']['3058']:
    original_children = mirror['workItems']['3058']['children']
    # Keep only children that are not US0 tasks
    mirror['workItems']['3058']['children'] = [
        child for child in original_children
        if child not in task_ids
    ]

# Add changelog entry
timestamp = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
changelog_entry = {
    "timestamp": timestamp,
    "action": "create",
    "workItemId": 3211,
    "workItemType": "User Story",
    "title": "Foundational Setup for System Operations",
    "changes": {
        "created": True,
        "linkedToParent": 3058,
        "linkedToChildren": task_ids
    },
    "note": "Created User Story 0 and relinked 21 US0 tasks (T001-T013, T060-T067) from Feature 3058 to User Story 3211"
}

if 'changelog' not in mirror:
    mirror['changelog'] = []
mirror['changelog'].append(changelog_entry)

# Update lastSync
mirror['lastSync'] = timestamp

# Write back to file
with open('.speckit/ado-mirror.json', 'w', encoding='utf-8') as f:
    json.dump(mirror, f, indent=2, ensure_ascii=False)

print("Successfully updated ado-mirror.json")
print("   - Added User Story 3211")
print("   - Updated 21 US0 tasks to link to User Story 3211")
print("   - Updated Feature 3058 children list")
print("   - Added changelog entry")
