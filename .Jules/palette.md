## 2026-11-11 - Missing AcceptButton/CancelButton in WinForms
**Learning:** Several WinForms lack keyboard accessibility (Enter/Escape) due to missing `AcceptButton` and `CancelButton` properties, violating UX standards and AGENTS.md Rule 5.
**Action:** Add `this.AcceptButton = ...;` and `this.CancelButton = ...;` to the `InitializeComponent` method of all forms.
