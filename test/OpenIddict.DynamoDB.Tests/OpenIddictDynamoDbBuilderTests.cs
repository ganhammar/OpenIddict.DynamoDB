using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Core;
using Xunit;

namespace OpenIddict.DynamoDB.Tests;

public class OpenIddictDynamoDbBuilderTests
{
    [Fact]
    public void Should_ThrowException_When_ServicesIsNullInConstructor()
    {
        // Arrange
        var services = (IServiceCollection) null!;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new OpenIddictDynamoDbBuilder(services));
        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void Should_ThrowException_When_TryingToConfigureAndActionIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).Configure(null!));
        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public void Should_ThrowException_When_SetApplicationsTableNameAndNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetApplicationsTableName(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Should_SetTableName_When_CallingSetApplicationsTableName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetApplicationsTableName("test");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal("test", options.ApplicationsTableName);
    }

    [Fact]
    public void Should_ThrowException_When_SetApplicationRedirectsTableNameAndNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetApplicationRedirectsTableName(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Should_SetTableName_When_CallingSetApplicationRedirectsTableName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetApplicationRedirectsTableName("test");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal("test", options.ApplicationRedirectsTableName);
    }

    [Fact]
    public void Should_ThrowException_When_SetAuthorizationsTableNameAndNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetAuthorizationsTableName(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Should_SetTableName_When_CallingSetAuthorizationsTableName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetAuthorizationsTableName("test");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal("test", options.AuthorizationsTableName);
    }

    [Fact]
    public void Should_ThrowException_When_SetTokensTableNameAndNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetTokensTableName(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Should_SetTableName_When_CallingSetTokensTableName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetTokensTableName("test");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal("test", options.TokensTableName);
    }

    [Fact]
    public void Should_ThrowException_When_SetScopesTableNameAndNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetScopesTableName(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Should_SetTableName_When_CallingSetScopesTableName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetScopesTableName("test");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal("test", options.ScopesTableName);
    }

    [Fact]
    public void Should_Succeed_When_ReplacingApplicationEntity()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).ReplaceDefaultApplicationEntity<CustomApplication>();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

        Assert.Equal(typeof(CustomApplication), options.DefaultApplicationType);
    }

    [Fact]
    public void Should_Succeed_When_ReplacingAuthorizationEntity()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).ReplaceDefaultAuthorizationEntity<CustomAuthorization>();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

        Assert.Equal(typeof(CustomAuthorization), options.DefaultAuthorizationType);
    }

    [Fact]
    public void Should_Succeed_When_ReplacingScopeEntity()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).ReplaceDefaultScopeEntity<CustomScope>();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

        Assert.Equal(typeof(CustomScope), options.DefaultScopeType);
    }

    [Fact]
    public void Should_Succeed_When_ReplacingTokenEntity()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).ReplaceDefaultTokenEntity<CustomToken>();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

        Assert.Equal(typeof(CustomToken), options.DefaultTokenType);
    }

    [Fact]
    public void Should_ThrowException_When_CallingUseDatabaseAndDatabaseIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).UseDatabase(null!));
        Assert.Equal("database", exception.ParamName);
    }

    [Fact]
    public void Should_SetDatabase_When_CallingUseDatabase()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            CreateBuilder(services).UseDatabase(database.Client);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
            Assert.Equal(database.Client, options.Database);
        }
    }

    [Fact]
    public void Should_ThrowException_When_SettingBillingModeAndBillingModeIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetBillingMode(null!));
        Assert.Equal("billingMode", exception.ParamName);
    }

    [Fact]
    public void Should_SetBillingMode_When_CallingSetBillingMode()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        CreateBuilder(services).SetBillingMode(BillingMode.PROVISIONED);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal(BillingMode.PROVISIONED, options.BillingMode);
    }
    
    [Fact]
    public void Should_ThrowException_When_SettingProvisionedThroughputAndProvisionedThroughputIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateBuilder(services).SetProvisionedThroughput(null!));
        Assert.Equal("provisionedThroughput", exception.ParamName);
    }

    [Fact]
    public void Should_SetProvisionedThroughput_When_CallingSetProvisionedThroughput()
    {
        // Arrange
        var services = new ServiceCollection();
        var throughput = new ProvisionedThroughput
        {
            ReadCapacityUnits = 99,
            WriteCapacityUnits = 99,
        };

        // Act
        CreateBuilder(services).SetProvisionedThroughput(throughput);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIddictDynamoDbOptions>>().CurrentValue;
        Assert.Equal(throughput, options.ProvisionedThroughput);
    }

    private static OpenIddictDynamoDbBuilder CreateBuilder(IServiceCollection services)
        => services.AddOpenIddict().AddCore().UseDynamoDb();

    public class CustomApplication : OpenIddictDynamoDbApplication { }
    public class CustomAuthorization : OpenIddictDynamoDbAuthorization { }
    public class CustomScope : OpenIddictDynamoDbScope { }
    public class CustomToken : OpenIddictDynamoDbToken { }
}