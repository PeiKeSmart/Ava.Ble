using JieLi.OTA.Core.Models;
using Xunit;

namespace JieLi.OTA.Tests.Core;

public class OtaProgressTests
{
    [Fact(DisplayName = "进度显示：达到或超过 100% 时封顶 99.9%（传输阶段）")]
    public void Percentage_ShouldCapAt99_9_BeforeCompletion()
    {
        var p = new OtaProgress
        {
            TotalBytes = 100,
            TransferredBytes = 100
        };
        Assert.True(p.Percentage < 100);
        Assert.Equal(99.9, p.Percentage, 1);
    }
}
