using Sentry.Testing;

namespace Sentry.Tests.Protocol;

public class SentryMessageTests
{
    private readonly IDiagnosticLogger _testOutputLogger;

    public SentryMessageTests(ITestOutputHelper output)
    {
        _testOutputLogger = new TestOutputDiagnosticLogger(output);
    }

    [Fact]
    public void SerializeObject_AllPropertiesSetToNonDefault_SerializesValidObject()
    {
        var sut = new SentryMessage
        {
            Message = "Message {eventId} {name}",
            Params = new object[] { 100, "test-name" },
            Formatted = "Message 100 test-name"
        };

        var actual = sut.ToJsonString(_testOutputLogger);

        Assert.Equal(
            "{\"message\":\"Message {eventId} {name}\"," +
            "\"params\":[100,\"test-name\"]," +
            "\"formatted\":\"Message 100 test-name\"}",
            actual);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SerializeObject_TestCase_SerializesAsExpected((SentryMessage msg, string serialized) @case)
    {
        var actual = @case.msg.ToJsonString(_testOutputLogger);

        Assert.Equal(@case.serialized, actual);
    }

    public static IEnumerable<object[]> TestCases()
    {
        yield return new object[] { (new SentryMessage(), "{}") };
        yield return new object[] { (new SentryMessage { Message = "some message" }, "{\"message\":\"some message\"}") };
        yield return new object[] { (new SentryMessage { Params = new[] { "param" } }, "{\"params\":[\"param\"]}") };
        yield return new object[] { (new SentryMessage { Formatted = "some formatted" }, "{\"formatted\":\"some formatted\"}") };
    }
}
