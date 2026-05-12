from __future__ import annotations

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
from xml.sax.saxutils import escape


OUT = Path("docs/status/Gymers-Mobile-App-Status-Update.docx")


def p(text: str = "", style: str | None = None, bold: bool = False, color: str | None = None) -> str:
    ppr = f"<w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr>" if style else ""
    rpr_parts = []
    if bold:
        rpr_parts.append("<w:b/>")
    if color:
        rpr_parts.append(f"<w:color w:val=\"{color}\"/>")
    rpr = f"<w:rPr>{''.join(rpr_parts)}</w:rPr>" if rpr_parts else ""
    return f"<w:p>{ppr}<w:r>{rpr}<w:t xml:space=\"preserve\">{escape(text)}</w:t></w:r></w:p>"


def bullet(text: str) -> str:
    return (
        "<w:p><w:pPr><w:pStyle w:val=\"ListParagraph\"/>"
        "<w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr>"
        f"<w:r><w:t>{escape('• ' + text)}</w:t></w:r></w:p>"
    )


def cell(text: str, fill: str = "FFFFFF", bold: bool = False) -> str:
    rpr = "<w:rPr><w:b/></w:rPr>" if bold else ""
    return (
        "<w:tc><w:tcPr><w:tcW w:w=\"0\" w:type=\"auto\"/>"
        f"<w:shd w:fill=\"{fill}\"/><w:tcMar>"
        "<w:top w:w=\"120\" w:type=\"dxa\"/><w:left w:w=\"120\" w:type=\"dxa\"/>"
        "<w:bottom w:w=\"120\" w:type=\"dxa\"/><w:right w:w=\"120\" w:type=\"dxa\"/>"
        "</w:tcMar></w:tcPr>"
        f"<w:p><w:r>{rpr}<w:t xml:space=\"preserve\">{escape(text)}</w:t></w:r></w:p></w:tc>"
    )


def table(headers: list[str], rows: list[list[str]]) -> str:
    borders = (
        "<w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/>"
        "<w:tblBorders>"
        "<w:top w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "<w:left w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "<w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "<w:right w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "<w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "<w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"D0D5DD\"/>"
        "</w:tblBorders></w:tblPr>"
    )
    out = ["<w:tbl>", borders]
    out.append("<w:tr>" + "".join(cell(h, "F2F4F7", True) for h in headers) + "</w:tr>")
    for row in rows:
        out.append("<w:tr>" + "".join(cell(c) for c in row) + "</w:tr>")
    out.append("</w:tbl>")
    return "".join(out)


def placeholder(title: str, description: str) -> str:
    return (
        p(title, "Heading3")
        + "<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/>"
        + "<w:tblBorders><w:top w:val=\"dashed\" w:sz=\"8\" w:color=\"98A2B3\"/>"
        + "<w:left w:val=\"dashed\" w:sz=\"8\" w:color=\"98A2B3\"/>"
        + "<w:bottom w:val=\"dashed\" w:sz=\"8\" w:color=\"98A2B3\"/>"
        + "<w:right w:val=\"dashed\" w:sz=\"8\" w:color=\"98A2B3\"/>"
        + "</w:tblBorders></w:tblPr><w:tr><w:tc><w:tcPr><w:shd w:fill=\"F8FAFC\"/>"
        + "<w:tcMar><w:top w:w=\"220\" w:type=\"dxa\"/><w:left w:w=\"220\" w:type=\"dxa\"/>"
        + "<w:bottom w:w=\"220\" w:type=\"dxa\"/><w:right w:w=\"220\" w:type=\"dxa\"/></w:tcMar>"
        + "</w:tcPr>"
        + p("Insert screenshot here. " + description)
        + "</w:tc></w:tr></w:tbl>"
    )


