using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;

namespace Analytics
{
    public class AnalyticsProvider
    {
        private const string TrackingId = "UA-49446143-5";
        private static readonly string CustomerSessionId = Guid.NewGuid().ToString();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string ProductVersion = ConfigurationManager.AppSettings["ProductVersion"];
        public static async Task TrackEvent(string category, string action, string label)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("v", "1"),
                new KeyValuePair<string, string>("tid", TrackingId),
                new KeyValuePair<string, string>("cid", CustomerSessionId),
                new KeyValuePair<string, string>("t", "event"),
                new KeyValuePair<string, string>("ec", category),
                new KeyValuePair<string, string>("ea", action),
                new KeyValuePair<string, string>("el", label),
                new KeyValuePair<string, string>("an", "AbittiUSB"),
                new KeyValuePair<string, string>("aiv", ProductVersion)
            });

            await SendUsingMeasurementProtocol(content);
        }

        public static async Task TrackException(Exception exception)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("v", "1"),
                new KeyValuePair<string, string>("tid", TrackingId),
                new KeyValuePair<string, string>("cid", CustomerSessionId),
                new KeyValuePair<string, string>("t", "exception"),
                new KeyValuePair<string, string>("exd", exception.ToString()),
                new KeyValuePair<string, string>("exf", "1"),
                new KeyValuePair<string, string>("an", "AbittiUSB"),
                new KeyValuePair<string, string>("aiv", ProductVersion)
            });

            await SendUsingMeasurementProtocol(content);
        }

        private static async Task SendUsingMeasurementProtocol(HttpContent content)
        {
            try
            {
                using (var client = new HttpClient {BaseAddress = new Uri("http://www.google-analytics.com")})
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CustomHttpClient/1.0");
                    using (var response = await client.PostAsync("/collect", content))
                    using (var responseContent = response.Content)
                    {
                        await responseContent.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception e)
            {
                // Analytics failure ... nothing we can do but log.
                // suppress, eg. do not let application crash if analytics fails
                Logger.Error(e);
            }
        }
    }
}