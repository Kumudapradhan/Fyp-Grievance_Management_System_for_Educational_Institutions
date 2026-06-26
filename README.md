# Grievance Management System (GMS) - Web Portal

Grievance Submission, Tracking, and Management Platform built for educational institutions. Designed as a BSc (Hons) Information Technology Final Year Project for Asia Pacific University (APU).

## Project Metadata
- **Student**: Kumuda Pradhan (ID: NP069687)
- **Supervisor**: Mr. Subash Khatiwada
- **Second Marker**: Mr. Gaurav Bhattarai
- **Sponsor Goal**: SDG Goal 4 — Quality Education

---

## Technical Stack
- **Backend Framework**: ASP.NET Core 9.0 (MVC Pattern)
- **Database Access**: Microsoft SQL Server Express / LocalDB + Entity Framework Core (Code-First)
- **Authentication**: ASP.NET Core Identity (Role-Based Access Control)
- **Email Notifications**: MailKit / local file-based SMTP mock logs
- **Frontend Utilities**: HTML5, CSS3, Bootstrap 5, Chart.js (CDN)
- **Unit Testing**: MSTest + Moq

---

## Solution Structure
```
GMS/
├── GMS.Web/                        ← ASP.NET Core MVC project
│   ├── Controllers/                ← Account, Grievance, Admin, Staff, Report, Home
│   ├── Data/                       ← ApplicationDbContext, DbInitializer (seed script), Migrations
│   ├── Models/                     
│   │   ├── Entities/               ← All 8 relational tables (User, Grievance, Dept, Category, etc.)
│   │   └── ViewModels/             ← Page structures and validation models
│   ├── Services/                   ← Grievance, Routing, Ticket, Notification, OverdueCheck, Repetitive, File
│   ├── Views/                      ← Structured user interfaces (Admin, Staff, Student, Public)
│   ├── wwwroot/                    ← Custom site.css, script assets, and uploaded evidence files
│   └── appsettings.json            ← Database settings, file limits, SLA, and SMTP options
├── GMS.Tests/                      ← MSTest project
│   ├── GrievanceServiceTests.cs
│   ├── RoutingServiceTests.cs
│   ├── TicketServiceTests.cs
│   ├── NotificationServiceTests.cs
│   ├── OverdueCheckServiceTests.cs
│   └── RepetitiveDetectionServiceTests.cs
└── README.md                       ← Setup and run guide (this file)
```

---

## Setup and Running the Application

### Prerequisites
- .NET 9.0 SDK installed
- LocalDB or SQL Server Express installed (configured in `appsettings.json`)

### Step 1: Clone and Restore Packages
Restore all NuGet dependencies:
```bash
dotnet restore GMS.sln
```

### Step 2: Database Migrations
Initialize database tables and seed configurations:
```bash
# If you are in the parent folder (C:\Users\USER\Desktop\fyp):
dotnet ef database update --project GMS/GMS.Web/GMS.Web.csproj --startup-project GMS/GMS.Web/GMS.Web.csproj

# If you are inside the GMS/ folder:
dotnet ef database update --project GMS.Web/GMS.Web.csproj --startup-project GMS.Web/GMS.Web.csproj
```
*(This creates `GMS_Db` database inside LocalDB and runs the startup seeding process).*

### Step 3: Run the Web App
Start the application server:
```bash
# If you are in the parent folder (C:\Users\USER\Desktop\fyp):
dotnet run --project GMS/GMS.Web/GMS.Web.csproj

# If you are inside the GMS/ folder:
dotnet run --project GMS.Web/GMS.Web.csproj
```
The application will listen on:
- **HTTPS**: `https://localhost:7067`
- **HTTP**: `http://localhost:5155`

---

## Authentication and Seeding Details

On initial run, the application seeds default database tables:
- **Roles**: `Student`, `Administrator`, `Staff`
- **Default Admin Account**:
  - **Email**: `admin@gms.edu.my`
  - **Password**: `Admin@123`
- **Default Departments**: Academic Affairs, Finance and Accounts, Student Welfare, IT Support, General Administration
- **Default Category Mappings**: Auto-routes issues (e.g. IT Issue to IT Support department).

---

## Running Unit Tests
We have built a suite of 10 tests verifying sequence generation, SLA checks, repetitive thresholds, and file validators. Run tests using:
```bash
dotnet test GMS.sln
```
All tests use an in-memory database configuration and Moq dependencies.
