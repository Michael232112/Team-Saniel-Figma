# Gymers — Demo Script

## What it is

A gym-management app for a small fictional gym. Front desk and owner each get a different view of the same underlying data. Built for Team Fafa as a working demo on .NET MAUI (iOS + Mac Catalyst), SQLite-backed.

**Why it exists.** Gyms juggle members, payments, attendance, trainers, workout plans, and equipment — usually in logbooks or spreadsheets. The app digitises all of that into a single mobile app so the same data is visible everywhere and changes persist across restarts.

## Two roles

- **Admin** (gym owner / manager) — full management. Can create / edit / delete members, trainers, workout plans, equipment. Sees financial KPIs and reports.
- **Staff** (front desk receptionist) — operational only. Can check members in, process walk-in payments, look up member status, view trainer schedules. Can't modify management data. Can't see reports.

## Credentials

- Admin: `admin` / `admin123`
- Staff: `staff` / `staff123`

Both shown on the login screen.

---

## Walkthrough (in demo order)

### 1. Login

Pick a role pill (Admin or Staff), enter the matching credentials. Wrong role + right credentials still errors — the role gate is real.

### 2. Dashboard

Everything else hangs off here.

- **KPI cards:** Total Members, Today's Attendance, Monthly Earnings (Admin only — Staff doesn't see Monthly Earnings).
- **Yellow alert banner** at top whenever any member is "Expiring Soon" (Sam Chen in the seed). Tap → jumps to Members.
- **Coach Spotlight** — picks the top-rated trainer (Marcus Sterling), shows rating, sessions completed, and weekly schedule. `VIEW PERFORMANCE PROFILE` → Trainers screen.
- **Featured Workout Plan** + **Equipment Status** — surface the top item; their buttons jump to full lists.
- **Today's Classes** — static schedule preview.
- **Bottom tab bar:** Admin = 5 pills, Staff = 4 (Reports hidden). "Signed in as Admin/Staff" badge under the title so the role is always visible.

### 3. Members

Search by name. Admin sees `+ ADD MEMBER` button and `EDIT` / `DELETE` per row. Staff sees the same list, read-only. Adds persist to SQLite — force-quit + relaunch and your new member is still there, at the top of the list.

### 4. Payments

Type a member name + amount + method (Card / Cash / Bank). Tap `RECORD PAYMENT` — transaction lands in Recent Payments. **Tap any payment row** → generates a real PDF receipt via the iOS share sheet (save to Files, email, AirDrop, print). Receipts are deterministic from SQLite, so any historical payment can be re-printed.

### 5. Attendance

Two paths into a check-in:

- **Search by name** — type, pick a suggestion, tap `CHECK IN`.
- **`SCAN MEMBER ID`** — opens a viewfinder overlay that mimics an ID scanner, picks the next un-checked-in member, shows a `CONFIRM` card with their tier + ID. Simulated scan (no real camera) — stands in for the README's QR/ID scanning scope.

### 6. Reports (Admin only)

Pick `Week` / `Month` / `All`, then generate **Revenue**, **Attendance**, or **Member Roster** as PDF or CSV. Each goes through the system share sheet. Direct URL access (`//Reports`) for Staff bounces them back to Dashboard.

### 7. Trainers / Workouts / Equipment

Reached from Dashboard buttons (Coach Spotlight → Trainers, etc.). Same CRUD shape as Members for Admin; read-only for Staff. Trainers carry a weekly schedule string ("Mon/Wed/Fri · 6am–2pm" style). Equipment carries category, status (Operational / Maintenance / Retired), and floor location.

---

## What's real vs what's representative

Be honest if the panel asks.

**Real:**
- SQLite persistence across restart
- PDF receipt generation via UIKit's native renderer (no third-party packages)
- PDF + CSV report export via the system share sheet
- Member / Trainer / Workout Plan / Equipment CRUD
- Role gating across navigation, dashboard, and management actions
- Expiring-soon banner driven from `Member.Status`
- Simulated check-in flow end-to-end

**Representative seed numbers** (for visual polish):
- "Total Members: 1,250" and "Today's Earnings: $1,250" KPI labels are static. Actual seed has 6 members and a handful of payments.
- The CRUD operations work on the *real* seed; the big KPI labels above are decorative.

**Simulated, not real:**
- `SCAN MEMBER ID` is a mock — no camera, no QR decoding. Picks the next member who hasn't checked in today and shows them as "Detected." Sells the workflow without the hardware integration.
- Trainer schedules and salaries are seeded static strings, not editable.

---

## 2-minute talking script

1. *"This is a .NET MAUI gym management app — iOS and Mac Catalyst targets, SQLite under the hood. Two roles: Admin and Staff."*
2. *"Login. I'll pick Admin first. The dashboard shows the gym at a glance — total members, today's attendance, monthly earnings, and which members need follow-up."*
3. *"This banner: a member is expiring soon. Tap it, jump to Members. I can add a new one — it lands at the top, and survives a restart."*
4. *"Payments tab — record a payment, tap any row, and it generates a real PDF receipt I can email, AirDrop, or print."*
5. *"Attendance — staff can search by name OR use this scan flow to identify members at the front desk."*
6. *"Now I sign out and come back as Staff. Notice: four tabs instead of five, no earnings card, no add/edit/delete on Members. Same data, different capabilities — that's the role split."*
7. *"Reports tab is hidden for Staff, and if I tried to URL into it directly, the app would bounce me back. Trainer schedules are still visible because Staff needs them to help members on the floor."*

---

## If asked "what would you build next?"

Three honest answers, in priority order:

1. **Real camera QR scanning** — wire the camera + a barcode library so the scan flow stops being simulated.
2. **Member ID photo upload** — README mentions it; not shipped.
3. **Trainer salary editing + payroll reports** — Admin currently sees trainer rating and sessions but salary is hardcoded static.
