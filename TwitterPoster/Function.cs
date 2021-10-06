using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TwitterPoster
{
    public class Function
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string TweetURI = Environment.GetEnvironmentVariable("TweetURI");
        private static readonly string WebHookURI = Environment.GetEnvironmentVariable("WebHookURI");
        private static readonly string DynamoDBAccess = Environment.GetEnvironmentVariable("DynamoDBAccess");
        private static readonly string DynamoDBSecret = Environment.GetEnvironmentVariable("DynamoDBSecret");
        private static readonly string BearerToken = Environment.GetEnvironmentVariable("BearerToken");
        private static readonly string TwitterAccountName = Environment.GetEnvironmentVariable("TwitterAccountName");
        private static readonly string RegexToMatch = Environment.GetEnvironmentVariable("RegexToMatch");

        private static AmazonDynamoDBClient dbClient;

        private static DateTime LastPostedDate = new DateTime(0);

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(string input, ILambdaContext context)
        {
            Thread t = new Thread(new ThreadStart(SetupDB));
            t.Start();
            DoWork().Wait();
            return input;
        }

        private async void SetupDB()
        {
            LambdaLogger.Log("SetupDB started");
            dbClient = new AmazonDynamoDBClient(new BasicAWSCredentials(DynamoDBAccess, DynamoDBSecret));
            ScanResponse data = await dbClient.ScanAsync("seenFashionReports", new List<string>() { "lastPostedDate" });
            LastPostedDate = DateTime.Parse(data.Items[0]["lastPostedDate"].S);
            LambdaLogger.Log("SetupDB finished");
        }

        private async Task<Task> DoWork()
        {
            LambdaLogger.Log("DoWork started");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            try
            {
                string responseBody = await client.GetStringAsync(TweetURI);
                TwitterData foundTweets = JsonConvert.DeserializeObject<TwitterData>(responseBody);

                foreach(Data d in foundTweets.data)
                {
                    if (!Regex.IsMatch(d.text, RegexToMatch))
                    {
                        continue;
                    }
                    while (LastPostedDate == new DateTime(0)) { Thread.Sleep(50); } //nap for 50ms while the database connection is made

                    if (LastPostedDate < d.created_at)
                    {
                        var values = new Dictionary<string, string> { { "content", $"https://twitter.com/{TwitterAccountName}/status/{d.id}" } };
                        var content = new FormUrlEncodedContent(values);

                        await dbClient.UpdateItemAsync(new UpdateItemRequest()
                        {
                            TableName = "seenFashionReports",
                            UpdateExpression = "SET lastPostedDate = :d",
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                                {
                                    { ":d", new AttributeValue(){ S = d.created_at.ToString()  } }
                                },
                            Key = new Dictionary<string, AttributeValue>() { { "data", new AttributeValue() { S = "1" } } }
                        });
                        await client.PostAsync(WebHookURI, content);
                    }
                }
            }
            catch (Exception e)
            {
                LambdaLogger.Log(e.Message);
                LambdaLogger.Log(e.StackTrace);
            }
            return Task.CompletedTask;
        }
    }
}
