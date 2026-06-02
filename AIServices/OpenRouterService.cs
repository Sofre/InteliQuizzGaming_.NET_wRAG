namespace QuizGamePlatform.Services;

using QuizGamePlatform.Models;
using System.Text.Json;
using System.Net.Http.Headers;

public class OpenAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIService> _logger;
    private readonly IConfiguration _configuration;
    private readonly float _similarityThreshold;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _embeddingModel;
    private readonly string _chatModel;

    public OpenAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _similarityThreshold = configuration.GetValue<float>("AI:SimilarityThreshold", 0.85f);
        _apiKey = configuration["OpenRouter:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("OpenRouter API key not configured. Set OpenRouter__ApiKey in environment variables or user secrets.");
        }
        _baseUrl = NormalizeBaseUrl(configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1");
        _embeddingModel = configuration["OpenRouter:EmbeddingModel"] ?? "openai/text-embedding-3-small";
        _chatModel = configuration["OpenRouter:ChatModel"] ?? "openai/gpt-4o-mini";
        
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        // Ensure trailing slash so relative URLs (e.g., "embeddings") resolve to /api/v1/embeddings,
        // not /api/embeddings when base URL ends with /api/v1.
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
    }

    private async Task<T> PostJsonAndReadAsync<T>(string relativePath, object requestBody)
    {
        // Use relative paths (no leading slash) so BaseAddress path /api/v1 is preserved.
        var response = await _httpClient.PostAsJsonAsync(relativePath, requestBody);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var preview = content.Length > 400 ? content[..400] : content;
            throw new HttpRequestException($"OpenRouter request failed ({(int)response.StatusCode} {response.ReasonPhrase}). Body: {preview}");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                throw new JsonException("Response deserialized to null.");
            }

            return parsed;
        }
        catch (Exception ex)
        {
            var preview = content.Length > 400 ? content[..400] : content;
            throw new InvalidOperationException($"OpenRouter returned non-JSON or unexpected payload. Body preview: {preview}", ex);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var requestBody = new
            {
                model = _embeddingModel,
                input = text
            };

            var result = await PostJsonAndReadAsync<EmbeddingResponse>("embeddings", requestBody);
            if (result?.Data == null || result.Data.Count == 0)
                throw new Exception("No embedding data returned");

            return result.Data[0].Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text: {Text}", text);
            throw;
        }
    }

    public async Task<GenerationResultDto> GenerateQuestionsAsync(SubArea subArea, List<Question> existingQuestions, List<(Question question, float[] embedding)> existingEmbeddings)
    {
        try
        {
            // RAG: Create semantic query from SubArea name and description
            var semanticQuery = $"{subArea.Name} {subArea.Area?.Name ?? ""}";
            var queryEmbedding = await GenerateEmbeddingAsync(semanticQuery);

            // RAG: Semantic retrieval - find most relevant existing questions
            var retrievedQuestions = new List<Question>();
            if (existingEmbeddings.Any())
            {
                var rankedByRelevance = existingEmbeddings
                    .Select(e => new
                    {
                        Question = e.question,
                        Similarity = CalculateCosineSimilarity(queryEmbedding, e.embedding)
                    })
                    .OrderByDescending(x => x.Similarity)
                    .Take(5) // Top 5 most relevant questions
                    .Select(x => x.Question)
                    .ToList();

                retrievedQuestions = rankedByRelevance;
            }
            else
            {
                // Fallback: use recent questions if no embeddings
                retrievedQuestions = existingQuestions.Take(5).ToList();
            }

            // Build RAG-augmented prompt with ONLY retrieved relevant questions
            var contextPrompt = BuildRAGPrompt(subArea, retrievedQuestions);
            
            var requestBody = new
            {
                model = _chatModel,
                messages = new[]
                {
                    new { role = "user", content = contextPrompt }
                },
                temperature = 0.7
            };

            var result = await PostJsonAndReadAsync<ChatCompletionResponse>("chat/completions", requestBody);
            var responseText = result?.Choices?[0]?.Message?.Content ?? "";
            
            var generationResult = ParseQuestionResponse(responseText);
            
            // Generate embeddings for each question
            foreach (var question in generationResult.Questions)
            {
                question.Embedding = await GenerateEmbeddingAsync(question.Text);
            }

            return generationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating questions for SubArea: {SubAreaName}", subArea.Name);
            throw;
        }
    }

    public async Task<GenerationResultDto> GenerateQuestionsSimpleAsync(SubArea subArea, List<Question> existingQuestions)
    {
        try
        {
            // Simple mode: Just pass recent questions as context (no semantic retrieval)
            var contextPrompt = BuildSimplePrompt(subArea, existingQuestions.Take(10).ToList());
            
            var requestBody = new
            {
                model = _chatModel,
                messages = new[]
                {
                    new { role = "user", content = contextPrompt }
                },
                temperature = 0.7
            };

            var result = await PostJsonAndReadAsync<ChatCompletionResponse>("chat/completions", requestBody);
            var responseText = result?.Choices?[0]?.Message?.Content ?? "";
            
            var generationResult = ParseQuestionResponse(responseText);
            
            // Generate embeddings for each question
            foreach (var question in generationResult.Questions)
            {
                question.Embedding = await GenerateEmbeddingAsync(question.Text);
            }

            return generationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating questions (simple mode) for SubArea: {SubAreaName}", subArea.Name);
            throw;
        }
    }

    public float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private string BuildRAGPrompt(SubArea subArea, List<Question> retrievedQuestions)
    {
        var prompt = $@"You are an expert educational content creator. Generate 5 new quiz questions for the following SubArea:

**SubArea:** {subArea.Name}
**Area:** {subArea.Area?.Name ?? "Unknown"}

";

        if (retrievedQuestions.Any())
        {
            prompt += "**Most relevant existing questions (semantically retrieved via RAG, DO NOT duplicate):**\n\n";
            foreach (var q in retrievedQuestions)
            {
                prompt += $"- {q.Text}\n";
            }
            prompt += "\n";
        }

        prompt += @"
**Requirements:**
1. Generate exactly 5 NEW questions that are different from the existing ones
2. Questions should be clear, educational, and relevant to the SubArea
3. Each question should have 4 multiple-choice options
4. Include the correct answer
5. Assign 10 points per question

**Output Format (JSON):**
```json
{
  ""description"": ""One sentence describing what this question set covers"",
  ""questions"": [
    {
      ""text"": ""Question text here?"",
      ""options"": ""Option A, Option B, Option C, Option D"",
      ""correctAnswer"": ""Option A"",
      ""points"": 10
    }
  ]
}
```

Generate the questions now in valid JSON format:";

        return prompt;
    }

    private string BuildSimplePrompt(SubArea subArea, List<Question> existingQuestions)
    {
        var prompt = $@"You are an expert educational content creator. Generate 5 new quiz questions for the following SubArea:

**SubArea:** {subArea.Name}
**Area:** {subArea.Area?.Name ?? "Unknown"}

";

        if (existingQuestions.Any())
        {
            prompt += "**Existing questions for context (DO NOT duplicate):**\n\n";
            foreach (var q in existingQuestions)
            {
                prompt += $"- {q.Text}\n";
            }
            prompt += "\n";
        }

        prompt += @"
**Requirements:**
1. Generate exactly 5 NEW questions that are different from the existing ones
2. Questions should be clear, educational, and relevant to the SubArea
3. Each question should have 4 multiple-choice options
4. Include the correct answer
5. Assign 10 points per question

**Output Format (JSON):**
```json
{
  ""description"": ""One sentence describing what this question set covers"",
  ""questions"": [
    {
      ""text"": ""Question text here?"",
      ""options"": ""Option A, Option B, Option C, Option D"",
      ""correctAnswer"": ""Option A"",
      ""points"": 10
    }
  ]
}
```

Generate the questions now in valid JSON format:";

        return prompt;
    }

    private GenerationResultDto ParseQuestionResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                _logger.LogWarning("No JSON object found in response");
                return new GenerationResultDto();
            }

            var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var wrapper = JsonSerializer.Deserialize<GenerationResponseJsonDto>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (wrapper == null)
                return new GenerationResultDto();

            return new GenerationResultDto
            {
                Description = wrapper.Description ?? string.Empty,
                Questions = (wrapper.Questions ?? new List<QuestionJsonDto>()).Select(q => new GeneratedQuestionDto
                {
                    Text = q.Text,
                    Options = q.Options,
                    CorrectAnswer = q.CorrectAnswer,
                    Points = q.Points
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing question response: {Response}", response);
            return new GenerationResultDto();
        }
    }

    private class GenerationResponseJsonDto
    {
        public string? Description { get; set; }
        public List<QuestionJsonDto>? Questions { get; set; }
    }

    private class QuestionJsonDto
    {
        public string Text { get; set; } = string.Empty;
        public string Options { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public int Points { get; set; } = 10;
    }

    private class EmbeddingResponse
    {
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private class EmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    private class ChatCompletionResponse
    {
        public List<Choice> Choices { get; set; } = new();
    }

    private class Choice
    {
        public MessageContent Message { get; set; } = new();
    }

    private class MessageContent
    {
        public string Content { get; set; } = string.Empty;
    }
}
