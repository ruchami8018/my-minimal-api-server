using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TodoApi.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<ToDoDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("ToDoDB"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("ToDoDB"))
    ));
var app = builder.Build();

// app.UseCors("AllowClient");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1"));
}

app.MapGet("/", () => "hello world!");

app.MapGet("/items", async (ToDoDbContext context) =>
{
    var items = await context.Items.ToListAsync();
    return Results.Ok(items);
});

app.MapPost("/items", async (Item item,ToDoDbContext context) =>
{
    context.Items.Add(item);
    await context.SaveChangesAsync();
    return Results.Created($"/items/{item.Id}", item);
});

app.MapPut("/items/{id}", async (int id,Item updatedItem,ToDoDbContext context) =>
{
    var existingItem=await context.Items.FindAsync(id);

    if(existingItem==null)
    return Results.NotFound();

    existingItem.Name=updatedItem.Name;
    existingItem.IsComplete=updatedItem.IsComplete;
    await context.SaveChangesAsync();
    return Results.Ok(existingItem);
});

app.MapDelete("/items/{id}", async (int id, ToDoDbContext context) =>
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
});
// app.Urls.Add("http://localhost:5000");
    // var newItem = new Item 
    // { 
    //     Name = "משימה לדוגמה", 
    //     IsComplete = false 
    // };
    // newItem.MapPost();

// builder.Services.AddDbContext<ToDoDbContext>(options =>
//     options.UseMySql(
//         builder.Configuration.GetConnectionString("ToDoDB"),
//         ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("ToDoDB"))
//     ));

app.UseCors("AllowAll");

app.Run();
