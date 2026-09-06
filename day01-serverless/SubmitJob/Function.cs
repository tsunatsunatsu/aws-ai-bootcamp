using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using Amazon.SQS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SubmitJob;

public class Function
{
    private readonly IAmazonSQS _sqsClient;

    public Function()
    {
        _sqsClient = new AmazonSQSClient();
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var jobId = Guid.NewGuid().ToString();
        var jobRequest = JsonSerializer.Deserialize<JobRequest>(request.Body);

        var jobMessage = new JobMessage
        {
            JobId = jobId,
            FileName = jobRequest.FileName
        };
        var sqsMessage = JsonSerializer.Serialize(jobMessage);
        var queueUrl = Environment.GetEnvironmentVariable("QUEUE_URL") ?? throw new InvalidOperationException("QUEUE_URL is not configured."); // 環境変数を取得してSQSのキューURLを取得

        var sendMessageRequest = new Amazon.SQS.Model.SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = sqsMessage
        };
        await _sqsClient.SendMessageAsync(sendMessageRequest);

        var res = new APIGatewayHttpApiV2ProxyResponse
        {
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            StatusCode = 202,
            Body = JsonSerializer.Serialize(new
            {
                jobId = jobId,
                status = "accepted"
            })
        };

        return res;
    }
}
