using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;

namespace QuizGamePlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
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
            Questions = q.Questions.Select(x => new { x.Id, x.Text, x.Options, x.CorrectAnswer, x.Points }) 
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
}
