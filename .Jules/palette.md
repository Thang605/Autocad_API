
## 2026-11-07 - [WinForms Keyboard Accessibility]
**Learning:** Several common WinForms lacked default Enter/Esc keyboard hooks (AcceptButton/CancelButton), severely slowing down power users navigating modals.
**Action:** Add AcceptButton and CancelButton mappings systematically at the end of InitializeComponent to ensure default fast-paths.
