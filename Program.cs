using LogViewerApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Add this to your dependency injection configuration
builder.Services.AddSingleton<SessionManager>();

// Register the FileService
builder.Services.AddSingleton<FileService>();

// Add CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthorization();
app.MapControllers();

// Setup periodic cleanup (every hour)
var timer = new System.Threading.Timer(_ =>
{
    using var scope = app.Services.CreateScope();
    var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
    fileService.CleanupTempFiles();
}, null, TimeSpan.Zero, TimeSpan.FromHours(1));

app.Run();
