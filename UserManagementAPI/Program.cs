using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add configuration for JWT
string secretKey = "5CH54CT128LOqYOa6I/wZ1a2hxZ/fk8OF1fss8p3Z3A="; // Use a secure, randomly generated key
string issuer = "https://your-app.com";
string audience = "https://your-app.com";

builder.Services.AddSingleton(new JwtTokenGenerator(secretKey, issuer, audience));

builder.Services.AddLogging(); // Ensure logging services are added

var app = builder.Build();

// Add custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Add the Token Validation Middleware
app.UseMiddleware<TokenValidationMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Simulated database using ConcurrentDictionary
var users = new ConcurrentDictionary<int, User>
{
    [1] = new User { Id = 1, Name = "Alice Johnson", Email = "alice.johnson@example.com" },
    [2] = new User { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com" }
};

// Calculate the nextId dynamically
var nextId = users.Keys.Any() ? users.Keys.Max() + 1 : 1;

// Add a root route
app.MapGet("/", () => "Welcome to User Management API");

// Error route for testing exception handling middleware
app.MapGet("/error", () =>
{
    throw new Exception("Test exception");
});

// GET: Retrieve a list of users or filter by name and/or email
app.MapGet("/users", (string? name, string? email) =>
{
    var filteredUsers = users.Values;

    if (!string.IsNullOrWhiteSpace(name))
        filteredUsers = filteredUsers.Where(u => u.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

    if (!string.IsNullOrWhiteSpace(email))
        filteredUsers = filteredUsers.Where(u => u.Email.Contains(email, StringComparison.OrdinalIgnoreCase)).ToList();

    return Results.Ok(filteredUsers);
});

// GET: Retrieve a user by ID
app.MapGet("/users/{id:int}", (int id) =>
{
    if (users.TryGetValue(id, out var user))
        return Results.Ok(user);
    return Results.NotFound("User not found");
});

// POST: Add a new user
app.MapPost("/users", (User newUser) =>
{
    // Validate input
    if (string.IsNullOrWhiteSpace(newUser.Name))
        return Results.BadRequest("Name is required.");
    if (string.IsNullOrWhiteSpace(newUser.Email))
        return Results.BadRequest("Email is required.");
    if (!IsValidEmail(newUser.Email))
        return Results.BadRequest("Invalid email format.");

    // Assign a new ID
    newUser.Id = Interlocked.Increment(ref nextId);
    if (!users.TryAdd(newUser.Id, newUser))
        return Results.Conflict("Failed to add user. Try again.");

    return Results.Created($"/users/{newUser.Id}", newUser);
});

// PUT: Update an existing user's details
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
    // Validate input
    if (string.IsNullOrWhiteSpace(updatedUser.Name))
        return Results.BadRequest("Name is required.");
    if (string.IsNullOrWhiteSpace(updatedUser.Email))
        return Results.BadRequest("Email is required.");
    if (!IsValidEmail(updatedUser.Email))
        return Results.BadRequest("Invalid email format.");

    // Check if user exists
    if (!users.TryGetValue(id, out var existingUser))
        return Results.NotFound("User not found.");

    // Update user details
    existingUser.Name = updatedUser.Name;
    existingUser.Email = updatedUser.Email;
    users[id] = existingUser; // Replace atomically

    return Results.Ok(existingUser);
});

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
    if (users.TryRemove(id, out _))
    {
        return Results.Ok("User deleted");
    }
    return Results.NotFound("User not found");
});

// Helper method for email validation
static bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

// POST: Login route to generate a JWT token
app.MapPost("/login", (string userName, string email, JwtTokenGenerator tokenGenerator) =>
{
    // Validate the user credentials (this is just a placeholder for actual authentication)
    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("Invalid username or email.");

    // Generate the JWT token
    var token = tokenGenerator.GenerateToken(userName, email, 60); // 60-minute expiration
    return Results.Ok(new { token });
});

// Protected route
app.MapGet("/protected", () => "Welcome to the protected endpoint!");

app.Run();

public class User
{
    public int Id { get; set; }
    required public string Name { get; set; }
    required public string Email { get; set; }
}
