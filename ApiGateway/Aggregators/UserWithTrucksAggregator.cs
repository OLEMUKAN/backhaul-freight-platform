using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Aggregators
{
    public class UserWithTrucksAggregator : IDefinedAggregator
    {
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            if (responses == null || responses.Count != 2)
            {
                return new DownstreamResponse(
                    new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("Error: Expected exactly two responses for aggregation")
                    });
            }

            var userResponse = responses[0].Items.DownstreamResponse();
            var trucksResponse = responses[1].Items.DownstreamResponse();            // Check if responses are successful (StatusCode between 200-299)
            if ((int)userResponse.StatusCode >= 200 && (int)userResponse.StatusCode < 300 && 
                (int)trucksResponse.StatusCode >= 200 && (int)trucksResponse.StatusCode < 300)
            {
                var userContentString = await userResponse.Content.ReadAsStringAsync();
                var trucksContentString = await trucksResponse.Content.ReadAsStringAsync();

                var user = JsonSerializer.Deserialize<JsonElement>(userContentString);
                var trucks = JsonSerializer.Deserialize<JsonElement>(trucksContentString);

                // Create aggregated response
                var aggregatedResponse = new 
                {
                    User = user,
                    Trucks = trucks
                };

                var jsonString = JsonSerializer.Serialize(aggregatedResponse);
                var stringContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var headers = userResponse.Headers.Concat(trucksResponse.Headers).ToList();

                // Create response with appropriate status code
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = stringContent
                };
                
                return new DownstreamResponse(response);
            }            // If any of the requests failed, return an error
            var errorMsg = new StringBuilder("Aggregation failed: ");
            if (!((int)userResponse.StatusCode >= 200 && (int)userResponse.StatusCode < 300))
            {
                errorMsg.Append($"User service returned {userResponse.StatusCode}. ");
            }
            if (!((int)trucksResponse.StatusCode >= 200 && (int)trucksResponse.StatusCode < 300))
            {
                errorMsg.Append($"Trucks service returned {trucksResponse.StatusCode}.");
            }

            return new DownstreamResponse(
                new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
                {
                    Content = new StringContent(errorMsg.ToString())
                });
        }
    }
}
