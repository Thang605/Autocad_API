## 2026-11-09T08:16:57Z
You are Explorer Survey 1 (C# Bridge Explorer).
Working directory: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_1
Original Request file: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\ORIGINAL_REQUEST.md

You MUST read ORIGINAL_REQUEST.md before starting work.
Investigate the C# bridge (AcadMcpBridge.cs and any related source files in c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject):
1. How does AcadMcpBridge establish IPC / communicate with the MCP server? Where is the server listening / connecting?
2. How does AcadMcpBridge currently handle document context (active document vs specific document)? How can we support targeting the active document by default with an optional target drawing name (R1)?
3. What inspection commands exist on the C# side (e.g. civil3d_inspect, autocad_inspect_drawing)? How are entities queried?
4. How are Civil 3D entities (Parcels, Parcel Segments, Tables, Alignments, Corridors, Surfaces) currently queried and represented?
5. How does table drawing currently work in C# (autocad_draw_table)? Does it create empty text entities or use native AcDbTable?
6. Write your detailed findings, code references, and recommendations into:
   - c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_1\report.md
   - c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_1\handoff.md
Update your progress.md periodically with "Last visited". When complete, send a message to the orchestrator.
