using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Worker;

public class Function
{

    private readonly IAmazonDynamoDB _dynamoDbClient;

    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach(var message in evnt.Records)
        {
            var jobMessage = JsonSerializer.Deserialize<JobMessage>(message.Body);
            await ProcessMessageAsync(jobMessage, context);
        }
    }

    private async Task ProcessMessageAsync(JobMessage jobMessage, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing message {jobMessage.JobId}, FileName: {jobMessage.FileName}");
        var tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? throw new InvalidOperationException("DYNAMODB_TABLE_NAME is not configured.");

        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobMessage.JobId },
                ["FileName"] = new AttributeValue { S = jobMessage.FileName },
                ["Status"] = new AttributeValue { S = "Processing" }
            }
        });

        await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobMessage.JobId }
            },
            UpdateExpression = "SET #S = :s",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#S"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":s"] = new AttributeValue { S = "completed" }
            }
        });
    }
}