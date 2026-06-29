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
One short paragraph. What the mod does in the game and a quick summary of its
features — the spec, not the code architecture. End with what is in progress.

---

## Domains & docs
A single nav tree: the wiki spine and the size/status map in one. Each entry links
to its Brief and Doc, then carries status, size, and a one-line description.

- **<Domain>** — [Brief](path/.git.md) · [Doc](path/.gpt.md)
  - **<Feature>** — [Brief](path/.git.md) · [Doc](path/.gpt.md) — Status, ~Nk LOC<br><one-line>
- **<Domain>** — _(todo)_

Shared / support areas listed below or as their own rows so the size picture is honest.

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
One paragraph. What the mod does in the game (the spec) and a quick run through the features — what the player interacts with, not how the code is laid out. Close with what is in progress. No domain-count/architecture inventory, no design rationale, no marketing.

Good:
> OddWire adds hands-on organic systems. The CompostPile turns dry grass and food scraps into compost over time; watering runs through the vanilla can. More features are in progress.

Bad:
> OddWire is organised into self-contained domains, each holding features and Harmony patches over a shared base of API extensions.

### Domains & docs
One merged tree — the navigation spine and the size/status map together. One entry per domain, nested entries per feature, each with:
- relative links to its `.git.md` brief and `.gpt.md` doc (omit a link that does not exist; mark whole gaps as `(todo)` rather than leaving dead links),
- status — `Active` (under development), `Support` (shared/stable plumbing), `Draft`, or `Parked`,
- approximate size (`~1.9k LOC`) — a sense of weight, not an exact count,
- a `<br>` one-line description of what the feature does.

Include support areas (shared extensions, common renderers) so the size picture is honest. What is in progress is carried by the status field and the closing line of *What this is* — no separate priorities section.

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
- Domains & docs tree makes relative size, maturity, and what each feature does obvious at a glance
- The closing line of *What this is* and the status fields reflect what is actually being worked on
- Every link in the tree resolves to a real file that renders on GitHub
- Points down into detail; never duplicates it

## What Bad Looks Like

- Restates feature loops, rules, or implementation that belong in lower briefs
- Marketing prose in the description or "What this is" line
- Docs map has dead links to docs that do not exist
- Size/status rows drift and no longer reflect the repo
- Still carries template boilerplate the project never used
