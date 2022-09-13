using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace OpenIddict.DynamoDB.Tests;

[Collection("Sequential")]
public class OpenIddictDynamoDbTokenStoreResolverTests
{
    [Fact]
    public void Should_ReturnTokenStore_When_ItHasBeenRegistered()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<
                IOpenIddictTokenStore<OpenIddictDynamoDbToken>,
                OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>>();
            serviceCollection.AddSingleton<IAmazonDynamoDB>(database.Client);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var resolver = new OpenIddictDynamoDbTokenStoreResolver(serviceProvider);

            // Act
            var store = resolver.Get<OpenIddictDynamoDbToken>();

            // Assert
            Assert.NotNull(store);
        }
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_ServiceProviderIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenIddictDynamoDbTokenStoreResolver(null!));
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_NoImplementationHasBeenRegistered()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var resolver = new OpenIddictDynamoDbTokenStoreResolver(serviceProvider);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            resolver.Get<OpenIddictDynamoDbToken>());
    }
}