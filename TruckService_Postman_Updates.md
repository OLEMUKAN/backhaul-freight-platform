# TruckService Postman Collection Updates

This document outlines the updates made to the TruckService Postman collection to ensure it matches the current implementation.

## Added Service Discovery Endpoints

The following Service Discovery endpoints have been added to the collection:

1. **Get All Services**
   - Path: `/api/servicediscovery/services`
   - Method: GET
   - Description: Retrieves basic information about all registered services
   - Tests: Checks for 200 status code and verifies response format

2. **Get Service Details**
   - Path: `/api/servicediscovery/services/details`
   - Method: GET
   - Description: Retrieves detailed information about all registered services
   - Tests: Checks for 200 status code and verifies the response contains an array with the expected fields

3. **Check Service Health**
   - Path: `/api/servicediscovery/check/{serviceName}`
   - Method: GET
   - Description: Checks the health status of a specific service
   - Tests: Verifies the response contains service health status and details

## Enhanced Testing Coverage

Added the following requests to improve testing coverage:

1. **Get Trucks with Status Filter**
   - Path: `/api/trucks?status=1`
   - Method: GET
   - Description: Gets all active trucks with status filter applied
   - Tests: Verifies that all returned trucks have the correct status

2. **Upload Truck Document - Missing Document Type**
   - Path: `/api/trucks/{id}/documents` (without documentType parameter)
   - Method: POST
   - Description: Tests error handling for missing document type
   - Tests: Verifies 400 Bad Request status code is returned

3. **Upload Truck Document - No File**
   - Path: `/api/trucks/{id}/documents?documentType=LicensePlate` (without file in request)
   - Method: POST
   - Description: Tests error handling for missing file upload
   - Tests: Verifies 400 Bad Request status code is returned

## Recommendations for Further Improvements

1. Add more tests for different status values in the truck filter
2. Create tests for each document type in the document upload endpoint
3. Consider adding environment variables for test file paths to make document upload tests more robust
4. Add pre-request scripts to ensure proper test data is available before running tests
