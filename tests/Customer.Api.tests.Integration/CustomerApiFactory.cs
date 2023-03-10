using System.Data.Common;
using Customers.Api;
using Customers.Api.Database;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;

namespace template.integration.tests;

public class CustomerApiFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private DbConnection _dbConnection = default!;
    private Respawner _respawner = default!;

    public HttpClient HttpClient { get; private set; } = default!;
    // can be any image! 

    // Collection!
    // private int port = Random.Shared.next(10000,60000); kinda hacky..

    // private readonly DockerContainer _postgresDatabaseContainer = new ContainerBuilder<DockerContainer>()
    //     .WithImage("postgres:11-alpine")
    //     .WithEnvironment("POSTGRES_USER", "course")
    //     .WithEnvironment("POSTGRES_PASSWORD", "changeme")
    //     .WithEnvironment("POSTGRES_DB", "mydb")
    //     .WithPortBinding(5555, 5432)
    //     .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
    //     .Build();

    // Multiple.
    private readonly TestcontainerDatabase _dbContainer = new ContainerBuilder<PostgreSqlTestcontainer>()
        .WithDatabase(new PostgreSqlTestcontainerConfiguration
        {
            Database = "mydb",
            Username = "course",
            Password = "changeme"
        })
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // Remove all logging
        });

        builder.ConfigureTestServices(collection =>
        {
            // Remove the one added by Program.
            collection.RemoveAll(typeof(IDbConnectionFactory));
            
            collection.AddSingleton<IDbConnectionFactory>(_ =>
                new NpgsqlConnectionFactory(_dbContainer.ConnectionString));

            // collection.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(
            //     "Server=localhost;Port=5555;Database=mydb;User ID=course;Password=changeme;"
            // ));
        });
    }

    public async Task ResetDatabase()
    {
        await _respawner.ResetAsync(_dbConnection);
    }
    
    public async Task InitializeAsync()
    {
        // await _postgresDatabaseContainer.StartAsync();
        await _dbContainer.StartAsync();

        _dbConnection = new NpgsqlConnection(_dbContainer.ConnectionString);
        HttpClient = CreateClient();

        await _dbConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" }
        });
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        // await _postgresDatabaseContainer.DisposeAsync();
    }
}