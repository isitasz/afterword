# `afterword`

a cloud-based media manager tracking books, games, movies & anime, featuring an automated ELT pipeline (PostgreSQL + dbt) for analytical modeling. 

this project marks the evolution of a local c# desktop application (which originally exported static json logs) into a fully decoupled, api-first web architecture backed by postgresql. it is designed from the ground up to serve as a foundation for an automated data engineering pipeline (elt) and habit analytics platform.

### `> status: v1 — online catalog (in progress)`

the project is currently in v1, focusing on establishing core database persistence, api endpoints, and remote management.

### `> v1 features & goals`

- [x] database schema migration: centralized postgresql schema on neon using flexible jsonb metadata columns and unified categories
- [x] backend infrastructure: refactored c# logic into a decoupled asp.net core web api middleware
- [ ] unified api endpoints: rest endpoints (/api/media) for crud operations and external api integrations (google books, igdb, tmdb, mal)
- [ ] admin dashboard (/admin): protected web interface to create, update, and manage media consumption statuses (planned, in progress, completed, dropped)
- [ ] public catalog: dynamic showcase rendering saved media records directly from the database

### `> architecture (v1)`

web dashboard / public frontend
  │
  ▼  (http / json payload)
asp.net core web api ──> fetches metadata from external apis (google books, igdb, etc.)
  │
  ▼  (npgsql driver)
neon postgresql database (media table + jsonb metadata)

### `> roadmap`

- [x] v1 — online catalog: decoupled web api, neon postgresql persistence, /admin management, external api metadata ingestion
- [ ] v2 — historical tracking: independent waitlists per category, manual queue ordering, consumption timeline logs, and initial metrics
- [ ] v3 — data engineering pipeline: automated elt pipeline, raw data landing zone, sql/dbt transformations into star schema (fact/dimension tables)
- [ ] v4 — analytics & optimization: annual consumption dashboards, cross-category genre evolution analytics, pipeline monitoring, and query optimizations

### `> stack`

`c#` · `.net 8` · `postgresql` · `neon` · `npgsql` · `git`

### `> getting started`

prerequisites:
- .net 8.0 sdk
- a postgresql instance (preferably neon)

setup:
1. clone the repository:
   git clone https://github.com/your-username/afterword.git
   cd afterword

2. configure your connection string in appsettings.json or environment variables:
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=your-neon-host;Database=neondb;Username=your-user;Password=your-password;SSL Mode=Require;"
     }
   }

3. run the web api:
   dotnet run

---

<sub>currently under development (v1 - online catalog mvp).</sub>