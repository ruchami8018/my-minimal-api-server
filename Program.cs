using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TodoApi.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// הגדרת לוגינג מפורט
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", 
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowClient", policy =>
//     {
//         policy.WithOrigins("https://clientminimalapi.onrender.com") // החלף ב-URL של הקליינט שלך
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Todo API", Version = "v1" });
});

// הוספת טיפול בשגיאות מפורט בהגדרת DbContext
try 
{
    var connectionString = builder.Configuration.GetConnectionString("ToDoDB");
    Console.WriteLine($"Connection String: {connectionString}");

    builder.Services.AddDbContext<ToDoDbContext>(options =>
    {
        try 
        {
            Console.WriteLine("Configuring database context...");
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                x => x.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)
            );
            Console.WriteLine("Database context configured successfully!");
        }
        catch (Exception dbEx)
        {
            Console.WriteLine($"Database Configuration Error: {dbEx.Message}");
            Console.WriteLine($"Full Database Error: {dbEx}");
            throw;
        }
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Critical Configuration Error: {ex.Message}");
    Console.WriteLine($"Full Error Details: {ex}");
}

var app = builder.Build();

// הוספת בדיקת חיבור למסד נתונים
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ToDoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Attempting to check database connection...");
        Console.WriteLine("Attempting to check database connection...");
        
        await dbContext.Database.OpenConnectionAsync();
        logger.LogInformation("✅ Database connection successful!");
        Console.WriteLine("✅ Database connection successful!");
        
        await dbContext.Database.CloseConnectionAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database Connection Error: {ex.Message}");
        Console.WriteLine($"Full Connection Error: {ex}");
    }
}

// הוספת טיפול בשגיאות גלובלי
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var exceptionHandlerPathFeature = 
            context.Features.Get<IExceptionHandlerPathFeature>();
        
        var exception = exceptionHandlerPathFeature?.Error;
        
        Console.WriteLine($"Unhandled Exception: {exception?.Message}");
        Console.WriteLine($"Full Exception: {exception}");

        var errorResponse = new 
        {
            Message = "An unexpected error occurred.",
            DetailedError = exception?.Message,
            StackTrace = exception?.StackTrace
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});

// app.UseCors("AllowClient");
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1"));
// }

    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TodoList API");
        c.RoutePrefix = string.Empty;
    });

app.UseCors("AllowAll");

app.MapGet("/", () => "hello world!");

app.MapGet("/items", async (ToDoDbContext context) =>
{
    try 
    {
        Console.WriteLine("Fetching items...");
        var items = await context.Items.ToListAsync();
        Console.WriteLine($"Fetched {items.Count} items successfully.");
        return Results.Ok(items);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error fetching items: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.MapPost("/items", async (Item item,ToDoDbContext context) =>
{
    try 
    {
        Console.WriteLine("Adding new item...");
        context.Items.Add(item);
        await context.SaveChangesAsync();
        Console.WriteLine("Item added successfully.");
        return Results.Created($"/items/{item.Id}", item);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding item: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.MapPut("/items/{id}", async (int id,Item updatedItem,ToDoDbContext context) =>
{
    try 
    {
        var existingItem=await context.Items.FindAsync(id);

        if(existingItem==null)
        return Results.NotFound();

        existingItem.Name=updatedItem.Name;
        existingItem.IsComplete=updatedItem.IsComplete;
        await context.SaveChangesAsync();
        return Results.Ok(existingItem);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating item: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.MapDelete("/items/{id}", async (int id, ToDoDbContext context) =>
{
    try 
    {
        // מוצא את הפריט למחיקה
        var itemToDelete = await context.Items.FindAsync(id);
        
        // אם הפריט לא נמצא, מחזיר שגיאה
        if (itemToDelete == null) 
            return Results.NotFound();
        
        // מוחק את הפריט
        context.Items.Remove(itemToDelete);
        await context.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting item: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.Run();
