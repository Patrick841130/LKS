using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LksBrothers.Explorer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SwaggerController : ControllerBase
{
    private readonly ILogger<SwaggerController> _logger;

    public SwaggerController(ILogger<SwaggerController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get API documentation in OpenAPI/Swagger format
    /// </summary>
    /// <returns>OpenAPI specification</returns>
    [HttpGet("openapi")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetOpenApiSpec()
    {
        var openApiSpec = new
        {
            openapi = "3.0.1",
            info = new
            {
                title = "LKS NETWORK API",
                version = "1.0.0",
                description = "Enterprise-grade blockchain explorer and payment API for LKS NETWORK",
                contact = new
                {
                    name = "LKS NETWORK Support",
                    url = "https://lksnetwork.com/support",
                    email = "support@lksnetwork.com"
                },
                license = new
                {
                    name = "MIT",
                    url = "https://opensource.org/licenses/MIT"
                }
            },
            servers = new[]
            {
                new { url = "https://api.lksnetwork.com", description = "Production server" },
                new { url = "https://api-staging.lksnetwork.com", description = "Staging server" },
                new { url = "http://localhost:5000", description = "Development server" }
            },
            paths = GetApiPaths(),
            components = GetApiComponents(),
            security = new[]
            {
                new { BearerAuth = new string[0] }
            }
        };

        return Ok(openApiSpec);
    }

    /// <summary>
    /// Get API health status for documentation
    /// </summary>
    /// <returns>API health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetApiHealth()
    {
        return Ok(new
        {
            status = "healthy",
            version = "1.0.0",
            documentation = new
            {
                swagger_ui = "/swagger",
                openapi_spec = "/api/swagger/openapi",
                postman_collection = "/api/swagger/postman"
            },
            endpoints = new
            {
                authentication = "/api/user/*",
                payments = "/api/payment/*",
                admin = "/api/admin/*",
                security = "/api/security/*",
                explorer = "/api/explorer/*"
            }
        });
    }

    /// <summary>
    /// Get Postman collection for API testing
    /// </summary>
    /// <returns>Postman collection JSON</returns>
    [HttpGet("postman")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetPostmanCollection()
    {
        var collection = new
        {
            info = new
            {
                name = "LKS NETWORK API",
                description = "Complete API collection for LKS NETWORK blockchain explorer and payment system",
                version = "1.0.0",
                schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            auth = new
            {
                type = "bearer",
                bearer = new[]
                {
                    new { key = "token", value = "{{jwt_token}}", type = "string" }
                }
            },
            variable = new[]
            {
                new { key = "base_url", value = "https://api.lksnetwork.com", type = "string" },
                new { key = "jwt_token", value = "", type = "string" }
            },
            item = GetPostmanItems()
        };

        return Ok(collection);
    }

    private object GetApiPaths()
    {
        return new
        {
            // Authentication endpoints
            ["/api/user/register"] = new
            {
                post = new
                {
                    tags = new[] { "Authentication" },
                    summary = "Register new user",
                    requestBody = new
                    {
                        required = true,
                        content = new
                        {
                            ["application/json"] = new
                            {
                                schema = new { @ref = "#/components/schemas/RegisterRequest" }
                            }
                        }
                    },
                    responses = new
                    {
                        ["201"] = new { description = "User registered successfully" },
                        ["400"] = new { description = "Invalid input data" },
                        ["409"] = new { description = "Email already exists" }
                    }
                }
            },
            ["/api/user/login"] = new
            {
                post = new
                {
                    tags = new[] { "Authentication" },
                    summary = "User login",
                    requestBody = new
                    {
                        required = true,
                        content = new
                        {
                            ["application/json"] = new
                            {
                                schema = new { @ref = "#/components/schemas/LoginRequest" }
                            }
                        }
                    },
                    responses = new
                    {
                        ["200"] = new { description = "Login successful", content = new { ["application/json"] = new { schema = new { @ref = "#/components/schemas/LoginResponse" } } } },
                        ["401"] = new { description = "Invalid credentials" }
                    }
                }
            },
            // Payment endpoints
            ["/api/payment/send-xrp"] = new
            {
                post = new
                {
                    tags = new[] { "Payments" },
                    summary = "Send XRP payment",
                    security = new[] { new { BearerAuth = new string[0] } },
                    requestBody = new
                    {
                        required = true,
                        content = new
                        {
                            ["application/json"] = new
                            {
                                schema = new { @ref = "#/components/schemas/XRPPaymentRequest" }
                            }
                        }
                    },
                    responses = new
                    {
                        ["200"] = new { description = "Payment sent successfully" },
                        ["400"] = new { description = "Invalid payment parameters" },
                        ["402"] = new { description = "Insufficient funds" }
                    }
                }
            },
            ["/api/payment/balance/{address}"] = new
            {
                get = new
                {
                    tags = new[] { "Payments" },
                    summary = "Get XRP balance",
                    parameters = new[]
                    {
                        new
                        {
                            name = "address",
                            @in = "path",
                            required = true,
                            schema = new { type = "string" },
                            description = "XRP wallet address"
                        }
                    },
                    responses = new
                    {
                        ["200"] = new { description = "Balance retrieved successfully" },
                        ["404"] = new { description = "Address not found" }
                    }
                }
            }
        };
    }

    private object GetApiComponents()
    {
        return new
        {
            schemas = new
            {
                RegisterRequest = new
                {
                    type = "object",
                    required = new[] { "email", "password", "firstName", "lastName" },
                    properties = new
                    {
                        email = new { type = "string", format = "email" },
                        password = new { type = "string", minLength = 8 },
                        firstName = new { type = "string" },
                        lastName = new { type = "string" },
                        walletAddress = new { type = "string" }
                    }
                },
                LoginRequest = new
                {
                    type = "object",
                    required = new[] { "email", "password" },
                    properties = new
                    {
                        email = new { type = "string", format = "email" },
                        password = new { type = "string" }
                    }
                },
                LoginResponse = new
                {
                    type = "object",
                    properties = new
                    {
                        success = new { type = "boolean" },
                        token = new { type = "string" },
                        expiresAt = new { type = "string", format = "date-time" },
                        user = new { @ref = "#/components/schemas/User" }
                    }
                },
                User = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        email = new { type = "string" },
                        firstName = new { type = "string" },
                        lastName = new { type = "string" },
                        role = new { type = "string", @enum = new[] { "User", "Admin", "Validator" } }
                    }
                },
                XRPPaymentRequest = new
                {
                    type = "object",
                    required = new[] { "sourceAddress", "destinationAddress", "amount" },
                    properties = new
                    {
                        sourceAddress = new { type = "string" },
                        destinationAddress = new { type = "string" },
                        amount = new { type = "string" },
                        destinationTag = new { type = "integer" },
                        memo = new { type = "string" }
                    }
                }
            },
            securitySchemes = new
            {
                BearerAuth = new
                {
                    type = "http",
                    scheme = "bearer",
                    bearerFormat = "JWT"
                }
            }
        };
    }

    private object[] GetPostmanItems()
    {
        return new[]
        {
            new
            {
                name = "Authentication",
                item = new[]
                {
                    new
                    {
                        name = "Register User",
                        request = new
                        {
                            method = "POST",
                            header = new[]
                            {
                                new { key = "Content-Type", value = "application/json" }
                            },
                            body = new
                            {
                                mode = "raw",
                                raw = "{\n  \"email\": \"user@example.com\",\n  \"password\": \"SecurePassword123!\",\n  \"firstName\": \"John\",\n  \"lastName\": \"Doe\",\n  \"walletAddress\": \"rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH\"\n}"
                            },
                            url = new
                            {
                                raw = "{{base_url}}/api/user/register",
                                host = new[] { "{{base_url}}" },
                                path = new[] { "api", "user", "register" }
                            }
                        }
                    },
                    new
                    {
                        name = "Login User",
                        request = new
                        {
                            method = "POST",
                            header = new[]
                            {
                                new { key = "Content-Type", value = "application/json" }
                            },
                            body = new
                            {
                                mode = "raw",
                                raw = "{\n  \"email\": \"user@example.com\",\n  \"password\": \"SecurePassword123!\"\n}"
                            },
                            url = new
                            {
                                raw = "{{base_url}}/api/user/login",
                                host = new[] { "{{base_url}}" },
                                path = new[] { "api", "user", "login" }
                            }
                        }
                    }
                }
            },
            new
            {
                name = "Payments",
                item = new[]
                {
                    new
                    {
                        name = "Send XRP Payment",
                        request = new
                        {
                            method = "POST",
                            header = new[]
                            {
                                new { key = "Authorization", value = "Bearer {{jwt_token}}" },
                                new { key = "Content-Type", value = "application/json" }
                            },
                            body = new
                            {
                                mode = "raw",
                                raw = "{\n  \"sourceAddress\": \"rSourceAddress123\",\n  \"destinationAddress\": \"rDestinationAddress123\",\n  \"amount\": \"10.5\",\n  \"memo\": \"Payment for services\"\n}"
                            },
                            url = new
                            {
                                raw = "{{base_url}}/api/payment/send-xrp",
                                host = new[] { "{{base_url}}" },
                                path = new[] { "api", "payment", "send-xrp" }
                            }
                        }
                    },
                    new
                    {
                        name = "Get XRP Balance",
                        request = new
                        {
                            method = "GET",
                            url = new
                            {
                                raw = "{{base_url}}/api/payment/balance/rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH",
                                host = new[] { "{{base_url}}" },
                                path = new[] { "api", "payment", "balance", "rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH" }
                            }
                        }
                    }
                }
            }
        };
    }
}
