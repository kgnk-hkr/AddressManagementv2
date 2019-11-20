using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using static Google.Cloud.Dialogflow.V2.Intent.Types.Message.Types;
using static Google.Cloud.Dialogflow.V2.Intent.Types;
using Microsoft.Azure.WebJobs.Hosting;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;

namespace AddressManagementv2
{
    public static class Function1
    {
        [StorageAccount("addressmanagementv2")]//ストレージへの接続

        public class Person : TableEntity
        {
            public string publishDate { get; set; }
            public string location { get; set; }
            public string items { get; set; }
            public string PartitionKey { get; set; }

        }


        [FunctionName("AddressManagementv2")]
        public static async Task<IActionResult> AddressManagement(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req,
           [Table("AddressManagementv2")]CloudTable inTable,
           ILogger log)
        {
            var parser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true)); // パーサーを作る
            var webhookRequest = parser.Parse<WebhookRequest>(await req.ReadAsStringAsync()); // パースする
            var webhookResponse = new WebhookResponse();
            log.LogInformation(webhookRequest.QueryResult.Intent.DisplayName);

            var items = webhookRequest.QueryResult.Parameters.Fields["items"].StringValue;

            log.LogInformation(items);

            //データの読み取り
            TableQuery<Person> rangeQuery = new TableQuery<Person>();

            log.LogInformation(TableQuery.GenerateFilterCondition("items", QueryComparisons.Equal, items));

            TableContinuationToken token = null;
            TableQuerySegment<Person> segment = await inTable.ExecuteQuerySegmentedAsync(rangeQuery, token);

            //応答部分
            switch (webhookRequest.QueryResult.Intent.DisplayName)
            {
                default:
                    {
                        if (segment.Any())
                        {
                            var fulfillmentText = "お調べします。\n";
                            {
                                foreach (Person entity in segment)
                                {
                                    log.LogInformation(entity.PartitionKey);
                                    log.LogInformation(entity.items);
                                    log.LogInformation(items);

                                    if (entity.items == items)//フィルター
                                    {
                                        fulfillmentText += $"{entity.items}は、{entity.location}にあります。";
                                        log.LogInformation(entity.PartitionKey);
                                        log.LogInformation(entity.items);
                                    }
                                }
                            }
                            webhookResponse.FulfillmentText = fulfillmentText;
                        }
                        else
                        {
                            webhookResponse.FulfillmentText = "登録されていません";
                        }
                    }
                    break;
            }

            return new ProtcolBufJsonResult(webhookResponse, JsonFormatter.Default);
        }
    }

    // Google.Protobuf を使ってシリアライズする IActionResult の実装
    public class ProtcolBufJsonResult : IActionResult
    {
        private readonly object _obj;
        private readonly JsonFormatter _formatter;

        public ProtcolBufJsonResult(object obj, JsonFormatter formatter)
        {
            _obj = obj;
            _formatter = formatter;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.Headers.Add("Content-Type", new Microsoft.Extensions.Primitives.StringValues("application/json; charset=utf-8"));
            var stringWriter = new StringWriter();
            _formatter.WriteValue(stringWriter, _obj);
            await context.HttpContext.Response.WriteAsync(stringWriter.ToString());
        }
    }

}
