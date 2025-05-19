# Backhaul Freight Matching Platform

A cloud-native, microservices-based digital marketplace that connects shippers with trucks on their return journeys, optimizing load utilization and reducing costs for both parties.

## Project Overview

Many logistics operators in Uganda face inefficiencies on their return trips: trucks often travel empty, resulting in wasted fuel costs, lost revenue, and underutilized capacity. Simultaneously, shippers struggle to find reliable carriers for one-way or partial routes. This platform addresses these issues by creating a digital marketplace for backhaul freight.

## Architecture

This project follows a microservices architecture with the following components:

### Core Services
- **User Service**: Authentication, profiles, roles
- **Truck Service**: Truck registration, verification, and management
- **Route Service**: Route planning and geospatial matching
- **Shipment Service**: Shipment listings and management
- **Booking Service**: Orchestrates the booking process

### Supporting Services
- **Matching Service**: Intelligent matching between shipments and routes
- **Chat Service**: Real-time communication between users
- **Tracking Service**: Real-time location and ETA tracking
- **Notification Service**: Handles emails, SMS, and push notifications
- **Payment Service**: Processes payments and financial transactions
- **AI Service**: Image recognition for cargo categorization
- **Admin Service**: Platform management and analytics

### Infrastructure
- API Gateway
- Message Broker (RabbitMQ)
- Distributed Tracing
- Centralized Logging
- Container Orchestration

## Technology Stack

- Backend: ASP.NET Core
- Databases: SQL Server, PostgreSQL with PostGIS
- Message Broker: RabbitMQ with MassTransit
- Real-time: SignalR
- Caching: Redis
- Authentication: JWT with IdentityServer
- Frontend: React with Mapbox integration

## Getting Started

[Documentation on setting up the development environment and running the services will be added here]
