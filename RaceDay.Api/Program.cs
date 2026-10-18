
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using RaceDayEvent = RaceDay.Api.Models.Event;
using RaceDayResult = RaceDay.Api.Models.Result;
using RaceDayRoute = RaceDay.Api.Models.Route;

var builder = WebApplication.CreateBuilder(args);

// ---------------- CONFIGURATION ----------------

var connectionString = builder.Configuration
    .GetConnectionString("RaceDayConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "RaceDayConnection is missing.");
}

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:Key as a secret of at least 32 bytes.");
}

// ---------------- DATABASE ----------------

builder.Services.AddDbContext<RaceDayContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ---------------- JWT AUTHENTICATION ----------------

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"]
                    ?? "RaceDay.Api",

                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"]
                    ?? "RaceDay.Client",

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),

                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();

// ---------------- SWAGGER ----------------

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for race events, enrolments and results."
    });

    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

// ---------------- MIDDLEWARE ----------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ---------------- HOME ----------------

app.MapGet("/", () => Results.Ok(new
{
    Application = "RaceDay API",
    Status = "Running"
}));

// ---------------- AUTHENTICATION ----------------

// Register a participant or organiser.
app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    RaceDayContext db,
    IPasswordHasher<User> hasher) =>
{
    var firstName = request.FirstName?.Trim();
    var lastName = request.LastName?.Trim();
    var email = request.Email?.Trim().ToLowerInvariant();
    var role = request.Role?.Trim();

    if (string.IsNullOrWhiteSpace(firstName) ||
        string.IsNullOrWhiteSpace(lastName) ||
        string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new
        {
            Message = "All required fields must be provided."
        });
    }

    if (email.Length > 256 ||
        firstName.Length > 100 ||
        lastName.Length > 100 ||
        request.Password.Length < 8)
    {
        return Results.BadRequest(new
        {
            Message = "Names/email are too long or the password is shorter than 8 characters."
        });
    }

    if (role is not ("Participant" or "Organiser"))
    {
        return Results.BadRequest(new
        {
            Message = "Role must be Participant or Organiser."
        });
    }

    if (await db.Users.AnyAsync(u => u.Email == email))
    {
        return Results.Conflict(new
        {
            Message = "That email is already registered."
        });
    }

    if (role == "Organiser" &&
        string.IsNullOrWhiteSpace(request.OrganisationName))
    {
        return Results.BadRequest(new
        {
            Message = "OrganisationName is required for an organiser."
        });
    }

    if (role == "Participant" && request.DateOfBirth is null)
    {
        return Results.BadRequest(new
        {
            Message = "DateOfBirth is required for a participant."
        });
    }

    if (role == "Participant" &&
        request.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
    {
        return Results.BadRequest(new
        {
            Message = "Date of birth cannot be in the future."
        });
    }

    var user = new User
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Role = role,
        PasswordHash = "temporary"
    };

    user.PasswordHash = hasher.HashPassword(
        user, request.Password);

    await using var transaction =
        await db.Database.BeginTransactionAsync();

    db.Users.Add(user);
    await db.SaveChangesAsync();

    if (role == "Organiser")
    {
        db.Organisers.Add(new Organiser
        {
            OrganiserId = user.UserId,
            OrganisationName = request.OrganisationName!.Trim()
        });
    }
    else
    {
        db.Participants.Add(new Participant
        {
            ParticipantId = user.UserId,
            DateOfBirth = request.DateOfBirth!.Value,
            EmergencyContact = request.EmergencyContact?.Trim()
        });
    }

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Created("/api/users/me", new
    {
        user.UserId,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role
    });
})
.AllowAnonymous();

// Login and issue a JWT.
app.MapPost("/api/auth/login", async (
    LoginRequest request,
    RaceDayContext db,
    IPasswordHasher<User> hasher,
    IConfiguration configuration) =>
{
    var email = request.Email?.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new
        {
            Message = "Email and password are required."
        });
    }

    var user = await db.Users.SingleOrDefaultAsync(
        u => u.Email == email);

    if (user is null)
        return Results.Unauthorized();

    var verification = hasher.VerifyHashedPassword(
        user, user.PasswordHash, request.Password);

    if (verification == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    var key = configuration["Jwt:Key"]!;
    var issuer = configuration["Jwt:Issuer"] ?? "RaceDay.Api";
    var audience = configuration["Jwt:Audience"] ?? "RaceDay.Client";

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new(ClaimTypes.Name, user.Email),
        new(ClaimTypes.Role, user.Role)
    };

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(2),
        signingCredentials: credentials);

    return Results.Ok(new
    {
        Token = new JwtSecurityTokenHandler().WriteToken(token),
        ExpiresAtUtc = token.ValidTo,
        user.UserId,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role
    });
})
.AllowAnonymous();

