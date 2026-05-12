# Admin CRUD Role Gating Design

## Goal

Make Admin versus Staff access materially different across the management modules. Admin can create, edit, and delete Members, Trainers, Workout Plans, and Equipment records. Staff can search and view those records, process front-desk workflows, and check membership status, but cannot mutate management data or open Reports.

## Current State

The app already has SQLite-backed collections for Members, Trainers, Workout Plans, and Equipment. Each management page renders from `DataStore` collections and refreshes through `CollectionChanged`. Role state is persisted in `Services.Session` after login. Staff currently sees a reduced bottom tab bar and the Dashboard hides Monthly Earnings, but page-level capability gating is still shallow.

## Scope

This slice adds full Admin-only CRUD for four existing management pages:

- Members
- Trainers
- Workout Plans
- Equipment

The slice also guards Reports for Staff and keeps Staff read-only on the four management pages.

Out of scope: member photo upload, QR code generation, salary editing, destructive cascade management, advanced trainer schedules, bulk import, and full automated test scaffolding.

## UX

Each management page keeps its current layout: top app bar, search field, KPI card, list, bottom tab bar. When `Session.Current.IsAdmin` is true, the page shows a `+ ADD` primary action above search and each rendered row includes `EDIT` and `DELETE` actions. When Staff is signed in, those controls are hidden and the list remains read-only.

Add and edit use an in-page overlay, matching the existing scan overlay pattern instead of introducing a new navigation stack. The overlay contains labeled fields, an inline validation label, and clear actions:

- `ADD` or `SAVE`
- `CANCEL`

Delete uses a confirmation alert before removing the record.

## Entity Fields

Members:
- Name
- Tier: Basic, Premium, Elite
- Expiry date
- Status: Active, Expiring Soon, Inactive

Trainers:
- Name
- Title
- Rating from 0.0 to 5.0
- Sessions completed, zero or greater

Workout Plans:
- Name
- Assigned trainer
- Level
- Sessions per week from 1 to 7
- Duration in weeks, one or greater
- Summary

Equipment:
- Name
- Category
- Status: Operational, Maintenance, Retired
- Location

## Data Flow

`DataStore` owns all entity mutations and keeps SQLite plus the observable collections in sync. It exposes add, update, and delete methods for each entity. Add methods generate stable ids using the current collection count and max numeric suffix where applicable. Update methods write through SQLite, then replace the item in the observable collection. Delete methods remove from SQLite, then remove from the observable collection.

`GymersDb` exposes matching insert, update, and delete methods for the row types already used by the app. No schema migration is required because all CRUD fields already exist in the row models.

## Validation

Forms validate before writing:

- Required text fields must be non-empty after trimming.
- Member expiry must be today or later.
- Trainer rating must parse and be between 0 and 5.
- Trainer sessions must parse and be at least 0.
- Workout sessions per week must be 1 through 7.
- Workout duration must be at least 1.
- Workout trainer must reference an existing trainer.

Validation errors stay inside the overlay and do not close it.

## Role Rules

Admin:
- Can add, edit, and delete records on all four management pages.
- Can open Reports.

Staff:
- Can search and view the same records.
- Cannot see add, edit, or delete controls.
- Is redirected to Dashboard if `//Reports` is reached programmatically.

## Verification

Verification is by `dotnet build Gymers.slnx` plus focused manual smoke paths on Mac Catalyst:

- Admin can add, edit, and delete one record from each management page.
- Changes appear immediately in lists.
- Added records persist after app restart because they are written to SQLite.
- Staff can view/search but cannot mutate records.
- Staff route access to Reports bounces to Dashboard.
