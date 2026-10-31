Imports System.Threading.RateLimiting
Imports BankTeller.Api.Data
Imports BankTeller.Api.Security
Imports BankTeller.Api.Services
Imports Microsoft.AspNetCore.Authentication.JwtBearer
Imports Microsoft.AspNetCore.Diagnostics
Imports Microsoft.AspNetCore.RateLimiting
Imports Microsoft.IdentityModel.Tokens

Public Class Program

    Public Shared Sub Main(args As String())
        BuildApp(args).Run()
    End Sub

    Public Shared Function BuildApp(args As String()) As WebApplication
        Dim builder = WebApplication.CreateBuilder(args)
        ConfigureServices(builder)
        Dim app = builder.Build()
        ConfigurePipeline(app)
        Return app
    End Function

    Private Shared Sub ConfigureServices(builder As WebApplicationBuilder)
        Dim config = builder.Configuration

        Dim bankOptions As New BankOptions()
        config.GetSection("Bank").Bind(bankOptions)

        Dim jwt As New JwtOptions()
        config.GetSection("Jwt").Bind(jwt)
        If String.IsNullOrWhiteSpace(jwt.Key) Then
            If Not builder.Environment.IsDevelopment() Then
                Throw New InvalidOperationException("Set a signing key of at least 32 characters in Jwt:Key (e.g. the Jwt__Key environment variable).")
            End If
            ' Development only: a random key per run. Tokens stop working when the API restarts.
            jwt.Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
        End If
        If jwt.Key.Length < 32 Then Throw New InvalidOperationException("Jwt:Key must be at least 32 characters.")

        Dim connectionString = If(config.GetConnectionString("Bank"), "Data Source=bank.db")

        builder.Services.AddSingleton(TimeProvider.System)
        builder.Services.AddSingleton(bankOptions)
        builder.Services.AddSingleton(jwt)
        builder.Services.AddSingleton(New Database(connectionString))
        builder.Services.AddSingleton(Of TokenService)()
        builder.Services.AddSingleton(Of AuditLog)()
        builder.Services.AddSingleton(Of AuthService)()
        builder.Services.AddSingleton(Of AccountService)()
        builder.Services.AddSingleton(Of DemoSeeder)()

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
            Sub(options)
                options.MapInboundClaims = False
                options.TokenValidationParameters = New TokenValidationParameters With {
                    .ValidIssuer = jwt.Issuer,
                    .ValidAudience = jwt.Audience,
                    .IssuerSigningKey = jwt.SigningKey(),
                    .ValidateIssuerSigningKey = True,
                    .ClockSkew = TimeSpan.FromSeconds(30),
                    .NameClaimType = "unique_name",
                    .RoleClaimType = "role"
                }
            End Sub)

        ' Check token lifetimes against the app's TimeProvider, the same clock that
        ' issued them, instead of the system clock (keeps tests deterministic too).
        builder.Services.AddOptions(Of JwtBearerOptions)(JwtBearerDefaults.AuthenticationScheme).Configure(Of TimeProvider)(
            Sub(options, clock)
                options.TokenValidationParameters.LifetimeValidator =
                    Function(notBefore, expires, token, parameters)
                        Dim now = clock.GetUtcNow().UtcDateTime
                        Return (Not notBefore.HasValue OrElse notBefore.Value <= now + parameters.ClockSkew) AndAlso
                               expires.HasValue AndAlso expires.Value > now - parameters.ClockSkew
                    End Function
            End Sub)

        builder.Services.AddAuthorizationBuilder() _
            .SetFallbackPolicy(New AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) _
            .AddPolicy("Supervisor", Function(policy) policy.RequireRole(Contracts.Roles.Supervisor))

        ' Slows down password guessing from one address, on top of per-user lockout.
        builder.Services.AddRateLimiter(
            Sub(options)
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests
                options.AddPolicy("login",
                    Function(context) RateLimitPartition.GetFixedWindowLimiter(
                        If(context.Connection.RemoteIpAddress?.ToString(), "unknown"),
                        Function(key) New FixedWindowRateLimiterOptions With {.PermitLimit = 10, .Window = TimeSpan.FromMinutes(1)}))
            End Sub)

        builder.Services.AddProblemDetails()
        ' Malformed request bodies get the same problem shape (with a code) as rule violations.
        builder.Services.AddControllers().ConfigureApiBehaviorOptions(
            Sub(options)
                options.InvalidModelStateResponseFactory =
                    Function(context)
                        Dim problem As New ProblemDetails With {
                            .Status = StatusCodes.Status400BadRequest,
                            .Title = "The request could not be read. Check that every field has a valid value.",
                            .Type = "https://httpstatuses.io/400"
                        }
                        problem.Extensions("code") = Services.ErrorCodes.InvalidRequest
                        Return New BadRequestObjectResult(problem) With {.ContentTypes = New Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection From {"application/problem+json"}}
                    End Function
            End Sub)
        builder.Services.AddOpenApi()
    End Sub

    Private Shared Sub ConfigurePipeline(app As WebApplication)
        app.Services.GetRequiredService(Of Database)().EnsureCreated()
        If app.Configuration.GetValue(Of Boolean)("Seed:Enabled") Then
            app.Services.GetRequiredService(Of DemoSeeder)().SeedIfEmpty(app.Configuration("Seed:DemoPassword"))
        End If

        ' Rule violations become RFC 7807 problem responses with a machine-readable code;
        ' anything unexpected is logged and returned as a generic 500 with no internals.
        app.UseExceptionHandler(
            Sub(errorApp)
                errorApp.Run(
                    Async Function(context)
                        Dim failure = context.Features.Get(Of IExceptionHandlerFeature)()?.Error
                        Dim bank = TryCast(failure, BankException)
                        Dim status = If(bank IsNot Nothing, bank.Status, StatusCodes.Status500InternalServerError)
                        If bank Is Nothing Then
                            context.RequestServices.GetRequiredService(Of ILoggerFactory)() _
                                .CreateLogger("BankTeller.Api").LogError(failure, "Unhandled error on {Path}", context.Request.Path)
                        End If

                        context.Response.StatusCode = status
                        Dim problem As New Microsoft.AspNetCore.Mvc.ProblemDetails With {
                            .Status = status,
                            .Title = If(bank IsNot Nothing, bank.Message, "Something went wrong on the server. The transaction was not completed."),
                            .Type = $"https://httpstatuses.io/{status}"
                        }
                        problem.Extensions("code") = If(bank?.Code, "server_error")
                        Await context.Response.WriteAsJsonAsync(problem, options:=Nothing, contentType:="application/problem+json")
                    End Function)
            End Sub)

        If Not app.Environment.IsDevelopment() Then
            app.UseHsts()
            app.UseHttpsRedirection()
        Else
            app.MapOpenApi().AllowAnonymous()
        End If

        app.UseRateLimiter()
        app.UseAuthentication()
        app.UseAuthorization()
        app.MapControllers()
        app.MapGet("/health", Function() Results.Ok(New With {.status = "ok"})).AllowAnonymous()
    End Sub

End Class
