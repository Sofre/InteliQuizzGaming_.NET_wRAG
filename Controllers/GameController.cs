using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;
using QuizGamePlatform.Services;
using System.Security.Claims;

namespace QuizGamePlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Student)]
public class GameController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPersonalizationService _personalizationService;

    public GameController(AppDbContext context, IPersonalizationService personalizationService)
    {
        _context = context;
        _personalizationService = personalizationService;
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("my-sessions")]
    public async Task<IActionResult> GetMySessions()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users
            .Include(u => u.Sessions)
            .ThenInclude(s => s.AssignedQuestionnaires)
            .FirstOrDefaultAsync(u => u.Id == userId);
            
        return Ok(user?.Sessions.Select(s => new {
            s.Id, s.Name,
            Questionnaires = s.AssignedQuestionnaires.Select(q => new { q.Id, q.Title, q.ThemeColor, q.BackgroundColor })
        }));
    }

    [HttpGet("questionnaire/{qId}")]
    public async Task<IActionResult> GetQuestionnaire(int qId)
    {
        var q = await _context.Questionnaires
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == qId);
        
        if (q == null) return NotFound();
        
        return Ok(new {
            q.Id, q.Title, q.ThemeColor, q.BackgroundColor,
            Questions = q.Questions.Select(x => new { x.Id, x.Text, x.Options, x.Points, x.SubAreaId }) // Do NOT send CorrectAnswer
        });
    }

    public class AnswerSubmission
    {
        public int QuestionId { get; set; }
        public string Choice { get; set; } = string.Empty;
        public bool IsSkipped { get; set; }
        public int TimeTakenSeconds { get; set; }
        public int? OverrideSubAreaId { get; set; }
    }

    [HttpPost("submit-answer")]
    public async Task<IActionResult> SubmitAnswer([FromBody] AnswerSubmission req)
    {
        var userId = GetCurrentUserId();
        var question = await _context.Questions.FindAsync(req.QuestionId);
        if (question == null) return NotFound();

        bool isCorrect = false;
        int points = 0;
        
        if (!req.IsSkipped && int.TryParse(req.Choice, out int rating) && rating >= 1 && rating <= 5) 
        {
            isCorrect = true;
            points = rating;
        } 
        else 
        {
            isCorrect = !req.IsSkipped && req.Choice == question.CorrectAnswer;
            points = isCorrect ? question.Points : 0;
        }

        var answer = new Answer
        {
            UserId = userId,
            QuestionId = req.QuestionId,
            Choice = req.Choice,
            IsSkipped = req.IsSkipped,
            IsCorrect = isCorrect,
            PointsEarned = points,
            TimeTakenSeconds = req.TimeTakenSeconds,
            UserAssignedSubAreaId = req.OverrideSubAreaId
        };

        _context.Answers.Add(answer);
        await _context.SaveChangesAsync();

        return Ok(new { isCorrect, pointsEarned = points, correctAnswer = req.IsSkipped ? question.CorrectAnswer : null });
    }

    // ========== RAG-POWERED PERSONALIZATION FEATURES ==========

    [HttpGet("my-weak-areas")]
    public async Task<IActionResult> GetMyWeakAreas()
    {
        var userId = GetCurrentUserId();
        var weakAreas = await _personalizationService.GetWeakSubAreasAsync(userId);
        
        return Ok(new
        {
            weakAreas = weakAreas,
            message = weakAreas.Any() 
                ? $"Found {weakAreas.Count} weak areas that need practice" 
                : "Great! No weak areas detected. You're performing well!"
        });
    }

    [HttpPost("personal-practice-quiz")]
    public async Task<IActionResult> GeneratePersonalPracticeQuiz([FromBody] PersonalQuizRequest? request = null)
    {
        var userId = GetCurrentUserId();
        var count = request?.QuestionCount ?? 15;

        // RAG: Get personalized questions using semantic search
        var personalizedQuestions = await _personalizationService.GetPersonalizedQuestionsAsync(userId, count);

        if (!personalizedQuestions.Any())
        {
            return Ok(new
            {
                message = "No personalized questions available. Try answering more questions first!",
                questions = new List<object>()
            });
        }

        // Create or update a "Personal Practice" questionnaire for this user
        var practiceQuestionnaire = await _context.Questionnaires
            .FirstOrDefaultAsync(q => q.Title == $"Personal Practice - User {userId}");

        if (practiceQuestionnaire == null)
        {
            practiceQuestionnaire = new Questionnaire
            {
                Title = $"Personal Practice - User {userId}",
                Description = "AI-generated personalized practice based on your weak areas",
                ThemeColor = "#f59e0b",
                BackgroundColor = "#fffbeb"
            };
            _context.Questionnaires.Add(practiceQuestionnaire);
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            questionnaireId = practiceQuestionnaire.Id,
            questionnaire = new
            {
                practiceQuestionnaire.Id,
                practiceQuestionnaire.Title,
                practiceQuestionnaire.Description,
                practiceQuestionnaire.ThemeColor,
                practiceQuestionnaire.BackgroundColor
            },
            questions = personalizedQuestions.Select(q => new
            {
                q.Id,
                q.Text,
                q.Options,
                q.Points,
                SubAreaId = q.SubAreaId,
                SubAreaName = q.SubArea?.Name
            }).ToList(),
            message = $"Generated {personalizedQuestions.Count} personalized questions targeting your weak areas"
        });
    }

    public class PersonalQuizRequest
    {
        public int QuestionCount { get; set; } = 15;
    }
}
