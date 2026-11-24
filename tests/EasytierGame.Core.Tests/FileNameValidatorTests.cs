using EasytierGame.Core.Utils;

namespace EasytierGame.Core.Tests;

/// <summary>文件名校验测试（对应旧版“请输入2个字以上的存储名 / 文件名不合法”）。</summary>
public class FileNameValidatorTests
{
    [Theory]
    [InlineData("我的配置")]
    [InlineData("room1")]
    [InlineData("a b")]
    [InlineData("配置.2024")]
    public void ValidNames(string name)
    {
        Assert.True(FileNameValidator.IsValidWindowsFileName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]                 // 少于 2 个字符
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    public void InvalidNames(string name)
    {
        Assert.False(FileNameValidator.IsValidWindowsFileName(name));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1.txt")]
    public void ReservedNames(string name)
    {
        Assert.False(FileNameValidator.IsValidWindowsFileName(name));
    }

    [Fact]
    public void TrailingDotRejected()
    {
        Assert.False(FileNameValidator.IsValidWindowsFileName("name."));
    }
}
