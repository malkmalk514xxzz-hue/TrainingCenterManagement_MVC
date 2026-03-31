using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

public class JwtBearerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // تعريف Security Scheme
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "أدخل التوكن هكذا: Bearer {your-jwt-token}"
        };

        if (!swaggerDoc.Components.SecuritySchemes.ContainsKey("Bearer"))
        {
            swaggerDoc.Components.SecuritySchemes.Add("Bearer", securityScheme);
        }

        // لكل Operation في Swagger
        foreach (var pathItem in swaggerDoc.Paths.Values)
        {
            foreach (var operation in pathItem.Operations.Values)
            {
                // نحصل على ApiDescription المقابلة لهذا الـ Operation
                var apiDescription = context.ApiDescriptions
                    .FirstOrDefault(desc =>
                        desc.ActionDescriptor?.EndpointMetadata != null &&
                        desc.ActionDescriptor.EndpointMetadata
                            .OfType<AuthorizeAttribute>()
                            .Any(attr =>
                                string.IsNullOrEmpty(attr.AuthenticationSchemes) ||
                                attr.AuthenticationSchemes.Contains(
                                    JwtBearerDefaults.AuthenticationScheme,
                                    StringComparison.OrdinalIgnoreCase)));

                if (apiDescription != null)
                {
                    // إضافة Security Requirement
                    operation.Security = new List<OpenApiSecurityRequirement>
                    {
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
                                new List<string>()
                            }
                        }
                    };
                }
            }
        }
    }
}