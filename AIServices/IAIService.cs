namespace QuizGamePlatform.Services;

using QuizGamePlatform.Models;

public interface IAIService
{
    /// <summary>
    /// Generate embedding vector for a given text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Generate new questions for a SubArea using RAG with semantic retrieval (if embeddings provided)
    /// Falls back to simple context if embeddings are empty
    /// </summary>
    Task<GenerationResultDto> GenerateQuestionsAsync(SubArea subArea, List<Question> existingQuestions, List<(Question question, float[] embedding)> existingEmbeddings);
    
    /// <summary>
    /// Generate new questions WITHOUT RAG - uses simple context from recent questions
    /// </summary>
    Task<GenerationResultDto> GenerateQuestionsSimpleAsync(SubArea subArea, List<Question> existingQuestions);

    /// <summary>
    /// Calculate cosine similarity between two embedding vectors
    /// </summary>
    float CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
}

public class GenerationResultDto
{
    public string Description { get; set; } = string.Empty;
    public List<GeneratedQuestionDto> Questions { get; set; } = new();
}

public class GeneratedQuestionDto
{
    public string Text { get; set; } = string.Empty;
    public string Options { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public int Points { get; set; } = 10;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
