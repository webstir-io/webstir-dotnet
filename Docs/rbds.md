# Requirements Based Development System (RBDS)

A simple system for building features. Use links, not IDs.

## Process

1. List feature sets.
2. List features in each set.
3. Define customer requirements per feature.
4. Define product requirements per customer requirement.
5. Define software requirements per product requirement.
6. In each hub doc, prioritize product requirements.
7. Aggregate prioritized product requirements into the central backlog.

## Document Structure

- `docs/features/` holds feature sets (e.g., `bundling`, `framework`).
- Each set has feature folders (e.g., `graph-resolution`, `data-fetching`).
- Each feature has `requirements.md` with CR/PR/SR and acceptance. Prioritization lives in the hub doc.
- Each set has a hub doc named after the set (e.g., `features/bundling/bundling.md`) that:
  - Describes the set
  - Indexes sub‑feature requirements
  - Lists prioritized product requirements per feature

## Backlog

The backlog:
- Aggregates prioritized product requirements from hub docs
- Links to each feature’s `requirements.md`
- Uses sections: Active, Todo, Done
- Is ordered by priority (top first)

## Instructions

- Always align features and requirements with the mission of this project.
