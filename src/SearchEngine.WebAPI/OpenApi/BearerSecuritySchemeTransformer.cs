using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SearchEngine.WebAPI.OpenApi;

/// <summary>
/// OpenAPI dokümanına JWT Bearer güvenlik şemasını ekleyen transformer.
/// Scalar UI'da "Authenticate" butonu gösterir, böylece token ile test yapılabilir.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider
) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();

        if (authSchemes.Any(s => s.Name == "Bearer"))
        {
            var bearerScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT token girin. Önce POST /api/v1/auth/login ile token alın (admin / admin123)."
            };

            document.Components ??= new OpenApiComponents();
            document.AddComponent("Bearer", bearerScheme);

            // Tüm operation'lara güvenlik gereksinimi ekle
            var securityRequirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            };

            if (document.Paths is null) return;

            foreach (var operation in document.Paths.Values
                .SelectMany(path => path.Operations ?? []))
            {
                operation.Value.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Value.Security.Add(securityRequirement);
            }
        }
    }
}
