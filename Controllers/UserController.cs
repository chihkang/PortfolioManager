using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly MongoDbService _mongoDbService;

    public UserController(MongoDbService mongoDbService, ILogger<UserController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    ///     Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        var users = await _mongoDbService.Users.Find(_ => true).ToListAsync();
        return Ok(users);
    }

    /// <summary>
    ///     Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(string id)
    {
        var user = await _mongoDbService.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    /// <summary>
    ///     Create a new user with portfolio
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(CreateUserDto createUserDto)
    {
        try
        {
            // Validate username
            if (string.IsNullOrWhiteSpace(createUserDto.Username)) return BadRequest("Username is required");

            // Check if username already exists
            var existingUser = await _mongoDbService.Users
                .Find(u => u.Username == createUserDto.Username)
                .FirstOrDefaultAsync();

            if (existingUser != null) return Conflict("Username already exists");

            // Start a transaction
            using var session = await _mongoDbService.Client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Create portfolio first
                var portfolio = new Portfolio
                {
                    LastUpdated = DateTime.UtcNow,
                    Stocks = new List<PortfolioStock>()
                };

                await _mongoDbService.Portfolios.InsertOneAsync(session, portfolio);

                // Create user with default values
                var user = new User
                {
                    Username = createUserDto.Username,
                    Email = $"{createUserDto.Username}@default.com", // Default email
                    PortfolioId = portfolio.Id,
                    CreatedAt = DateTime.UtcNow,
                    Settings = new Dictionary<string, object>
                    {
                        { "currency", "TWD" }, // Default currency
                        { "timeZone", "Asia/Taipei" } // Default timezone
                    }
                };

                await _mongoDbService.Users.InsertOneAsync(session, user);

                // Update portfolio with user ID
                var updateDefinition = Builders<Portfolio>.Update
                    .Set(p => p.UserId, user.Id);
                await _mongoDbService.Portfolios.UpdateOneAsync(
                    session,
                    p => p.Id == portfolio.Id,
                    updateDefinition
                );

                // Commit transaction
                await session.CommitTransactionAsync();

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user and portfolio creation");
                await session.AbortTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user and portfolio");
            return StatusCode(500,
                new { message = "Internal server error occurred while creating user and portfolio" });
        }
    }

    /// <summary>
    ///     Update an existing user
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, User user)
    {
        if (id != user.Id) return BadRequest("User ID mismatch");

        try
        {
            var result = await _mongoDbService.Users.ReplaceOneAsync(
                u => u.Id == id,
                user,
                new ReplaceOptions { IsUpsert = false }
            );

            if (result.ModifiedCount == 0) return NotFound($"User with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating user {id}");
            return StatusCode(500, "Internal server error occurred while updating user");
        }
    }

    /// <summary>
    ///     Delete a user and their portfolio
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            using var session = await _mongoDbService.Client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Get user to find portfolio ID
                var user = await _mongoDbService.Users.Find(session, u => u.Id == id)
                    .FirstOrDefaultAsync();

                if (user == null) return NotFound($"User with ID {id} not found");

                // Delete portfolio
                if (!string.IsNullOrEmpty(user.PortfolioId))
                    await _mongoDbService.Portfolios.DeleteOneAsync(
                        session,
                        p => p.Id == user.PortfolioId
                    );

                // Delete user
                await _mongoDbService.Users.DeleteOneAsync(session, u => u.Id == id);

                // Commit transaction
                await session.CommitTransactionAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user and portfolio deletion");
                await session.AbortTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete user {id}");
            return StatusCode(500, "Internal server error occurred while deleting user");
        }
    }
}