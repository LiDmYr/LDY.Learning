using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Wrap;
using System.Net.Http.Headers;

namespace LDY.Learning.Polly.Infrastructure
{
    public static class HttpClientsExtensions
    {
        public static IServiceCollection AddHttpClients(this IServiceCollection serviceCollection) {
            AsyncRetryPolicy<HttpResponseMessage> foreverRetryPolicy = HttpPolicyExtensions
              .HandleTransientHttpError()
              .WaitAndRetryForeverAsync(r => TimeSpan.FromSeconds(1), async (delegateException, r, _) =>
              {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine($"Failed: retried {r}, {DateTime.Now}, Exception: {delegateException?.Exception?.Message}");
              });

            AsyncRetryPolicy<HttpResponseMessage> realeaseLockOnSuccessPolicy = Policy.HandleResult<HttpResponseMessage>(r =>
            {
                // We only check here whether the response is successful and we can release the lock
                // We don't expect that this policy will retry the request
                // We don't expect that this policy can be triggered in case of exception because `foreverRetryPolicy` has to cover that case
                if (r.IsSuccessStatusCode) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Here we should release our lock - Wifi is turned on back");
                }

                // Returning false we "say" - please don't retry
                return false;
            }).RetryAsync();

            AsyncPolicyWrap<HttpResponseMessage> chainOfPolicies = Policy.WrapAsync(foreverRetryPolicy, realeaseLockOnSuccessPolicy);

            serviceCollection.AddHttpClient(HttpClients.BlockingHttpClient, client =>
            {
                client.BaseAddress = new Uri(uriString: "https://google.com");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            // OPTION 1 - manually add all the policies
            //.AddPolicyHandler(foreverRetryPolicy)
            //.AddPolicyHandler(realeaseLockOnSuccessPolicy);

            // OPTION 2 - add chain of policies
            .AddPolicyHandler(chainOfPolicies);

            return serviceCollection;
        }
    }
}