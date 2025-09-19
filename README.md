# BikePartsTracker.Backend

A .NET 9 Web API backend for tracking bike parts, maintenance schedules, and bike information. This application provides a RESTful API for managing bike parts inventory, maintenance records, and bike details.

## 🚀 Features

- **Bike Management**: Add, update, and track multiple bikes
- **Parts Tracking**: Manage bike parts inventory and usage history
- **Maintenance Scheduling**: Track maintenance schedules and history
- **Strava Integration**: Sync with Strava for activity data
- **PostgreSQL Database**: Robust data storage with Entity Framework Core
- **Swagger Documentation**: Interactive API documentation

## 🛠️ Technology Stack

- **.NET 9** - Latest .NET framework
- **ASP.NET Core Web API** - RESTful API framework
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database
- **Docker** - Containerization
- **Swagger/OpenAPI** - API documentation

## 📋 Prerequisites

Before running this project, ensure you have the following installed:

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for containerized setup)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (for local development)
- [PostgreSQL](https://www.postgresql.org/download/) (for local development without Docker)

## 🚀 Quick Start with Docker (Recommended)

The easiest way to run this project is using Docker Compose, which will set up both the application and PostgreSQL database automatically.

### 1. Clone the Repository

```bash
git clone <your-repository-url>
cd BikePartsTracker.Backend
```

### 2. Run with Docker Compose

```bash
docker compose up --build
```

This command will:
- Build the .NET application Docker image
- Start a PostgreSQL database container
- Start the application container
- Set up networking between containers

### 3. Access the Application

Once the containers are running, you can access:

- **API**: http://localhost:8080
- **Swagger Documentation**: http://localhost:8080/swagger
- **PostgreSQL Database**: localhost:5432

### 4. Stop the Application

```bash
docker compose down
```

To also remove the database volume (this will delete all data):
```bash
docker compose down -v
```

## 🔧 Local Development Setup

If you prefer to run the application locally without Docker:

### 1. Database Setup

1. Install PostgreSQL locally
2. Create a database named `BikePartsTracker`
3. Update the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=BikePartsTracker;Username=your_username;Password=your_password"
  }
}
```

### 2. Run Database Migrations

```bash
dotnet ef database update
```

### 3. Run the Application

```bash
dotnet run
```

The application will be available at:
- **API**: http://localhost:5000
- **Swagger Documentation**: http://localhost:5000/swagger

## 📁 Project Structure

```
BikePartsTracker.Backend/
├── Controllers/          # API controllers
├── Data/                 # Database context and migrations
├── DTOs/                 # Data transfer objects
├── Jobs/                 # Background jobs (Strava sync)
├── Models/               # Entity models
├── Services/             # Business logic services
├── Dockerfile            # Docker configuration
├── docker-compose.yml    # Docker Compose orchestration
├── appsettings.json      # Application configuration
└── Program.cs            # Application entry point
```

## 🔌 API Endpoints

The API provides the following main endpoints:

### Bikes
- `GET /api/bikes` - Get all bikes
- `POST /api/bikes` - Create a new bike
- `GET /api/bikes/{id}` - Get bike by ID
- `PUT /api/bikes/{id}` - Update bike
- `DELETE /api/bikes/{id}` - Delete bike

### Parts
- `GET /api/parts` - Get all parts
- `POST /api/parts` - Create a new part
- `GET /api/parts/{id}` - Get part by ID
- `PUT /api/parts/{id}` - Update part
- `DELETE /api/parts/{id}` - Delete part

### Maintenance
- `GET /api/maintenance` - Get all maintenance records
- `POST /api/maintenance` - Create maintenance record
- `GET /api/maintenance/{id}` - Get maintenance by ID
- `PUT /api/maintenance/{id}` - Update maintenance
- `DELETE /api/maintenance/{id}` - Delete maintenance

For detailed API documentation, visit the Swagger UI at `/swagger` when the application is running.

## 🐳 Docker Commands

### Build the Application
```bash
docker build -t bikepartstracker-backend .
```

### Run PostgreSQL Only
```bash
docker compose up postgres
```

### View Logs
```bash
# View all logs
docker compose logs

# View application logs
docker compose logs app

# Follow logs in real-time
docker compose logs -f app
```

### Access Database
```bash
# Connect to PostgreSQL container
docker compose exec postgres psql -U postgres -d BikePartsTracker
```

### Reset Everything
```bash
# Stop and remove containers, networks, and volumes
docker compose down -v

# Remove all images
docker rmi $(docker images -q)
```

## 🔧 Configuration

### Environment Variables

The application can be configured using environment variables:

- `ASPNETCORE_ENVIRONMENT` - Set to `Development`, `Staging`, or `Production`
- `ASPNETCORE_URLS` - Configure the URLs the application listens on

### Database Configuration

Database connection can be configured in:
- `appsettings.json` - Default configuration
- `appsettings.Development.json` - Development-specific settings
- `appsettings.Docker.json` - Docker-specific settings

## 🧪 Testing

To run tests (if any are added):

```bash
dotnet test
```

## 📝 Development Notes

- The application uses Entity Framework Core for database operations
- Migrations are included in the `Data/Migrations` folder
- The `Jobs` folder contains background job implementations (e.g., Strava sync)
- Swagger documentation is automatically generated from controller attributes

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

[Add your license information here]

## 🆘 Troubleshooting

### Common Issues

**Port 5000 already in use:**
- The Docker setup uses ports 8080 and 8443 to avoid conflicts with macOS AirPlay
- If you need different ports, update the `docker-compose.yml` file

**Database connection issues:**
- Ensure PostgreSQL is running
- Check connection string in `appsettings.json`
- Verify database exists and migrations are applied

**Docker build fails:**
- Ensure Docker Desktop is running
- Check that all required files are present
- Verify Dockerfile syntax

For more help, check the logs using `docker compose logs`.