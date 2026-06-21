using Backend.Repository;
using Backend.Service;
using Backend.Service.Interface;
using FirebaseAdmin;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Firebase key cho cả local dev và Railway / Production
var firebaseKeyBase64 = Environment.GetEnvironmentVariable("FIREBASE_KEY_BASE64");

if (!string.IsNullOrWhiteSpace(firebaseKeyBase64))
{
    // Railway / Production: đọc Firebase key từ biến môi trường base64
    var jsonBytes = Convert.FromBase64String(firebaseKeyBase64);

    var tempFirebaseKeyPath = Path.Combine(
        Path.GetTempPath(),
        "firebase-key.json"
    );

    File.WriteAllBytes(tempFirebaseKeyPath, jsonBytes);

    Environment.SetEnvironmentVariable(
        "GOOGLE_APPLICATION_CREDENTIALS",
        tempFirebaseKeyPath
    );
}
else
{
    // Local dev: đọc file firebase-key.json trong project
    var localFirebaseKeyPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "firebase-key.json"
    );

    if (!File.Exists(localFirebaseKeyPath))
    {
        throw new FileNotFoundException(
            "Không tìm thấy firebase-key.json ở local và cũng chưa cấu hình FIREBASE_KEY_BASE64 trên Railway."
        );
    }

    Environment.SetEnvironmentVariable(
        "GOOGLE_APPLICATION_CREDENTIALS",
        localFirebaseKeyPath
    );
}

if (FirebaseApp.DefaultInstance == null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.GetApplicationDefault()
    });
}

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{

    options.AddSecurityDefinition("FirebaseEmailPassword", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Chi dung de test Swagger: nhap email Firebase vao username va password Firebase vao password."
    });


    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "FirebaseEmailPassword"
                }
            },
            Array.Empty<string>()
        }
    });
});


// Firestore (Singleton)
builder.Services.AddSingleton(provider =>
{
    var projectId =
        builder.Configuration["Firebase:ProjectId"]
        ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
        ?? "myapp-8fb3f";

    return FirestoreDb.Create(projectId);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Auto register
builder.Services.Scan(scan => scan
    .FromAssemblyOf<Program>()
    .AddClasses(c => c.Where(t =>
        t.Name.EndsWith("Service")
        && !typeof(IHostedService).IsAssignableFrom(t)))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Repository")))
        .AsSelf()
        .WithScopedLifetime()
);

builder.Services.AddHttpClient<IDrivingCenterImportService, DrivingCenterImportService>();
builder.Services.AddHttpClient<IPayOsPaymentService, PayOsPaymentService>();
builder.Services.AddHttpClient<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddHttpClient<IAdMobService, AdMobService>();
builder.Services.AddHttpClient<IFeedbackAiService, FeedbackAiService>();

builder.Services.AddScoped<ModerationRepository>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddHttpClient<IAiModerationService, AiModerationService>();
builder.Services.AddHttpClient<ITrafficSignRecognitionService, TrafficSignRecognitionService>();
builder.Services.Configure<WrongQuestionReminderOptions>(
    builder.Configuration.GetSection("WrongQuestionReminder"));
builder.Services.AddHostedService<WrongQuestionReminderHostedService>();
builder.Services.AddScoped<INotificationPushService, NotificationPushService>();
builder.Services.AddScoped<NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            100L * 1024 * 1024;
    }
);

builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits.MaxRequestBodySize =
            100L * 1024 * 1024;
    }
);

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseSwagger();
app.UseSwaggerUI();

// Port deployment
//var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
//app.Urls.Add($"http://0.0.0.0:{port}");

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
