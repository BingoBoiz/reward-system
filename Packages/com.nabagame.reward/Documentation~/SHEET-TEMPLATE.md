# Sheet template — obsolete

**Obsolete since the 0.5.0 contract flip (2026-08-20, ARCHITECTURE decision #16).** The package no longer defines importer-shaped config ScriptableObjects, so there is no package sheet contract to template. Research of the actual consumer team showed the Googlesheet Importer installed but unused (CONSUMER-STYLE.md).

Data is now filled directly into each feature manager's serialized row list — see INTEGRATION-GUIDE.md §4.

If your game authors reward content in Google Sheets anyway, that is host-side tooling: import into your own ScriptableObject however you like, then copy the rows into the manager's list before `StartClass`. The pre-0.5.0 tab layouts remain in this file's git history if you want a starting point.