def document_xml() -> str:
    completed_rows = [
        ["Project setup",
         "Completed",
         ".NET MAUI app targeting net10.0-ios, with net10.0-maccatalyst added on 2026-05-09 as a secondary target for fast local verification. Build succeeds with 0 warnings and 0 errors on both targets."],
        ["Login screen",
         "Completed",
         "Validates fixed admin/staff credentials per the selected role pill (admin/admin123, staff/staff123). An inline error label surfaces empty-field and bad-credential states; correct credentials route to the Dashboard."],
        ["Admin dashboard",
         "Completed",
         "KPI cards for Total Members, Today's Attendance, and Monthly Earnings (sample figures) plus a Live Capacity card with zone breakdown, a Coach Spotlight card, and a Today's Classes list rendered from sample class sessions."],
        ["Member management",
         "Completed",
         "Live name-search filter over the in-memory member list with a muted empty-state notice when no matches are found. Search results re-render imperatively as the user types."],
        ["Payment processing",
         "Completed",
         "Form validates Member name (must match an existing member), Amount (positive number with up to 2 decimals), and Method (Card/Cash/Bank, case-insensitive). Recorded payments insert into the store and appear at the top of Recent Payments. A status label flips between error (red) and success (green) states."],
        ["Attendance monitoring",
         "Completed",
         "Search field surfaces up to 3 member suggestions, or auto-selects on an exact name match. CHECK IN inserts a CheckIn into the store; the Recent Check-ins list updates with the new row at the top. An empty store renders a muted fallback notice."],
        ["In-memory data layer",
         "Completed",
         "DataStore singleton exposes ObservableCollection of Members, Payments, and CheckIns, seeded from SampleData. Pages subscribe to CollectionChanged and re-render. Registered as a DI singleton; pages are transient."],
        ["SQLite persistence",
         "Completed",
         "DataStore is now SQLite-backed via sqlite-net-pcl. Members, payments, and check-ins persist across app restart in gymers.db3 under FileSystem.AppDataDirectory. Bootstrap seeds from SampleData on first run; runtime mutations write through SQLiteAsyncConnection."],
        ["Receipt PDF generation",
         "Completed",
         "Tapping any row in Recent Payments renders a one-page PDF receipt via UIKit's UIGraphicsPdfRenderer (built into iOS and Mac Catalyst SDKs, no third-party packages), saves under FileSystem.CacheDirectory/receipts/, and opens the system share sheet for save / email / AirDrop / print. Re-issues are deterministic from SQLite, so any historical payment can be re-printed; deleted-member receipts gracefully fall back to a placeholder."],
        ["Reports + export",
         "Completed",
         "A Reports tab generates Revenue, Attendance, and Member Roster reports for a chosen period (Week / Month / All). Each report can be shared as a multi-page PDF (UIKit's UIGraphicsPdfRenderer) or a CSV (plain UTF-8, RFC 4180-style quoting with LF line endings), via the system share sheet — save to Files, email, AirDrop, or open in Numbers."],
        ["Trainer roster",
         "Completed",
         "A SQLite-backed Trainers screen lists all trainers with a live name-search filter; rows render with initials avatars and 'Title · Rating · Sessions' subtitles. The Dashboard's Coach Spotlight now reads from the trainers table (top by rating, with sessions as a tiebreaker), and its VIEW PERFORMANCE PROFILE button navigates to the Trainers screen via //Trainers."],
        ["Workout plans",
         "Completed",
         "A SQLite-backed Workout Plans screen lists curated plans with a live name-search filter; each row shows the assigned trainer, level, weekly cadence, and total duration. The Dashboard's new Featured Workout Plan card highlights the top-ranked plan (Foundations of Strength, led by Marcus Sterling) and its BROWSE WORKOUT PLANS button navigates to the Workouts screen via //Workouts. Plans are FK'd to trainers, with the trainer name resolved at render time."],
        ["Equipment management",
         "Completed",
         "A SQLite-backed Equipment screen lists the gym's equipment roster with a live name-search filter and an Operational-count KPI; each row shows category, status, and floor location. The Dashboard's new Equipment Status card surfaces operational-vs-maintenance counts (5 of 6 operational with one item under maintenance in the seed) and its VIEW EQUIPMENT button navigates to the Equipment screen via //Equipment."],
        ["Role-based access control",
         "Completed",
         "Login now persists the selected role through a Session singleton (Services/Session.cs), exposed both via a static Current accessor and as a DI singleton. The bottom tab bar reflows from five tabs (Admin) to four (Staff) by hiding Reports and rebuilding the grid's column definitions at construction time. The Dashboard suppresses the Monthly Earnings KPI for Staff and displays a 'Signed in as Admin'/'Signed in as Staff' badge under the top app bar. Payments remains visible to Staff per the README's scope (Staff handles walk-in payments, renewals, and receipts)."],
        ["Member ID scan check-in",
         "Completed",
         "Attendance now exposes a SCAN MEMBER ID primary path alongside the existing name search. Tapping it opens a fullscreen viewfinder overlay (240px bordered box, 'SCANNING...' state); a 1.2-second timer resolves to the next member who hasn't yet checked in today (wrapping to the first member if all are already in) and reveals a detected-member card with name, tier, and ID. CONFIRM CHECK-IN flows through the existing RecordCheckInAsync so the new CheckIn appears at the top of Recent Check-ins; CANCEL dismisses without recording. Simulates the README's 'QR code or ID scanning' attendance path on hardware where a real camera pipeline is out of scope."],
        ["Membership expiry alerts",
         "Completed",
         "The Dashboard now prepends a tappable yellow alert banner listing members whose Status is 'Expiring Soon' (Sam Chen in the seed). The banner shows the count and the comma-joined member names; tap navigates to the Members screen for follow-up. It stays hidden when no memberships are expiring, surfacing only when action is required. Visible to both Admin and Staff per the README's 'Membership Status Viewing' scope."],
    ]

    parts = [
        p("Gymers Mobile Application Project Status Update", "Title"),
        p("Team Fafa | Gym Management System | Status date: May 12, 2026", None, False, "667085"),
        p("Overall Status", "Heading1"),
        p("The Gymers project is a working .NET MAUI iOS app with a Mac Catalyst secondary target for fast local verification. Six core tab screens (Login, Dashboard, Members, Payments, Attendance, Reports) plus three route-only screens (Trainers, Workouts, Equipment) are implemented and persist their state in a SQLite-backed DataStore. Tapping any row in Recent Payments generates a one-page PDF receipt via UIKit's native PDF renderer; the Reports tab generates Revenue, Attendance, and Member Roster reports as multi-page PDF or CSV via the system share sheet. Role-based access control persists the Admin/Staff role from login through navigation, reflowing the bottom tab bar and hiding admin-only Dashboard analytics for Staff. Attendance offers a simulated QR/ID scan check-in alongside name search, and the Dashboard surfaces expiring-membership alerts. The build succeeds with 0 warnings and 0 errors on both iOS and Mac Catalyst. Every README scope item is now implemented; remaining work is broader test coverage and visual polish."),
        p("Completed Features", "Heading1"),
        table(["Feature", "Status", "Description"], completed_rows),
        p("Ongoing Tasks", "Heading1"),
        bullet("Testing and polish: the build is clean and the demo workflow has been manually verified end-to-end on Mac Catalyst, but broader test coverage and visual polish remain."),
        p("Challenges Encountered", "Heading1"),
        bullet("Time constraint: the original scope spans many modules, so this iteration prioritizes a fully working demo of the six core screens over breadth."),
        bullet("Tooling setup: configuring .NET MAUI 10 with Xcode 26.2 for both the iOS simulator and Mac Catalyst targets required matching workload and platform versions."),
        bullet("Scope management: validation and data flow were prioritized first; Trainer, Workout Plan, and Equipment slices then landed once persistence and reports were stable, closing every README scope gap."),
        bullet("Data layer: started with an in-memory DataStore singleton (ObservableCollections seeded from SampleData) for live, reactive lists, then migrated to SQLite (sqlite-net-pcl) so members, payments, check-ins, trainers, workout plans, and equipment all persist across app restart."),
        p("Screenshots to Attach", "Heading1"),
        p("Reference screenshots are stored under docs/status/screenshots/ in the repository. Drag them into the placeholders below before final submission."),
        placeholder("Screenshot 1: Login Screen",
                    "screenshots/01-login.png — Login screen with the Gymers title, demo credentials caption, role pills, and sign-in form."),
        placeholder("Screenshot 2: Admin Dashboard",
                    "screenshots/02-dashboard.png — Dashboard after signing in as Admin, including KPI cards and the Live Capacity / Coach Spotlight / Today's Classes blocks."),
        placeholder("Screenshot 3: Member Management",
                    "screenshots/03-members.png — Members screen with the search field and the live-filtered member list."),
        placeholder("Screenshot 4: Payment Processing",
                    "screenshots/04-payments.png — Payments screen with the Record Payment form and the Recent Payments list."),
        placeholder("Screenshot 5: Attendance Monitoring",
                    "screenshots/05-attendance.png — Attendance screen with the Check In card and the Recent Check-ins list."),
        placeholder("Screenshot 6: Reports + Export",
                    "screenshots/06-reports.png — Reports tab with the period selector (Week / Month / All), the three report kinds (Revenue / Attendance / Member Roster), and the Export PDF / Export CSV buttons."),
        placeholder("Screenshot 7: Trainer Roster",
                    "screenshots/07-trainers.png — Trainers screen showing the search field, Active Trainers KPI card, and the All Trainers list with five rows (Marcus Sterling, Sienna Vega, Rohan Iyer, Maya Okafor, Caleb Whit)."),
        placeholder("Screenshot 8: Workout Plans",
                    "screenshots/08-workouts.png — Workout Plans screen showing the search field, Active Plans KPI card, and the All Plans list (Foundations of Strength, HIIT Conditioning Cycle, Power Build 8-Week, Mindful Mobility Series, Active Recovery Block) with trainer-name and cadence subtitles."),
        placeholder("Screenshot 9: Equipment Roster",
                    "screenshots/09-equipment.png — Equipment screen showing the search field, Operational KPI card (5 of 6 items), and the All Equipment list with six rows (Treadmill TR-01, Treadmill TR-02, Power Rack PR-A, Smith Machine SM-01 marked Maintenance, Spin Bike SB-03, Yoga Mat Set YM-01)."),
        placeholder("Screenshot 10: Login Error State",
                    "screenshots/v2-login-error.png — Login screen with the inline validation error visible after pressing Sign In with empty or wrong credentials."),
        placeholder("Screenshot 11: Staff Dashboard",
                    "screenshots/10-staff-dashboard.png — Dashboard signed in as Staff: 4-pill bottom tab bar with Reports hidden, 'Signed in as Staff' badge under the top app bar, no Monthly Earnings KPI, and the yellow expiring-soon banner visible at the top of the scroll."),
        placeholder("Screenshot 12: Member ID Scan Overlay",
                    "screenshots/11-scan-overlay.png — Attendance screen with the SCAN MEMBER ID overlay in its captured state: viewfinder mock with 'CAPTURED' text and the detected-member card showing the member name, tier, and ID with the CONFIRM CHECK-IN button."),
        p("Build Verification", "Heading1"),
        table(["Command", "Result"], [
            ["dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug", "Build succeeded with 0 warnings and 0 errors."],
            ["dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug", "Build succeeded with 0 warnings and 0 errors."],
        ]),
        p("Note: This document is prepared as a progress update for evaluation. Drop the referenced PNGs into the placeholder areas before final submission if the evaluator requires images embedded directly in the Word file.", None, False, "667085"),
    ]

    body = "".join(parts)
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
        f"<w:body>{body}<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/>"
        "<w:pgMar w:top=\"1080\" w:right=\"1080\" w:bottom=\"1080\" w:left=\"1080\"/>"
        "</w:sectPr></w:body></w:document>"
    )


def styles_xml() -> str:
    return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="22"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Title">
    <w:name w:val="Title"/><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:b/><w:color w:val="134E4A"/><w:sz w:val="56"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/>
    <w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:b/><w:color w:val="0F766E"/><w:sz w:val="34"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading3">
    <w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/>
    <w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:b/><w:color w:val="18212F"/><w:sz w:val="26"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="ListParagraph">
    <w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/>
  </w:style>
</w:styles>"""


def write_docx() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(OUT, "w", ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
</Types>""")
        z.writestr("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>""")
        z.writestr("word/_rels/document.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>""")
        z.writestr("word/document.xml", document_xml())
        z.writestr("word/styles.xml", styles_xml())


if __name__ == "__main__":
    write_docx()
    print(OUT)
