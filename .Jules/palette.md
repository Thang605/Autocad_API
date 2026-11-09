## 2026-11-09 - Adding WinForms Keyboard Accessibility Shortcuts
**Learning:** WinForms dialogs commonly lacked the `AcceptButton` and `CancelButton` properties, requiring mouse clicks and breaking expected keyboard navigation UX (Enter for OK/Apply, Escape for Cancel/Close).
**Action:** Always set `AcceptButton` and `CancelButton` properties on newly created or modified WinForms as noted in AGENTS.md to ensure keyboard accessibility.
