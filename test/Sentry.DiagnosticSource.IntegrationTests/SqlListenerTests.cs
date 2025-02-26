using Sentry.Testing;

namespace Sentry.DiagnosticSource.IntegrationTests;

[UsesVerify]
public class SqlListenerTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;
    private readonly TestOutputDiagnosticLogger _logger;

    public SqlListenerTests(LocalDbFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _logger = new TestOutputDiagnosticLogger(output);
    }

#if !NETFRAMEWORK
    [SkippableFact]
    public async Task RecordsSql()
    {
        Skip.If(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        var transport = new RecordingTransport();
        var options = new SentryOptions
        {
            TracesSampleRate = 1,
            Transport = transport,
            Dsn = ValidDsn,
            DiagnosticLogger = _logger,
            Debug = true
        };

#if NET6_0_OR_GREATER
        await using var database = await _fixture.SqlInstance.Build();
#else
        using var database = await _fixture.SqlInstance.Build();
#endif
        using (var hub = new Hub(options))
        {
            var transaction = hub.StartTransaction("my transaction", "my operation");
            hub.ConfigureScope(scope => scope.Transaction = transaction);
            hub.CaptureException(new("my exception"));
            await TestDbBuilder.AddDataAsync(database);
            await TestDbBuilder.GetDataAsync(database);
            transaction.Finish();
        }

        var result = await Verify(transport.Payloads)
            .IgnoreMember<IEventLike>(_ => _.Environment)
            .IgnoreStandardSentryMembers();
        Assert.DoesNotContain("SHOULD NOT APPEAR IN PAYLOAD", result.Text);
    }
#endif

#if NET6_0_OR_GREATER
    [SkippableFact]
    public async Task Logging()
    {
        _logger.LogDebug(RelationalEventId.CommandError.Name!);

        Skip.If(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        var transport = new RecordingTransport();

        void ApplyOptions(SentryLoggingOptions sentryOptions)
        {
            sentryOptions.TracesSampleRate = 1;
            sentryOptions.Transport = transport;
            sentryOptions.Dsn = ValidDsn;
            sentryOptions.DiagnosticLogger = _logger;
            sentryOptions.Debug = true;
        }

        var options = new SentryLoggingOptions();
        ApplyOptions(options);

        var loggerFactory = LoggerFactory.Create(_ => _.AddSentry(ApplyOptions));

#if NET6_0_OR_GREATER
        await using var database = await _fixture.SqlInstance.Build();
#else
        using var database = await _fixture.SqlInstance.Build();
#endif
        var builder = new DbContextOptionsBuilder<TestDbContext>();
        builder.UseSqlServer(database);
        builder.UseLoggerFactory(loggerFactory);
        builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        await using var dbContext = new TestDbContext(builder.Options);
        dbContext.Add(new TestEntity
        {
            Property = "Value"
        });

        await dbContext.SaveChangesAsync();

        using (var hub = new Hub(options))
        {
            var transaction = hub.StartTransaction("my transaction", "my operation");
            hub.ConfigureScope(scope => scope.Transaction = transaction);

            dbContext.Add(new TestEntity
            {
                Property = "Value"
            });

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch
            {
                // Suppress the exception so we can test that we received the error through logging.
                // Note, this uses the Sentry.Extensions.Logging integration.
            }

            transaction.Finish();
        }

        var result = await Verify(transport.Payloads)
            .ScrubInlineGuids()
            .IgnoreMember<SentryEvent>(_ => _.SentryThreads)
            .IgnoreMember<IEventLike>(_ => _.Environment)
            .ScrubLinesWithReplace(line =>
            {
                if (line.StartsWith("Executed DbCommand ("))
                {
                    return "Executed DbCommand";
                }

                if (line.StartsWith("Failed executing DbCommand ("))
                {
                    return "Failed executing DbCommand";
                }

                var efVersion = typeof(DbContext).Assembly.GetName().Version!.ToString(3);
                return line.Replace(efVersion, "");
            })
            .IgnoreStandardSentryMembers()
            .UniqueForRuntimeAndVersion();
        Assert.DoesNotContain("An error occurred while saving the entity changes", result.Text);
    }
#endif

    [Fact]
    public void ShouldIgnoreAllErrorAndExceptionIds()
    {
        var eventIds = typeof(CoreEventId).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.FieldType == typeof(EventId))
            .ToList();
        Assert.NotEmpty(eventIds);
        foreach (var field in eventIds)
        {
            var eventId = (EventId)field.GetValue(null)!;
            var isEfExceptionMessage = SentryLogger.IsEfExceptionMessage(eventId);
            var name = field.Name;
            if (name.EndsWith("Exception") ||
                name.EndsWith("Error") ||
                name.EndsWith("Failed"))
            {
                Assert.True(isEfExceptionMessage, eventId.Name);
            }
            else
            {
                Assert.False(isEfExceptionMessage, eventId.Name);
            }
        }
    }

    [SkippableFact]
    public async Task RecordsEf()
    {
        Skip.If(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        var transport = new RecordingTransport();
        var options = new SentryOptions
        {
            TracesSampleRate = 1,
            Transport = transport,
            Dsn = ValidDsn,
            DiagnosticLogger = _logger,
            Debug = true
        };

#if NETFRAMEWORK
        options.AddDiagnosticSourceIntegration();
#endif

#if NET6_0_OR_GREATER
        await using var database = await _fixture.SqlInstance.Build();
#else
        using var database = await _fixture.SqlInstance.Build();
#endif
        using (var hub = new Hub(options))
        {
            var transaction = hub.StartTransaction("my transaction", "my operation");
            hub.ConfigureScope(scope => scope.Transaction = transaction);
            hub.CaptureException(new("my exception"));
            await TestDbBuilder.AddEfDataAsync(database);
            await TestDbBuilder.GetEfDataAsync(database);
            transaction.Finish();
        }

        var result = await Verify(transport.Payloads)
            .IgnoreMember<IEventLike>(_ => _.Environment)
            .IgnoreStandardSentryMembers()
            .UniqueForRuntimeAndVersion();
        Assert.DoesNotContain("SHOULD NOT APPEAR IN PAYLOAD", result.Text);
    }
}
