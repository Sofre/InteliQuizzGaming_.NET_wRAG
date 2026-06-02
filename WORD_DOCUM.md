# Quiz Game Platform - Complete Documentation (Word Version)

## 1. Document Control

### 1.1 Title
Quiz Game Platform - Full Technical and Functional Documentation

### 1.2 Version
v1.0

### 1.3 Date
June 2026

### 1.4 Audience
- Product Owners
- Developers
- QA/Test Engineers
- DevOps Engineers
- Academic/Institutional Stakeholders

### 1.5 Purpose
This document provides full project documentation for the Quiz Game Platform, including architecture, data model, API design, authentication, AI/RAG behavior, frontend behavior, game flow, admin operations, export/reporting, deployment, testing, and operational guidance.

---

## 2. Executive Summary

Quiz Game Platform is a full-stack gamified questionnaire system with role-based access (Admin and Student). Admins manage taxonomy, questionnaires, sessions, AI-generated questions, and reporting. Students solve assigned questionnaires in a game-like interface with score and timer mechanics.

The platform includes:
- ASP.NET Core Web API backend (.NET 10)
- SQLite database via Entity Framework Core
- JWT authentication and authorization
- AI-driven question generation (OpenRouter integration)
- RAG-style semantic indexing and retrieval via embeddings
- Personalized practice quiz generation for weak areas
- Web frontend delivered from static files in `wwwroot`
- Excel export reporting via ClosedXML

---

## 3. Scope

### 3.1 In Scope
- Authentication and role-based access
- Admin catalog and session management
- Student gameplay and answer tracking
- AI question generation and moderation
- RAG semantic retrieval support
- Personalized weak-area practice generation
- Exporting results to Excel

### 3.2 Out of Scope (Current Version)
- Multi-tenant institution separation
- Advanced proctoring/anti-cheat controls
- External LMS integration (Moodle/Canvas)
- Real-time multiplayer competition

---

## 4. System Architecture

### 4.1 High-Level Architecture
- Presentation Layer: Single-page frontend served from static files (`wwwroot/index.html`, `wwwroot/js/app.js`, `wwwroot/css/style.css`)
- API Layer: ASP.NET Core controllers under `Controllers`
- Domain/Data Layer: Entity models under `Models`, EF Core DbContext under `Data/AppDbContext.cs`
- Persistence Layer: SQLite database (`quizgame.db`)
- AI Integration Layer: OpenRouter-backed service under `AIServices/OpenRouterService.cs`

### 4.2 Core Backend Components
- `Program.cs`: Service registration, middleware pipeline, DB migration, seeding
- `Data/AppDbContext.cs`: DbSets and data context
- `Controllers/AuthController.cs`: registration/login/JWT issuing
- `Controllers/AdminController.cs`: admin CRUD, rankings, AI actions
- `Controllers/GameController.cs`: student session, quiz play, answer submission, personalization
- `Controllers/AdminExportController.cs`: Excel report generation and file streaming
- `AIServices/IAIService.cs`: AI abstraction
- `AIServices/OpenRouterService.cs`: embeddings + generation + similarity calculation
- `AIServices/PersonalizationService.cs`: weak area analysis + personalized quiz retrieval

### 4.3 Technology Stack
- .NET 10 (`net10.0`)
- ASP.NET Core Web API
- Entity Framework Core 10
- SQLite
- JWT Bearer authentication
- BCrypt for password hashing
- ClosedXML for Excel exports
- HTML/CSS/JavaScript frontend (Vue 3 CDN migration in current code state)

---

## 5. Configuration and Environment

### 5.1 Main Configuration File
- `appsettings.json`

### 5.2 Important Configuration Sections
- Logging levels
- Connection string (`DefaultConnection` -> SQLite file)
- JWT (`Key`, `Issuer`, `Audience`)
- Admin secret key (present in config)
- OpenRouter settings (`ApiKey`, `BaseUrl`, models)
- AI behavior settings:
  - `SimilarityThreshold`
  - `MaxRetries`
  - `WeakAreaThreshold`
  - `PersonalQuizSize`
  - `WeakAreaCount`
  - `QuestionsToGenerate`

