This gpt_brief..md file defines the structure and content expectations for a Project Summary — a single project-altitude orientation doc that maps the whole mod in one glance and serves as the entry point into every lower brief.

# Project Summary

## Purpose
A Project Summary is the top of the brief hierarchy. It sits above every Feature Brief and In-Repo Doc.

It captures **what the whole project is, how big each part is, and where the priorities sit** — in the most compressed form that still lets a reader orient before opening anything else.

It is the first artifact a reader hits. From it they should be able to name the domains, gauge the relative size and maturity of each, see what is being worked on next, and follow a link down into any feature without first hunting through the file tree.

It is a navigation hub, not a content store. It points at Domain and Feature briefs; it does not restate their loops, rules, or implementation. When a domain or feature changes shape, the Summary's links and size/status rows update — its prose does not grow.

It lives at the repo root, normally as `README.md`, so it doubles as the GitHub front page and the in-code wiki spine. Relative markdown links from it into the `.git.md` / `.gpt.md` briefs render and navigate in the GitHub web UI.

A Project Summary does not change per session. It evolves when a domain is added, removed, or meaningfully re-prioritised.

---

## Scope Field

A Project Summary is always `Project` scope. It is the only artifact at that scope. Everything it links to is `Domain`, `Feature`, or `Subsystem`.

| Scope | Meaning | Example |
|---|---|---|
| `Project` | The whole mod — every domain under one roof | OddWire |

---

## Structure

```md
# <Project Name>

<one-line description — system terms, what the mod adds>

---

## What this is
One short paragraph. What the mod is and the shape of its content. Not marketing.

---

## Domains

| Domain | Adds | Size | Status |
|---|---|---|---|
| ... | ... | ~Nk LOC | Active / Support / Draft |

---

## Priorities
Short block or list — what is active now, what is next, what is parked.

---

## Docs map
Nav tree of relative links into the Domain and Feature briefs. This is the wiki spine.

- **<Domain>** — [Brief](path/.git.md) · [Doc](path/.gpt.md)
  - <Feature> — [Brief](path/.git.md) · [Doc](path/.gpt.md)

---

## Build / Usage
Only the practical instructions a contributor needs — build command, output location.
```

---

## Section Rules

### Description line
One line, system terms. What the mod adds, not who it is for.

Good:
> Vintage Story mod — farming and survival quality-of-life systems.

Bad:
> The ultimate farming overhaul that makes the game feel alive.

### What this is
One paragraph. The shape of the project — how many domains, what kind of content (blocks, patches, extensions). Enough for a reader to know whether what they want is in here. No design rationale.

### Domains table
One row per domain. Four columns: domain name, one-line of what it adds, rough code size, status.
- Size is approximate (`~2.1k LOC`) — a sense of weight, not an exact count.
- Status is `Active` (under development), `Support` (shared/stable plumbing), `Draft`, or `Parked`.
- Include support areas (shared extensions, common renderers) as their own rows so the size picture is honest.

### Priorities
What is being worked on, what is next, what is deliberately parked. Keep it current — this is the one section expected to move between milestones. A few lines is enough.

### Docs map
The navigation spine. One entry per domain, nested entries per feature, each with relative links to its `.git.md` brief and `.gpt.md` doc. Use the smallest honest tree — do not link to docs that do not exist yet; mark gaps as `(todo)` rather than dead links.

### Build / Usage
Only what a contributor actually runs. Drop template boilerplate and migration notes that do not apply to this project.

---

## Production

A Project Summary is produced once the mod has more than one domain or enough content that a flat file tree no longer orients a newcomer. It is normally written as the repo-root `README.md` so it is also the GitHub landing page.

When to produce one:
- when the repo has grown past a single feature
- when the front page is still a template or empty and the repo is going public

When to update one:
- when a domain is added, removed, or renamed
- when priorities shift between milestones
- when a new Domain or Feature brief is added that the docs map should link

When not to update one:
- to record per-session change detail — that is the Code Brief's job
- to restate a feature's loop or rules — that lives in the Feature Brief and In-Repo Doc

---

## Consumption

**By humans browsing the repo / GitHub:**
- the first thing seen on the front page — names the domains and links into everything
- the in-code wiki spine: README → Domain brief → Feature brief → code

**By Plan and Code:**
- cheap top-level orientation before dropping into a specific domain or feature
- tells them what else exists so a change in one domain accounts for its neighbours

---

## What Good Looks Like

- Readable in under 30 seconds; a newcomer can name every domain afterwards
- Domains table makes relative size and maturity obvious at a glance
- Priorities section reflects what is actually being worked on right now
- Every docs-map link resolves to a real file that renders on GitHub
- Points down into detail; never duplicates it

## What Bad Looks Like

- Restates feature loops, rules, or implementation that belong in lower briefs
- Marketing prose in the description or "What this is" line
- Docs map has dead links to docs that do not exist
- Size/status rows drift and no longer reflect the repo
- Still carries template boilerplate the project never used
