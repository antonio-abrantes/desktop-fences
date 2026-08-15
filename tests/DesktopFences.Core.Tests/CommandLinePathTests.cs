using DesktopFences.Core.Process;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class CommandLinePathTests
{
    [Fact]
    public void Quote_WrapsPathWithSpaces()
    {
        CommandLinePath.Quote(@"C:\Program Files\DesktopFences\DesktopFences.exe")
            .Should().Be(@"""C:\Program Files\DesktopFences\DesktopFences.exe""");
    }

    [Fact]
    public void Quote_LeavesAlreadyQuotedPath()
    {
        CommandLinePath.Quote(@"""C:\Apps\DesktopFences.exe""")
            .Should().Be(@"""C:\Apps\DesktopFences.exe""");
    }
}
