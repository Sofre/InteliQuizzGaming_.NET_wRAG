using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuizGamePlatform.Data;
using QuizGamePlatform.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// Configure SQLite DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "FallbackSuperSecretKey_1234567890";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
// app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use Static Files for Frontend (wwwroot)
app.UseDefaultFiles(); // Enables index.html as default
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure Database is Created & Seed Admin
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();

    if (!context.Users.Any(u => u.Email == "student@quiz.com"))
    {
        var student = new User { Email = "student@quiz.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("student123"), Role = Roles.Student };
        var admin = new User { Email = "admin@quiz.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), Role = Roles.Admin };
        context.Users.AddRange(student, admin);
        context.SaveChanges();

        var q = new Questionnaire { Title = "Programming Basics", ThemeColor = "#10b981", BackgroundColor = "#022c22" };
        var q1 = new Question { Text = "What does HTML stand for?", Options = "A. Hyper Text Markup Language,B. High Text Machine Language,C. Hyperloop Machine Language", CorrectAnswer = "A. Hyper Text Markup Language", Points = 10 };
        var q2 = new Question { Text = "Is C# an Object Oriented language?", Options = "Yes,No", CorrectAnswer = "Yes", Points = 15 };
        q.Questions.Add(q1); q.Questions.Add(q2);
        context.Questionnaires.Add(q);
        context.SaveChanges();

        var session = new Session { Name = "First Year Students 2026" };
        session.AssignedUsers.Add(student);
        session.AssignedQuestionnaires.Add(q);
        context.Sessions.Add(session);
        context.SaveChanges();
    }

    if (!context.Users.Any(u => u.Email == "dusko@uacs.com"))
    {
        var dusko = new User { Email = "dusko@uacs.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("dusko123"), Role = Roles.Admin };
        var eva = new User { Email = "eva@uacs.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("eva123"), Role = Roles.Admin };
        var david = new User { Email = "david@uacs.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("david123"), Role = Roles.Admin };
        context.Users.AddRange(dusko, eva, david);
        context.SaveChanges();
    }
}

app.Run();
