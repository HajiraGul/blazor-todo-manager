using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Models;

namespace TodoApp.Services;

public class TodoService
{
    private readonly ApplicationDbContext _context;
    private readonly AuthenticationStateProvider _authStateProvider;

    public TodoService(ApplicationDbContext context, AuthenticationStateProvider authStateProvider)
    {
        _context = context;
        _authStateProvider = authStateProvider;
    }

    private async Task<string?> GetCurrentUserIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public async Task<List<TodoItem>> GetTodosAsync(string? filter = null)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return [];
        }

        var query = _context.TodoItems.Where(t => t.UserId == userId);

        query = filter switch
        {
            "active" => query.Where(t => !t.IsCompleted),
            "completed" => query.Where(t => t.IsCompleted),
            _ => query
        };

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<(int Total, int Completed, int Pending)> GetStatsAsync()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return (0, 0, 0);
        }

        var todos = await _context.TodoItems
            .Where(t => t.UserId == userId)
            .ToListAsync();

        var completed = todos.Count(t => t.IsCompleted);
        return (todos.Count, completed, todos.Count - completed);
    }

    public async Task<TodoItem?> GetByIdAsync(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return null;
        }

        return await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<TodoItem> CreateAsync(string title, string? description)
    {
        var userId = await GetCurrentUserIdAsync()
            ?? throw new InvalidOperationException("User must be authenticated to create todos.");

        var todo = new TodoItem
        {
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();
        return todo;
    }

    public async Task<bool> UpdateAsync(int id, string title, string? description)
    {
        var todo = await GetByIdAsync(id);
        if (todo is null)
        {
            return false;
        }

        todo.Title = title.Trim();
        todo.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        todo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCompleteAsync(int id)
    {
        var todo = await GetByIdAsync(id);
        if (todo is null)
        {
            return false;
        }

        todo.IsCompleted = !todo.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var todo = await GetByIdAsync(id);
        if (todo is null)
        {
            return false;
        }

        _context.TodoItems.Remove(todo);
        await _context.SaveChangesAsync();
        return true;
    }
}
