# Reference ecosystem

This document defines how DSPRE research should use external implementations and historical project
knowledge. It replaces migration-only reference notes as the portable, public-safe policy.

## Source order

Prefer evidence in this order:

1. Current DSPRE production code, project files, tests, and bundled data definitions.
2. Local Git history for the commit that introduced or changed the behavior.
3. Public specifications and public upstream repositories.
4. Independent implementations used as cross-checks, after checking provenance and licensing.
5. Direct measurements from representative ROM data or emulator behavior.
6. Historical notes and assistant memories, used only as search leads.

The current tree wins over historical summaries. Runtime observation can reveal that the current tree
is wrong, but the observation and reproduction conditions must be recorded before changing an
architectural or binary-format claim.

## Public repositories used by the project

- [DS-Pokemon-Rom-Editor/ds-rom](https://github.com/DS-Pokemon-Rom-Editor/ds-rom) is the ds-rom
  implementation built by the repository's release and canary workflows.
- [DS-Pokemon-Rom-Editor/scrcmd-database](https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database)
  supplies the script command databases cloned by those workflows and linked from the README.

These relationships are verified by the current workflow files. A repository's presence here does
not make every branch, version, or implementation detail authoritative for DSPRE.

## Recording a finding

A durable research note should state:

- the exact question and affected game family or version;
- the public source, current code path, commit, test, or measurement used;
- whether the result was merely located, statically traced, built, tested, or observed at runtime;
- competing interpretations and counter-evidence;
- the complete set used for any coverage or parity claim;
- remaining uncertainty and a concrete way to resolve it.

Use stable public links where practical. For Git history, record a commit identifier and the behavior
it establishes rather than an author's identity or a copied private discussion.

## Non-public material

ROMs, save states, emulator captures, local paths, private repositories, unpublished documentation,
and assistant transcripts may guide an investigation, but tracked notes must not reveal their
identifying paths, filenames, quotations, or personal details.

Retain a claim derived from non-public material only when it is independently supported by public
sources, current DSPRE code, Git history, tests, or reproducible measurement. Otherwise keep it out
of normative documentation and label it unverified in the handoff.

## Cross-checking another implementation

Before adopting a result from another tool:

1. Identify the exact parser, table, or behavior being compared.
2. Confirm the implementation targets the same game family and format variant.
3. Check whether it reads metadata dynamically or assumes a fixture-specific layout.
4. Compare more than one representative input when the format permits variation.
5. Search DSPRE for an existing reader or renderer before introducing another one.
6. Add a meaningful round-trip, differential, or runtime test when the result affects production
   behavior.

External code is reference evidence, not permission to copy it. Check its license and adapt the
verified behavior to DSPRE's current project boundaries.

## Maintenance

Keep subject findings in the existing `Research/` hierarchy and link them from
`Research/ResearchNotes.md`. Update or remove a note when the implementation or evidence changes.
Avoid volatile test counts, local machine details, and status snapshots in permanent guidance.
