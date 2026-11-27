# Nurtura

Nurtura is a responsive, phone-first web app for mothers in the first year after birth. It helps a
mother track and organize her own wellbeing and her baby's sleep and feeding. It never diagnoses:
no text, label or logic in the app draws a medical conclusion. Medical feedback comes only from a
certified professional in a booked consultation. Nurtura is a master's thesis project.

## Features

- Three roles: mother, professional (approved by the admin) and admin.
- Daily check-in (mood, sleep quality, energy, stress), wellbeing charts and a weekly summary.
- Baby profiles, a live sleep timer, a feeding log, a 7-day timeline and a routine summary.
- Booking a one-hour consultation by topic, with professionals' weekly availability, requests,
  cancelling and rescheduling.
- Real-time consultation chat, a pre-consultation summary of what the mother chose to share, and a
  closing note from the professional.
- In-app notifications.
- Guides with named sources, and the Nurtura Assistant search that suggests guides and a topic.
- A forum for mothers, with reporting and moderation.
- An admin panel: professional approval, reports, guide editing and usage counts.
- Account deletion for mothers.

## Tech stack

- .NET 10, ASP.NET Core 10 MVC and Web API in one app
- ASP.NET Core Identity (email and password, optional Google sign-in)
- Entity Framework Core 10 with Npgsql, PostgreSQL 18
- SignalR, Chart.js, Bootstrap 5
- xUnit for the tests

## Project structure

| Project | Contents |
| --- | --- |
| `src/Nurtura.Web` | MVC controllers, Razor views, the API controller, the SignalR hub, wwwroot, `Program.cs` |
| `src/Nurtura.Core` | Entities, services and business logic, the `INurturaDbContext` interface |
| `src/Nurtura.Data` | `NurturaDbContext`, entity configuration and migrations |
| `tests/Nurtura.Tests` | Integration and unit tests, focused on access control and data protection |

## Run locally

Prerequisites: the .NET 10 SDK, PostgreSQL 18, and the EF Core tool:

```powershell
dotnet tool install --global dotnet-ef
```

Set these user secrets on `src/Nurtura.Web` (names only, choose your own values):

| Name | Required |
| --- | --- |
| `ConnectionStrings:Nurtura` | Yes, the development database |
| `Seed:AdminEmail` | Yes, the admin account created at startup |
| `Seed:AdminPassword` | Yes |
| `Seed:TestPassword` | Yes in Development, for the test and demo accounts |
| `Authentication:Google:ClientId` | No, without it the Google button is hidden |
| `Authentication:Google:ClientSecret` | No |

```powershell
dotnet user-secrets set "ConnectionStrings:Nurtura" "<value>" --project src/Nurtura.Web
```

Apply the migrations, then run the app:

```powershell
dotnet ef database update --project src/Nurtura.Data --startup-project src/Nurtura.Web
dotnet run --project src/Nurtura.Web --launch-profile https
```

Open https://localhost:7130. Google sign-in expects this address
(https://localhost:7130/signin-google). The `http` profile runs on http://localhost:5050.

## Tests

The tests run the real app in memory against a separate database, `nurtura_test`. They create and
migrate it themselves and reset it between tests. The development database is never touched.

Set one user secret on `tests/Nurtura.Tests`. It must point to a database named `nurtura_test`:

```powershell
dotnet user-secrets set "ConnectionStrings:NurturaTest" "<value>" --project tests/Nurtura.Tests
```

Then run, from the repository root:

```powershell
dotnet test
```

The browser tests run in Microsoft Edge, headless, so Edge must be installed on the PC.

## Demo data

Realistic demo accounts with three months of logs, check-ins, appointments and forum posts:

```powershell
dotnet run --project src/Nurtura.Web -- --seed-demo
dotnet run --project src/Nurtura.Web -- --reset-demo
```

`--seed-demo` adds the demo accounts if there are none. `--reset-demo` removes every
`@demo.nurtura` account and its data, then seeds again. Both run only in Development, refuse the
`nurtura_test` database, seed and stop without starting the app. Demo accounts use the
`Seed:TestPassword` setting.

## Languages

The interface is in English (the default) and Polish, with an EN / PL switch. The texts live in
`src/Nurtura.Web/Resources/en.json` and `pl.json`.
