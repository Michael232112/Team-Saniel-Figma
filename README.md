# Gymers - Gym Management System
(Team-Saniel-Figma)

## Team Fafa
* Gonzaga, Michael
* Magallones, Vince
* Medillo, Mark
* Naces, Welben
* Saniel, Mitch

## Introduction
Gyms and fitness centers manage a large number of members, payments, trainers, equipment, and workout plans on a daily basis. Without a proper system, gym owners may struggle with tracking membership statuses, monitoring attendance, managing payments, and organizing trainer schedules. Manual processes using logbooks and spreadsheets can slow down operations and increase the possibility of human error.

This project introduces a **Gym Management System** designed to streamline gym operations by digitizing member registration, payment tracking, attendance monitoring, trainer management, workout plan assignments, and equipment maintenance. The system will help gym staff and owners perform daily tasks more efficiently while ensuring that all records are properly stored and managed in a centralized database.

## Objectives
* Develop a Gym Management System that manages members, payments, and gym operations.
* Improve member registration and membership tracking.
* Track member attendance using QR code or ID scanning.
* Manage trainer profiles, schedules, and assignments.
* Provide alerts for expiring memberships and inactive members.
* Monitor gym equipment maintenance and availability.
* Generate reports and analytics for business decision-making.

## Recommended Technology Stack
* **Frontend:** .NET MAUI (C#)
* **Backend / Database:** MySQL or SQLite
* **Optional:** QR Code Scanner Library

## System Users & Modules

### 1. Admin (Gym Owner / Manager)
Responsible for managing members, trainers, payments, equipment, reports, and overall system control.
* **Member Management:** Register new members, edit details, upload ID photos, view membership status, and search members.
* **Membership & Payment:** Track membership payments, subscription status, auto-expiry alerts, payment history, and receipt generation.
* **Attendance Monitoring:** Track member visits (time in/out), QR code/ID scanning, daily reports, and inactive alerts.
* **Trainer Management:** Add trainer profiles, assign trainers, manage schedules, and track salary information.
* **Workout Plan Management:** Store workout templates, assign custom plans, and track progress.
* **Equipment Management:** Maintain equipment inventory, maintenance schedules, and monitor availability.
* **Reports & Analytics:** Generate total members, active/inactive, monthly income, and attendance statistics.
* **Admin Dashboard:** Overview of total members, today's attendance, expiring memberships, and monthly earnings.

### 2. Staff (Front Desk / Receptionist)
Responsible for handling daily member check-ins, processing payments, and assisting members with inquiries.
* **Member Check-In:** Handle member attendance by scanning QR codes or IDs upon gym entry.
* **Member Search:** Search for member profiles and membership details.
* **Payment Processing:** Record walk-in payments, process membership renewals, and issue receipts.
* **Membership Status Viewing:** Check membership validity, expiration dates, and activity history.
* **Trainer Schedule Viewing:** View trainer availability and schedules to assist members.

## Suggested Database Tables
* Members
* Trainers
* Payments
* Attendance
* WorkoutPlans
* Equipment
* Admin

## UI / UX Design
The user interface design for this application is provided in the `Team saniel.fig` Figma file. It outlines the visual components and user flows that will be implemented using .NET MAUI.
