using CardActions.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace CardActions.UnitTests;

[Trait("Category", "Unit")]
[Trait("Feature", "EventBusOptions")]
public class EventBusOptionsTests
{
    [Fact]
    public void Validate_WhenUseInMemoryTrue_ShouldPass()
    {
        var options = new EventBusOptions { UseInMemory = true };
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        options.Invoking(o => o.Validate(env.Object)).Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenUseInMemoryFalseAndRabbitMQNull_ShouldThrow()
    {
        var options = new EventBusOptions { UseInMemory = false, RabbitMQ = null };
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        options.Invoking(o => o.Validate(env.Object))
               .Should().Throw<InvalidOperationException>()
               .WithMessage("*RabbitMQ configuration is missing*");
    }

    [Fact]
    public void Validate_WhenUseInMemoryFalseAndRabbitMQValid_ShouldPass()
    {
        var options = new EventBusOptions
        {
            UseInMemory = false,
            RabbitMQ = new RabbitMQOptions
            {
                Host = "localhost",
                Port = 5672,
                Exchange = "test.events"
            }
        };
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        options.Invoking(o => o.Validate(env.Object)).Should().NotThrow();
    }

    [Theory]
    [Trait("Enhancement", "RabbitMQValidation")]
    [InlineData("", 5672, "events")]
    [InlineData("localhost", 0, "events")]
    [InlineData("localhost", 70000, "events")]
    [InlineData("localhost", 5672, "")]
    public void RabbitMQOptions_Validate_WithInvalidData_ShouldThrow(string host, int port, string exchange)
    {
        var options = new RabbitMQOptions { Host = host, Port = port, Exchange = exchange };
        options.Invoking(o => o.Validate()).Should().Throw<InvalidOperationException>();
    }
}
