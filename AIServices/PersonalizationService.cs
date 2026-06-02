namespace QuizGamePlatform.Services;

using Microsoft.EntityFrameworkCore;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;
using System.Text.Json;

public interface IPersonalizationService
{
    Task<List<WeakAreaDto>> GetWeakSubAreasAsync(int userId);
    Task<List<Question>> GetPersonalizedQuestionsAsync(int userId, int count = 15);
}

public class PersonalizationService : IPersonalizationService
{
    private readonly AppDbContext _context;
    private readonly IAIService _aiService;
    private readonly IConfiguration _configuration;
    private readonly float _weakAreaThreshold;

    public PersonalizationService(
        AppDbContext context,
        IAIService aiService,
        IConfiguration configuration)
    {
        _context = context;
        _aiService = aiService;
        _configuration = configuration;
        _weakAreaThreshold = configuration.GetValue<float>("AI:WeakAreaThreshold", 0.5f);
    }

    public async Task<List<WeakAreaDto>> GetWeakSubAreasAsync(int userId)
    {
        var answers = await _context.Answers
            .Include(a => a.Question)
                .ThenInclude(q => q!.SubArea)
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var weakAreas = answers
            .GroupBy(a => a.Question!.SubAreaId)
            .Select(g =>
            {
                var total = g.Count();
                var correct = g.Count(a => a.IsCorrect);
                var skipped = g.Count(a => a.IsSkipped);
                var successRate = total > 0 ? (double)correct / total : 0;
                var skipRate = total > 0 ? (double)skipped / total : 0;

                // Weighted score: penalize low success rate and high skip rate
                var score = successRate * (1 - skipRate * 0.5);

                var subArea = g.First().Question!.SubArea!;
                
                return new WeakAreaDto
                {
                    SubAreaId = subArea.Id,
                    SubAreaName = subArea.Name,
                    SuccessRate = successRate,
                    SkipRate = skipRate,
                    Score = score,
                    TotalAttempted = total,
                    CorrectCount = correct,
                    SkippedCount = skipped
                };
            })
            .Where(w => w.Score < _weakAreaThreshold) // Only weak areas
            .OrderBy(w => w.Score) // Weakest first
            .Take(_configuration.GetValue<int>("AI:WeakAreaCount", 5))
            .ToList();

        return weakAreas;
    }

    public async Task<List<Question>> GetPersonalizedQuestionsAsync(int userId, int count = 15)
    {
        // Get user's weak areas
        var weakAreas = await GetWeakSubAreasAsync(userId);
        
        if (!weakAreas.Any())
        {
            // No weak areas found, return random approved questions
            return await _context.Questions
                .Where(q => q.IsApproved)
                .OrderBy(q => Guid.NewGuid())
                .Take(count)
                .ToListAsync();
        }

        // Get user's incorrect/skipped answers to create a "weakness pattern" embedding
        var weakQuestionIds = await _context.Answers
            .Where(a => a.UserId == userId && (!a.IsCorrect || a.IsSkipped))
            .Select(a => a.QuestionId)
            .ToListAsync();

        var weakQuestionTexts = await _context.Questions
            .Where(q => weakQuestionIds.Contains(q.Id))
            .Select(q => q.Text)
            .ToListAsync();

        // RAG: Create semantic query from user's weakness pattern
        var weaknessQuery = string.Join(" | ", weakQuestionTexts.Take(5));
        var queryEmbedding = await _aiService.GenerateEmbeddingAsync(weaknessQuery);

        // Get all approved questions from weak SubAreas with embeddings
        var weakSubAreaIds = weakAreas.Select(w => w.SubAreaId).ToList();
        var candidateQuestions = await _context.Questions
            .Include(q => q.SubArea)
            .Where(q => q.IsApproved && weakSubAreaIds.Contains(q.SubAreaId))
            .ToListAsync();

        // Get their embeddings
        var questionEmbeddings = await _context.QuestionEmbeddings
            .Where(e => candidateQuestions.Select(q => q.Id).Contains(e.QuestionId))
            .ToListAsync();

        // RAG: Semantic search - find most relevant questions using cosine similarity
        var rankedQuestions = new List<(Question question, float similarity)>();

        foreach (var question in candidateQuestions)
        {
            var embedding = questionEmbeddings.FirstOrDefault(e => e.QuestionId == question.Id);
            if (embedding == null) continue;

            var embVector = JsonSerializer.Deserialize<float[]>(embedding.EmbeddingJson) ?? Array.Empty<float>();
            var similarity = _aiService.CalculateCosineSimilarity(queryEmbedding, embVector);

            // Exclude questions user already answered correctly
            var alreadyCorrect = await _context.Answers
                .AnyAsync(a => a.UserId == userId && a.QuestionId == question.Id && a.IsCorrect);

            if (!alreadyCorrect)
            {
                rankedQuestions.Add((question, similarity));
            }
        }

        // Return top-ranked questions by semantic similarity
        var personalizedQuestions = rankedQuestions
            .OrderByDescending(x => x.similarity)
            .Take(count)
            .Select(x => x.question)
            .ToList();

        return personalizedQuestions;
    }
}

public class WeakAreaDto
{
    public int SubAreaId { get; set; }
    public string SubAreaName { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public double SkipRate { get; set; }
    public double Score { get; set; }
    public int TotalAttempted { get; set; }
    public int CorrectCount { get; set; }
    public int SkippedCount { get; set; }
}
