using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;
using QuizGamePlatform.Services;
using System.Text.Json;

namespace QuizGamePlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAIService _aiService;

    public AdminController(AppDbContext context, IAIService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() => Ok(await _context.Users.Select(u => new { u.Id, u.Email, u.Role }).ToListAsync());

    [HttpPost("invite/{userId}")]
    public async Task<IActionResult> SendInvitation(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound("User not found.");
        
        Console.WriteLine($"\n[EMAIL DISPATCH SYSTEM]\nTo: {user.Email}\nSubject: You have been invited to the Gamified Quiz Platform!\nBody: Your administrator has allocated new Questions for you. Log in immediately to play and track your score: http://localhost:5292/\n");
        return Ok(new { message = "Email invitation simulated and printed to server console." });
    }

    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings()
    {
        var answers = await _context.Answers
            .Include(a => a.Question)
                .ThenInclude(q => q.SubArea)
            .Include(a => a.User)
                .ThenInclude(u => u.Sessions)
            .ToListAsync();

        var rankedList = answers
            .Where(a => a.Question != null && a.Question.SubArea != null)
            .GroupBy(a => a.Question.SubArea.Name)
            .Select(g => new {
                SubCriteriaName = g.Key,
                TotalScore = g.Sum(a => a.PointsEarned),
                SessionBreakdown = g
                    .SelectMany(a => a.User.Sessions.Select(s => new { SessionName = s.Name, Score = a.PointsEarned }))
                    .GroupBy(x => x.SessionName)
                    .Select(sg => new { SessionName = sg.Key, TotalSessionScore = sg.Sum(x => x.Score) })
            })
            .OrderByDescending(x => x.TotalScore)
            .ToList();

        return Ok(rankedList);
    }

    public class AreaCreateDto { public string Name { get; set; } = ""; }

    [HttpPost("area")]
    public async Task<IActionResult> CreateArea([FromBody] AreaCreateDto req)
    {
        var area = new Area { Name = req.Name };
        _context.Areas.Add(area);
        await _context.SaveChangesAsync();
        return Ok(new { area.Id, area.Name });
    }
    
    [HttpGet("areas")]
    public async Task<IActionResult> GetAreas() => Ok(await _context.Areas
        .Include(a => a.SubAreas)
        .Select(a => new { a.Id, a.Name, a.Description, SubAreas = a.SubAreas.Select(s => new { s.Id, s.Name }) })
        .ToListAsync());

    public class SubAreaCreateDto { public string Name { get; set; } = ""; public int AreaId { get; set; } }

    [HttpPost("subarea")]
    public async Task<IActionResult> CreateSubArea([FromBody] SubAreaCreateDto req)
    {
        var subArea = new SubArea { Name = req.Name, AreaId = req.AreaId };
        _context.SubAreas.Add(subArea);
        await _context.SaveChangesAsync();
        return Ok(new { subArea.Id, subArea.Name, subArea.AreaId });
    }

    [HttpPut("area/{id}")]
    public async Task<IActionResult> UpdateArea(int id, [FromBody] AreaCreateDto req)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null) return NotFound("Area not found");
        
        area.Name = req.Name;
        await _context.SaveChangesAsync();
        return Ok(new { area.Id, area.Name });
    }

    [HttpDelete("area/{id}")]
    public async Task<IActionResult> DeleteArea(int id)
    {
        var area = await _context.Areas.Include(a => a.SubAreas).FirstOrDefaultAsync(a => a.Id == id);
        if (area == null) return NotFound("Area not found");
        
        if (area.SubAreas.Any())
        {
            return BadRequest(new { error = "Cannot delete area with existing subareas. Delete subareas first." });
        }
        
        _context.Areas.Remove(area);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Area deleted successfully" });
    }

    [HttpPut("subarea/{id}")]
    public async Task<IActionResult> UpdateSubArea(int id, [FromBody] SubAreaCreateDto req)
    {
        var subArea = await _context.SubAreas.FindAsync(id);
        if (subArea == null) return NotFound("SubArea not found");
        
        subArea.Name = req.Name;
        subArea.AreaId = req.AreaId;
        await _context.SaveChangesAsync();
        return Ok(new { subArea.Id, subArea.Name, subArea.AreaId });
    }

    [HttpDelete("subarea/{id}")]
    public async Task<IActionResult> DeleteSubArea(int id)
    {
        var subArea = await _context.SubAreas.FindAsync(id);
        if (subArea == null) return NotFound("SubArea not found");
        
        // Check if there are questions using this subarea
        var hasQuestions = await _context.Questions.AnyAsync(q => q.SubAreaId == id);
        if (hasQuestions)
        {
            return BadRequest(new { error = "Cannot delete subarea with existing questions. Delete or reassign questions first." });
        }
        
        _context.SubAreas.Remove(subArea);
        await _context.SaveChangesAsync();
        return Ok(new { message = "SubArea deleted successfully" });
    }

    public class QuestCreateDto { public string Title { get; set; } = ""; public string ThemeColor { get; set; } = ""; public string BackgroundColor { get; set; } = ""; }

    [HttpPost("questionnaire")]
    public async Task<IActionResult> CreateQuestionnaire([FromBody] QuestCreateDto req)
    {
        var q = new Questionnaire { Title = req.Title, ThemeColor = req.ThemeColor, BackgroundColor = req.BackgroundColor };
        _context.Questionnaires.Add(q);
        await _context.SaveChangesAsync();
        return Ok(new { q.Id, q.Title });
    }

    [HttpGet("questionnaires")]
    public async Task<IActionResult> GetQuestionnaires() => Ok(await _context.Questionnaires
        .Include(q => q.Questions)
        .Select(q => new { 
            q.Id, q.Title, q.ThemeColor, q.BackgroundColor, 
            Questions = q.Questions.Select(x => new {
                x.Id,
                x.Text,
                x.Options,
                x.CorrectAnswer,
                x.Points,
                x.SubAreaId,
                x.IsAIGenerated,
                x.IsApproved
            }) 
        })
        .ToListAsync());

    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] Session s)
    {
        _context.Sessions.Add(s);
        await _context.SaveChangesAsync();
        return Ok(s);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions() => Ok(await _context.Sessions
        .Include(s => s.AssignedUsers)
        .Include(s => s.AssignedQuestionnaires)
        .Select(s => new {
            s.Id, s.Name,
            AssignedUsers = s.AssignedUsers.Select(u => new { u.Id, u.Email }),
            AssignedQuestionnaires = s.AssignedQuestionnaires.Select(q => new { q.Id, q.Title })
        })
        .ToListAsync());

    [HttpPost("session/{sessionId}/assign-user/{userId}")]
    public async Task<IActionResult> AssignUser(int sessionId, int userId)
    {
        var session = await _context.Sessions.Include(s => s.AssignedUsers).FirstOrDefaultAsync(s => s.Id == sessionId);
        var user = await _context.Users.FindAsync(userId);
        if (session == null || user == null) return NotFound();
        if (!session.AssignedUsers.Contains(user)) { session.AssignedUsers.Add(user); await _context.SaveChangesAsync(); }
        return Ok();
    }
    
    [HttpPost("session/{sessionId}/assign-questionnaire/{qId}")]
    public async Task<IActionResult> AssignQuestionnaire(int sessionId, int qId)
    {
        var session = await _context.Sessions.Include(s => s.AssignedQuestionnaires).FirstOrDefaultAsync(s => s.Id == sessionId);
        var q = await _context.Questionnaires.FindAsync(qId);
        if (session == null || q == null) return NotFound();
        if (!session.AssignedQuestionnaires.Contains(q)) { session.AssignedQuestionnaires.Add(q); await _context.SaveChangesAsync(); }
        return Ok();
    }

    public class QuestionCreateDto { public int QuestionnaireId { get; set; } public int SubAreaId { get; set; } public string Text { get; set; } = ""; public string Options { get; set; } = ""; public string CorrectAnswer { get; set; } = ""; public int Points { get; set; } }

    [HttpPost("question")]
    public async Task<IActionResult> CreateQuestion([FromBody] QuestionCreateDto req)
    {
        var question = new Question {
            QuestionnaireId = req.QuestionnaireId,
            SubAreaId = req.SubAreaId,
            Text = req.Text,
            Options = req.Options,
            CorrectAnswer = req.CorrectAnswer,
            Points = req.Points
        };
        _context.Questions.Add(question);
        await _context.SaveChangesAsync();
        return Ok(question);
    }

    [HttpPut("questionnaire/{id}")]
    public async Task<IActionResult> UpdateQuestionnaire(int id, [FromBody] QuestCreateDto req)
    {
        var existing = await _context.Questionnaires.FindAsync(id);
        if (existing == null) return NotFound();
        
        existing.Title = req.Title;
        existing.ThemeColor = req.ThemeColor;
        existing.BackgroundColor = req.BackgroundColor;
        
        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("questionnaire/{id}")]
    public async Task<IActionResult> DeleteQuestionnaire(int id)
    {
        var questionnaire = await _context.Questionnaires.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == id);
        if (questionnaire == null) return NotFound("Questionnaire not found");
        
        if (questionnaire.Questions.Any())
        {
            return BadRequest(new { error = "Cannot delete questionnaire with existing questions. Delete questions first." });
        }
        
        _context.Questionnaires.Remove(questionnaire);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Questionnaire deleted successfully" });
    }

    [HttpPut("question/{id}")]
    public async Task<IActionResult> UpdateQuestion(int id, [FromBody] QuestionCreateDto req)
    {
        var existing = await _context.Questions.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Text = req.Text;
        existing.Options = req.Options;
        existing.CorrectAnswer = req.CorrectAnswer;
        existing.Points = req.Points;
        
        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("question/{id}")]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await _context.Questions.FindAsync(id);
        if (question == null) return NotFound("Question not found");
        
        // Also delete the embedding if it exists
        var embedding = await _context.QuestionEmbeddings.FirstOrDefaultAsync(e => e.QuestionId == id);
        if (embedding != null)
        {
            _context.QuestionEmbeddings.Remove(embedding);
        }
        
        _context.Questions.Remove(question);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Question deleted successfully" });
    }

    // ========== AI-POWERED FEATURES ==========

    [HttpPost("index-questions/{subAreaId?}")]
    public async Task<IActionResult> IndexQuestions(int? subAreaId = null)
    {
        var query = _context.Questions.Include(q => q.SubArea).AsQueryable();
        
        if (subAreaId.HasValue)
        {
            query = query.Where(q => q.SubAreaId == subAreaId.Value);
        }

        var questions = await query.Where(q => q.IsApproved).ToListAsync();
        int indexed = 0;

        foreach (var question in questions)
        {
            // Check if embedding already exists
            var existingEmbedding = await _context.QuestionEmbeddings
                .FirstOrDefaultAsync(e => e.QuestionId == question.Id);
            
            if (existingEmbedding == null)
            {
                try
                {
                    var embedding = await _aiService.GenerateEmbeddingAsync(question.Text);
                    var embeddingJson = JsonSerializer.Serialize(embedding);
                    
                    _context.QuestionEmbeddings.Add(new QuestionEmbedding
                    {
                        QuestionId = question.Id,
                        EmbeddingJson = embeddingJson,
                        CreatedAt = DateTime.UtcNow
                    });
                    
                    indexed++;
                }
                catch (Exception ex)
                {
                    // Log error but continue with other questions
                    Console.WriteLine($"Failed to index question {question.Id}: {ex.Message}");
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"Indexed {indexed} new questions", total = questions.Count, newlyIndexed = indexed });
    }

    [HttpPost("generate-questions/{subAreaId}")]
    public async Task<IActionResult> GenerateQuestions(int subAreaId, [FromQuery] bool useRAG = true)
    {
        var subArea = await _context.SubAreas.FindAsync(subAreaId);
        if (subArea == null) return NotFound("SubArea not found");
        
        // Get existing questions for this SubArea
        var existingQuestions = await _context.Questions
            .Where(q => q.SubAreaId == subAreaId && q.IsApproved)
            .ToListAsync();

        List<GeneratedQuestionDto> generatedQuestions;
        string generatedDescription = string.Empty;

        if (useRAG)
        {
            // RAG MODE: Semantic retrieval
            var existingEmbeddings = await _context.QuestionEmbeddings
                .Include(e => e.Question)
                .Where(e => e.Question!.SubAreaId == subAreaId && e.Question.IsApproved)
                .ToListAsync();

            if (!existingEmbeddings.Any())
            {
                return BadRequest(new
                {
                    error = "No embeddings found for RAG mode. Please index questions first using POST /api/admin/index-questions/{subAreaId}, or use simple mode by setting useRAG=false"
                });
            }

            // Prepare embeddings for RAG
            var embeddingsWithVectors = existingEmbeddings
                .Select(e => (
                    question: e.Question!,
                    embedding: JsonSerializer.Deserialize<float[]>(e.EmbeddingJson) ?? Array.Empty<float>()
                ))
                .ToList();

            try
            {
                // RAG: Generate questions with semantic retrieval
                var ragResult = await _aiService.GenerateQuestionsAsync(subArea, existingQuestions, embeddingsWithVectors);
                generatedQuestions = ragResult.Questions;
                generatedDescription = ragResult.Description;
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to generate questions with RAG", details = ex.Message });
            }
        }
        else
        {
            // SIMPLE MODE: No semantic retrieval
            try
            {
                var simpleResult = await _aiService.GenerateQuestionsSimpleAsync(subArea, existingQuestions);
                generatedQuestions = simpleResult.Questions;
                generatedDescription = simpleResult.Description;
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to generate questions in simple mode", details = ex.Message });
            }
        }

        // Get all existing embeddings for duplicate detection
        var allExistingEmbeddings = await _context.QuestionEmbeddings
            .Include(e => e.Question)
            .Where(e => e.Question!.SubAreaId == subAreaId && e.Question.IsApproved)
            .ToListAsync();

        var embeddingsForDuplicateCheck = allExistingEmbeddings
            .Select(e => (
                question: e.Question!,
                embedding: JsonSerializer.Deserialize<float[]>(e.EmbeddingJson) ?? Array.Empty<float>()
            ))
            .ToList();

        var validQuestions = new List<GeneratedQuestionDto>();

        foreach (var genQ in generatedQuestions)
        {
            // Check similarity against existing questions (duplicate detection)
            bool isDuplicate = false;

            foreach (var (question, embedding) in embeddingsForDuplicateCheck)
            {
                var similarity = _aiService.CalculateCosineSimilarity(genQ.Embedding, embedding);

                if (similarity > 0.85f) // Threshold from config
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                validQuestions.Add(genQ);
            }
        }

        // Find or create a per-SubArea staging questionnaire
        var questTitle = $"{subArea.Name} — AI Generated";
        var aiQuestionnaire = await _context.Questionnaires
            .FirstOrDefaultAsync(q => q.Title == questTitle);
        
        if (aiQuestionnaire == null)
        {
            aiQuestionnaire = new Questionnaire 
            { 
                Title = questTitle,
                Description = generatedDescription,
                ThemeColor = "#9333ea",
                BackgroundColor = "#faf5ff"
            };
            _context.Questionnaires.Add(aiQuestionnaire);
            await _context.SaveChangesAsync();
        }

        // Save generated questions as pending
        var savedQuestions = new List<object>();
        foreach (var genQ in validQuestions)
        {
            var question = new Question
            {
                QuestionnaireId = aiQuestionnaire.Id,
                SubAreaId = subAreaId,
                Text = genQ.Text,
                Options = genQ.Options,
                CorrectAnswer = genQ.CorrectAnswer,
                Points = genQ.Points,
                IsAIGenerated = true,
                IsApproved = false
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync(); // Save to get ID

            // Save embedding
            var embeddingJson = JsonSerializer.Serialize(genQ.Embedding);
            _context.QuestionEmbeddings.Add(new QuestionEmbedding
            {
                QuestionId = question.Id,
                EmbeddingJson = embeddingJson,
                CreatedAt = DateTime.UtcNow
            });

            savedQuestions.Add(new 
            { 
                question.Id, 
                question.Text, 
                question.Options, 
                question.CorrectAnswer,
                question.Points 
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mode = useRAG ? "RAG (Semantic Retrieval)" : "Simple (Recent Context)",
            message = useRAG 
                ? $"Generated {validQuestions.Count} new questions using RAG semantic retrieval (filtered {generatedQuestions.Count - validQuestions.Count} duplicates)"
                : $"Generated {validQuestions.Count} new questions using simple context (filtered {generatedQuestions.Count - validQuestions.Count} duplicates)",
            questions = savedQuestions,
            subArea = new { subArea.Id, subArea.Name }
        });
    }

    [HttpGet("pending-questions/{subAreaId?}")]
    public async Task<IActionResult> GetPendingQuestions(int? subAreaId = null)
    {
        var query = _context.Questions
            .Include(q => q.SubArea)
                .ThenInclude(s => s!.Area)
            .Where(q => q.IsAIGenerated && !q.IsApproved);

        if (subAreaId.HasValue)
        {
            query = query.Where(q => q.SubAreaId == subAreaId.Value);
        }

        var pendingQuestions = await query
            .Select(q => new
            {
                q.Id,
                q.Text,
                q.Options,
                q.CorrectAnswer,
                q.Points,
                SubArea = new { q.SubArea!.Id, q.SubArea.Name },
                Area = new { q.SubArea.Area!.Id, q.SubArea.Area.Name }
            })
            .ToListAsync();

        return Ok(pendingQuestions);
    }

    [HttpPost("approve-question/{questionId}")]
    public async Task<IActionResult> ApproveQuestion(int questionId)
    {
        var question = await _context.Questions.FindAsync(questionId);
        
        if (question == null)
            return NotFound("Question not found");

        if (!question.IsAIGenerated)
            return BadRequest("This question is not AI-generated");

        question.IsApproved = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Question approved", question });
    }

    [HttpDelete("reject-question/{questionId}")]
    public async Task<IActionResult> RejectQuestion(int questionId)
    {
        var question = await _context.Questions.FindAsync(questionId);
        
        if (question == null)
            return NotFound("Question not found");

        if (!question.IsAIGenerated)
            return BadRequest("This question is not AI-generated");

        // Delete associated embedding
        var embedding = await _context.QuestionEmbeddings
            .FirstOrDefaultAsync(e => e.QuestionId == questionId);
        
        if (embedding != null)
        {
            _context.QuestionEmbeddings.Remove(embedding);
        }

        _context.Questions.Remove(question);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Question rejected and deleted" });
    }

    [HttpDelete("ai-questions")]
    public async Task<IActionResult> DeleteAllAIQuestions()
    {
        var aiQuestions = await _context.Questions
            .Where(q => q.IsAIGenerated)
            .ToListAsync();

        if (!aiQuestions.Any())
            return Ok(new { message = "No AI-generated questions found", deleted = 0 });

        var questionIds = aiQuestions.Select(q => q.Id).ToList();

        var embeddings = await _context.QuestionEmbeddings
            .Where(e => questionIds.Contains(e.QuestionId))
            .ToListAsync();

        _context.QuestionEmbeddings.RemoveRange(embeddings);
        _context.Questions.RemoveRange(aiQuestions);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Deleted {aiQuestions.Count} AI-generated questions", deleted = aiQuestions.Count });
    }
}
