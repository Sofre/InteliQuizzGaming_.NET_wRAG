namespace QuizGamePlatform.Models;

public class QuestionEmbedding
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    // float[] serialized as JSON — no pgvector needed, SQLite compatible
    public string EmbeddingJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
