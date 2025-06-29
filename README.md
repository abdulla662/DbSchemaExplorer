# DbSchemaExplorer

A Blazor Server web app that connects to a local SQL Server database, retrieves all tables and foreign key relationships, and dynamically renders an interactive **ERD (Entity Relationship Diagram)** using [Mermaid.js](https://mermaid.js.org/).

---

## 🚀 Features

- 🔌 Connect to any local SQL Server database (Integrated Security).
- 📋 List all base tables and their columns.
- 🔗 Display table relationships (foreign key constraints).
- 🎨 Generate dynamic ERD diagram using Mermaid.
- 🔍 Zoom in/out functionality.
- 💡 Fully client-rendered, no Mermaid dependency at build time.

---

## 🧠 Technologies Used

- [Blazor Server (.NET 8)](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor)
- SQL Server + ADO.NET
- Mermaid.js for diagram rendering
- Bootstrap for UI styling

---

## 🖼 Preview

![ERD Sample Screenshot](screenshots/erd-sample.png) <!-- Replace this with an actual image from your project -->

---

## ⚙️ Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/your-username/DbSchemaExplorer.git
cd DbSchemaExplorer
