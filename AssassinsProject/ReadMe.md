# Hendrix Assassins Game Hub

A web-based management and gameplay platform for the Hendrix College Assassins Game.  
Built with ASP.NET Core 8 Razor Pages, Entity Framework Core, and deployed via Azure App Service.

---

## Overview

The Hendrix Assassins Game Hub is a centralized web application for organizing and managing the annual campus-wide Assassins Game.  
It allows participants to register, verify their identities, and play securely under the supervision of game administrators.

Current features include:
- Public sign-up with email verification.
- Automatic email confirmation and secure passcode generation.
- Game creation, setup, and management tools.
- Randomized target assignments when the game begins.
- Elimination reporting and score tracking.
- Automated communication through Azure Communication Services.

At present, administrative pages (game setup, player management, and eliminations) are open-access for development and testing.  
Authentication and admin-only restrictions are planned for future updates.

---

## Project Structure

AssassinsProject/
│
├── Pages/
│ ├── Games/ # Game creation, management, and details
│ ├── Players/ # Player administration
│ ├── Signup/ # Public signup and email verification
│ ├── Eliminations/ # Public elimination report page
│ └── Shared/ # Layouts and shared UI
│
├── Services/
│ ├── GameService.cs # Core game logic (creation, start, elimination)
│ ├── FileStorageService.cs
│ ├── Email/ # Azure Communication Service email sender
│ └── Utilities/ # Helper utilities (EmailNormalizer, Passcode, etc.)
│
├── Data/
│ ├── AppDbContext.cs # Entity Framework Core database context
│ └── Migrations/ # EF Core migration files
│
├── Models/ # Entity classes: Game, Player, Elimination, etc.
├── Program.cs # Application setup and middleware
├── appsettings.json # Configuration (database, Azure email, etc.)
└── README.md # Project documentation


---

## Running Locally

### Prerequisites
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb)
- [Azure Communication Service (Email)](https://learn.microsoft.com/azure/communication-services/)
- Visual Studio 2022 (recommended)

### Setup
1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/assassins-project.git
   cd assassins-project
2. Create or upload the local database:
    dotnet ef database update
3. Add local secrets for development
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=AssassinsDB;Trusted_Connection=True;"
    dotnet user-secrets set "Email:ConnectionString" "YOUR_AZURE_COMMUNICATION_CONNECTION_STRING"
4. Run the project:
    dotnet run
5. Open the site locally:   
    https://localhost:7038
    