// ---------------- PROFILE ----------------

app.MapGet("/api/users/me", async (
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var user = await db.Users.AsNoTracking()
        .Where(u => u.UserId == userId.Value)
        .Select(u => new ProfileResponse(
            u.UserId,
            u.FirstName,
            u.LastName,
            u.Email,
            u.Role))
        .SingleOrDefaultAsync();

    return user is null
        ? Results.NotFound()
        : Results.Ok(user);
})
.RequireAuthorization();

app.MapPut("/api/users/me", async (
    UpdateProfileRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var user = await db.Users.FindAsync(userId.Value);

    if (user is null)
        return Results.NotFound();

    var firstName = request.FirstName?.Trim();
    var lastName = request.LastName?.Trim();
    var email = request.Email?.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(firstName) ||
        string.IsNullOrWhiteSpace(lastName) ||
        string.IsNullOrWhiteSpace(email) ||
        firstName.Length > 100 ||
        lastName.Length > 100 ||
        email.Length > 256)
    {
        return Results.BadRequest(new
        {
            Message = "Valid first name, last name and email are required."
        });
    }

    if (await db.Users.AnyAsync(
        u => u.Email == email && u.UserId != user.UserId))
    {
        return Results.Conflict(new
        {
            Message = "That email is already in use."
        });
    }

    user.FirstName = firstName;
    user.LastName = lastName;
    user.Email = email;

    await db.SaveChangesAsync();

    return Results.Ok(new ProfileResponse(
        user.UserId,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role));
})
.RequireAuthorization();

// ---------------- EVENTS ----------------

// List events.
app.MapGet("/api/events", async (RaceDayContext db) =>
{
    var events = await db.Events.AsNoTracking()
        .OrderBy(e => e.EventDate)
        .Select(e => new EventResponse(
            e.EventId,
            e.OrganiserId,
            e.EventName,
            e.Description,
            e.EventDate,
            e.Location,
            e.EventType,
            e.Routes.Select(r => r.DistanceKm).FirstOrDefault()))
        .ToListAsync();

    return Results.Ok(events);
})
.AllowAnonymous();

// Get one event.
app.MapGet("/api/events/{id:int}", async (
    int id,
    RaceDayContext db) =>
{
    var item = await db.Events.AsNoTracking()
        .Where(e => e.EventId == id)
        .Select(e => new EventResponse(
            e.EventId,
            e.OrganiserId,
            e.EventName,
            e.Description,
            e.EventDate,
            e.Location,
            e.EventType,
            e.Routes.Select(r => r.DistanceKm).FirstOrDefault()))
        .SingleOrDefaultAsync();

    return item is null
        ? Results.NotFound()
        : Results.Ok(item);
})
.AllowAnonymous();

