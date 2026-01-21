Spec‑Kit ↔ GitHub ↔ Azure DevOps Governance Model
1. Purpose of Governance
This governance model ensures a single source of truth for intent (Spec‑Kit), a single source of truth for execution (Azure DevOps), and a deterministic sync layer that reconciles the two. It prevents drift, duplication, and ambiguity across systems.
2. Ownership Boundaries
Spec‑Kit owns: Features, Epics, User Stories, Tasks, acceptance criteria, PR feedback converted into tasks, estimates, engineering guidelines, and hierarchy structure.
Azure DevOps owns: State transitions, assignments, iteration paths, area paths, work item lifecycle, analytics, and dashboards.
GitHub owns: Pull requests, PR comments, code review feedback, file diffs, and commit history.
The Sync Layer owns: Mapping, reconciliation, drift detection, hierarchy enforcement, local mirror file, and API interactions.
3. Change‑Control Rules
Work definition changes must originate in Spec‑Kit. Execution changes must originate in Azure DevOps. PR feedback originates in GitHub and becomes tasks in Spec‑Kit.
4. Hierarchy Governance
Hierarchy: Epic → Feature → User Story → Task. Every Task must have a User Story parent, every User Story a Feature parent, every Feature an Epic parent. Orphans are blocked during push.
5. Drift Governance
Intent Drift: Spec‑Kit wins.
Execution Drift: ADO wins.
PR Drift: Spec‑Kit must be updated to reflect PR feedback status.
6. Sync Governance
Push: Allowed only when hierarchy is valid and no unresolved intent drift exists.
Pull: Always allowed; updates mirror file and flags drift.
Reconcile: Required when drift exists; user chooses which system wins.
7. Authentication Governance
Use Entra ID for authentication to ADO
<!--OAuth via Azure DevOps MCP server and GitHub App/OAuth. No PATs or secrets in repos.-->
8. Reporting & Audit Governance
Sync layer maintains timestamps, drift history, PR feedback mapping, and work item lineage for traceability and compliance.
9. Release & Sprint Governance
Sprints: ADO owns sprint dates; Spec‑Kit references sprint numbers.
Releases: Features map to releases; User Stories map to sprints; tasks map to User Stories.
10. Summary
This governance model ensures clean separation of intent, execution, and code; deterministic sync behavior; enforced hierarchy; PR feedback integration; and enterprise‑grade auditability.
