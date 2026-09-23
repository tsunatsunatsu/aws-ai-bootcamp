using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

///
/// C# Application
/// -> Bedrock Runtime Client
/// -> Resolve Credentails 
/// -> Bedrock Runtime 
var client = new AmazonBedrockRuntimeClient(
    RegionEndpoint.APNortheast1 
);

var request = new ConverseRequest
{
    ModelId = "amazon.nova-lite-v1:0",
    Messages = [
        new Message
        {
            Role = ConversationRole.User,
            Content =
            [
                new ContentBlock
                {
                    Text = "Amazon Bedrockを一文で説明しなさい。"
                }
            ]
        }
    ],
    InferenceConfig = new InferenceConfiguration
    {
        MaxTokens = 256,
        Temperature = 0.3f,
        TopP = 0.9f
    }
};

var response = await client.ConverseAsync(request); // Model呼び出し

var text = response.Output?.Message?.Content?
    .FirstOrDefault()?.Text; // Modelの返却値を取得

Console.WriteLine(text);

Console.WriteLine($"Input Tokens: {response.Usage?.InputTokens}");
Console.WriteLine($"Output Tokens: {response.Usage?.OutputTokens}");
Console.WriteLine($"Total Tokens: {response.Usage?.TotalTokens}");