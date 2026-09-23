using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;

var document = """
PX-2847でE1032が発生した場合は、給紙部を確認してください。

PX-2847でE2040が発生した場合は、排紙部に紙詰まりがないか確認してください。

PX-2847の印刷速度は毎分80枚です。
""";

var chunks = document.Split(
    "\n\n",
    StringSplitOptions.RemoveEmptyEntries
);

using var client =
    new AmazonBedrockRuntimeClient(RegionEndpoint.APNortheast1);

var chunkEmbeddings = new List<float[]>();

for (var i = 0; i < chunks.Length; i++)
{
    var embedding = await CreateEmbeddingAsync(
        client,
        chunks[i]
    );

    chunkEmbeddings.Add(embedding);

    Console.WriteLine($"Chunk {i + 1}");
    Console.WriteLine(chunks[i]);
    Console.WriteLine($"Dimensions: {embedding.Length}");
    Console.WriteLine(
        $"First 5 values: {string.Join(", ", embedding.Take(5))}"
    );
    Console.WriteLine();
}

// Vector Search
var query =
    "PX-2847でE1032が出たときはどうすればいいですか？";

var queryEmbedding =
    await CreateEmbeddingAsync(client, query);

Console.WriteLine($"Query: {query}");
Console.WriteLine();

var results = chunks
    .Select((chunk, index) => new
    {
        Chunk = chunk,
        Score = CosineSimilarity(
            queryEmbedding,
            chunkEmbeddings[index])
    })
    .OrderByDescending(x => x.Score)
    .ToList();

foreach (var result in results)
{
    Console.WriteLine($"Score: {result.Score:F4}");
    Console.WriteLine(result.Chunk);
    Console.WriteLine();
}


// Top-kを指定し、上位k件の結果を取得する。
var k = 2;

var topKResults = results
    .Take(k)
    .ToList();

Console.WriteLine($"Top-{k} Results");
Console.WriteLine();

foreach (var result in topKResults)
{
    Console.WriteLine($"Score: {result.Score:F4}");
    Console.WriteLine(result.Chunk);
    Console.WriteLine();
}

// Retreieved Context
var context = string.Join(
    "\n\n",
    topKResults.Select((result, index) =>
        $"[Source {index + 1}]\n{result.Chunk}")
);

Console.WriteLine("Context");
Console.WriteLine("--------------------");
Console.WriteLine(context);

// Sys. Prompt + Context + User Queryを組み合わせて、RAGの質問応答を行う。
var systemPrompt = """
あなたは製品マニュアル回答アシスタントです。
必ず与えられたContextを根拠に回答してください。
Contextに答えがない場合は「情報がありません」と回答してください。
""";

var userMessage = $"""
以下のContextを参照して質問に回答してください。

Context:
{context}

Question:
{query}
""";

var converseRequest = new ConverseRequest
{
    ModelId = "amazon.nova-lite-v1:0",

    System = new List<SystemContentBlock>
    {
        new SystemContentBlock
        {
            Text = systemPrompt
        }
    },

    Messages = new List<Message>
    {
        new Message
        {
            Role = ConversationRole.User,
            Content = new List<ContentBlock>
            {
                new ContentBlock
                {
                    Text = userMessage
                }
            }
        }
    },

    InferenceConfig = new InferenceConfiguration
    {
        MaxTokens = 300,
        Temperature = 0.0f // 忠実に回答させる。創造性を出す場合は値を大きくする。
    }
};

var converseResponse =
    await client.ConverseAsync(converseRequest);

var answer =
    converseResponse.Output
        .Message
        .Content[0]
        .Text;

Console.WriteLine("Answer");
Console.WriteLine("--------------------");
Console.WriteLine(answer);

static async Task<float[]> CreateEmbeddingAsync(
    AmazonBedrockRuntimeClient client,
    string text)
{
    // Embeddingモデルのリクエストボディを作成
    // 学習のため、出力Vectorの次元をデフォ1024->256に指定する
    var requestBody = JsonSerializer.Serialize(new
    {
        inputText = text,
        dimensions = 256,
        normalize = true
    });

    var request = new InvokeModelRequest
    {
        ModelId = "amazon.titan-embed-text-v2:0",
        ContentType = "application/json",
        Accept = "application/json",
        Body = new MemoryStream(
            Encoding.UTF8.GetBytes(requestBody))
    };

    var response = await client.InvokeModelAsync(request);

    using var json =
        await JsonDocument.ParseAsync(response.Body);

    return json.RootElement
        .GetProperty("embedding")
        .EnumerateArray()
        .Select(x => x.GetSingle())
        .ToArray();
}

static double CosineSimilarity(
    float[] vectorA,
    float[] vectorB)
{
    double dotProduct = 0;
    double normA = 0;
    double normB = 0;

    for (var i = 0; i < vectorA.Length; i++)
    {
        dotProduct += vectorA[i] * vectorB[i];

        normA += vectorA[i] * vectorA[i];
        normB += vectorB[i] * vectorB[i];
    }

    return dotProduct /
        (Math.Sqrt(normA) * Math.Sqrt(normB));
}