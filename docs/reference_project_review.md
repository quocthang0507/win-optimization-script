# Reference project review

Review date: 2026-07-17

## Scope

The two reference projects used consistently by this repository are:

- [FluentCleaner](https://github.com/builtbybel/FluentCleaner): focused WinUI cleanup, explicit Winapp2 rules, preview-first interaction, automation, and detailed logs.
- [Winhance](https://github.com/memstechtips/Winhance): searchable Windows settings, reversible toggles, software management, presets, and reusable configuration files.

The implementation borrows product ideas and interaction principles only. No source code or protected assets are copied from either project.

## Comparison and decisions

| Area | Reference strength | Current decision |
| --- | --- | --- |
| Cleanup safety | FluentCleaner uses explicit, inspectable rules and user selection | Keep Winapp2 rules explicit; scan and show file count/size before deletion; require confirmation |
| Cleanup history | FluentCleaner records automatic cleanup details | Persist every maintenance result as JSON plus a readable text log in `logs/` |
| Optimize navigation | Winhance provides search and quick navigation | Add text search and category filtering to System Tweaks |
| Reusable settings | Winhance supports configuration files | Retain import/export, validate unknown IDs, and add confirmed built-in presets |
| Reversibility | Winhance models settings as reversible controls | Verify the state after applying each tweak instead of assuming command success |
| Software management | Both projects organize app actions around clear selections | Continue using searchable WinGet and installed-app views with explicit confirmations |

## Implemented in this review

- Dedicated `logs/` and `backups/` directories.
- Atomic JSON maintenance reports and matching readable `.log` files.
- Reliable latest-report lookup for Dashboard and History.
- Winapp2 shown only on Cleanup, not Repair.
- Winapp2 preview with selected app count, exact file count, estimated size, and permanent-delete warning.
- Winapp2 cleanup results saved to History, including skipped files and errors.
- Search and category filters for System Tweaks.
- Balanced, Gaming, and Windows-default presets with confirmation and progress.
- Profile import ignores unknown IDs explicitly and reports partial failures.
- Profile export remains complete even while the UI is filtered.
- Post-apply state verification for every tweak.
- Unified acknowledgment text for FluentCleaner and Winhance in both READMEs and Settings.
- Grouped, collapsible navigation with the duplicate Tweaks entry consolidated into System Tweaks.
- Persistent window size and navigation-pane state.
- A consistent maximum content width for readability on large displays.
- Software Installer bulk selection, visible-result counts, localized actions, and accessible checkbox names.

## Recommended next increments

1. Move Winapp2 scanning/execution from the view into a dedicated service and add wildcard-path and `ExcludeKey` support.
2. Add cancellation and per-item progress to Winapp2 preview and cleanup.
3. Persist the user's Winapp2 selection and optionally expose a scheduled cleanup command.
4. Consolidate the duplicate Tweaks and Optimize navigation entries into one page.
5. Expand tweak metadata with risk, supported Windows builds, restart requirements, and Microsoft documentation links.
6. Add optional-feature and legacy-capability management after defining rollback and compatibility tests.
