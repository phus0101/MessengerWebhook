using FluentAssertions;
using MessengerWebhook.Services.Observability;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace MessengerWebhook.UnitTests.Services.Observability;

/// <summary>
/// Unit tests for PiiRedactingEnricher — Serilog enricher that auto-redacts PII from string log properties.
/// </summary>
public class PiiRedactingEnricherTests
{
    private readonly PiiRedactingEnricher _enricher = new();

    private LogEvent CreateLogEvent(Dictionary<string, LogEventPropertyValue> properties)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse("Test message");
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            template,
            properties.Select(kvp => new LogEventProperty(kvp.Key, kvp.Value))
        );
    }

    private PropertyFactory CreatePropertyFactory()
    {
        return new PropertyFactory();
    }

    [Fact]
    public void Enrich_StringPropertyWithPhone_RedactsPhone()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "UserMessage", new ScalarValue("Call me at 0912345678") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var userMessageProp = logEvent.Properties["UserMessage"];
        userMessageProp.Should().NotBeNull();
        userMessageProp.Should().BeOfType<ScalarValue>();
        var value = ((ScalarValue)userMessageProp).Value?.ToString();
        value.Should().Contain("091***5678");
        value.Should().NotContain("0912345678");
    }

    [Fact]
    public void Enrich_StringPropertyWithAddress_RedactsAddress()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "CustomerAddress", new ScalarValue("Phường 1, Quận 1, TP. Hồ Chí Minh") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var addressProp = logEvent.Properties["CustomerAddress"];
        var value = ((ScalarValue)addressProp).Value?.ToString();
        value.Should().Contain("[address]");
    }

    [Fact]
    public void Enrich_StringPropertyWithBothPhoneAndAddress_RedactsBoth()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Notes", new ScalarValue("Customer 0912345678 lives at Phường 1, Quận 1") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var notesProp = logEvent.Properties["Notes"];
        var value = ((ScalarValue)notesProp).Value?.ToString();
        value.Should().Contain("091***5678");
        value.Should().Contain("[address]");
    }

    [Fact]
    public void Enrich_StringPropertyWithoutSensitiveData_DoesNotModify()
    {
        // Arrange
        var originalMessage = "This is a normal message";
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Message", new ScalarValue(originalMessage) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var messageProp = logEvent.Properties["Message"];
        var value = ((ScalarValue)messageProp).Value?.ToString();
        value.Should().Be(originalMessage);
    }

    [Fact]
    public void Enrich_NonStringProperty_IsSkipped()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Count", new ScalarValue(42) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var countProp = logEvent.Properties["Count"];
        ((ScalarValue)countProp).Value.Should().Be(42);
    }

    [Fact]
    public void Enrich_MultipleStringProperties_RedactsAll()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Phone", new ScalarValue("0912345678") },
            { "Address", new ScalarValue("số 123 đường Lê Lợi") },
            { "Name", new ScalarValue("John Doe") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var phoneValue = ((ScalarValue)logEvent.Properties["Phone"]).Value?.ToString();
        phoneValue.Should().Contain("091***5678");
        var addressValue = ((ScalarValue)logEvent.Properties["Address"]).Value?.ToString();
        addressValue.Should().Contain("[address]");
        var nameValue = ((ScalarValue)logEvent.Properties["Name"]).Value?.ToString();
        nameValue.Should().Be("John Doe");
    }

    [Fact]
    public void Enrich_EmptyStringProperty_IsSkipped()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Empty", new ScalarValue("") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var emptyProp = logEvent.Properties["Empty"];
        var value = ((ScalarValue)emptyProp).Value?.ToString();
        value.Should().Be("");
    }

    [Fact]
    public void Enrich_NullStringProperty_IsSkipped()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Null", new ScalarValue(null) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var nullProp = logEvent.Properties["Null"];
        ((ScalarValue)nullProp).Value.Should().BeNull();
    }

    [Fact]
    public void Enrich_StringPropertyWithMultiplePhoneNumbers_RedactsAll()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Contacts", new ScalarValue("Primary: 0912345678, Secondary: 0987654321") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var contactsProp = logEvent.Properties["Contacts"];
        var value = ((ScalarValue)contactsProp).Value?.ToString();
        value.Should().Contain("091***5678");
        value.Should().Contain("098***4321");
        value.Should().NotContain("0912345678");
        value.Should().NotContain("0987654321");
    }

    [Fact]
    public void Enrich_LongTextWithMultiplePiiInstances_RedactsAll()
    {
        // Arrange
        var longText = @"Customer report:
            Name: John Doe
            Phone: 0912345678
            Address: số 123 đường Lê Lợi, Phường 1, Quận 1
            Alternative contact: 0987654321
            Notes: Visit at Huyện Hoài Đức, Hà Nội";

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Report", new ScalarValue(longText) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var reportValue = ((ScalarValue)logEvent.Properties["Report"]).Value?.ToString();
        reportValue.Should().Contain("091***5678");
        reportValue.Should().Contain("098***4321");
        reportValue.Should().Contain("[address]");
        reportValue.Should().NotContain("0912345678");
        reportValue.Should().NotContain("0987654321");
    }

    [Fact]
    public void Enrich_PreservesPropertyNamesAfterRedaction()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "CustomerData", new ScalarValue("0912345678") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        logEvent.Properties.Should().ContainKey("CustomerData");
    }

    [Fact]
    public void Enrich_StructuredPropertyValue_IsSkipped()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Object", new StructureValue(
                new[]
                {
                    new LogEventProperty("Field", new ScalarValue("0912345678"))
                },
                null) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert - Structure values are not modified
        logEvent.Properties["Object"].Should().BeOfType<StructureValue>();
    }

    [Fact]
    public void Enrich_WithSpecialCharactersInPhone_RedactsCorrectly()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "Message", new ScalarValue("Contact: 0912345678!@#$") }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert
        var messageProp = logEvent.Properties["Message"];
        var value = ((ScalarValue)messageProp).Value?.ToString();
        value.Should().Contain("091***5678");
    }

    [Fact]
    public void Enrich_DefenseInDepth_WorksWithNoPriorRedaction()
    {
        // Arrange - Simulate a case where the string was NOT redacted before logging
        var unredactedMessage = "WARNING: Customer 0912345678 provided address số 123 đường Lê Lợi";
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            { "UnredactedWarning", new ScalarValue(unredactedMessage) }
        };
        var logEvent = CreateLogEvent(properties);
        var factory = CreatePropertyFactory();

        // Act
        _enricher.Enrich(logEvent, factory);

        // Assert - Enricher catches what slipped through
        var warningValue = ((ScalarValue)logEvent.Properties["UnredactedWarning"]).Value?.ToString();
        warningValue.Should().Contain("091***5678");
        warningValue.Should().Contain("[address]");
        warningValue.Should().NotContain("0912345678");
    }
}

/// <summary>
/// Minimal PropertyFactory implementation for testing.
/// </summary>
internal class PropertyFactory : ILogEventPropertyFactory
{
    public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
    {
        return new LogEventProperty(name, new ScalarValue(value));
    }
}
