using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using OpenIddict.Abstractions;

namespace OpenIddict.DynamoDB;

public class OpenIddictDynamoDbApplicationStore<TApplication> : IOpenIddictApplicationStore<TApplication>
    where TApplication : OpenIddictDynamoDbApplication
{
    private IAmazonDynamoDB _client;
    private IDynamoDBContext _context;
    private OpenIddictDynamoDbOptions _openIddictDynamoDbOptions;

    public OpenIddictDynamoDbApplicationStore(OpenIddictDynamoDbOptions openIddictDynamoDbOptions)
    {
        if (openIddictDynamoDbOptions.Database is null)
        {
            throw new ArgumentNullException(nameof(openIddictDynamoDbOptions.Database));
        }

        _client = openIddictDynamoDbOptions.Database;
        _context = new DynamoDBContext(_client);
        _openIddictDynamoDbOptions = openIddictDynamoDbOptions;
    }

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var description = await _client.DescribeTableAsync(new DescribeTableRequest
        {
            TableName = Constants.DefaultApplicationTableName,
        });

        return description.Table.ItemCount;
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public async ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _context.SaveAsync(application, cancellationToken);
        await SaveRedirectUris(application, cancellationToken);
    }

    private async Task SaveRedirectUris(TApplication application, CancellationToken cancellationToken)
    {
        if (application.RedirectUris?.Any() == true)
        {
            foreach (var redirectUri in application.RedirectUris)
            {
                var applicationRedirect = new OpenIddictDynamoDbApplicationRedirect
                {
                    RedirectUri = redirectUri,
                    RedirectType = RedirectType.RedirectUri,
                    ApplicationId = application.Id,
                };

                await _context.SaveAsync(applicationRedirect, cancellationToken);
            }
        }

        if (application.PostLogoutRedirectUris?.Any() == true)
        {
            foreach (var redirectUri in application.PostLogoutRedirectUris)
            {
                var applicationRedirect = new OpenIddictDynamoDbApplicationRedirect
                {
                    RedirectUri = redirectUri,
                    RedirectType = RedirectType.PostLogoutRedirectUri,
                    ApplicationId = application.Id,
                };

                await _context.SaveAsync(applicationRedirect, cancellationToken);
            }
        }
    }

    public async ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _context.DeleteAsync(application, cancellationToken);
    }

    public async ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (identifier == null)
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        var search = _context.FromQueryAsync<TApplication>(new QueryOperationConfig
        {
            IndexName = "ClientId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "ClientId = :clientId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":clientId", identifier },
                }
            },
            Limit = 1
        });
        var applications = await search.GetRemainingAsync(cancellationToken);
        var application = applications?.FirstOrDefault();

        if (application != default)
        {
            await SetRedirectUris(application, cancellationToken);
        }

        return application;
    }

    public async ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (identifier == null)
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        var application = await _context.LoadAsync<TApplication>(identifier, cancellationToken);

        if (application != default)
        {
            await SetRedirectUris(application, cancellationToken);
        }

        return application;
    }

    private async Task SetRedirectUris(TApplication application, CancellationToken cancellationToken)
    {
        var applicationId = application.Id;
        var search = _context.FromQueryAsync<OpenIddictDynamoDbApplicationRedirect>(new QueryOperationConfig
        {
            IndexName = "ApplicationId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "ApplicationId = :applicationId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":applicationId", applicationId },
                }
            },
        });
        var applicationRedirects = await search.GetRemainingAsync(cancellationToken);
        application.RedirectUris = applicationRedirects
            .Where(x => x.RedirectType == RedirectType.RedirectUri)
            .Select(x => x.RedirectUri!)
            .ToList();
        application.PostLogoutRedirectUris = applicationRedirects
            .Where(x => x.RedirectType == RedirectType.PostLogoutRedirectUri)
            .Select(x => x.RedirectUri!)
            .ToList();
    }

    public IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new ArgumentException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0143), nameof(address));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var applicationRedirect = await _context.LoadAsync<OpenIddictDynamoDbApplicationRedirect>(
                address, rangeKey: RedirectType.PostLogoutRedirectUri, cancellationToken);

            if (applicationRedirect != default)
            {
                var application = await FindByIdAsync(applicationRedirect.ApplicationId!, cancellationToken);

                if (application != default)
                {
                    yield return application;
                }
            }
        }
    }

    public IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new ArgumentException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0143), nameof(address));
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var applicationRedirect = await _context.LoadAsync<OpenIddictDynamoDbApplicationRedirect>(
                address, rangeKey: RedirectType.RedirectUri, cancellationToken);

            if (applicationRedirect != default)
            {
                var application = await FindByIdAsync(applicationRedirect.ApplicationId!, cancellationToken);

                if (application != default)
                {
                    yield return application;
                }
            }
        }
    }

    public ValueTask<TResult> GetAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.ClientId);
    }

    public ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.ClientSecret);
    }

    public ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.Type);
    }

    public ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.ConsentType);
    }

    public ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.DisplayName);
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (application.DisplayNames is not { Count: > 0 })
        {
            return new(ImmutableDictionary.Create<CultureInfo, string>());
        }

        return new(application.DisplayNames.ToImmutableDictionary(
            pair => CultureInfo.GetCultureInfo(pair.Key),
            pair => pair.Value));
    }

    public ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new(application.Id.ToString());
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (application.Permissions is not { Count: > 0 })
        {
            return new(ImmutableArray.Create<string>());
        }

        return new(application.Permissions.ToImmutableArray());
    }

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (application.PostLogoutRedirectUris is not { Count: > 0 })
        {
            return new(ImmutableArray.Create<string>());
        }

        return new(application.PostLogoutRedirectUris.ToImmutableArray());
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (string.IsNullOrEmpty(application.Properties))
        {
            return new(ImmutableDictionary.Create<string, JsonElement>());
        }

        using var document = JsonDocument.Parse(application.Properties);
        var properties = ImmutableDictionary.CreateBuilder<string, JsonElement>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            properties[property.Name] = property.Value.Clone();
        }

        return new(properties.ToImmutable());
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (application.RedirectUris is not { Count: > 0 })
        {
            return new(ImmutableArray.Create<string>());
        }

        return new(application.RedirectUris.ToImmutableArray());
    }

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (application.Requirements is not { Count: > 0 })
        {
            return new(ImmutableArray.Create<string>());
        }

        return new(application.Requirements.ToImmutableArray());
    }

    public ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new(Activator.CreateInstance<TApplication>());
        }
        catch (MemberAccessException exception)
        {
            return new(Task.FromException<TApplication>(
                new InvalidOperationException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0240), exception)));
        }
    }

    public ConcurrentDictionary<int, string?> ListCursors { get; set; } = new ConcurrentDictionary<int, string?>();
    public IAsyncEnumerable<TApplication> ListAsync(int? count, int? offset, CancellationToken cancellationToken)
    {
        string? initalToken = default;
        if (offset.HasValue)
        {
            ListCursors.TryGetValue(offset.Value, out initalToken);

            if (initalToken == default)
            {
                throw new NotSupportedException("Pagination support is very limited (see documentation)");
            }
        }

        return ExecuteAsync(cancellationToken);

        async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (token, items) = await DynamoDbUtils.Paginate<TApplication>(_client, count, initalToken, cancellationToken);

            if (count.HasValue)
            {
                ListCursors.TryAdd(count.Value + (offset ?? 0), token);
            }

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetClientIdAsync(TApplication application, string? identifier, CancellationToken cancellationToken)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ClientId = identifier;

        return default;
    }

    public ValueTask SetClientSecretAsync(TApplication application, string? secret, CancellationToken cancellationToken)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ClientSecret = secret;

        return default;
    }

    public ValueTask SetClientTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Type = type;

        return default;
    }

    public ValueTask SetConsentTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ConsentType = type;

        return default;
    }

    public ValueTask SetDisplayNameAsync(TApplication application, string? name, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.DisplayName = name;

        return default;
    }

    public ValueTask SetDisplayNamesAsync(TApplication application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (names is not { Count: > 0 })
        {
            application.DisplayNames = null;

            return default;
        }

        application.DisplayNames = names.ToDictionary(x => x.Key.ToString(), x => x.Value);

        return default;
    }

    public ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (permissions.IsDefaultOrEmpty)
        {
            application.Permissions = null;

            return default;
        }

        application.Permissions = permissions.ToList();

        return default;
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(TApplication application, ImmutableArray<string> addresses, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (addresses.IsDefaultOrEmpty)
        {
            application.PostLogoutRedirectUris = null;

            return default;
        }

        application.PostLogoutRedirectUris = addresses.ToList();

        return default;
    }

    public ValueTask SetPropertiesAsync(TApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (properties is not { Count: > 0 })
        {
            application.Properties = null;

            return default;
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        });

        writer.WriteStartObject();

        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Key);
            property.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        application.Properties = Encoding.UTF8.GetString(stream.ToArray());

        return default;
    }

    public ValueTask SetRedirectUrisAsync(TApplication application, ImmutableArray<string> addresses, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (addresses.IsDefaultOrEmpty)
        {
            application.RedirectUris = null;

            return default;
        }

        application.RedirectUris = addresses.ToList();

        return default;
    }

    public ValueTask SetRequirementsAsync(TApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (requirements.IsDefaultOrEmpty)
        {
            application.Requirements = null;

            return default;
        }

        application.Requirements = requirements.ToList();

        return default;
    }

    public async ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
    {
        if (application == null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        // Ensure no one else is updating
        var databaseApplication = await _context.LoadAsync<TApplication>(application.Id, cancellationToken);
        if (databaseApplication == default || databaseApplication.ConcurrencyToken != application.ConcurrencyToken)
        {
            throw new ArgumentException("Given application is invalid", nameof(application));
        }

        application.ConcurrencyToken = Guid.NewGuid().ToString();

        await _context.SaveAsync(application, cancellationToken);

        // Update application redirects
        // Fetch all redirects
        var applicationId = application.Id;
        var search = _context.FromQueryAsync<OpenIddictDynamoDbApplicationRedirect>(new QueryOperationConfig
        {
            IndexName = "ApplicationId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "ApplicationId = :applicationId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":applicationId", applicationId },
                }
            },
        });
        var applicationRedirects = await search.GetRemainingAsync(cancellationToken);

        // Remove previously stored redirects
        if (applicationRedirects.Any())
        {
            var writeRequests = applicationRedirects
                .Select(x => new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "RedirectUri", new AttributeValue { S = x.RedirectUri } },
                            { "RedirectType", new AttributeValue { N = ((int)x.RedirectType).ToString() } },
                        },
                    },
                })
                .ToList();

            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    { Constants.DefaultApplicationRedirectsTableName, writeRequests },
                },
            };

            await _client.BatchWriteItemAsync(request, cancellationToken);
        }

        // Save current redirects
        await SaveRedirectUris(application, cancellationToken);
    }

    public Task EnsureInitializedAsync()
    {
        if (_client == null)
        {
            throw new ArgumentNullException(nameof(_client));
        }

        if (_context == null)
        {
            throw new ArgumentNullException(nameof(_context));
        }

        if (_openIddictDynamoDbOptions.ApplicationsTableName != Constants.DefaultApplicationTableName)
        {
            AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
                _openIddictDynamoDbOptions.ApplicationsTableName,
                Constants.DefaultApplicationTableName));
        }

        if (_openIddictDynamoDbOptions.ApplicationRedirectsTableName != Constants.DefaultApplicationRedirectsTableName)
        {
            AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
                _openIddictDynamoDbOptions.ApplicationRedirectsTableName,
                Constants.DefaultApplicationRedirectsTableName));
        }

        return EnsureInitializedAsync(_client);
    }

    private async Task EnsureInitializedAsync(IAmazonDynamoDB client)
    {
        var applicationGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "ClientId-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("ClientId", KeyType.HASH),
                },
                ProvisionedThroughput = _openIddictDynamoDbOptions.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };
        var applicationRedirectGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "ApplicationId-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("ApplicationId", KeyType.HASH),
                },
                ProvisionedThroughput = _openIddictDynamoDbOptions.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };

        var tableNames = await client.ListTablesAsync();

        if (!tableNames.TableNames.Contains(_openIddictDynamoDbOptions.ApplicationsTableName))
        {
            await CreateApplicationTableAsync(
                client, applicationGlobalSecondaryIndexes);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                client,
                _openIddictDynamoDbOptions.ApplicationsTableName,
                applicationGlobalSecondaryIndexes);
        }

        if (!tableNames.TableNames.Contains(_openIddictDynamoDbOptions.ApplicationRedirectsTableName))
        {
            await CreateApplicationRedirectTableAsync(client, applicationRedirectGlobalSecondaryIndexes);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                client,
                _openIddictDynamoDbOptions.ApplicationRedirectsTableName,
                applicationRedirectGlobalSecondaryIndexes);
        }
    }

    private async Task CreateApplicationTableAsync(IAmazonDynamoDB client, List<GlobalSecondaryIndex>? globalSecondaryIndexes = default)
    {
        var response = await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _openIddictDynamoDbOptions.ApplicationsTableName,
            ProvisionedThroughput = _openIddictDynamoDbOptions.ProvisionedThroughput,
            BillingMode = _openIddictDynamoDbOptions.BillingMode,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "Id",
                    KeyType = KeyType.HASH,
                },
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = "Id",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "ClientId",
                    AttributeType = ScalarAttributeType.S,
                },
            },
            GlobalSecondaryIndexes = globalSecondaryIndexes,
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Couldn't create table {_openIddictDynamoDbOptions.ApplicationsTableName}");
        }

        await DynamoDbUtils.WaitForActiveTableAsync(client, _openIddictDynamoDbOptions.ApplicationsTableName);
    }

    private async Task CreateApplicationRedirectTableAsync(IAmazonDynamoDB client,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes = default)
    {
        var response = await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _openIddictDynamoDbOptions.ApplicationRedirectsTableName,
            ProvisionedThroughput = _openIddictDynamoDbOptions.ProvisionedThroughput,
            BillingMode = _openIddictDynamoDbOptions.BillingMode,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "RedirectUri",
                    KeyType = KeyType.HASH,
                },
                new KeySchemaElement
                {
                    AttributeName = "RedirectType",
                    KeyType = KeyType.RANGE,
                },
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = "RedirectUri",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "RedirectType",
                    AttributeType = ScalarAttributeType.N,
                },
                new AttributeDefinition
                {
                    AttributeName = "ApplicationId",
                    AttributeType = ScalarAttributeType.S,
                },
            },
            GlobalSecondaryIndexes = globalSecondaryIndexes,
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Couldn't create table {_openIddictDynamoDbOptions.ApplicationRedirectsTableName}");
        }

        await DynamoDbUtils.WaitForActiveTableAsync(client, _openIddictDynamoDbOptions.ApplicationRedirectsTableName);
    }
}