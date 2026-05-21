using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;

namespace QuizGamePlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminExportController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminExportController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportResults()
    {
        var answers = await _context.Answers
            .Include(a => a.User)
            .Include(a => a.Question).ThenInclude(q => q!.SubArea).ThenInclude(sa => sa!.Area)
            .Include(a => a.UserAssignedSubArea).ThenInclude(sa => sa!.Area)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Quiz Results");

        worksheet.Cell(1, 1).Value = "User Email";
        worksheet.Cell(1, 2).Value = "Question";
        worksheet.Cell(1, 3).Value = "User Choice";
        worksheet.Cell(1, 4).Value = "Is Correct?";
        worksheet.Cell(1, 5).Value = "Points Earned";
        worksheet.Cell(1, 6).Value = "Time (sec)";
        worksheet.Cell(1, 7).Value = "Admin Assigned Area -> SubArea";
        worksheet.Cell(1, 8).Value = "User Assigned Area -> SubArea";

        var row = 2;
        foreach (var a in answers)
        {
            worksheet.Cell(row, 1).Value = a.User?.Email ?? "Unknown";
            worksheet.Cell(row, 2).Value = a.Question?.Text ?? "Unknown";
            worksheet.Cell(row, 3).Value = a.IsSkipped ? "(Skipped)" : a.Choice;
            worksheet.Cell(row, 4).Value = a.IsCorrect;
            worksheet.Cell(row, 5).Value = a.PointsEarned;
            worksheet.Cell(row, 6).Value = a.TimeTakenSeconds;
            
            worksheet.Cell(row, 7).Value = a.Question?.SubArea != null 
                ? $"{a.Question.SubArea.Area?.Name} -> {a.Question.SubArea.Name}" : "None";
                
            worksheet.Cell(row, 8).Value = a.UserAssignedSubArea != null 
                ? $"{a.UserAssignedSubArea.Area?.Name} -> {a.UserAssignedSubArea.Name}" : "None";
                
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "QuizResults.xlsx");
    }
}
