# Cloud Ticketing App (.NET + GCP)

The hosted website can be accessed here: https://cloud-ticket-app-665990973538.europe-west1.run.app/Authenticator

A cloud-native ticket management backend built with **.NET**, containerized using **Docker**, and integrated with **Google Cloud Platform (GCP)** for deployment and scaling.

📌 **Purpose**: Developed as part of the SWD6.3B coursework and added to my portfolio to demonstrate backend development, cloud integration, and DevOps-ready engineering.

---

## 🚀 Features

- ✅ RESTful API backend (assumed structure)
- ☁️ GCP integration via service account
- ⚙️ Configured with `appsettings.json` for environment flexibility
- 🐳 Fully Dockerized for deployment
- 🔐 Secure architecture with `.gitignore` for credentials

---

## 🧰 Tech Stack

- **Language**: C#
- **Framework**: .NET 6 / ASP.NET Core
- **Cloud Provider**: Google Cloud Platform
- **Containerization**: Docker
- **IDE**: JetBrains Rider / Visual Studio

---

## 📁 Project Structure
cloud-ticket-app/
├── Program.cs # App entry point
├── appsettings.json # Main config
├── appsettings.Development.json # Local dev config
├── Dockerfile # Container definition
├── gcp-service-account.json # 🔒 (ignored in .gitignore)
├── .gitignore
├── .dockerignore
├── cloud-ticket-app.sln # Solution file

---

## 🔧 Setup & Usage

### ▶️ Run Locally (Dev Mode)

```
dotnet restore
dotnet build
dotnet run
```
Access locally at ```http://localhost:5000``` (or whatever port you configure).

## 🐳 Run with Docker
```
docker build -t cloud-ticket-app
docker run -p 5000:80 cloud-ticket-app
```
## ☁️ GCP Integration
This project uses a Google service account for cloud access. You’ll need to:
1. Create a service account on GCP.
2. Download the credentials file.
3. Set the environment variable:
``` export GOOGLE_APPLICATION_CREDENTIALS=/path/to/your/credentials.json```
