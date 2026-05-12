# Admin CRUD Role Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Admin-only create, edit, and delete flows to Members, Trainers, Workout Plans, and Equipment while keeping Staff read-only and guarding Reports.

**Architecture:** Reuse the current code-behind page pattern and `DataStore` as the mutation boundary. `GymersDb` handles SQLite row insert/update/delete, `DataStore` updates observable collections on the main thread, and pages render CRUD controls only for Admin.

**Tech Stack:** .NET MAUI 10, C#, SQLite via `sqlite-net-pcl`, existing XAML controls and code-behind.

---

## File Structure

- Modify `Gymers/Data/GymersDb.cs`: add insert/update/delete methods for Member, Trainer, WorkoutPlan, and Equipment rows.
- Modify `Gymers/Data/DataStore.cs`: add add/update/delete methods for the four entities and id generation helpers.
- Modify `Gymers/Pages/MembersPage.xaml` and `.xaml.cs`: add Admin-only CRUD overlay and row actions.
- Modify `Gymers/Pages/TrainersPage.xaml` and `.xaml.cs`: add Admin-only CRUD overlay and row actions.
- Modify `Gymers/Pages/WorkoutsPage.xaml` and `.xaml.cs`: add Admin-only CRUD overlay and row actions.
- Modify `Gymers/Pages/EquipmentPage.xaml` and `.xaml.cs`: add Admin-only CRUD overlay and row actions.
- Modify `Gymers/Pages/ReportsPage.xaml.cs`: redirect Staff to Dashboard.
- Modify `docs/status/gymers-mobile-app-status-update.html`: document CRUD status and build warning state.

## Tasks

### Task 1: Data Mutations

- [ ] Add SQLite mutation methods in `GymersDb`.
- [ ] Add `DataStore` add/update/delete methods for Members.
- [ ] Add `DataStore` add/update/delete methods for Trainers.
- [ ] Add `DataStore` add/update/delete methods for Workout Plans.
- [ ] Add `DataStore` add/update/delete methods for Equipment.
- [ ] Build to catch signature and row mapping issues.

### Task 2: Members CRUD UI

- [ ] Add Admin-only add button and overlay fields to `MembersPage.xaml`.
- [ ] Add add/edit/delete handlers and validation to `MembersPage.xaml.cs`.
- [ ] Build to catch XAML names and handler issues.

### Task 3: Trainers CRUD UI

- [ ] Add Admin-only add button and overlay fields to `TrainersPage.xaml`.
- [ ] Add add/edit/delete handlers and validation to `TrainersPage.xaml.cs`.
- [ ] Build to catch XAML names and handler issues.

### Task 4: Workout Plans CRUD UI

- [ ] Add Admin-only add button and overlay fields to `WorkoutsPage.xaml`.
- [ ] Add add/edit/delete handlers and validation to `WorkoutsPage.xaml.cs`.
- [ ] Build to catch XAML names and handler issues.

### Task 5: Equipment CRUD UI

- [ ] Add Admin-only add button and overlay fields to `EquipmentPage.xaml`.
- [ ] Add add/edit/delete handlers and validation to `EquipmentPage.xaml.cs`.
- [ ] Build to catch XAML names and handler issues.

### Task 6: Role Guard and Docs

- [ ] Redirect Staff away from Reports in `ReportsPage.xaml.cs`.
- [ ] Update the status HTML with full CRUD and current warning state.
- [ ] Run `dotnet build Gymers.slnx`.
- [ ] Review `git diff --stat` and summarize changed files.