### 5.3 Security Note
Do not keep real API keys and secrets in source control. Replace with environment variables or secure secret storage before production deployment.

---

## 6. Data Model

### 6.1 Entities Overview
- `User`
- `Area`
- `SubArea`
- `Questionnaire`
- `Question`
- `Session`
- `Answer`
- `QuestionEmbedding`

### 6.2 Entity Details

#### User
- `Id`
- `Email`
- `PasswordHash`
- `Role` (`Admin` or `Student`)
- `Sessions` (many-to-many)
- `Answers` (one-to-many)

#### Area
- `Id`
- `Name`
- `Description`
- `SubAreas` (one-to-many)

#### SubArea
- `Id`
- `Name`
- `AreaId`
- `Area`

#### Questionnaire
- `Id`
- `Title`
- `Description`
- `ThemeColor`
- `BackgroundColor`
- `Questions` (one-to-many)
- `Sessions` (many-to-many)

#### Question
- `Id`
- `Text`
- `Options` (stored as string, often comma separated or JSON)
- `CorrectAnswer`
- `Points`
- `QuestionnaireId`
- `SubAreaId`
- `IsAIGenerated`
- `IsApproved`

#### Session
- `Id`
- `Name`
- `AssignedUsers` (many-to-many)
- `AssignedQuestionnaires` (many-to-many)

#### Answer
- `Id`
- `UserId`
- `QuestionId`
- `Choice`
- `IsSkipped`
- `IsCorrect`
- `PointsEarned`
- `TimeTakenSeconds`
- `UserAssignedSubAreaId`

#### QuestionEmbedding
- `Id`
- `QuestionId`
- `EmbeddingJson` (float vector serialized to JSON)
- `CreatedAt`

---

## 7. Authentication and Authorization

### 7.1 Authentication Flow
1. User registers or logs in via `/api/auth/register` or `/api/auth/login`.
2. Backend verifies credentials and issues JWT token.
3. Frontend stores token and role in local storage.
4. Protected API requests include `Authorization: Bearer <token>`.

### 7.2 Authorization Rules
- Admin endpoints require role `Admin`.
- Game endpoints require role `Student`.
- Auth endpoints are public.

### 7.3 JWT Claims
- `NameIdentifier` (User ID)
- `Email`
- `Role`

---

## 8. API Documentation

Base route conventions:
- Auth: `/api/auth/*`
- Admin: `/api/admin/*`
- Game: `/api/game/*`
- Admin Export: `/api/adminexport/*`

### 8.1 Auth API

#### POST `/api/auth/register`
Registers a new user.
- Request: email, password, role
- Response: success message

#### POST `/api/auth/login`
Logs in user and returns JWT.
- Request: email, password
- Response: token, role

### 8.2 Admin API

#### Users and Communication
- GET `/api/admin/users`
- POST `/api/admin/invite/{userId}`

#### Rankings
- GET `/api/admin/rankings`

#### Area/SubArea Management
- POST `/api/admin/area`
- GET `/api/admin/areas`
- PUT `/api/admin/area/{id}`
- DELETE `/api/admin/area/{id}`
- POST `/api/admin/subarea`
- PUT `/api/admin/subarea/{id}`
- DELETE `/api/admin/subarea/{id}`

#### Questionnaire/Question Management
- POST `/api/admin/questionnaire`
- GET `/api/admin/questionnaires`
- PUT `/api/admin/questionnaire/{id}`
- DELETE `/api/admin/questionnaire/{id}`
- POST `/api/admin/question`
- PUT `/api/admin/question/{id}`
- DELETE `/api/admin/question/{id}`

#### Session Management
- POST `/api/admin/session`
- GET `/api/admin/sessions`
- POST `/api/admin/session/{sessionId}/assign-user/{userId}`
- POST `/api/admin/session/{sessionId}/assign-questionnaire/{qId}`

#### AI/RAG Operations
- POST `/api/admin/index-questions/{subAreaId?}`
- POST `/api/admin/generate-questions/{subAreaId}?useRAG=true|false`
- GET `/api/admin/pending-questions/{subAreaId?}`
- POST `/api/admin/approve-question/{questionId}`
- DELETE `/api/admin/reject-question/{questionId}`
- DELETE `/api/admin/ai-questions`