// Create event: organisers only.
app.MapPost("/api/events", async (
    EventCreateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var organiser = await db.Organisers
        .AnyAsync(o => o.OrganiserId == userId.Value);

    if (!organiser)
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(request.EventName) ||
        string.IsNullOrWhiteSpace(request.Location) ||
        string.IsNullOrWhiteSpace(request.EventType) ||
        request.EventName.Trim().Length > 200 ||
        request.Location.Trim().Length > 200 ||
        request.EventType.Trim().Length > 20 ||
        (request.Description?.Length ?? 0) > 1000 ||
        request.DistanceKm <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Check event fields and ensure distance is greater than zero."
        });
    }

    var item = new RaceDayEvent
    {
        OrganiserId = userId.Value,
        EventName = request.EventName.Trim(),
        Description = request.Description?.Trim(),
        EventDate = request.EventDate,
        Location = request.Location.Trim(),
        EventType = request.EventType.Trim()
    };

    item.Routes.Add(new RaceDayRoute
    {
        RouteName = !string.IsNullOrWhiteSpace(request.RouteName)
            ? request.RouteName.Trim()
            : $"{request.EventName.Trim()} Route",
        DistanceKm = request.DistanceKm,
        RouteDescription = request.RouteDescription?.Trim()
    });

    db.Events.Add(item);
    await db.SaveChangesAsync();

    return Results.Created($"/api/events/{item.EventId}", new
    {
        item.EventId,
        item.EventName,
        item.EventDate,
        item.Location,
        request.DistanceKm
    });
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// Update event: organisers only.
app.MapPut("/api/events/{id:int}", async (
    int id,
    EventUpdateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var item = await db.Events
        .Include(e => e.Routes)
        .SingleOrDefaultAsync(e => e.EventId == id);

    if (item is null)
        return Results.NotFound();

    if (item.OrganiserId != userId.Value)
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(request.EventName) ||
        string.IsNullOrWhiteSpace(request.Location) ||
        string.IsNullOrWhiteSpace(request.EventType) ||
        request.EventName.Trim().Length > 200 ||
        request.Location.Trim().Length > 200 ||
        request.EventType.Trim().Length > 20 ||
        (request.Description?.Length ?? 0) > 1000 ||
        request.DistanceKm <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Check event fields and ensure distance is greater than zero."
        });
    }

    item.EventName = request.EventName.Trim();
    item.Description = request.Description?.Trim();
    item.EventDate = request.EventDate;
    item.Location = request.Location.Trim();
    item.EventType = request.EventType.Trim();

    var route = item.Routes.FirstOrDefault();

    if (route is null)
    {
        item.Routes.Add(new RaceDayRoute
        {
            RouteName = !string.IsNullOrWhiteSpace(request.RouteName)
                ? request.RouteName.Trim()
                : $"{item.EventName} Route",
            DistanceKm = request.DistanceKm,
            RouteDescription = request.RouteDescription?.Trim()
        });
    }
    else
    {
        route.RouteName = !string.IsNullOrWhiteSpace(request.RouteName)
            ? request.RouteName.Trim()
            : $"{item.EventName} Route";

        route.DistanceKm = request.DistanceKm;
        route.RouteDescription = request.RouteDescription?.Trim();
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Event updated.",
        item.EventId
    });
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// Delete event: organisers only.
app.MapDelete("/api/events/{id:int}", async (
    int id,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var item = await db.Events
        .SingleOrDefaultAsync(e => e.EventId == id);

    if (item is null)
        return Results.NotFound();

    if (item.OrganiserId != userId.Value)
        return Results.Forbid();

    var hasEnrolments = await db.EventCategories
        .Where(ec => ec.EventId == id)
        .AnyAsync(ec => ec.Enrolments.Any());

    if (hasEnrolments)
    {
        return Results.Conflict(new
        {
            Message = "This event has enrolments. Remove or resolve them before deleting the event."
        });
    }

    db.Events.Remove(item);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// ---------------- CATEGORIES ----------------


app.MapPost("/api/categories", async (
    CategoryCreateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.CategoryName))
    {
        return Results.BadRequest(new
        {
            Message = "Category name is required."
        });
    }

    if (request.DistanceKm <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "DistanceKm must be greater than zero."
        });
    }

    var category = new Category
    {
        CategoryName = request.CategoryName.Trim(),
        DistanceKm = request.DistanceKm
    };

    db.Categories.Add(category);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/categories/{category.CategoryId}",
        new CategoryResponse(
            category.CategoryId,
            category.CategoryName,
            category.DistanceKm));
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));


app.MapGet("/api/categories", async (RaceDayContext db) =>
    Results.Ok(await db.Categories.AsNoTracking()
        .Select(c => new CategoryResponse(
            c.CategoryId,
            c.CategoryName,
            c.DistanceKm))
        .ToListAsync()))
.AllowAnonymous();

app.MapGet("/api/events/{eventId:int}/categories", async (
    int eventId,
    RaceDayContext db) =>
{
    var exists = await db.Events.AnyAsync(e => e.EventId == eventId);

    if (!exists)
        return Results.NotFound();

    var categories = await db.EventCategories.AsNoTracking()
        .Where(ec => ec.EventId == eventId)
        .Select(ec => new EventCategoryResponse(
            ec.EventCategoryId,
            ec.EventId,
            ec.CategoryId,
            ec.Category.CategoryName,
            ec.Category.DistanceKm,
            ec.EntryFee,
            ec.MaximumParticipants))
        .ToListAsync();

    return Results.Ok(categories);
})
.AllowAnonymous();

