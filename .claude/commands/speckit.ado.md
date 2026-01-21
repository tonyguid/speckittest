---
description: "Sync Azure DevOps work items with specifications and tasks. Ensure work items in ADO are aligned with specifications in ADO."
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

# Governance Model

This slash command implements the Spec-Kit ↔ Azure DevOps governance model defined in `docs/speckit_ado_gh_governance.md`.

## Ownership Boundaries

- **Spec-Kit owns**: Features, Epics, User Stories, Tasks, State transitions and assignments for Tasks, acceptance criteria, estimates, engineering guidelines, and hierarchy structure
- **Azure DevOps owns**: State transitions and assignments for Epics, Features, and User Stories, iteration paths, area paths, work item lifecycle, analytics, and dashboards
- **Sync Layer owns**: Mapping, reconciliation, drift detection, hierarchy enforcement, local mirror file, and API interactions

## Change-Control Rules

- Work definition changes MUST originate in Spec-Kit
- Execution changes MUST originate in Azure DevOps
- The sync layer reconciles both systems according to drift governance rules

## Hierarchy Governance

Enforce strict hierarchy: **Epic → Feature → User Story → Task**
- Every Task MUST have a User Story parent
- Every User Story MUST have a Feature parent
- Every Feature MUST have an Epic parent
- Block push operations when orphaned work items are detected

## Drift Governance

- **Intent Drift** (titles, descriptions, acceptance criteria, hierarchy): Spec-Kit wins
- **Execution Drift** (state, assignments, iteration paths): ADO wins
- User must reconcile drift before push operations

# Command Behavior

## Determine Intent from $ARGUMENTS

- **`push`** or empty: Execute one-way sync from Spec-Kit → ADO
- **`pull`**: Fetch ADO work items and update local mirror file, flag drift
- **`reconcile`**: Interactive drift resolution - prompt user to choose which system wins for each conflict
- **`status`**: Show current sync status, drift summary, and hierarchy validation

## Authentication

You MUST use Entra ID via Azure DevOps MCP server. Do NOT use PATs or store secrets in repos.
<!--Use OAuth via Azure DevOps MCP server. Do NOT use PATs or store secrets in repos.-->

## Push Operation (Spec-Kit → ADO)

1. **Pre-flight checks**:
   - Validate hierarchy (Epic → Feature → User Story → Task)
   - Detect orphaned work items and block if found
   - Check for unresolved intent drift and require reconciliation first

2. **Read spec.md** and identify work items:
   - Existing items have `ADO ID` field (work item ID in ADO)
   - New items do NOT have `ADO ID` field yet
   - update local mirror file:
        - For the feature:
            - Everything other than the User Stories should be in the description
        - For User Stories
            - Priority should be added as a field (P0, P1, P2 as an example)
            - Any content before Acceptance Criteria should be in the description
            - Acceptance Scenario should be added as a field

2. **Read tasks.md** and identify additional work items:
   - Existing items have `ADO ID` field (work item ID in ADO)
   - New items do NOT have `ADO ID` field yet
   - update local mirror file:
        - if the the task is marked complete in spec-kit:
            - Change the State field to Closed
            - Add a comment noting the change with timestamp

3. **For existing work items**:
   <!--- Compare each work item to its relevant Feature/User Story/Task to determine if they need to be updated
   - If updates to ADO are necessary:-->
        - update title, description, effort, and acceptance criteria in ADO
        - Add comment noting changes with timestamp
        - Tag with `Speckit` tag
<!-->   - For Tasks:
        - if the the task is marked complete in spec-kit:
            - Change the State field in ADO to Closed
            - Add a comment noting the change with timestamp -->
   - For Epics, Features and User Stories, preserve ADO-owned fields (state, assignments, iteration paths)

4. **For new work items**:
   - Create work item in ADO with appropriate type (Epic, Feature, User Story, Task)
   - Set parent links according to hierarchy
   - Tag with `Speckit` tag
   - Update spec.md with new `ADO ID` field
   - Update tasks.md with new 'ADO ID' field

5. **Hierarchy enforcement**:
   - Set parent-child relationships via "System.Parent" field
   - Validate no orphans exist

6. **Update local mirror file** with sync timestamp and lineage data

## Pull Operation (ADO → Mirror)

1. Fetch all work items tagged with `Speckit` from ADO
2. Update local mirror file with current ADO state
3. Compare with spec.md to detect drift
4. Flag intent drift (titles, descriptions, hierarchy changes)
5. Report drift summary to user
6. Do NOT modify spec.md automatically

## Reconcile Operation

1. Load drift report from previous pull
2. For each drifted item, show:
   - Spec-Kit version
   - ADO version
   - Field(s) that differ
3. Prompt user: "Keep Spec-Kit version or ADO version?"
4. Apply user choices:
   - If Spec-Kit wins: stage for next push
   - If ADO wins: update spec.md immediately
5. Mark drift as resolved in mirror file

## Status Operation

1. Show sync timestamp
2. Show drift summary (count of drifted items by type)
3. Show hierarchy validation status
4. Show count of new items in Spec-Kit not yet in ADO
5. Show count of orphaned work items (if any)

# API Usage

Use Azure DevOps REST API v7.0 for all operations:
- `GET /_apis/wit/workitems?ids={ids}&$expand=relations` - Fetch work items
- `POST /_apis/wit/workitems/${type}` - Create work items
- `PATCH /_apis/wit/workitems/{id}` - Update work items
- Use JSON Patch format for updates

# Reporting & Audit

Maintain in local mirror file:
- Sync timestamps
- Drift history
- Work item lineage (parent-child mappings)
- Change log for traceability