### 8.3 Game API

#### Student Sessions and Questionnaire Fetch
- GET `/api/game/my-sessions`
- GET `/api/game/questionnaire/{qId}`

#### Gameplay
- POST `/api/game/submit-answer`

#### Personalization
- GET `/api/game/my-weak-areas`
- POST `/api/game/personal-practice-quiz`

### 8.4 Export API
- GET `/api/adminexport/export`

---

## 9. Frontend Documentation

### 9.1 Frontend Structure
- `wwwroot/index.html`: single-page shell and view containers
- `wwwroot/js/app.js`: client logic and API interactions
- `wwwroot/css/style.css`: visual styles and animations

### 9.2 Main UI Views
- Auth view
- Admin view
  - Dashboard
  - Workflow guide
  - Catalog
  - AI generator
  - Sessions
  - Evaluation rankings
  - Export
- Student view
- Game UI view
- Edit modal

### 9.3 Frontend Runtime Behavior
- Uses token-based API calls
- Renders admin and student flow by current role
- Handles state for game progress, timer, score, answer submission
- Supports CRUD and moderation operations from admin UI

### 9.5 Catalog Management Model (Important)
The platform uses a dual organization model that must be understood by admins.

1. Topic Taxonomy (used by AI and personalization)
- Area -> SubArea
- Example: Mathematics -> Algebra

2. Quiz Organization (used by student gameplay)
- Questionnaire -> Questions
- Each Question must also reference a SubArea

Mandatory question linkage:
- One Question belongs to one Questionnaire
- One Question belongs to one SubArea

This dual linkage enables:
- gameplay delivery through Questionnaire grouping
- AI generation and personalization through SubArea semantics

### 9.6 Catalog Creation and Deletion Rules
Recommended creation order:
1. Create Area
2. Create SubArea
3. Create Questionnaire
4. Create Questions

Deletion constraints:
- Area can be deleted only if it has no SubAreas
- SubArea can be deleted only if it has no Questions
- Questionnaire can be deleted only if it has no Questions
- Question can be deleted directly

### 9.7 Admin AI Workflow in UI
Operational flow:
1. Prepare taxonomy and base questions in Catalog.
2. Run indexing for selected topic or all topics.
3. Generate AI questions for a selected SubArea.
4. Review pending generated questions.
5. Approve or reject each generated question.
6. Approved questions become part of active pool.

---

## 10. Gameplay and Scoring Logic

### 10.1 Student Game Flow
1. Student opens assigned sessions.
2. Student chooses a questionnaire.
3. Client fetches questions (without correct answers).
4. Timer starts per question.
5. Student answers or skips.
6. Backend evaluates answer.
7. Score updates.
8. On completion, final score is displayed.

### 10.2 Answer Evaluation Rules
- If skipped: zero score
- If numeric scale answer (1-5): treated as valid with points equal to rating
- Else string equality with `CorrectAnswer` determines correctness

### 10.3 Stored Telemetry Per Answer
- Choice
- Correct/incorrect/skip
- Points earned
- Time taken
- Optional user-assigned subarea

---

## 11. AI and RAG Subsystem

### 11.1 Objective
Provide high-quality, context-grounded question generation and personalized practice selection.

### 11.2 AI Service Responsibilities
- Generate embedding vectors from text
- Generate questions with RAG (semantic retrieval)
- Generate questions in simple mode (context-only)
- Calculate cosine similarity for vector comparisons

### 11.3 Indexing Process
Endpoint: `POST /api/admin/index-questions/{subAreaId?}`
- Generates embeddings for approved questions
- Stores vectors in `QuestionEmbeddings`

### 11.4 Generation Process
Endpoint: `POST /api/admin/generate-questions/{subAreaId}`
- Optional RAG mode via `useRAG`
- Retrieves semantic neighbors via cosine similarity
- Prompts model to generate new questions
- Filters duplicates by similarity threshold
- Saves generated questions as pending (`IsApproved = false`)

