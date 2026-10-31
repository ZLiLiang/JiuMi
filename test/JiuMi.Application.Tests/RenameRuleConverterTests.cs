using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameRuleConverterTests
{
    [Fact]
    public void FromBasic_GivenReplace_ReturnsLiteralRuleWithoutIndexTokens()
    {
        RenameRule rule = RenameRuleConverter.FromBasic(
            RenameBasicTask.Replace,
            "产品图",
            "product",
            RenameAffixPosition.Prefix,
            string.Empty,
            string.Empty,
            0);

        Assert.Equal(RenameRuleKind.Replace, rule.Kind);
        Assert.Equal("产品图", rule.Find);
        Assert.Equal("product", rule.Replace);
        Assert.False(rule.UseRegex);
        Assert.False(rule.ExpandIndexTokens);
    }

    [Theory]
    [InlineData(RenameAffixPosition.Prefix, "2026-{name}{ext}")]
    [InlineData(RenameAffixPosition.Suffix, "{name}2026-{ext}")]
    public void FromBasic_GivenAffix_ReturnsTemplate(RenameAffixPosition position, string expected)
    {
        RenameRule rule = RenameRuleConverter.FromBasic(
            RenameBasicTask.Affix,
            string.Empty,
            string.Empty,
            position,
            "2026-",
            string.Empty,
            0);

        Assert.Equal(RenameRuleKind.Template, rule.Kind);
        Assert.Equal(expected, rule.Template);
    }

    [Theory]
    [InlineData(0, "图片{index}{ext}")]
    [InlineData(3, "图片{index:000}{ext}")]
    public void FromBasic_GivenSequence_ReturnsTemplateWithPadWidth(int padWidth, string expected)
    {
        RenameRule rule = RenameRuleConverter.FromBasic(
            RenameBasicTask.Sequence,
            string.Empty,
            string.Empty,
            RenameAffixPosition.Prefix,
            string.Empty,
            "图片",
            padWidth);

        Assert.Equal(RenameRuleKind.Template, rule.Kind);
        Assert.Equal(expected, rule.Template);
    }
}
