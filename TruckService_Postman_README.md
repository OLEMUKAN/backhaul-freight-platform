# Truck Service API Testing with Postman

This repository contains Postman files for testing the Truck Service API in the Backhaul Freight Matching Platform.

## Files

- `TruckService_Postman_Collection.json` - The Postman collection with all API requests
- `TruckService_Postman_Environment.json` - Environment variables for the collection

## Setup Instructions

### 1. Install Postman

If you don't have Postman installed, download and install it from [the Postman website](https://www.postman.com/downloads/).

### 2. Import Collection and Environment

1. Open Postman
2. Click on "Import" in the top left corner
3. Drag and drop both JSON files or use the file selection dialog to import them
4. Both the collection and environment should appear in your Postman workspace

### 3. Configure Environment Variables

1. Click on the environment dropdown in the top right corner of Postman
2. Select "Truck Service Environment"
3. Update the following variables as needed:
   - `truck_service_url` - URL of your Truck Service API
   - `user_service_url` - URL of your User Service API
   - `truck_owner_email` - Email of a user with the TruckOwner role
   - `truck_owner_password` - Password for the truck owner user
   - `admin_email` - Email of a user with the Admin role
   - `admin_password` - Password for the admin user

### 4. Running the Tests

The collection is organized into folders that group related endpoints. You can run the requests in the following sequence:

1. **Authentication**
   - Run "Login as Truck Owner" to get a token for truck owner operations
   - Run "Login as Admin" to get a token for admin operations

2. **Truck Management**
   - Run "Create Truck" to add a new truck
   - Run "Get Trucks" to list trucks for the authenticated user
   - Run "Get Truck by ID" to retrieve a specific truck
   - Run "Update Truck" to modify truck details
   - Run "Verify Truck (Admin)" to mark a truck as verified (requires admin token)
   - Run "Upload Truck Document" to add documents to a truck
   - Run "Delete Truck" to remove a truck

3. **Edge Cases**
   - These tests verify the API's behavior for error conditions

4. **Health Check**
   - Monitors the health of the service

### 5. Automated Testing

You can run the entire collection as an automated test:

1. Click on the "..." (three dots) next to the collection name
2. Select "Run collection"
3. Configure the run order and iterations
4. Click "Run Truck Service API"

## Notes

- The Postman scripts automatically set environment variables based on responses. For example, after creating a truck, the truck ID is stored in the `truck_id` variable.
- Authentication tokens are automatically captured and used for subsequent requests.
- Folder structure follows the logical flow of API testing.
- Each request includes tests to validate the response.

## Troubleshooting

- If authentication fails, ensure your user credentials are correct in the environment variables.
- If requests fail with 404 errors, check that the service URLs are correct and the services are running.
- Look at the Console in Postman (at the bottom) for detailed error messages.
- Verify that the test users have the correct roles assigned in the User Service. 