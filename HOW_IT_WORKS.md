# How It Works (Architecture & Flow)

This document deeply explains the mechanics and flow driving the **Quiz Game Platform**. 

## 🗺️ Architectural Mapping

At its core, this application operates via a thick REST API Backend and a Single-Page Application (SPA) Frontend built natively with DOM manipulation.

### 1. Database Schema (EF Core)
The system is built on a relational architecture heavily structured to support educational cohorts:
- `Users`: Either `Student` or `Admin`.
- `Areas` and `SubAreas`: Groupings of subjects (e.g. Mathematics -> Algebra).
- `Questionnaires` and `Questions`: Groupings of specific tasks linking directly to SubAreas. Contains customizable Theme/Background color maps.
- `Sessions`: A distinct cohort assignment (e.g., "Seniors 2026"). It links `Users` to `Questionnaires`.
- `Answers`: A trace log holding user decisions, points, time taken, and skips. 

### 2. The Authentication Engine
When opening the page `index.html`, Javascript evaluates `localStorage` for a JWT Auth Token. 
- If not present, the `auth-view` is shown. 
- The Auth screen holds two hidden HTML panels separating Admin and Student login. Toggling applies specific CSS classes to change visual palettes.
- Sending login hits `/api/auth/login`. On success, the API spins up an encrypted JWT containing Role Claims.
- Subsequent Frontend API requests route through the `authFetch()` wrapper which automatically attaches the Bearer token.

### 3. Gamification Protocol
The actual "Game" element is built dynamically. 
1. The student selects an assigned Questionnaire. 
2. A Javascript interval begins tracking seconds.
3. DOM elements aggressively re-render inside `renderQuestion()` parsing stringified JSON Arrays from the SQLite db to create options.
4. Answering hits the `/api/game/submit-answer` endpoint. 
5. The backend verifies the correct answer, computes points scored, logs the interaction in the database, and responds with `pointsEarned` and `isCorrect`.
6. Finally, visual CSS transitions (keyframes defined in `style.css`) apply to the active form before advancing.

### 4. Admin Management Stability
JSON serialization of SQL relationships often creates deadly nested loops, crashing platforms. The Quiz Backend specifically sidesteps this using pure DTO (Data Transfer Object) Projections via `.Select()` mapping during `HttpGet` operations. This enforces extreme stability when navigating enormous collections in the Admin Catalog, converting EF logic to clean payloads before the Frontend ever parses it.

### 5. Excel Results Engine
When an Admin exports data, `AdminExportController` navigates all logged `Answers` and invokes `ClosedXML`. It constructs a real `.xlsx` byte array in system memory and pipes it directly over the Web API as an `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` payload—never writing the persistent file locally, which prevents disk clutter.
