# SmartWebShop

A SmartWebShop application built with ASP .NET Technology. The frontend is an ASP .NET web app with Razor Pages, and the backend is an ASP .NET Core web API. The project is written in .NET 8.

## Table of Contents


- [Frontend Screens](#frontend-screens)
- [Backend API](#backend-api)
- [Usage](#usage)
- [Data Migration Tool](#data-migration-tool)


## Frontend Screens

- `/Login` - Login screen
- `/Home` - Viewing products available in the webshop
- `/Product/id` - Viewing details of the selected product
- `/Cart` - Cart site with checkout functionality

## Backend API

The API uses a SQL Server database with Product, Order, OrderItem, and User tables. The database is currently deployed locally via docker-compose.
he API has the following controllers:

- `/api/Order` - Create, get one, get all orders
- `/api/Product` - CRUD for products
- `/api/Account` - Login, Register

### Setting up User Secrets

To securely store sensitive information such as connection strings, you can use the `dotnet user-secrets` tool. Follow the steps below to set up user secrets for the project:

1. Navigate to the backend project directory:

    ```bash
    cd ../Contoso.Api
    ```

2. Initialize user secrets for the project:

    ```bash
    dotnet user-secrets init
    ```

3. Set the connection string as a user secret:

    ```bash
    dotnet user-secrets set "ConnectionStrings:ContosoDBConnection" "YOUR_CONNECTION_STRING_FOR_SQL_SERVER"
    ```

By using user secrets, you ensure that sensitive information is not hard-coded in your source code and is kept secure.

## Usage

Instructions on how to use the project.

```bash
# Run the frontend project
cd Contoso.WebApp
dotnet run

# Run the backend project
cd ../Contoso.Api
dotnet run
```

## Data Migration Tool

This console application migrates data from a SQL Server database to Azure Cosmos DB. Follow the steps below to set it up and use it:

**Note:** The tool assumes that Cosmos DB containers are named `Users`, `Products`, and `Orders`.

### Setup

1. **Navigate to the Application Directory**

    Open your terminal and navigate to the application directory:

    ```bash
    cd Contoso.DataMigrationTool
    ```

2. **Configure Environment Variables**

    Update the `appsettings.json` file with your Azure SQL and Azure Cosmos DB connection details. Replace the placeholders with actual values:

### Usage

Run the console application:

```bash
dotnet run
```

