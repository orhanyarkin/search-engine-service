namespace SearchEngine.Application.DTOs;

/// <summary>
/// Başarılı giriş yanıtı için veri transfer nesnesi.
/// </summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAt);
