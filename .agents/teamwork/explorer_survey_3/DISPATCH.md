# Dispatch for Explorer Survey 3 (Build Compliance, Civil 3D Parcel/Table Mechanics & E2E Testing)
- Working directory: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_3
- Original Request: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\ORIGINAL_REQUEST.md
- Objective: Survey build environment (BuildProject.ps1, .NET 10, references, CAD_Reloader), Civil 3D Parcel geometry/statistics/site extraction APIs, AcDbTable generation and view/preview framing mechanics, test infrastructure, and non-destructive verification strategies against live AutoCAD session. Report findings in report.md and handoff.md.

## 2026-11-09T08:17:39Z
[Message] sender=0bacc69e-6c1a-4e6c-a083-63d1ef025e4f priority=MESSAGE_PRIORITY_HIGH
You are Explorer Survey 3 (Civil 3D & Build Explorer).
Working directory: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_3
Original Request file: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\ORIGINAL_REQUEST.md

You MUST read ORIGINAL_REQUEST.md before starting work.
Investigate:
1. Build system: Check BuildProject.ps1, .NET SDK requirements, solution setup, CAD_Reloader vs NetReload, and how compilation is structured.
2. Civil 3D Parcel geometry, area (via Statistics), Site membership, Parcel styles in Autodesk.Civil.DatabaseServices.
3. Native AcDbTable creation in AutoCAD (Table, TableStyle, formatting, rows/columns, borders) vs any current custom line/text implementation, and export_preview framing mechanics.
4. Non-destructive testing strategy: How to test the MCP server and AcadMcpBridge against the live AutoCAD/Civil 3D session without corrupting user drawings.
5. Write your detailed findings, code references, and recommendations into:
   - c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_3\report.md
   - c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_3\handoff.md
Update your progress.md periodically with "Last visited". When complete, send a message to the orchestrator.
