# Blob Copy Application Constitution

<!--
Sync Impact Report (2026-01-20):
- Version change: Template → 1.0.0 (Initial ratification)
- Principles defined: 7 core principles established
- Sections added: Core Principles, Security & Compliance, Development Workflow, Governance
- Templates status: All aligned with initial constitution
-->

## Core Principles

### I. Specification-Driven Development

Every feature MUST begin with complete specification artifacts before implementation:
- `spec.md` with functional requirements, non-functional requirements, user stories, and acceptance criteria
- `plan.md` with architecture decisions, data models, and technical implementation approach
- `tasks.md` with granular, testable task breakdown mapped to requirements

**Rationale**: Prevents scope creep, ensures traceability, and enables parallel workstream planning.

### II. Azure-First Architecture

All cloud services and integrations MUST use Azure-native solutions where available:
- Azure Blob Storage for blob operations
- Azure Entra ID (formerly Azure AD) for authentication
- Azure Application Insights for telemetry and monitoring
- DefaultAzureCredential for authentication (supports Azure CLI, Managed Identity, Environment Variables)

**Rationale**: Ensures consistency, leverages Microsoft ecosystem, simplifies authentication and compliance.

###

 III. Security by Default (NON-NEGOTIABLE)

Security MUST be built-in from the start, not added later:
- OAuth 2.0 with PKCE for all authentication flows
- HttpOnly, Secure, SameSite=Strict cookies for sensitive tokens
- CSRF protection via state parameter validation
- No inline credentials or connection strings in code or UI
- Sensitive data MUST NOT appear in logs or error messages

**Rationale**: Security vulnerabilities discovered post-deployment are exponentially more expensive to fix.

### IV. Test Coverage Standards

Every user story MUST have independent test coverage:
- Unit tests for business logic and services
- Integration tests for API contracts and external dependencies
- End-to-end tests validating complete user workflows
- Accessibility compliance (WCAG AA) verified via automated tooling

**Rationale**: Enables confident refactoring, prevents regressions, ensures quality at every layer.

### V. Observability & Monitoring

Production systems MUST be observable:
- Application Insights telemetry for all critical paths
- Performance metrics tracked against success criteria (e.g., auth <10s, copy progress updates every 5s)
- Error rates and failure modes monitored with alerting
- Structured logging with correlation IDs for distributed tracing

**Rationale**: You cannot fix what you cannot measure; observability enables proactive issue detection.

### VI. Explicit Over Implicit

Configuration, dependencies, and assumptions MUST be explicit:
- Environment variables documented in quickstart.md
- Package versions pinned in dependency files
- Assumptions section required in all spec.md files
- No "magic" configuration defaults without documentation

**Rationale**: Reduces onboarding friction, prevents environment-specific bugs, aids troubleshooting.

### VII. Incremental Delivery

Features MUST be broken into independently testable user stories with clear priorities (P0/P1/P2):
- P0 (Foundational): Blocking infrastructure required for all other stories
- P1 (MVP): Core value delivery that defines feature success
- P2 (Enhancement): Error handling, edge cases, polish

**Rationale**: Enables iterative delivery, de-risks large features, allows early user feedback.

## Security & Compliance

### Authentication & Authorization
- All users MUST authenticate via Azure Entra ID before accessing any feature
- Spec-Kit owns task state transitions and assignments; Azure DevOps owns Epic/Feature/User Story state
- Token refresh MUST occur automatically at least 5 minutes before expiration
- Logout MUST clear all session data and revoke tokens

### Data Protection
- Tokens stored in-memory (access tokens) or HttpOnly cookies (refresh tokens) only
- No sensitive data persisted to browser localStorage or sessionStorage
- All API endpoints MUST validate authentication tokens on every request
- Blob URIs validated against Azure Blob Storage naming rules

## Development Workflow

### Specification Lifecycle
1. **Specify**: Use `/speckit.specify` to create spec.md from requirements
2. **Clarify**: Use `/speckit.clarify` to resolve ambiguities with stakeholders
3. **Plan**: Use `/speckit.plan` to define architecture and data models
4. **Tasks**: Use `/speckit.tasks` to generate granular task breakdown
5. **Analyze**: Use `/speckit.analyze` to validate cross-artifact consistency
6. **Implement**: Execute tasks in priority order (P0 → P1 → P2)
7. **Sync**: Use `/speckit.ado` to synchronize work items with Azure DevOps

### Quality Gates
- No feature implementation begins without approved spec.md, plan.md, and tasks.md
- All CRITICAL and HIGH findings from `/speckit.analyze` MUST be resolved before implementation
- Tasks marked complete (`[X]`) in tasks.md MUST sync to "Closed" state in Azure DevOps
- Code reviews verify adherence to architecture decisions in plan.md

### Azure DevOps Integration
- Spec-Kit is the source of truth for task definitions and task state
- Azure DevOps is the source of truth for Epic/Feature/User Story execution state
- Drift detection runs automatically; intent drift (spec changes) wins over execution drift (ADO changes)
- All work items tagged with `Speckit` for traceability

## Governance

The constitution supersedes all other practices. Amendments require:
1. Documented rationale for the change
2. Version bump following semantic versioning (MAJOR.MINOR.PATCH)
3. Update to impacted templates in `.specify/templates/`
4. Sync Impact Report documenting changes and follow-up actions

All pull requests and code reviews MUST verify constitutional compliance. Complexity that violates a principle MUST be justified in writing or refactored.

**Version**: 1.0.0 | **Ratified**: 2026-01-20 | **Last Amended**: 2026-01-20
