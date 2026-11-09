# Dispatch for Worker M2 Fix (Error Propagation for autocad_inspect_drawing)
- Working directory: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\worker_m2_fix
- Original Request: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\ORIGINAL_REQUEST.md
- Scope Document: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\PROJECT.md
- Challenger Failure Report: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\challenger_m2_2\handoff.md
- Test Harness: c:\Dropbox\0.AI AGENT\11.Autocad Drawing\test_challenger_m2.py

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Objective:
Fix error propagation in `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`:
When `autocad_inspect_drawing(document_name=...)` is called for a non-existent drawing:
1. `_collect_modelspace_entities_for_preview` must NOT fall back to `_collect_modelspace_entities_via_lisp()` on the active drawing.
2. `autocad_inspect_drawing` must surface the C# 404 error with the open drawing list.
3. Run `python "c:\Dropbox\0.AI AGENT\11.Autocad Drawing\test_challenger_m2.py"` to verify tests pass.
4. Run `python -m py_compile "C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py"`.
5. Document changes in `changes.md` and write `handoff.md`.