### 11.5 Moderation Process
- Pending questions are reviewed by admin
- Approve -> `IsApproved = true`
- Reject -> deletes question and associated embedding

### 11.6 RAG Dependency Rule
- Indexing builds vector memory
- Generation queries vector memory
- If vectors do not exist, RAG mode cannot proceed

### 11.7 RAG Topic Semantics (Critical)
There are two topic selectors in admin flow that look similar but have different semantics.

1. Topic in Index step (data preparation)
- Endpoint: POST /api/admin/index-questions/{subAreaId}
- Meaning: choose which approved questions are converted to embeddings and stored.
- Effect: defines what enters the vector memory.

2. Topic in Generate step (semantic query)
- Endpoint: POST /api/admin/generate-questions/{subAreaId}
- Meaning: choose which SubArea name is embedded as query and used for retrieval.
- Effect: defines what context is retrieved from vector memory for generation.

In short:
- Index topic = FROM WHAT knowledge base is built
- Generate topic = FOR WHAT the model generates

### 11.8 RAG Query Construction and Retrieval Behavior
Generation in RAG mode follows this pattern:
1. Build semantic query from SubArea context (name and Area context).
2. Generate embedding for query text.
3. Load candidate question embeddings from indexed approved questions.
4. Compute cosine similarity for each candidate.
5. Select top-k most relevant questions (current implementation uses top 5).
6. Inject retrieved items into the generation prompt.
7. Ask model to generate new questions.
8. Generate embeddings for newly generated questions.
9. Run duplicate filter using similarity threshold.
10. Save non-duplicates as pending (unapproved).

### 11.9 RAG Alignment Scenarios
The quality of generated questions depends on alignment between indexed data and generation topic.

Scenario A: Index Algorithms, Generate Algorithms
- Expected result: best context quality, relevant generation.

Scenario B: Index Algorithms, Generate Data Structures
- Expected result: retrieval drifts toward indexed Algorithms context, generation quality degrades for Data Structures intent.

Scenario C: No indexing done, Generate with useRAG=true
- Expected result: request blocked (no embeddings available); admin must index first or switch to simple mode.

Best practice:
- Always index a SubArea before generating for that SubArea.
- Re-index after significant approved question additions.

### 11.10 Simple Mode vs RAG Mode
Simple mode (useRAG=false):
- Uses recent question context directly
- No semantic retrieval stage
- Useful fallback when embeddings are unavailable

RAG mode (useRAG=true):
- Uses embedding search and similarity ranking
- Better topical grounding when indexed corpus is healthy
- Requires indexed embeddings

### 11.11 Prompt and Output Contract
Generation prompt contract requires:
- exactly 5 new questions
- multiple options per question
- explicit correct answer
- points value
- JSON output object with description and questions array

Post-processing contract:
- parse model JSON
- validate shape
- attach embeddings per generated question
- filter semantically duplicate content before persistence

---

## 12. Personalization Engine

### 12.1 Goal
Identify weak subareas and generate personalized practice question sets.

### 12.2 Weak Area Detection
- Groups user answers by `SubAreaId`
- Computes:
  - success rate
  - skip rate
  - weighted score
- Marks weakest subareas below configured threshold

### 12.3 Personalized Question Selection
- Builds a semantic query from incorrect/skipped question text
- Retrieves candidate approved questions in weak subareas
- Ranks by cosine similarity
- Excludes already-correct answers
- Returns top N questions

### 12.4 Personal Practice Quiz
Endpoint: `POST /api/game/personal-practice-quiz`
- Returns personalized set and a per-user practice questionnaire descriptor

---

## 13. Rankings and Reporting

### 13.1 Rankings
Endpoint: `GET /api/admin/rankings`
- Aggregates scores by sub-criteria (subarea)
- Provides total score and session-level breakdown

### 13.2 Excel Export
Endpoint: `GET /api/adminexport/export`
- Exports all answer-level logs
- Includes:
  - user email
  - question text
  - selected choice
  - correctness
  - points
  - time
  - admin assigned area/subarea
  - user assigned area/subarea

---

## 14. Startup, Seeding, and Lifecycle

