# Quiz Game Platform

Welcome to the **Quiz Game Platform**! This is a robust Gamified Questionnaire System built for tracking student engagement through interactive, visually stunning quizzes. It was engineered with a dual-role architecture, allowing Administrators to effortlessly manage large structures of learning material, while Students experience a fluid, game-like interface.

## 🌟 Core Features

- **Gamified Student Experience**: Students navigate questions linearly with modern tracking elements like progress bars, live timers, accurate score computations, and smooth micro-animations.
- **Dual-Role Authentication**: Dedicated portals for Students and Admins, dynamically displaying custom themes (`glassmorphism`/blue hues for students, sharp dark/amber styles for admins).
- **Dynamic Catalog Administration**: Admins can construct nested `Areas`, `SubAreas`, and rapidly generate `Questionnaires` and isolated `Questions`. 
- **Session Engineering**: Administrators can bind specific sets of Students and Questionnaires into unique `Sessions` representing student cohorts, allowing for complex segmentation.
- **Reporting Engine**: One-click extraction of student results, aggregating metrics natively to an exported Excel (`.xlsx`) sheet.

## 🛠️ Technology Stack

- **Backend Protocol**: .NET 10.0 Web API 
- **Database Architecture**: Entity Framework Core utilizing SQLite (`quizgame.db`)
- **Authentication Mechanism**: JWT (JSON Web Token) Bearer Authorization
- **Reporting Generator**: ClosedXML
- **Frontend Core**: Vanilla HTML5, CSS3, and JavaScript (ES6+). No heavy frameworks involved, ensuring ultra-low latency DOM manipulation and highly responsive CSS rendering.
- **Deployment**: Highly portable via native Multi-stage Docker configurations.

## 🚀 How to Run (Development)

1. **Install Prerequisites**: Verify the .NET SDK (8/9/10) is installed.
2. **Navigate** into the project folder (`QuizGamePlatform/`) using PowerShell or your terminal.
3. **Execute the API**:
   ```bash
   dotnet run --launch-profile "http"
   ```
4. **Access Platform**: Open your Web Browser and navigate to `http://localhost:5292` (or the port specified in your output).

## 🐳 How to Run (Docker/Production)

You can containerize this entire platform (the API, SQLite Database, and Frontend) within a self-sufficient Docker container to leave running permanently or host anywhere.

1. Ensure **Docker Desktop** or the Docker daemon is running.
2. **Build the Image** (Run this in the directory that contains the `Dockerfile`):
   ```bash
   docker build -t quiz-platform .
   ```
3. **Run the Container** in detached background mode:
   ```bash
   docker run -d -p 8080:8080 --name quiz-app quiz-platform
   ```
4. **Access the Application**: Open your browser and navigate to `http://localhost:8080`.

## 🧪 Built-in Demo Credentials

The SQLite database is seeded with dummy data at startup to allow instant evaluation geometry. Jump right into the action using:
- **Admin Command Center**: `admin@quiz.com` | Password: `admin123`
- **Student Portal**: `student@quiz.com` | Password: `student123`

---
*Built organically prioritizing high performance, modular backend APIs, and premium aesthetic UX.*
