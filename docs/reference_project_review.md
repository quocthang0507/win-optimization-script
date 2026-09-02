# Reference project review

Review date: 2026-09-02. Previous review: 2026-07-17.

## Scope and sources

All six application references in the README and Settings were checked against upstream documentation and release notes. Microsoft WinUI/.NET are framework credits, not feature-donor apps. Versions below are those displayed by the sources on the review date; an upstream feature may be older but still new to this app.

| Reference | Verified upstream changes / ideas | Decision for this app |
| --- | --- | --- |
| [FluentCleaner 26.08.03](https://github.com/builtbybel/FluentCleaner/releases/tag/26.08.03), Aug 30 | Cookie Manager with per-site keep lists and search; clearer Settings sections. Existing cleanup philosophy emphasizes explicit selection and inspectable rules. | Searchable Winapp2 selection, visible warnings, warning-bearing/custom entries opt-in. Defer per-site cookie editing. |
| [Winhance v26.06.12](https://github.com/memstechtips/Winhance/releases), Jun 12 | Builder Mode, change history, accessibility/scaling improvements; imports avoid unnecessary changes. | Before/after preset review, skip unchanged settings, record verified batch results in History. Builder Mode remains future work. |
| [Win11Debloat 2026.08.24](https://github.com/Raphire/Win11Debloat/releases/tag/2026.08.24) | Backup/restore validation, cancellation fixes, WinGet uninstall timeout/verification, desktop Spotlight icon option. | Strengthen profile/snapshot validation; require readable states before applying/restoring. No transplanted scripts or automatically enabled new tweaks. |
| [Optimizer](https://github.com/hellzerg/optimizer) | Original project deprecated in favor of OptimizerNXT; README lists 16.7 as its last version. | Keep acknowledgment and existing network/UTC toolbox features. No aggressive service, Defender, update or HPET changes. Successor not audited in this pass. |
| [QDirStat 2.0](https://github.com/shundhammer/qdirstat), Jan 18 | Qt6, translation infrastructure, ISO date formatting; existing Find/bookmark/discovery features remain relevant. | Improve existing filtering and preserve tabs. Dates already use ISO-like formatting. Recursive indexed search and bookmarks remain future work. |
| [WinMole 0.1.1 / 0.1.0](https://github.com/bhadraagada/winmole/blob/master/CHANGELOG.md), Jul 31 / Jul 28 | Terminal repaint fixes, corrected disk totals and explicit partial-scan indicators, cleanup robustness. | Lower-bound totals for partial/excluded/inaccessible scans. Keep protected paths and project-marker-based artifact discovery. Terminal changes do not apply to WinUI. |

Only product ideas and documented behavior were used. Changes are independently implemented in C#/WinUI; no upstream code, scripts, icons or assets were copied. Upstream license terms differ; acknowledgment is not a substitute for permission to reuse source.

## Implemented in this increment

### Cleanup / FluentCleaner-inspired selection

- Search detected Winapp2 entries by application/category without discarding selections.
- Display an entry's `Warning` before selection, carry it into preview, and retain it in reports.
- Do not preselect warning-bearing or custom-database entries, even if they declare `Default=True`.
- Show actual preview warnings, not only a count. Disable scan/picker controls during an operation; restore them after failure or cancellation of confirmation.
- Registry rules remain excluded from deletion.

### Tweaks / Winhance and Win11Debloat-inspired review

- Strict boolean profile parsing; reject empty/duplicate IDs, including case-only duplicates.
- Canonicalize recognized IDs, report unknown IDs, and omit unchanged settings from execution.
- Refresh machine states before constructing a preset plan; refuse it if a recognized setting's current state is unavailable.
- Show proposed before/after states with Cancel as the default action.
- Require a saved undo snapshot before a non-empty batch; record per-setting verified results/failures in JSON/text History reports.
- Ignore malformed, oversized, unreadable or linked snapshots without breaking History.
- Restore checks recognized current states and refuses unsupported IDs before changing anything. Failed restoration retains the snapshot.

Snapshots still record this app's **boolean tweak states**, not arbitrary registry value types or an exact full-system backup. Existing per-tweak post-apply verification remains in place.

### Storage / QDirStat and WinMole-inspired clarity

- Preserve Details/Discover and nested discovery tabs when filtering or receiving scan updates.
- Filter every retained direct child before the display cap, fixing missed matches beyond the former 400-item cutoff.
- Show displayed/matching/retained discovery counts. Bounded discovery lists are not a complete recursive search index.
- Mark enumeration-error scans partial; display `≥` and an explanation when totals exclude items or are incomplete.
- Do not expand deletion permissions or automatically select cleanup candidates.

## Follow-up priorities (not implemented)

1. **Cookie keep list**: browser-specific SQLite transactions, closed-browser detection, schema/WAL compatibility, profile selection and recovery tests are prerequisites. Never delete the whole cookie database for selective retention.
2. **Cancellable batches / uninstall timeout**: preserve partial reports and distinguish timed-out installers from successful removal.
3. **Configuration builder**: separate desired-state editing from immediate toggles; export without changing this PC.
4. **Storage bookmarks / bounded recursive index**: define memory and stale-scan behavior first.
5. **Windows customizations**: consider Spotlight-icon visibility after build compatibility and rollback behavior are verified.

## Verification

Regression tests cover invalid/duplicate profile values, canonical IDs, unknown/unreadable states, malformed snapshots, Winapp2 warnings/default-selection policy, search beyond 400 entries, and incomplete-total semantics. No host-PC cleanup, tweak application, app removal or snapshot restoration is performed during review.

- Debug build: succeeded with 0 warnings and 0 errors.
- Automated tests: 165 passed, 0 failed, 0 skipped.
- Read-only desktop checks: Winapp2 search preserves selections; preset preview shows before/after states and cancels without applying; storage search filters the scanned model folder from 42 to 4 matching files while keeping the Details tab selected.
- No release, tag or push was created.