### 14.1 Startup Actions in Program
- Configure services
- Configure middleware
- Apply EF migrations at runtime
- Seed default users and baseline data if missing

### 14.2 Seed Data
- Student: `student@quiz.com`
- Admin: `admin@quiz.com`
- Additional admins seeded
- Default area/subareas
- Default questionnaire and session mapping

---

## 15. Deployment Guide

### 15.1 Local Development
1. Install .NET SDK compatible with `net10.0`.
2. Navigate to project directory.
3. Run: `dotnet run`
4. Open URL from launch settings (for example `http://localhost:5292`).

### 15.2 Docker
1. Build image from project Dockerfile.
2. Run container with mapped port.
3. Ensure writable path for SQLite if needed.

### 15.3 Production Hardening Checklist
- Move secrets out of config files
- Use HTTPS certificates
- Rotate JWT keys
- Restrict CORS as needed
- Add structured logging/monitoring
- Backup SQLite or migrate to managed DB for scale

---

## 16. Non-Functional Requirements

### 16.1 Security
- JWT-protected endpoints
- Role-based authorization
- Password hashing with BCrypt
- Secret management required for production

### 16.2 Performance
- Lightweight frontend
- SQLite suitable for low/medium throughput
- RAG operations are compute/network dependent

### 16.3 Reliability
- DTO projections to avoid circular serialization issues
- Runtime migration ensures schema up-to-date
- Graceful AI fallback behaviors included

### 16.4 Maintainability
- Clear controller separation by domain
- Service abstractions for AI and personalization
- Extendable model for future adaptive learning features

---

## 17. Testing Strategy

### 17.1 Unit Test Targets
- JWT token generation and validation
- scoring/evaluation logic
- weak area score calculations
- cosine similarity behavior

### 17.2 Integration Test Targets
- auth flow (register/login)
- admin CRUD endpoints
- session assignment flows
- game submission persistence
- export generation response

### 17.3 End-to-End Test Targets
- Admin creates content -> student plays -> admin exports report
- AI generation -> moderation -> approved question usage
- Personalized practice generation

---

## 18. Operational Monitoring and Troubleshooting

### 18.1 Common Issues
- Missing/invalid JWT -> 401
- Missing embeddings -> RAG generation blocked
- External AI API errors -> generation failures
- SQLite file lock/contention under concurrent operations

### 18.2 Troubleshooting Checklist
1. Verify app settings and environment values.
2. Confirm DB migrations applied.
3. Confirm token contains role claim.
4. Verify embeddings exist for targeted subarea.
5. Check AI provider API key and connectivity.

---

## 19. Gaps and Known Limitations

- Admin registration secret is present in config but not enforced by backend registration endpoint in current implementation.
- OpenRouter service class is named `OpenAIService` despite file name implying OpenRouter.
- Some dynamic frontend details rely on client-side state assumptions.
- SQLite may become a bottleneck for high concurrency workloads.

---

## 20. Future Enhancements

- Implement adaptive remediation questionnaire flow (targeted retry based on wrong subdomains)
- Add attempt/session analytics dashboards
- Add richer anti-duplication for question generation
- Add full OpenAPI/Swagger with schema docs
- Add audit trail for admin actions
- Add tenant/institution support

---

## 21. Appendices

### Appendix A - File Structure Snapshot
- `Program.cs`
- `appsettings.json`
- `Data/AppDbContext.cs`
- `Models/*`
- `Controllers/*`
- `AIServices/*`
- `wwwroot/index.html`
- `wwwroot/js/app.js`
- `wwwroot/css/style.css`

### Appendix B - Dependency List
From `QuizGamePlatform.csproj`:
- BCrypt.Net-Next
- Microsoft.Extensions.AI
- ClosedXML
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.OpenApi
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.EntityFrameworkCore.Tools

## 22. Word Formatting Tips

When moving this file into Microsoft Word:
1. Apply Heading 1/2/3 styles to section hierarchy.
2. Insert page breaks before major sections (API, Data Model, Deployment).
3. Insert table of contents from Heading styles.
4. Add header/footer with version and date.

End of document.
