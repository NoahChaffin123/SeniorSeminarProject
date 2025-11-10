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

```text
AssassinsProject/
│
├── Pages/
│   ├── Games/         # Game creation, management, and details
│   ├── Players/       # Player administration
│   ├── Signup/        # Public signup and email verification
│   ├── Eliminations/  # Public elimination report page
│   └── Shared/        # Layouts and shared UI
│
├── Services/
│   ├── GameService.cs       # Core game logic (creation, start, elimination)
│   ├── FileStorageService.cs
│   ├── Email/               # Azure Communication Service email sender
│   └── Utilities/           # Helper utilities (EmailNormalizer, Passcode, etc.)
│
├── Data/
│   ├── AppDbContext.cs      # Entity Framework Core database context
│   └── Migrations/          # EF Core migration files
│
├── Models/                  # Entity classes: Game, Player, Elimination, etc.
├── Program.cs               # Application setup and middleware
├── appsettings.json         # Configuration (database, Azure email, etc.)
└── README.md                # Project documentation
```


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
    
### Deploying to Azure
1. Publish via Visual Studio COde of the .NET CLI:
    dotnet publish -c Release

2. Deploy the published files to your Azure App Service

3. In the Azure Portal, Navigate to your App Service -> Configuration, and ensure the following settings are present:
    - ConnectionStrings:DefaultConnection (SQL Connection String)
    - Email:ConnectionString (Azure Communication Service connection string)
    - APP_BASE_URL (the base URL of the hosted site)

### Email Verification

The email verification system is powered by Azure Communication services. When a player signs up, the following occurs:
1. A verification token is generated and stored securely in the database
2. A confirmation email containing a unique link is sent to each player.
3. When the players clicks the link, they are verified and activated for gameplay.

### Gameplay logic
    - Setup Phase: The game is created, and players register with their information and picture. Sign-ups are open (or closed) until the administrator chooses to start the game.
    - Verification: Players must verify their email before being eligible for participation.
    - Game Start: Once started, the system automatically generates a single target cycle, ensuring no two players target each other directly.
    - Active Phase: Players report eliminations using both their own and their target's passcodes. Verified eliminations award points and reassign targets automatically. 
    - Game Completion: The game ends when there is one person left, or when the admin ends the game.



Technology Stack:
| Component    | Description                           |
| ------------ | ------------------------------------- |
| Framework    | ASP.NET Core 8 Razor Pages            |
| Database     | Entity Framework Core with SQL Server |
| Email        | Azure Communication Services          |
| File Storage | Local or Azure Blob storage           |
| Hosting      | Azure App Service                     |
| Language     | C# 12                                 |

Common Commands:
| Purpose                | Command                           |
| ---------------------- | --------------------------------- |
| Run the project        | `dotnet run`                      |
| Update the database    | `dotnet ef database update`       |
| Add a new migration    | `dotnet ef migrations add <Name>` |
| Remove last migration  | `dotnet ef migrations remove`     |
| Build project          | `dotnet build`                    |
| Publish for deployment | `dotnet publish -c Release`       |

### Future Plans
The following features are planned for future development:

- Secure administator login using Microsoft Entra ID (Azure AD)
- Role-based access control (restricting admin tools to verified users).
- In-game status dashboard for active players
- Optional Mobile-friendly interface.

