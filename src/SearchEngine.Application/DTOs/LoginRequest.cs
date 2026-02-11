namespace SearchEngine.Application.DTOs;

/// <summary>
/// Giriş isteği için veri transfer nesnesi.
/// </summary>
public sealed record LoginRequest(string Username, string Password);
