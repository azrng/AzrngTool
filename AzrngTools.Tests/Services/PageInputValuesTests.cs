using AzrngTools.Services;
using Xunit;

namespace AzrngTools.Tests.Services;

// 标量编解码回归：编码值可无损读回，非法或缺键输入兜底默认值
public class PageInputValuesTests
{
    private enum SampleOption
    {
        First = 0,
        Second = 1,
        Third = 2
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Encode_Bool_ProducesLowerInvariant(bool value, string expected)
    {
        Assert.Equal(expected, PageInputValues.Encode(value));
        Assert.Equal(value, PageInputValues.DecodeBool(expected, !value));
    }

    [Fact]
    public void Encode_Int_UsesInvariantCulture()
    {
        Assert.Equal("1234567", PageInputValues.Encode(1234567));
        Assert.Equal(-42, PageInputValues.DecodeInt32("-42", 0));
    }

    [Fact]
    public void Encode_Long_RoundTripsLargeValues()
    {
        Assert.Equal("9223372036854775807", PageInputValues.Encode(long.MaxValue));
        Assert.Equal(long.MaxValue, PageInputValues.DecodeInt64(PageInputValues.Encode(long.MaxValue), 0));
    }

    [Fact]
    public void Encode_Enum_StoresOrdinalAndRejectsOutOfRange()
    {
        Assert.Equal("2", PageInputValues.Encode(SampleOption.Third));
        Assert.Equal(SampleOption.Third, PageInputValues.DecodeEnum<SampleOption>("2", SampleOption.First));
        // 枚举缩水（旧存档序号已不存在）按默认值兜底
        Assert.Equal(SampleOption.First, PageInputValues.DecodeEnum<SampleOption>("99", SampleOption.First));
        Assert.Equal(SampleOption.First, PageInputValues.DecodeEnum<SampleOption>(null, SampleOption.First));
    }

    [Fact]
    public void Encode_DateTime_RoundTripsKindAndSubsecond()
    {
        var value = new DateTime(2026, 11, 5, 12, 30, 45, 123, DateTimeKind.Local);
        var decoded = PageInputValues.DecodeDateTime(PageInputValues.Encode(value), default);

        Assert.Equal(value, decoded);
        Assert.Equal(value.Kind, decoded.Kind);
    }

    [Fact]
    public void Decode_WithGarbageValues_ReturnsFallbacks()
    {
        Assert.True(PageInputValues.DecodeBool("不是布尔", true));
        Assert.Equal(7, PageInputValues.DecodeInt32("乱码", 7));
        Assert.Equal(7L, PageInputValues.DecodeInt64("乱码", 7L));
        Assert.Equal(SampleOption.Second, PageInputValues.DecodeEnum<SampleOption>("乱码", SampleOption.Second));
        Assert.Equal(default, PageInputValues.DecodeDateTime("乱码", default));
    }

    [Fact]
    public void Encode_String_PassesThrough()
    {
        Assert.Null(PageInputValues.Encode((string?)null));
        Assert.Equal("原文 <>&\"", PageInputValues.Encode("原文 <>&\""));
    }
}