// Link category to event: organisers only.
app.MapPost("/api/events/{eventId:int}/categories", async (
    int eventId,
    EventCategoryCreateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var item = await db.Events
        .SingleOrDefaultAsync(e => e.EventId == eventId);

    if (item is null)
        return Results.NotFound();

    if (item.OrganiserId != userId.Value)
        return Results.Forbid();

    if (request.EntryFee < 0 || request.MaximumParticipants <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "EntryFee must be non-negative and MaximumParticipants must be positive."
        });
    }

    if (!await db.Categories.AnyAsync(
        c => c.CategoryId == request.CategoryId))
    {
        return Results.BadRequest(new
        {
            Message = "CategoryId does not exist."
        });
    }

    if (await db.EventCategories.AnyAsync(ec =>
        ec.EventId == eventId &&
        ec.CategoryId == request.CategoryId))
    {
        return Results.Conflict(new
        {
            Message = "This category is already linked to the event."
        });
    }

    var link = new EventCategory
    {
        EventId = eventId,
        CategoryId = request.CategoryId,
        EntryFee = request.EntryFee,
        MaximumParticipants = request.MaximumParticipants
    };

    db.EventCategories.Add(link);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/events/{eventId}/categories",
        new
        {
            link.EventCategoryId,
            link.EventId,
            link.CategoryId,
            link.EntryFee,
            link.MaximumParticipants
        });
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// Update event category: organisers only.
app.MapPut("/api/event-categories/{id:int}", async (
    int id,
    EventCategoryUpdateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var link = await db.EventCategories
        .Include(ec => ec.Event)
        .SingleOrDefaultAsync(ec => ec.EventCategoryId == id);

    if (link is null)
        return Results.NotFound();

    if (link.Event.OrganiserId != userId.Value)
        return Results.Forbid();

    if (request.EntryFee < 0 || request.MaximumParticipants <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "EntryFee must be non-negative and MaximumParticipants must be positive."
        });
    }

    link.EntryFee = request.EntryFee;
    link.MaximumParticipants = request.MaximumParticipants;

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Event category updated."
    });
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// Delete event category: organisers only.
app.MapDelete("/api/event-categories/{id:int}", async (
    int id,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var link = await db.EventCategories
        .Include(ec => ec.Event)
        .SingleOrDefaultAsync(ec => ec.EventCategoryId == id);

    if (link is null)
        return Results.NotFound();

    if (link.Event.OrganiserId != userId.Value)
        return Results.Forbid();

    if (await db.Enrolments.AnyAsync(
        e => e.EventCategoryId == id))
    {
        return Results.Conflict(new
        {
            Message = "This category has enrolments and cannot be deleted."
        });
    }

    db.EventCategories.Remove(link);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// ---------------- ENROLMENTS ----------------

// Create enrolment: participants only.
app.MapPost("/api/enrolments", async (
    EnrolmentCreateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var participant = await db.Participants
        .SingleOrDefaultAsync(p => p.ParticipantId == userId.Value);

    if (participant is null)
        return Results.Forbid();

    var category = await db.EventCategories
        .Include(ec => ec.Event)
        .SingleOrDefaultAsync(
            ec => ec.EventCategoryId == request.EventCategoryId);

    if (category is null)
    {
        return Results.NotFound(new
        {
            Message = "Event category not found."
        });
    }

    if (category.Event.EventDate <
        DateOnly.FromDateTime(DateTime.Today))
    {
        return Results.BadRequest(new
        {
            Message = "You cannot enrol in a past event."
        });
    }

    var currentCount = await db.Enrolments.CountAsync(e =>
        e.EventCategoryId == request.EventCategoryId &&
        e.Status != "Cancelled");

    if (currentCount >= category.MaximumParticipants)
    {
        return Results.Conflict(new
        {
            Message = "This category is full."
        });
    }

    if (await db.Enrolments.AnyAsync(e =>
        e.ParticipantId == participant.ParticipantId &&
        e.EventCategoryId == request.EventCategoryId &&
        e.Status != "Cancelled"))
    {
        return Results.Conflict(new
        {
            Message = "You are already enrolled in this category."
        });
    }

    var enrolment = new Enrolment
    {
        ParticipantId = participant.ParticipantId,
        EventCategoryId = request.EventCategoryId,
        Status = "Enrolled"
    };

    db.Enrolments.Add(enrolment);
    await db.SaveChangesAsync();

    return Results.Created("/api/enrolments/me", new
    {
        enrolment.EnrolmentId,
        enrolment.ParticipantId,
        enrolment.EventCategoryId,
        enrolment.EnrolmentDate,
        enrolment.Status
    });
})
.RequireAuthorization(policy => policy.RequireRole("Participant"));

// List own enrolments: participants only.
app.MapGet("/api/enrolments/me", async (
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var participantExists = await db.Participants
        .AnyAsync(p => p.ParticipantId == userId.Value);

    if (!participantExists)
        return Results.Forbid();

    var items = await db.Enrolments.AsNoTracking()
        .Where(e => e.ParticipantId == userId.Value)
        .Select(e => new EnrolmentResponse(
            e.EnrolmentId,
            e.ParticipantId,
            e.EventCategoryId,
            e.EventCategory.Event.EventName,
            e.EventCategory.Category.CategoryName,
            e.EnrolmentDate,
            e.Status))
        .ToListAsync();

    return Results.Ok(items);
})
.RequireAuthorization(policy => policy.RequireRole("Participant"));

// View enrolments for own event: organisers only.
app.MapGet("/api/events/{eventId:int}/enrolments", async (
    int eventId,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var item = await db.Events
        .SingleOrDefaultAsync(e => e.EventId == eventId);

    if (item is null)
        return Results.NotFound();

    if (item.OrganiserId != userId.Value)
        return Results.Forbid();

    var enrolments = await db.Enrolments.AsNoTracking()
        .Where(e => e.EventCategory.EventId == eventId)
        .Select(e => new
        {
            e.EnrolmentId,
            e.Status,
            e.EnrolmentDate,
            ParticipantName =
                e.Participant.ParticipantNavigation.FirstName +
                " " +
                e.Participant.ParticipantNavigation.LastName,
            e.Participant.ParticipantNavigation.Email,
            CategoryName = e.EventCategory.Category.CategoryName
        })
        .ToListAsync();

    return Results.Ok(enrolments);
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// Cancel own enrolment: participants only.
app.MapDelete("/api/enrolments/{id:int}", async (
    int id,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var enrolment = await db.Enrolments
        .SingleOrDefaultAsync(e => e.EnrolmentId == id);

    if (enrolment is null)
        return Results.NotFound();

    if (enrolment.ParticipantId != userId.Value)
        return Results.Forbid();

    if (enrolment.Status == "Cancelled")
    {
        return Results.Conflict(new
        {
            Message = "This enrolment is already cancelled."
        });
    }

    enrolment.Status = "Cancelled";
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole("Participant"));

// ---------------- RESULTS ----------------

// Record result for own event: organisers only.
app.MapPost("/api/results", async (
    ResultCreateRequest request,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var enrolment = await db.Enrolments
        .Include(e => e.EventCategory)
            .ThenInclude(ec => ec.Event)
        .SingleOrDefaultAsync(
            e => e.EnrolmentId == request.EnrolmentId);

    if (enrolment is null)
        return Results.NotFound();

    if (enrolment.EventCategory.Event.OrganiserId != userId.Value)
        return Results.Forbid();

    if (enrolment.Status == "Cancelled")
    {
        return Results.BadRequest(new
        {
            Message = "Cannot record a result for a cancelled enrolment."
        });
    }

    if (request.Position is <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Position must be greater than zero."
        });
    }

    if (await db.Results.AnyAsync(
        r => r.EnrolmentId == request.EnrolmentId))
    {
        return Results.Conflict(new
        {
            Message = "A result already exists for this enrolment."
        });
    }

    var result = new RaceDayResult
    {
        EnrolmentId = request.EnrolmentId,
        FinishTime = request.FinishTime,
        Position = request.Position
    };

    db.Results.Add(result);
    await db.SaveChangesAsync();

    return Results.Created("/api/results/me", new
    {
        result.ResultId,
        result.EnrolmentId,
        result.FinishTime,
        result.Position,
        result.RecordedAt
    });
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

// View own results: participants only.
app.MapGet("/api/results/me", async (
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var participantExists = await db.Participants
        .AnyAsync(p => p.ParticipantId == userId.Value);

    if (!participantExists)
        return Results.Forbid();

    var items = await db.Results.AsNoTracking()
        .Where(r => r.Enrolment.ParticipantId == userId.Value)
        .Select(r => new
        {
            r.ResultId,
            r.EnrolmentId,
            EventName = r.Enrolment.EventCategory.Event.EventName,
            CategoryName = r.Enrolment.EventCategory.Category.CategoryName,
            r.FinishTime,
            r.Position,
            r.RecordedAt
        })
        .ToListAsync();

    return Results.Ok(items);
})
.RequireAuthorization(policy => policy.RequireRole("Participant"));

// View results for own event: organisers only.
app.MapGet("/api/events/{eventId:int}/results", async (
    int eventId,
    ClaimsPrincipal principal,
    RaceDayContext db) =>
{
    var userId = GetUserId(principal);

    if (userId is null)
        return Results.Unauthorized();

    var item = await db.Events
        .SingleOrDefaultAsync(e => e.EventId == eventId);

    if (item is null)
        return Results.NotFound();

    if (item.OrganiserId != userId.Value)
        return Results.Forbid();

    var results = await db.Results.AsNoTracking()
        .Where(r => r.Enrolment.EventCategory.EventId == eventId)
        .OrderBy(r => r.Position)
        .Select(r => new
        {
            r.ResultId,
            r.EnrolmentId,
            ParticipantName =
                r.Enrolment.Participant.ParticipantNavigation.FirstName +
                " " +
                r.Enrolment.Participant.ParticipantNavigation.LastName,
            CategoryName = r.Enrolment.EventCategory.Category.CategoryName,
            r.FinishTime,
            r.Position,
            r.RecordedAt
        })
        .ToListAsync();

    return Results.Ok(results);
})
.RequireAuthorization(policy => policy.RequireRole("Organiser"));

app.Run();

// ---------------- HELPER ----------------

static int? GetUserId(ClaimsPrincipal principal)
{
    var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

    return int.TryParse(value, out var id) ? id : null;
}

// ---------------- REQUEST/RESPONSE DTOs ----------------

public partial class Program { }

public sealed record RegisterRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? Role,
    string? OrganisationName,
    DateOnly? DateOfBirth,
    string? EmergencyContact);

public sealed record LoginRequest(
    string? Email,
    string? Password);

public sealed record ProfileResponse(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role);

public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Email);

public sealed record EventCreateRequest(
    string? EventName,
    string? Description,
    DateOnly EventDate,
    string? Location,
    string? EventType,
    decimal DistanceKm,
    string? RouteName,
    string? RouteDescription);

public sealed record EventUpdateRequest(
    string? EventName,
    string? Description,
    DateOnly EventDate,
    string? Location,
    string? EventType,
    decimal DistanceKm,
    string? RouteName,
    string? RouteDescription);

public sealed record EventResponse(
    int EventId,
    int OrganiserId,
    string EventName,
    string? Description,
    DateOnly EventDate,
    string Location,
    string EventType,
    decimal DistanceKm);

public sealed record CategoryResponse(
    int CategoryId,
    string CategoryName,
    decimal DistanceKm);

public sealed record EventCategoryCreateRequest(
    int CategoryId,
    decimal EntryFee,
    int MaximumParticipants);

public sealed record EventCategoryUpdateRequest(
    decimal EntryFee,
    int MaximumParticipants);

public sealed record EventCategoryResponse(
    int EventCategoryId,
    int EventId,
    int CategoryId,
    string CategoryName,
    decimal CategoryDistanceKm,
    decimal EntryFee,
    int MaximumParticipants);

public sealed record EnrolmentCreateRequest(
    int EventCategoryId);

public sealed record EnrolmentResponse(
    int EnrolmentId,
    int ParticipantId,
    int EventCategoryId,
    string EventName,
    string CategoryName,
    DateTime EnrolmentDate,
    string Status);

public sealed record ResultCreateRequest(
    int EnrolmentId,
    TimeOnly? FinishTime,
    int? Position);


public record CategoryCreateRequest(
    string CategoryName,
    decimal DistanceKm);
